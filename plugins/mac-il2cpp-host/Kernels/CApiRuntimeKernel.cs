using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using TpmMacIl2CppHost.Host;
using TpmMacIl2CppHost.Native;
using RuntimeObjectHandle = TpmMacIl2CppHost.Contracts.RuntimeObjectHandle;
using RuntimeTypeHandle = TpmMacIl2CppHost.Contracts.RuntimeTypeHandle;

namespace TpmMacIl2CppHost.Kernels;

/// <summary>
/// Default production kernel: IL2CPP C API via <see cref="Il2CppNative"/> +
/// <see cref="ClassFromNameService"/>. No Multiselect-side dlsym.
/// Task 3: bounded TryInvoke; Field; Field-list; Find: TryFindObject via Type object + Object.Find*
/// (0.2.4+: hard-fail circuit — stop on first Find* il2cpp exception; never auto-escalate includeInactive).
/// </summary>
public sealed class CApiRuntimeKernel : IRuntimeKernel
{
    public HostKernelKind Kind => HostKernelKind.CApi;

    public bool Ready => Il2CppNative.Ready;

    public bool TryResolveType(string? imageHint, string namespaze, string name, out RuntimeTypeHandle handle)
    {
        handle = default;
        if (!Ready)
            return false;
        if (string.IsNullOrEmpty(namespaze) || string.IsNullOrEmpty(name))
            return false;

        var klass = ClassFromNameService.Resolve(imageHint, namespaze, name);
        if (klass == IntPtr.Zero)
            return false;

        handle = RuntimeTypeHandle.FromNative(klass);
        return handle.IsValid;
    }

    /// <summary>
    /// Resolve method on <paramref name="type"/> and invoke.
    /// Default / invalid <paramref name="instance"/> = static (may run on caller thread).
    /// <para>
    /// <b>0.2.7:</b> instance Invoke is marshaled onto <see cref="MainThreadDispatcher"/>
    /// (same path as Find). Bg-thread <c>il2cpp_runtime_invoke</c> on Unity objects
    /// (e.g. <c>set_Rotating</c>) can native-AV before managed logs — soft-fail instead.
    /// Instance handle must pass <c>object_get_class != null</c>. Bool args use 1-byte scratch.
    /// </para>
    /// Simple returns mapped to string / bool / int / RuntimeObjectHandle; failure → false + log.
    /// </summary>
    public bool TryInvoke(
        RuntimeTypeHandle type,
        string methodName,
        RuntimeObjectHandle instance,
        object?[]? args,
        out object? result)
    {
        result = null;
        if (!Ready || !Il2CppNative.InvokeReady)
        {
            LogFail(methodName, "InvokeReady=false (missing class_get_method_from_name / runtime_invoke)");
            return false;
        }

        if (!type.IsValid || string.IsNullOrEmpty(methodName))
        {
            LogFail(methodName, "invalid type or empty methodName");
            return false;
        }

        if (instance.IsValid)
        {
            if (Il2CppNative.ObjectGetClass != null
                && Il2CppNative.ObjectGetClass(instance.NativeObject) == IntPtr.Zero)
            {
                LogFail(methodName, "instance handle wild (object_get_class=null)");
                return false;
            }

            // Instance methods: never runtime_invoke from bg poll thread.
            if (!MainThreadDispatcher.IsMainThread)
            {
                object? boxed = null;
                bool ok;
                try
                {
                    ok = MainThreadDispatcher.Run(() =>
                        TryInvokeCore(type, methodName, instance, args, out boxed));
                }
                catch (Exception ex)
                {
                    LogFail(methodName, $"main-thread marshal EX: {ex}");
                    return false;
                }

                result = boxed;
                if (!ok)
                {
                    LogFail(
                        methodName,
                        "instance Invoke soft-fail (main marshal timeout/unavailable or invoke miss) — no bg runtime_invoke");
                }

                return ok;
            }
        }

        return TryInvokeCore(type, methodName, instance, args, out result);
    }

