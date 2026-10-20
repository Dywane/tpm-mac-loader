using System.Collections.Concurrent;
using System.Reflection;
using BepInEx;
using RuntimeObjectHandle = TpmMacIl2CppHost.Contracts.RuntimeObjectHandle;
using RuntimeTypeHandle = TpmMacIl2CppHost.Contracts.RuntimeTypeHandle;

namespace TpmMacIl2CppHost.Kernels;

/// <summary>
/// Track A production kernel (this branch): load BepInEx/interop via AssemblyResolve + LoadFrom.
/// Ready when Il2Cppmscorlib loads and Il2CppSystem.String has readable fields/methods.
/// TaskA10: TryInvoke / TryGetField / TrySetField / TryAsObjectHandle / TryGetTypeName / TryListFields
/// via managed reflection on Il2CppInterop stubs. TryFindObject remains NotSupported.
/// </summary>
public sealed class InteropRuntimeKernel : IRuntimeKernel
{
    public const string FindNotSupportedDiagnostic =
        "NotSupported (TaskA10): TryFindObject deferred; use TryInvoke/Field/Resolve";

    static readonly object ResolveLock = new();
    static bool _resolveHooked;
    static string? _interopDir;
    static string? _coreDir;

    readonly ConcurrentDictionary<IntPtr, Type> _handleToType = new();
    readonly ConcurrentDictionary<Type, RuntimeTypeHandle> _typeToHandle = new();
    readonly ConcurrentDictionary<IntPtr, object> _handleToObject = new();
    readonly ConcurrentDictionary<object, RuntimeObjectHandle> _objectToHandle =
        new(ReferenceEqualityComparer.Instance);
    int _nextHandleId = 1;
    int _nextObjectId = 1;

    bool _ready;
    string _failDiag = "not initialized";
    Assembly? _mscorlib;
    Type? _stringType;
    int _stringFields;
    int _stringMethods;

    public HostKernelKind Kind => HostKernelKind.Interop;

    public bool Ready => _ready;

    /// <summary>Last Ready failure / success diagnostic (for Host log).</summary>
    public string Diagnostic => _ready
        ? $"OK Il2CppSystem.String fields={_stringFields} methods={_stringMethods} interop={_interopDir}"
        : _failDiag;