    bool TryInvokeCore(
        RuntimeTypeHandle type,
        string methodName,
        RuntimeObjectHandle instance,
        object?[]? args,
        out object? result)
    {
        result = null;
        var klass = type.NativeClass;
        var argCount = args?.Length ?? 0;
        var method = FindMethod(klass, methodName, argCount);
        if (method == IntPtr.Zero)
        {
            LogFail(methodName, $"method not found (tried argCount={argCount} and common counts)");
            return false;
        }

        IntPtr paramsBlock = IntPtr.Zero;
        IntPtr[]? argSlots = null;
        IntPtr[]? valueScratch = null;
        try
        {
            if (argCount > 0)
            {
                if (!TryBuildParams(args!, out paramsBlock, out argSlots, out valueScratch, out var buildErr))
                {
                    LogFail(methodName, $"param build failed: {buildErr}");
                    return false;
                }
            }

            var obj = instance.IsValid ? instance.NativeObject : IntPtr.Zero;
            IntPtr exc = IntPtr.Zero;
            var raw = Il2CppNative.RuntimeInvoke!(method, obj, paramsBlock, ref exc);
            if (exc != IntPtr.Zero)
            {
                LogFail(methodName, $"il2cpp exception ptr=0x{exc.ToInt64():X}");
                return false;
            }

            result = MapReturn(raw);
            return true;
        }
        catch (Exception ex)
        {
            LogFail(methodName, ex.ToString());
            return false;
        }
        finally
        {
            FreeParams(paramsBlock, argSlots, valueScratch);
        }
    }

    public bool TryAsObjectHandle(object? value, out RuntimeObjectHandle handle)
    {
        if (value is RuntimeObjectHandle h && h.IsValid)
        {
            handle = h;
            return true;
        }

        // Compat for probes still holding a raw IntPtr from older MapReturn.
        if (value is IntPtr p && p != IntPtr.Zero)
        {
            handle = RuntimeObjectHandle.FromNative(p);
            return true;
        }

        handle = default;
        return false;
    }