    /// <summary>
    /// Locate BepInEx/interop, hook AssemblyResolve, LoadFrom Il2Cppmscorlib,
    /// prove Il2CppSystem.String fields/methods readable → Ready=true.
    /// </summary>
    public bool TryBootstrap()
    {
        try
        {
            _interopDir = Path.Combine(Paths.BepInExRootPath, "interop");
            _coreDir = Path.Combine(Paths.BepInExRootPath, "core");
            if (!Directory.Exists(_interopDir))
            {
                _failDiag = "FAIL no interop dir: " + _interopDir;
                _ready = false;
                return false;
            }

            EnsureAssemblyResolve();

            try
            {
                _ = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a =>
                        {
                            try { return a.GetName().Name == "Il2CppInterop.Runtime"; }
                            catch { return false; }
                        })
                    ?? Assembly.Load("Il2CppInterop.Runtime");
            }
            catch (Exception ex)
            {
                LogInfo($"Il2CppInterop.Runtime preload soft-fail: {ex.Message}");
            }

            var primaryPath = Path.Combine(_interopDir, "Il2Cppmscorlib.dll");
            if (!File.Exists(primaryPath))
            {
                _failDiag = "FAIL missing Il2Cppmscorlib.dll under " + _interopDir;
                _ready = false;
                return false;
            }

            _mscorlib = FindLoaded("Il2Cppmscorlib") ?? TryLoadFrom(primaryPath);
            if (_mscorlib == null)
            {
                _failDiag = "FAIL cannot LoadFrom Il2Cppmscorlib.dll";
                _ready = false;
                return false;
            }

            PreloadNamed(new[] { "TPS.Game.dll", "TPS.Core.dll", "UnityEngine.CoreModule.dll" });

            var hit = _mscorlib.GetType("Il2CppSystem.String", throwOnError: false);
            if (hit == null)
            {
                foreach (var t in SafeGetTypes(_mscorlib))
                {
                    if (t != null && t.Name == "String" && (t.Namespace == "Il2CppSystem" || t.FullName == "Il2CppSystem.String"))
                    {
                        hit = t;
                        break;
                    }
                }
            }

            if (hit == null)
            {
                _failDiag = "FAIL cannot resolve Il2CppSystem.String after LoadFrom";
                _ready = false;
                return false;
            }

            var fields = hit.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var methods = hit.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (fields.Length == 0 && methods.Length == 0)
            {
                _failDiag = "FAIL Il2CppSystem.String has 0 fields and 0 methods (unreadable)";
                _ready = false;
                return false;
            }

            _stringType = hit;
            _stringFields = fields.Length;
            _stringMethods = methods.Length;
            _ = InternHandle(hit);
            _ready = true;
            _failDiag = Diagnostic;
            return true;
        }
        catch (Exception ex)
        {
            _failDiag = "FAIL " + ex.GetType().Name + ": " + ex.Message;
            _ready = false;
            return false;
        }
    }

    public bool TryResolveType(string? imageHint, string namespaze, string name, out RuntimeTypeHandle handle)
    {
        handle = default;
        if (!_ready)
        {
            LogWarn($"TryResolveType: not Ready — {_failDiag}");
            return false;
        }

        if (string.IsNullOrEmpty(name))
            return false;

        try
        {
            var type = ResolveManagedType(imageHint, namespaze ?? "", name);
            if (type == null)
                return false;

            handle = InternHandle(type);
            return handle.IsValid;
        }
        catch (Exception ex)
        {
            LogWarn($"TryResolveType error {namespaze}.{name}: {ex.Message}");
            return false;
        }
    }

    public bool TryInvoke(
        RuntimeTypeHandle type,
        string methodName,
        RuntimeObjectHandle instance,
        object?[]? args,
        out object? result)
    {
        result = null;
        if (!_ready)
        {
            LogWarn("TryInvoke: not Ready");
            return false;
        }

        if (string.IsNullOrEmpty(methodName))
        {
            LogWarn("TryInvoke: empty methodName");
            return false;
        }

        try
        {
            if (!TryResolveInvokeTarget(type, instance, out var managedType, out var managedInstance, out var err))
            {
                LogWarn($"TryInvoke {methodName}: {err}");
                return false;
            }

            var preparedArgs = PrepareArgs(args);
            var method = FindMethod(managedType, methodName, preparedArgs, wantStatic: !instance.IsValid);
            if (method == null)
            {
                LogWarn($"TryInvoke: method not found name={methodName} argc={preparedArgs.Length} type={managedType.FullName}");
                return false;
            }

            if (method.IsStatic)
                managedInstance = null;
            else if (managedInstance == null)
            {
                LogWarn($"TryInvoke: instance method {methodName} needs valid instance");
                return false;
            }

            object? raw;
            try
            {
                raw = method.Invoke(managedInstance, preparedArgs);
            }
            catch (TargetInvocationException tie)
            {
                LogWarn($"TryInvoke {methodName}: {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message ?? tie.Message}");
                return false;
            }

            result = BoxReturn(raw);
            return true;
        }
        catch (Exception ex)
        {
            LogWarn($"TryInvoke {methodName}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public bool TryAsObjectHandle(object? value, out RuntimeObjectHandle handle)
    {
        if (value is RuntimeObjectHandle h && h.IsValid)
        {
            handle = h;
            return true;
        }

        // Already-interned managed Il2Cpp wrapper (or any reference we track).
        if (value is not null && !value.GetType().IsValueType && value is not string)
        {
            handle = InternObject(value);
            return handle.IsValid;
        }

        handle = default;
        return false;
    }

    public bool TryGetField(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        string fieldName,
        out object? value)
    {
        value = null;
        if (!_ready)
        {
            LogWarn("TryGetField: not Ready");
            return false;
        }

        if (string.IsNullOrEmpty(fieldName))
        {
            LogWarn("TryGetField: empty fieldName");
            return false;
        }

        try
        {
            if (!TryResolveFieldTarget(type, instance, out var managedType, out var managedInstance, out var err))
            {
                LogWarn($"TryGetField {fieldName}: {err}");
                return false;
            }

            var field = FindField(managedType, fieldName);
            if (field == null)
            {
                LogWarn($"TryGetField: field not found name={fieldName} type={managedType.FullName}");
                return false;
            }

            if (field.IsLiteral)
            {
                LogWarn($"TryGetField {fieldName}: literal skipped");
                return false;
            }

            var isStatic = field.IsStatic || !instance.IsValid;
            if (!isStatic && managedInstance == null)
            {
                LogWarn($"TryGetField {fieldName}: instance field needs valid instance");
                return false;
            }

            var raw = field.GetValue(isStatic ? null : managedInstance);
            // Null ref field → success with null (contract).
            value = BoxReturn(raw);
            return true;
        }
        catch (Exception ex)
        {
            LogWarn($"TryGetField {fieldName}: {ex.GetType().Name}: {ex.Message}");
            value = null;
            return false;
        }
    }

    public bool TrySetField(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        string fieldName,
        object? value)
    {
        if (!_ready)
        {
            LogWarn("TrySetField: not Ready");
            return false;
        }

        if (string.IsNullOrEmpty(fieldName))
        {
            LogWarn("TrySetField: empty fieldName");
            return false;
        }

        try
        {
            if (!TryResolveFieldTarget(type, instance, out var managedType, out var managedInstance, out var err))
            {
                LogWarn($"TrySetField {fieldName}: {err}");
                return false;
            }

            var field = FindField(managedType, fieldName);
            if (field == null)
            {
                LogWarn($"TrySetField: field not found name={fieldName} type={managedType.FullName}");
                return false;
            }

            if (field.IsLiteral || field.IsInitOnly)
            {
                LogWarn($"TrySetField {fieldName}: literal/initonly skipped");
                return false;
            }

            var isStatic = field.IsStatic || !instance.IsValid;
            if (!isStatic && managedInstance == null)
            {
                LogWarn($"TrySetField {fieldName}: instance field needs valid instance");
                return false;
            }

            var coerced = UnboxArg(value, field.FieldType);
            field.SetValue(isStatic ? null : managedInstance, coerced);
            return true;
        }
        catch (Exception ex)
        {
            LogWarn($"TrySetField {fieldName}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public bool TryGetTypeName(RuntimeObjectHandle instance, out string? fullName)
    {
        fullName = null;
        if (!_ready || !instance.IsValid)
            return false;

        if (!_handleToObject.TryGetValue(instance.NativeObject, out var obj) || obj == null)
            return false;

        fullName = obj.GetType().FullName ?? obj.GetType().Name;
        return !string.IsNullOrEmpty(fullName);
    }

    public bool TryListFields(RuntimeObjectHandle instance, out string[]? names)
    {
        names = null;
        if (!_ready || !instance.IsValid)
            return false;

        if (!_handleToObject.TryGetValue(instance.NativeObject, out var obj) || obj == null)
            return false;

        try
        {
            var list = new List<string>();
            var t = obj.GetType();
            const BindingFlags bf =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var guard = 0;
            for (var cur = t; cur != null && cur != typeof(object) && guard < 32; cur = cur.BaseType, guard++)
            {
                foreach (var f in cur.GetFields(bf | BindingFlags.DeclaredOnly))
                {
                    if (list.Count >= 512) break;
                    if (!list.Contains(f.Name, StringComparer.Ordinal))
                        list.Add(f.Name);
                }
            }

            if (list.Count == 0)
                return false;
            names = list.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            LogWarn($"TryListFields: {ex.Message}");
            return false;
        }
    }

    public bool TryFindObject(RuntimeTypeHandle type, bool includeInactive, out RuntimeObjectHandle instance)
    {
        _ = type;
        _ = includeInactive;
        instance = default;
        LogWarn($"TryFindObject: {FindNotSupportedDiagnostic}");
        return false;
    }

    /// <summary>Map opaque handle back to managed Type (kernel-internal).</summary>
    internal bool TryGetManagedType(RuntimeTypeHandle handle, out Type? type)
    {
        type = null;
        if (!handle.IsValid)
            return false;
        return _handleToType.TryGetValue(handle.NativeClass, out type) && type != null;
    }

    bool TryResolveInvokeTarget(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        out Type managedType,
        out object? managedInstance,
        out string err)
    {
        managedType = null!;
        managedInstance = null;
        err = "";

        if (instance.IsValid)
        {
            if (!_handleToObject.TryGetValue(instance.NativeObject, out managedInstance) || managedInstance == null)
            {
                err = "unknown instance handle";
                return false;
            }

            managedType = managedInstance.GetType();
            if (type.IsValid && _handleToType.TryGetValue(type.NativeClass, out var declared) && declared != null)
            {
                // Prefer declared type when it is compatible (static helpers on base, etc.)
                if (declared.IsAssignableFrom(managedType) || managedType.IsAssignableFrom(declared))
                    managedType = declared;
            }

            return true;
        }

        if (!type.IsValid || !_handleToType.TryGetValue(type.NativeClass, out var t) || t == null)
        {
            err = "invalid type handle (static invoke)";
            return false;
        }

        managedType = t;
        return true;
    }

    bool TryResolveFieldTarget(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        out Type managedType,
        out object? managedInstance,
        out string err)
    {
        // Same resolution rules as invoke.
        return TryResolveInvokeTarget(type, instance, out managedType, out managedInstance, out err);
    }

    static MethodInfo? FindMethod(Type type, string methodName, object?[] args, bool wantStatic)
    {
        const BindingFlags bf =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        var names = new List<string> { methodName };
        if (!methodName.StartsWith("get_", StringComparison.Ordinal)
            && !methodName.StartsWith("set_", StringComparison.Ordinal)
            && !methodName.StartsWith("op_", StringComparison.Ordinal))
        {
            names.Add("get_" + methodName);
        }

        var candidates = new List<MethodInfo>();
        foreach (var n in names)
        {
            foreach (var m in type.GetMethods(bf))
            {
                if (m.Name != n) continue;
                if (wantStatic && !m.IsStatic) continue;
                candidates.Add(m);
            }
        }

        if (candidates.Count == 0)
            return null;

        // Exact argc match preferred.
        var argc = args.Length;
        var argcMatch = candidates.Where(m => m.GetParameters().Length == argc).ToList();
        var pool = argcMatch.Count > 0 ? argcMatch : candidates;

        // Prefer assignable parameter types.
        MethodInfo? best = null;
        var bestScore = int.MinValue;
        foreach (var m in pool)
        {
            var ps = m.GetParameters();
            if (ps.Length != argc) continue;
            var score = 0;
            var ok = true;
            for (var i = 0; i < argc; i++)
            {
                var arg = args[i];
                var pt = ps[i].ParameterType;
                if (arg == null)
                {
                    if (pt.IsValueType && Nullable.GetUnderlyingType(pt) == null)
                    {
                        ok = false;
                        break;
                    }

                    score += 1;
                    continue;
                }

                var at = arg.GetType();
                if (pt.IsAssignableFrom(at))
                {
                    score += 10;
                    if (pt == at) score += 5;
                }
                else if (pt == typeof(object))
                {
                    score += 1;
                }
                else
                {
                    ok = false;
                    break;
                }
            }

            if (!ok) continue;
            if (score > bestScore)
            {
                bestScore = score;
                best = m;
            }
        }

        return best ?? pool.FirstOrDefault(m => m.GetParameters().Length == argc) ?? pool[0];
    }

    static FieldInfo? FindField(Type type, string fieldName)
    {
        const BindingFlags bf =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var direct = type.GetField(fieldName, bf);
        if (direct != null) return direct;

        // Walk parents for DeclaredOnly misses / backing fields.
        for (var cur = type; cur != null && cur != typeof(object); cur = cur.BaseType)
        {
            var f = cur.GetField(fieldName, bf | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }

        return null;
    }

    object?[] PrepareArgs(object?[]? args)
    {
        if (args == null || args.Length == 0)
            return Array.Empty<object?>();

        var prepared = new object?[args.Length];
        for (var i = 0; i < args.Length; i++)
            prepared[i] = UnboxArg(args[i], expectedType: null);
        return prepared;
    }

    object? UnboxArg(object? value, Type? expectedType)
    {
        if (value is RuntimeObjectHandle roh)
        {
            if (!roh.IsValid) return null;
            if (_handleToObject.TryGetValue(roh.NativeObject, out var obj))
                return obj;
            return null;
        }

        if (expectedType != null && value != null && !expectedType.IsInstanceOfType(value))
        {
            try
            {
                if (expectedType.IsEnum && value is IConvertible)
                    return Enum.ToObject(expectedType, value);
                if (expectedType == typeof(IntPtr) && value is long l)
                    return new IntPtr(l);
                if (expectedType == typeof(IntPtr) && value is int i)
                    return new IntPtr(i);
            }
            catch
            {
                /* keep original */
            }
        }

        return value;
    }

    object? BoxReturn(object? raw)
    {
        if (raw == null) return null;

        var t = raw.GetType();
        if (t.IsValueType)
            return raw;

        // Keep System.String as string (readable smoke / plugin convenience).
        if (raw is string)
            return raw;

        // Il2Cpp / other reference → opaque object handle.
        return InternObject(raw);
    }

    RuntimeObjectHandle InternObject(object obj)
    {
        if (_objectToHandle.TryGetValue(obj, out var existing))
            return existing;

        var id = Interlocked.Increment(ref _nextObjectId);
        var ptr = new IntPtr(id);
        var handle = RuntimeObjectHandle.FromNative(ptr);
        _handleToObject[ptr] = obj;
        _objectToHandle[obj] = handle;
        return handle;
    }

    Type? ResolveManagedType(string? imageHint, string namespaze, string name)
    {
        if (IsCorlibHint(imageHint) && (namespaze == "System" || namespaze == "Il2CppSystem") && name == "String")
            return _stringType;

        if (_stringType != null
            && namespaze == "Il2CppSystem"
            && name == "String")
            return _stringType;

        var candidates = BuildFullNameCandidates(namespaze, name);
        var asmCandidates = BuildAssemblyCandidates(imageHint, namespaze);

        foreach (var asmName in asmCandidates)
        {
            var asm = FindLoaded(asmName) ?? TryLoadAssemblyFile(asmName);
            if (asm == null)
                continue;

            foreach (var full in candidates)
            {
                var t = asm.GetType(full, throwOnError: false);
                if (t != null)
                    return t;
            }

            foreach (var t in SafeGetTypes(asm))
            {
                if (t == null) continue;
                if (t.Name != name) continue;
                if (string.IsNullOrEmpty(namespaze)
                    || t.Namespace == namespaze
                    || t.Namespace == "Il2Cpp" + namespaze
                    || (namespaze.StartsWith("Il2Cpp", StringComparison.Ordinal)
                        && t.Namespace == namespaze))
                    return t;
            }
        }

        foreach (var full in candidates)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(full, throwOnError: false);
                    if (t != null)
                        return t;
                }
                catch
                {
                    /* skip */
                }
            }
        }

        return null;
    }

    static bool IsCorlibHint(string? imageHint) =>
        string.IsNullOrEmpty(imageHint)
        || imageHint.Equals("corlib", StringComparison.OrdinalIgnoreCase)
        || imageHint.Equals("mscorlib", StringComparison.OrdinalIgnoreCase)
        || imageHint.Equals("Il2Cppmscorlib", StringComparison.OrdinalIgnoreCase)
        || imageHint.Contains("mscorlib", StringComparison.OrdinalIgnoreCase);

    static List<string> BuildFullNameCandidates(string namespaze, string name)
    {
        var list = new List<string>();
        void Add(string? ns, string n)
        {
            if (string.IsNullOrEmpty(ns))
                list.Add(n);
            else
                list.Add(ns + "." + n);
        }

        Add(namespaze, name);
        if (!string.IsNullOrEmpty(namespaze) && !namespaze.StartsWith("Il2Cpp", StringComparison.Ordinal))
            Add("Il2Cpp" + namespaze, name);
        if (namespaze == "System")
            Add("Il2CppSystem", name);
        if (namespaze == "Il2CppSystem")
            Add("System", name);
        return list.Distinct(StringComparer.Ordinal).ToList();
    }

    static List<string> BuildAssemblyCandidates(string? imageHint, string namespaze)
    {
        var list = new List<string>();
        void Add(string? s)
        {
            if (!string.IsNullOrEmpty(s) && !list.Contains(s, StringComparer.OrdinalIgnoreCase))
                list.Add(s!);
        }

        if (!string.IsNullOrEmpty(imageHint) && !IsCorlibHint(imageHint))
        {
            Add(imageHint);
            Add(Path.GetFileNameWithoutExtension(imageHint));
            if (!imageHint.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase))
                Add("Il2Cpp" + imageHint);
        }

        if (IsCorlibHint(imageHint))
            Add("Il2Cppmscorlib");

        if (!string.IsNullOrEmpty(namespaze))
        {
            var ns = namespaze.StartsWith("Il2Cpp", StringComparison.Ordinal)
                ? namespaze.Substring("Il2Cpp".Length)
                : namespaze;
            var parts = ns.Split('.');
            if (parts.Length >= 2)
                Add(parts[0] + "." + parts[1]);
            if (parts.Length >= 1)
                Add(parts[0]);
            Add(ns);
        }

        Add("TPS.Game");
        Add("TPS.Core");
        Add("UnityEngine.CoreModule");
        Add("Il2Cppmscorlib");
        return list;
    }

    RuntimeTypeHandle InternHandle(Type type)
    {
        if (_typeToHandle.TryGetValue(type, out var existing))
            return existing;

        var id = Interlocked.Increment(ref _nextHandleId);
        var ptr = new IntPtr(id);
        var handle = RuntimeTypeHandle.FromNative(ptr);
        _handleToType[ptr] = type;
        _typeToHandle[type] = handle;
        return handle;
    }

    void PreloadNamed(IEnumerable<string> dllFiles)
    {
        if (string.IsNullOrEmpty(_interopDir)) return;
        foreach (var dll in dllFiles)
        {
            var path = Path.Combine(_interopDir, dll);
            if (!File.Exists(path)) continue;
            var simple = Path.GetFileNameWithoutExtension(dll)!;
            if (FindLoaded(simple) != null) continue;
            _ = TryLoadFrom(path);
        }
    }

    Assembly? TryLoadAssemblyFile(string assemblySimpleName)
    {
        if (string.IsNullOrEmpty(_interopDir)) return null;
        var path = Path.Combine(_interopDir, assemblySimpleName + ".dll");
        if (!File.Exists(path))
        {
            var alt = Path.Combine(_interopDir, "Il2Cpp" + assemblySimpleName + ".dll");
            if (File.Exists(alt)) path = alt;
            else return null;
        }

        return TryLoadFrom(path);
    }

    static void EnsureAssemblyResolve()
    {
        lock (ResolveLock)
        {
            if (_resolveHooked) return;
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                try
                {
                    var name = new AssemblyName(args.Name).Name;
                    if (string.IsNullOrEmpty(name)) return null;
                    var loaded = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a =>
                        {
                            try { return a.GetName().Name == name; }
                            catch { return false; }
                        });
                    if (loaded != null) return loaded;

                    foreach (var dir in new[] { _interopDir, _coreDir })
                    {
                        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                        var candidate = Path.Combine(dir, name + ".dll");
                        if (!File.Exists(candidate))
                        {
                            var alt = Path.Combine(dir, "Il2Cpp" + name + ".dll");
                            if (File.Exists(alt)) candidate = alt;
                            else continue;
                        }

                        return Assembly.LoadFrom(candidate);
                    }
                }
                catch
                {
                    /* swallow */
                }

                return null;
            };
            _resolveHooked = true;
        }
    }

    static Assembly? FindLoaded(string simpleName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a =>
            {
                try { return a.GetName().Name == simpleName; }
                catch { return false; }
            });
    }

    static Assembly? TryLoadFrom(string path)
    {
        try { return Assembly.LoadFrom(path); }
        catch { return null; }
    }

    static Type?[] SafeGetTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types ?? Array.Empty<Type>();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    static void LogInfo(string msg)
    {
        try { Il2CppHostPlugin.LogSrc?.LogInfo("[RT] InteropRuntimeKernel: " + msg); }
        catch { /* never throw */ }
    }

    static void LogWarn(string msg)
    {
        try { Il2CppHostPlugin.LogSrc?.LogWarning("[RT] InteropRuntimeKernel: " + msg); }
        catch { /* never throw */ }
    }
}