    /// <summary>
    /// Read field → MapReturn (refs become RuntimeObjectHandle).
    /// Invalid instance = static on <paramref name="type"/>; instance path may omit type
    /// (class from object_get_class).
    /// </summary>
    public bool TryGetField(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        string fieldName,
        out object? value)
    {
        value = null;
        if (!Ready || !Il2CppNative.FieldReady)
        {
            LogFieldFail(fieldName, "FieldReady=false (missing class_get_field_from_name / field_get_value_object)");
            return false;
        }

        if (string.IsNullOrEmpty(fieldName))
        {
            LogFieldFail(fieldName, "empty fieldName");
            return false;
        }

        if (!TryResolveFieldKlass(type, instance, out var klass, out var resolveErr))
        {
            LogFieldFail(fieldName, resolveErr);
            return false;
        }

        try
        {
            var field = FindField(klass, fieldName);
            if (field == IntPtr.Zero)
            {
                LogFieldFail(fieldName, "field not found");
                return false;
            }

            var flags = Il2CppNative.FieldGetFlags != null ? Il2CppNative.FieldGetFlags(field) : 0;
            // Literal / RVA: get_value_object is undefined / can corrupt — soft miss.
            if ((flags & Il2CppNative.FieldAttributeLiteral) != 0
                || (flags & Il2CppNative.FieldAttributeHasFieldRva) != 0)
            {
                LogFieldFail(fieldName, "literal/RVA field skipped (unsafe get_value_object)");
                return false;
            }

            var isStatic = (flags & Il2CppNative.FieldAttributeStatic) != 0 || !instance.IsValid;
            if (!isStatic && !instance.IsValid)
            {
                LogFieldFail(fieldName, "instance field needs valid instance");
                return false;
            }

            // Static MUST pass null obj; instance MUST pass live object (never confuse the two).
            var obj = isStatic ? IntPtr.Zero : instance.NativeObject;
            var raw = Il2CppNative.FieldGetValueObject!(field, obj);

            // Null ref field → success with null (contract).
            if (raw == IntPtr.Zero)
            {
                value = null;
                return true;
            }

            if (!TryMapFieldReturn(raw, out value, out var mapErr))
            {
                LogFieldFail(fieldName, mapErr);
                value = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogFieldFail(fieldName, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Write field. Refs: set_value_object (instance) or static_set_value (static).
    /// bool/int: field_set_value / static_set_value scratch.
    /// </summary>
    public bool TrySetField(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        string fieldName,
        object? value)
    {
        if (!Ready || !Il2CppNative.FieldReady)
        {
            LogFieldFail(fieldName, "FieldReady=false");
            return false;
        }

        if (string.IsNullOrEmpty(fieldName))
        {
            LogFieldFail(fieldName, "empty fieldName");
            return false;
        }

        if (!TryResolveFieldKlass(type, instance, out var klass, out var resolveErr))
        {
            LogFieldFail(fieldName, resolveErr);
            return false;
        }

        IntPtr scratch = IntPtr.Zero;
        try
        {
            var field = FindField(klass, fieldName);
            if (field == IntPtr.Zero)
            {
                LogFieldFail(fieldName, "field not found");
                return false;
            }

            var isStatic = IsStaticField(field) || !instance.IsValid;

            if (value is null || value is RuntimeObjectHandle || value is IntPtr)
            {
                var nativeVal = value switch
                {
                    null => IntPtr.Zero,
                    RuntimeObjectHandle roh => roh.NativeObject,
                    IntPtr ip => ip,
                    _ => IntPtr.Zero,
                };

                if (isStatic)
                {
                    if (Il2CppNative.FieldStaticSetValue == null)
                    {
                        LogFieldFail(fieldName, "FieldStaticSetValue unbound");
                        return false;
                    }

                    scratch = Marshal.AllocHGlobal(IntPtr.Size);
                    Marshal.WriteIntPtr(scratch, nativeVal);
                    Il2CppNative.FieldStaticSetValue(field, scratch);
                    return true;
                }

                if (Il2CppNative.FieldSetValueObject == null)
                {
                    LogFieldFail(fieldName, "FieldSetValueObject unbound");
                    return false;
                }

                Il2CppNative.FieldSetValueObject(instance.NativeObject, field, nativeVal);
                return true;
            }

            if (value is bool b)
            {
                scratch = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(scratch, b ? 1 : 0);
                return WriteValueField(field, instance, isStatic, scratch, fieldName);
            }

            if (value is int ii)
            {
                scratch = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(scratch, ii);
                return WriteValueField(field, instance, isStatic, scratch, fieldName);
            }

            LogFieldFail(fieldName, $"unsupported value type {value.GetType().FullName}");
            return false;
        }
        catch (Exception ex)
        {
            LogFieldFail(fieldName, ex.ToString());
            return false;
        }
        finally
        {
            if (scratch != IntPtr.Zero)
                Marshal.FreeHGlobal(scratch);
        }
    }

    /// <summary>
    /// Process-wide Find hard-fail latch. Once any Find* returns an il2cpp exception (or a later
    /// native abort was observed as process death historically), further TryFindObject calls
    /// return false immediately — including includeInactive=true — without more Find* invokes.
    /// </summary>
    static int _findHardFailed; // 0 = open, 1 = tripped
    static int _findDegradedLogged;

    /// <summary>
    /// Resolve UnityEngine.Object + invoke FindFirstObjectByType / FindObjectOfType / FindAnyObjectByType.
    /// Converts <paramref name="type"/> (Il2CppClass*) → System.Type via class_get_type + type_get_object.
    /// Host marshals onto Unity main thread via <see cref="MainThreadDispatcher"/> (no Multiselect
    /// ClassInjector / AddComponent / op_Equality required).
    /// <para>
    /// <b>0.2.4 harden:</b> on first Find* <c>il2cpp exception</c>, stop the variant cascade, trip a
    /// process-wide circuit, log <c>FindIl2CppException</c> + <c>FindHardFailStopped</c>, return false.
    /// Default overload never escalates to <c>includeInactive:true</c>. After circuit open, both
    /// overloads refuse without further Find*.
    /// </para>
    /// </summary>

    /// <summary>
    /// Metadata-only type dump: object_get_class → class_get_namespace + class_get_name.
    /// No Find / no Unity scene walk; callable from any thread.
    /// </summary>
    public bool TryGetTypeName(RuntimeObjectHandle instance, out string? fullName)
    {
        fullName = null;
        if (!Ready || !Il2CppNative.TypeNameReady)
        {
            LogTypeNameFail("TypeNameReady=false (need object_get_class + class_get_name)");
            return false;
        }

        if (!instance.IsValid)
        {
            LogTypeNameFail("invalid instance handle");
            return false;
        }

        try
        {
            var klass = Il2CppNative.ObjectGetClass!(instance.NativeObject);
            if (klass == IntPtr.Zero)
            {
                LogTypeNameFail("object_get_class returned null");
                return false;
            }

            var namePtr = Il2CppNative.ClassGetName!(klass);
            var name = PtrToUtf8(namePtr);
            if (string.IsNullOrEmpty(name))
            {
                LogTypeNameFail("class_get_name empty");
                return false;
            }

            string? ns = null;
            if (Il2CppNative.ClassGetNamespace != null)
                ns = PtrToUtf8(Il2CppNative.ClassGetNamespace(klass));

            fullName = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            return true;
        }
        catch (Exception ex)
        {
            LogTypeNameFail(ex.ToString());
            return false;
        }
    }

    const int ListFieldsMaxDepth = 32;
    const int ListFieldsMaxNames = 512;

    /// <summary>
    /// Metadata field-name dump: object_get_class → class_get_fields (+ optional parent walk).
    /// Includes instance / static / &lt;X&gt;k__BackingField. Does not read values.
    /// </summary>
    public bool TryListFields(RuntimeObjectHandle instance, out string[]? names)
    {
        names = null;
        if (!Ready || !Il2CppNative.FieldListReady)
        {
            LogListFieldsFail("FieldListReady=false (need object_get_class + class_get_fields + field_get_name)");
            return false;
        }

        if (!instance.IsValid)
        {
            LogListFieldsFail("invalid instance handle");
            return false;
        }

        try
        {
            var klass = Il2CppNative.ObjectGetClass!(instance.NativeObject);
            if (klass == IntPtr.Zero)
            {
                LogListFieldsFail("object_get_class returned null");
                return false;
            }

            var list = new List<string>(64);
            var depth = 0;
            var cur = klass;
            while (cur != IntPtr.Zero && depth < ListFieldsMaxDepth && list.Count < ListFieldsMaxNames)
            {
                CollectFieldsOnClass(cur, list);
                depth++;
                if (Il2CppNative.ClassGetParent == null)
                    break;
                var parent = Il2CppNative.ClassGetParent(cur);
                if (parent == IntPtr.Zero || parent == cur)
                    break;
                cur = parent;
            }

            if (list.Count == 0)
            {
                LogListFieldsFail("no fields enumerated");
                return false;
            }

            names = list.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            LogListFieldsFail(ex.ToString());
            return false;
        }
    }

    static void CollectFieldsOnClass(IntPtr klass, List<string> list)
    {
        IntPtr iter = IntPtr.Zero;
        while (list.Count < ListFieldsMaxNames)
        {
            var field = Il2CppNative.ClassGetFields!(klass, ref iter);
            if (field == IntPtr.Zero)
                break;
            var namePtr = Il2CppNative.FieldGetName!(field);
            var name = PtrToUtf8(namePtr);
            if (string.IsNullOrEmpty(name))
                continue;
            list.Add(name);
        }
    }

    public bool TryFindObject(RuntimeTypeHandle type, bool includeInactive, out RuntimeObjectHandle instance)
    {
        instance = default;
        if (Volatile.Read(ref _findHardFailed) != 0)
        {
            LogFindFail("FindHardFailStopped (circuit open — no further Find*)");
            return false;
        }

        if (!Ready || !Il2CppNative.InvokeReady)
        {
            LogFindFail("InvokeReady=false");
            return false;
        }

        if (!Il2CppNative.TypeObjectReady)
        {
            LogFindFail("TypeObjectReady=false (need class_get_type + type_get_object)");
            return false;
        }

        if (!type.IsValid)
        {
            LogFindFail("invalid type");
            return false;
        }

        // Residual: GCD main queue ≈ process main, not proven identical to Unity player for Find*.
        if (MainThreadDispatcher.FindMarshalDegraded
            && Interlocked.CompareExchange(ref _findDegradedLogged, 1, 0) == 0)
        {
            LogFindFail(
                "FindMarshalDegraded: GcdMainQueue without SyncContext — Find may be off Unity player "
                + "thread (residual; circuit trips on first FindIl2CppException)");
        }

        RuntimeObjectHandle found = default;
        bool ok;
        try
        {
            ok = MainThreadDispatcher.Run(() => TryFindObjectOnMainThread(type, includeInactive, out found));
        }
        catch (Exception ex)
        {
            TripFindHardFail("FindHardFailStopped (dispatcher/body threw)");
            LogFindFail(ex.ToString());
            return false;
        }

        instance = found;
        return ok;
    }

    bool TryFindObjectOnMainThread(RuntimeTypeHandle type, bool includeInactive, out RuntimeObjectHandle instance)
    {
        instance = default;
        try
        {
            if (Volatile.Read(ref _findHardFailed) != 0)
            {
                LogFindFail("FindHardFailStopped (circuit open)");
                return false;
            }

            var il2Type = Il2CppNative.ClassGetType!(type.NativeClass);
            if (il2Type == IntPtr.Zero)
            {
                LogFindFail("class_get_type returned 0");
                return false;
            }

            var typeObj = Il2CppNative.TypeGetObject!(il2Type);
            if (typeObj == IntPtr.Zero)
            {
                LogFindFail("type_get_object returned 0");
                return false;
            }

            var systemType = RuntimeObjectHandle.FromNative(typeObj);

            if (!TryResolveUnityObject(out var objectType))
            {
                LogFindFail("UnityEngine.Object resolve failed");
                return false;
            }

            // Exactly one batch per call — never fall through inactive → active after failures.
            // Default overload uses includeInactive=false and does NOT escalate.
            if (includeInactive)
            {
                // Inactive-only candidates. Stop on first il2cpp exception (historical crash vector).
                if (TryFindStep(objectType, "FindObjectOfType", new object?[] { systemType, true }, out instance))
                    return true;
                if (Volatile.Read(ref _findHardFailed) != 0)
                    return false;
                if (TryFindStep(objectType, "FindFirstObjectByType", new object?[] { systemType, 1 }, out instance))
                    return true;
                if (Volatile.Read(ref _findHardFailed) != 0)
                    return false;
                if (TryFindStep(objectType, "FindAnyObjectByType", new object?[] { systemType, 1 }, out instance))
                    return true;
                return false;
            }

            // Active / default batch only.
            if (TryFindStep(objectType, "FindFirstObjectByType", new object?[] { systemType }, out instance))
                return true;
            if (Volatile.Read(ref _findHardFailed) != 0)
                return false;
            if (TryFindStep(objectType, "FindObjectOfType", new object?[] { systemType }, out instance))
                return true;
            if (Volatile.Read(ref _findHardFailed) != 0)
                return false;
            if (TryFindStep(objectType, "FindAnyObjectByType", new object?[] { systemType }, out instance))
                return true;

            // Methods missing / null — not found (soft miss; circuit stays closed).
            return false;
        }
        catch (Exception ex)
        {
            TripFindHardFail("FindHardFailStopped (main-thread body threw)");
            LogFindFail(ex.ToString());
            return false;
        }
    }

    bool TryResolveUnityObject(out RuntimeTypeHandle objectType)
    {
        if (TryResolveType("UnityEngine.CoreModule", "UnityEngine", "Object", out objectType) && objectType.IsValid)
            return true;
        if (TryResolveType("UnityEngine", "UnityEngine", "Object", out objectType) && objectType.IsValid)
            return true;
        if (TryResolveType(null, "UnityEngine", "Object", out objectType) && objectType.IsValid)
            return true;
        objectType = default;
        return false;
    }

    /// <summary>
    /// One Find* candidate. On il2cpp exception: log FindIl2CppException, trip circuit, stop cascade.
    /// Soft miss (method missing / null result) → false without tripping.
    /// </summary>
    bool TryFindStep(
        RuntimeTypeHandle objectType,
        string methodName,
        object?[] args,
        out RuntimeObjectHandle instance)
    {
        instance = default;
        var outcome = TryInvokeFindRaw(objectType, methodName, args, out instance);
        switch (outcome)
        {
            case FindStepOutcome.Hit:
                return true;
            case FindStepOutcome.Il2CppException:
                LogFindFail($"FindIl2CppException method={methodName}");
                TripFindHardFail("FindHardFailStopped (no more Find* variants / includeInactive escalate)");
                return false;
            case FindStepOutcome.Error:
                // Unexpected managed throw during invoke — treat as hard fail to avoid further Find*.
                TripFindHardFail($"FindHardFailStopped (Find step error method={methodName})");
                return false;
            default:
                // SoftMiss — try next candidate in same batch only.
                return false;
        }
    }

    enum FindStepOutcome
    {
        Hit,
        SoftMiss,
        Il2CppException,
        Error
    }

    FindStepOutcome TryInvokeFindRaw(
        RuntimeTypeHandle objectType,
        string methodName,
        object?[] args,
        out RuntimeObjectHandle instance)
    {
        instance = default;
        if (!Ready || !Il2CppNative.InvokeReady)
            return FindStepOutcome.SoftMiss;

        if (!objectType.IsValid || string.IsNullOrEmpty(methodName))
            return FindStepOutcome.SoftMiss;

        var klass = objectType.NativeClass;
        var argCount = args.Length;
        var method = FindMethod(klass, methodName, argCount);
        if (method == IntPtr.Zero)
            return FindStepOutcome.SoftMiss;

        IntPtr paramsBlock = IntPtr.Zero;
        IntPtr[]? argSlots = null;
        IntPtr[]? valueScratch = null;
        try
        {
            if (argCount > 0)
            {
                if (!TryBuildParams(args, out paramsBlock, out argSlots, out valueScratch, out _))
                    return FindStepOutcome.SoftMiss;
            }

            IntPtr exc = IntPtr.Zero;
            var raw = Il2CppNative.RuntimeInvoke!(method, IntPtr.Zero, paramsBlock, ref exc);
            if (exc != IntPtr.Zero)
            {
                LogFail(methodName, $"il2cpp exception ptr=0x{exc.ToInt64():X}");
                return FindStepOutcome.Il2CppException;
            }

            var result = MapReturn(raw);
            if (result is RuntimeObjectHandle h && h.IsValid)
            {
                instance = h;
                return FindStepOutcome.Hit;
            }

            if (TryAsObjectHandle(result, out var h2) && h2.IsValid)
            {
                instance = h2;
                return FindStepOutcome.Hit;
            }

            // Invoke ok but null → soft miss for this candidate.
            return FindStepOutcome.SoftMiss;
        }
        catch (Exception ex)
        {
            LogFail(methodName, ex.ToString());
            return FindStepOutcome.Error;
        }
        finally
        {
            FreeParams(paramsBlock, argSlots, valueScratch);
        }
    }

    static void TripFindHardFail(string reason)
    {
        if (Interlocked.Exchange(ref _findHardFailed, 1) == 0)
            LogFindFail(reason);
        else
            LogFindFail(reason); // still log each stop site for evidence
    }

    static void LogFindFail(string detail)
    {
        try
        {
            Il2CppHostPlugin.LogSrc?.LogWarning($"[RT] TryFindObject fail: {detail}");
        }
        catch
        {
            // Logging must never throw out of Try*.
        }
    }

    static bool WriteValueField(
        IntPtr field,
        RuntimeObjectHandle instance,
        bool isStatic,
        IntPtr scratch,
        string fieldName)
    {
        if (isStatic)
        {
            if (Il2CppNative.FieldStaticSetValue == null)
            {
                LogFieldFail(fieldName, "FieldStaticSetValue unbound");
                return false;
            }

            Il2CppNative.FieldStaticSetValue(field, scratch);
            return true;
        }

        if (Il2CppNative.FieldSetValue == null)
        {
            LogFieldFail(fieldName, "FieldSetValue unbound");
            return false;
        }

        Il2CppNative.FieldSetValue(instance.NativeObject, field, scratch);
        return true;
    }

    static bool TryResolveFieldKlass(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        out IntPtr klass,
        out string error)
    {
        klass = IntPtr.Zero;
        error = "";

        if (instance.IsValid)
        {
            if (Il2CppNative.ObjectGetClass != null)
            {
                klass = Il2CppNative.ObjectGetClass(instance.NativeObject);
                if (klass != IntPtr.Zero)
                    return true;
            }

            // Fallback: declaring type supplied by caller.
            if (type.IsValid)
            {
                klass = type.NativeClass;
                return true;
            }

            error = "instance valid but object_get_class failed and type invalid";
            return false;
        }

        if (type.IsValid)
        {
            klass = type.NativeClass;
            return true;
        }

        error = "need valid instance or type (static)";
        return false;
    }

    static IntPtr FindField(IntPtr klass, string fieldName)
    {
        var namePtr = Marshal.StringToHGlobalAnsi(fieldName);
        try
        {
            return Il2CppNative.ClassGetFieldFromName!(klass, namePtr);
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
        }
    }

    static bool IsStaticField(IntPtr field)
    {
        if (Il2CppNative.FieldGetFlags == null)
            return false;
        return (Il2CppNative.FieldGetFlags(field) & Il2CppNative.FieldAttributeStatic) != 0;
    }

    static void LogFieldFail(string? fieldName, string detail)
    {
        try
        {
            Il2CppHostPlugin.LogSrc?.LogWarning($"[RT] TryGet/SetField fail field={fieldName}: {detail}");
        }
        catch
        {
            // Logging must never throw out of Try*.
        }
    }

    static IntPtr FindMethod(IntPtr klass, string methodName, int preferredArgCount)
    {
        var hit = CallGetMethod(klass, methodName, preferredArgCount);
        if (hit != IntPtr.Zero)
            return hit;

        // Bounded fallback: common arities only (not a full reflection scan).
        foreach (var n in new[] { 0, 1, 2, 3, 4 })
        {
            if (n == preferredArgCount)
                continue;
            hit = CallGetMethod(klass, methodName, n);
            if (hit != IntPtr.Zero)
                return hit;
        }

        return IntPtr.Zero;
    }

    static IntPtr CallGetMethod(IntPtr klass, string methodName, int argsCount)
    {
        var namePtr = Marshal.StringToHGlobalAnsi(methodName);
        try
        {
            return Il2CppNative.ClassGetMethodFromName!(klass, namePtr, argsCount);
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
        }
    }

    static bool TryBuildParams(
        object?[] args,
        out IntPtr paramsBlock,
        out IntPtr[] argSlots,
        out IntPtr[] valueScratch,
        out string error)
    {
        paramsBlock = IntPtr.Zero;
        argSlots = new IntPtr[args.Length];
        valueScratch = new IntPtr[args.Length];
        error = "";

        paramsBlock = Marshal.AllocHGlobal(IntPtr.Size * args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            var slot = Marshal.AllocHGlobal(IntPtr.Size);
            argSlots[i] = slot;
            Marshal.WriteIntPtr(paramsBlock, i * IntPtr.Size, slot);

            var arg = args[i];
            if (arg is null)
            {
                Marshal.WriteIntPtr(slot, IntPtr.Zero);
                continue;
            }

            if (arg is RuntimeObjectHandle roh)
            {
                Marshal.WriteIntPtr(slot, roh.NativeObject);
                continue;
            }

            if (arg is IntPtr ip)
            {
                Marshal.WriteIntPtr(slot, ip);
                continue;
            }

            if (arg is string s)
            {
                if (Il2CppNative.StringNew == null)
                {
                    error = "StringNew unbound";
                    return false;
                }

                var utf8 = Marshal.StringToHGlobalAnsi(s);
                try
                {
                    var il2Str = Il2CppNative.StringNew(utf8);
                    Marshal.WriteIntPtr(slot, il2Str);
                }
                finally
                {
                    Marshal.FreeHGlobal(utf8);
                }

                continue;
            }

            if (arg is bool b)
            {
                // IL2CPP System.Boolean is 1 byte; 4-byte write was oversized (usually harmless
                // on dedicated scratch, but match ABI — 0.2.7 set_Rotating harden).
                var scratch = Marshal.AllocHGlobal(1);
                valueScratch[i] = scratch;
                Marshal.WriteByte(scratch, (byte)(b ? 1 : 0));
                // Value-type params: params[i] points at the value storage itself.
                Marshal.WriteIntPtr(paramsBlock, i * IntPtr.Size, scratch);
                Marshal.FreeHGlobal(slot);
                argSlots[i] = IntPtr.Zero;
                continue;
            }

            if (arg is int ii)
            {
                var scratch = Marshal.AllocHGlobal(4);
                valueScratch[i] = scratch;
                Marshal.WriteInt32(scratch, ii);
                Marshal.WriteIntPtr(paramsBlock, i * IntPtr.Size, scratch);
                Marshal.FreeHGlobal(slot);
                argSlots[i] = IntPtr.Zero;
                continue;
            }

            error = $"unsupported arg type {arg.GetType().FullName} at [{i}]";
            return false;
        }

        return true;
    }

    static void FreeParams(IntPtr paramsBlock, IntPtr[]? argSlots, IntPtr[]? valueScratch)
    {
        if (argSlots != null)
        {
            foreach (var s in argSlots)
            {
                if (s != IntPtr.Zero)
                    Marshal.FreeHGlobal(s);
            }
        }

        if (valueScratch != null)
        {
            foreach (var s in valueScratch)
            {
                if (s != IntPtr.Zero)
                    Marshal.FreeHGlobal(s);
            }
        }

        if (paramsBlock != IntPtr.Zero)
            Marshal.FreeHGlobal(paramsBlock);
    }

    /// <summary>
    /// Field-only mapper (0.2.7 harden): reject wild non-objects; do not advertise unsupported
    /// valuetypes as <see cref="RuntimeObjectHandle"/> (contract: out of scope → false).
    /// </summary>
    static bool TryMapFieldReturn(IntPtr raw, out object? value, out string error)
    {
        value = null;
        error = "";
        if (raw == IntPtr.Zero)
            return true;

        if (Il2CppNative.ObjectGetClass == null)
        {
            error = "object_get_class unbound — cannot validate field ref";
            return false;
        }

        var klass = Il2CppNative.ObjectGetClass(raw);
        if (klass == IntPtr.Zero)
        {
            error = "wild/non-object pointer (object_get_class=null; not a RuntimeObjectHandle)";
            return false;
        }

        var name = "";
        if (Il2CppNative.ClassGetName != null)
            name = PtrToUtf8(Il2CppNative.ClassGetName(klass)) ?? "";

        if (name == "String")
        {
            value = Il2CppStringToManaged(raw) ?? "";
            return true;
        }

        var isVt = Il2CppNative.ClassIsValuetype != null && Il2CppNative.ClassIsValuetype(klass) != 0;
        if (!isVt && IsKnownBoxedValuetypeName(name))
            isVt = true;

        if (isVt)
        {
            if (name is "Int32" or "Boolean" or "Int64" or "Single" or "Double" or "Byte" or "SByte"
                or "Int16" or "UInt16" or "UInt32" or "UInt64")
            {
                var prim = MapBoxedPrimitive(raw, name);
                // Boolean false is a valid non-null boxed result (MapBoxedPrimitive returns bool).
                if (prim is null)
                {
                    error = $"valuetype {name} unbox failed";
                    return false;
                }
                value = prim;
                return true;
            }

            error = $"valuetype {name} out of scope (not advertised as RuntimeObjectHandle)";
            return false;
        }

        // Reference type with validated klass — opaque handle.
        value = RuntimeObjectHandle.FromNative(raw);
        return true;
    }

    static bool IsKnownBoxedValuetypeName(string name) =>
        name is "Quaternion" or "Vector2" or "Vector3" or "Vector4" or "Color" or "Color32"
            or "Rect" or "Bounds" or "Ray" or "RaycastHit" or "Matrix4x4" or "ProfilerMarker"
            or "NativeArray`1" or "FixedString32Bytes" or "FixedString64Bytes";

    static object? MapBoxedPrimitive(IntPtr raw, string name)
    {
        if (Il2CppNative.ObjectUnbox == null)
            return null;
        var unboxed = Il2CppNative.ObjectUnbox(raw);
        if (unboxed == IntPtr.Zero)
            return null;
        return name switch
        {
            "Boolean" => Marshal.ReadByte(unboxed) != 0,
            "Int32" => Marshal.ReadInt32(unboxed),
            "Int64" => Marshal.ReadInt64(unboxed),
            "Byte" => Marshal.ReadByte(unboxed),
            "SByte" => (sbyte)Marshal.ReadByte(unboxed),
            "Int16" => Marshal.ReadInt16(unboxed),
            "UInt16" => (ushort)Marshal.ReadInt16(unboxed),
            "UInt32" => (uint)Marshal.ReadInt32(unboxed),
            "UInt64" => (ulong)Marshal.ReadInt64(unboxed),
            "Single" => ReadSingle(unboxed),
            "Double" => ReadDouble(unboxed),
            _ => null,
        };
    }

    static object? MapReturn(IntPtr raw)
    {
        if (raw == IntPtr.Zero)
            return null;

        // Prefer string mapping when class name is String.
        if (Il2CppNative.ObjectGetClass != null && Il2CppNative.ClassGetName != null)
        {
            var klass = Il2CppNative.ObjectGetClass(raw);
            // 0.2.7: never ClassGetName(null) — wild ptr would AV / undefined.
            if (klass == IntPtr.Zero)
                return null;

            var namePtr = Il2CppNative.ClassGetName(klass);
            var name = PtrToUtf8(namePtr) ?? "";
            if (name == "String")
                return Il2CppStringToManaged(raw) ?? "";

            if (name is "Int32" or "Boolean" or "Int64" or "Single" or "Double" or "Byte" or "SByte" or "Int16" or "UInt16" or "UInt32" or "UInt64")
            {
                var prim = MapBoxedPrimitive(raw, name);
                if (prim is not null || name == "Boolean")
                    return prim ?? false;
            }
        }

        // Opaque object pointer — Multiselect consumes RuntimeObjectHandle (FromNative stays internal).
        return RuntimeObjectHandle.FromNative(raw);
    }

    static string? Il2CppStringToManaged(IntPtr il2Str)
    {
        if (il2Str == IntPtr.Zero)
            return null;
        if (Il2CppNative.StringChars == null || Il2CppNative.StringLength == null)
            return null;
        var len = Il2CppNative.StringLength(il2Str);
        if (len < 0 || len > 1_000_000)
            return null;
        var chars = Il2CppNative.StringChars(il2Str);
        if (chars == IntPtr.Zero)
            return null;
        return Marshal.PtrToStringUni(chars, len);
    }

    static float ReadSingle(IntPtr p)
    {
        var bytes = new byte[4];
        Marshal.Copy(p, bytes, 0, 4);
        return BitConverter.ToSingle(bytes, 0);
    }

    static double ReadDouble(IntPtr p)
    {
        var bytes = new byte[8];
        Marshal.Copy(p, bytes, 0, 8);
        return BitConverter.ToDouble(bytes, 0);
    }

    static string? PtrToUtf8(IntPtr p) =>
        p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);


    static void LogTypeNameFail(string detail)
    {
        try
        {
            Il2CppHostPlugin.LogSrc?.LogWarning($"[RT] TryGetTypeName fail: {detail}");
        }
        catch
        {
            // Logging must never throw out of Try*.
        }
    }

    static void LogListFieldsFail(string detail)
    {
        try
        {
            Il2CppHostPlugin.LogSrc?.LogWarning($"[RT] TryListFields fail: {detail}");
        }
        catch
        {
            // Logging must never throw out of Try*.
        }
    }

    static void LogFail(string? methodName, string detail)
    {
        try
        {
            Il2CppHostPlugin.LogSrc?.LogWarning($"[RT] TryInvoke fail method={methodName}: {detail}");
        }
        catch
        {
            // Logging must never throw out of TryInvoke.
        }
    }
}
