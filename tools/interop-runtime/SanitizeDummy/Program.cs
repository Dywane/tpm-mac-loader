using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.Builder;
using AsmResolver.PE.DotNet.Metadata.Tables;

static int Usage()
{
    Console.Error.WriteLine("Usage: SanitizeDummy --input <dir> --output <dir> [--strip-all-fields] [--strip-all-methods] [--seed-primitives]");
    return 2;
}

var argsList = args.ToList();
string? GetOpt(string n) { var i = argsList.IndexOf(n); return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : null; }
bool Has(string n) => argsList.Contains(n);

var input = GetOpt("--input");
var output = GetOpt("--output");
if (input == null || output == null) return Usage();
var stripAllFields = Has("--strip-all-fields");
var stripAllMethods = Has("--strip-all-methods"); // default OFF — EnrichDummyMethods needs method defs; Pass90 avoided via empty --unity-libs
var seedPrimitives = Has("--seed-primitives") || true; // default on — required for Pass60

Directory.CreateDirectory(output);

var paths = Directory.EnumerateFiles(input, "*.dll").OrderBy(p => p).ToList();
var assemblies = new List<AssemblyDefinition>();
var typeNames = new HashSet<string>(StringComparer.Ordinal);
foreach (var path in paths)
{
    var asm = AssemblyDefinition.FromFile(path);
    assemblies.Add(asm);
    foreach (var t in asm.Modules.SelectMany(m => m.GetAllTypes()))
    {
        if (t.Name != "<Module>")
            typeNames.Add(t.FullName);
    }
}

int removedFields = 0, removedMethods = 0, removedProps = 0, removedEvents = 0, seeded = 0;
var samples = new List<string>();

bool FieldTypeOk(TypeSignature? sig)
{
    if (sig == null) return false;
    var core = Unwrap(sig);
    if (core is CorLibTypeSignature) return true;
    if (core is GenericParameterSignature) return true;
    TypeDefinition? resolved = null;
    try { resolved = core.Resolve(); } catch { return false; }
    if (resolved == null) return false;
    if (!typeNames.Contains(resolved.FullName)) return false;
    var fn = core.FullName ?? "";
    if (fn.Contains("$BurstDirectCall", StringComparison.Ordinal)) return false;
    if (fn.Contains("Instance+", StringComparison.Ordinal) && fn.Contains('$')) return false;
    return true;
}

static TypeSignature Unwrap(TypeSignature s)
{
    while (true)
    {
        switch (s)
        {
            case SzArrayTypeSignature a: s = a.BaseType; continue;
            case ArrayBaseTypeSignature ab: s = ab.BaseType; continue;
            case ByReferenceTypeSignature b: s = b.BaseType; continue;
            case PointerTypeSignature p: s = p.BaseType; continue;
            case PinnedTypeSignature pin: s = pin.BaseType; continue;
            case GenericInstanceTypeSignature g: s = g.GenericType.ToTypeSignature(); continue;
            default: return s;
        }
    }
}

// --- Seed mscorlib primitives as real value types with m_value (Pass60 needs this) ---
if (seedPrimitives)
{
    var mscorlib = assemblies.FirstOrDefault(a => a.Name == "mscorlib")
                   ?? throw new Exception("mscorlib.dll not in input");
    var mod = mscorlib.ManifestModule ?? mscorlib.Modules[0];
    var corlib = mod.CorLibTypeFactory;

    TypeDefinition? Find(string full)
        => mod.GetAllTypes().FirstOrDefault(t => t.FullName == full);

    var valueType = Find("System.ValueType")
                    ?? throw new Exception("System.ValueType missing");
    var enumType = Find("System.Enum");
    var objectType = Find("System.Object");

    // Ensure ValueType : Object
    if (objectType != null && valueType.BaseType == null)
        valueType.BaseType = objectType;
    // Ensure Enum : ValueType
    if (enumType != null)
        enumType.BaseType = valueType;

    // (fullName, corlib element getter)
    (string name, Func<CorLibTypeSignature> elem)[] primitives =
    {
        ("System.Boolean", () => corlib.Boolean),
        ("System.Byte", () => corlib.Byte),
        ("System.SByte", () => corlib.SByte),
        ("System.Int16", () => corlib.Int16),
        ("System.UInt16", () => corlib.UInt16),
        ("System.Int32", () => corlib.Int32),
        ("System.UInt32", () => corlib.UInt32),
        ("System.Int64", () => corlib.Int64),
        ("System.UInt64", () => corlib.UInt64),
        ("System.Single", () => corlib.Single),
        ("System.Double", () => corlib.Double),
        ("System.Char", () => corlib.Char),
        ("System.IntPtr", () => corlib.IntPtr),
        ("System.UIntPtr", () => corlib.UIntPtr),
    };

    foreach (var (name, elem) in primitives)
    {
        var td = Find(name);
        if (td == null)
        {
            Console.WriteLine($"[Sanitize] WARN missing {name}");
            continue;
        }
        // Force value-type shape
        td.Attributes = TypeAttributes.Public | TypeAttributes.SequentialLayout
                        | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;
        td.BaseType = valueType;
        // Clear bogus fields then add m_value
        td.Fields.Clear();
        var sig = FieldSignature.CreateInstance(elem());
        var field = new FieldDefinition("m_value",
            FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RuntimeSpecialName,
            sig);
        td.Fields.Add(field);
        seeded++;
        Console.WriteLine($"[Sanitize] seeded {name} : ValueType + m_value:{elem().FullName} isValueType={td.IsValueType}");
    }

    // System.Void must be a valuetype (no m_value) or Harmony hits Il2CppSystem.Void TypeLoad mismatch
    {
        var voidTd = Find("System.Void");
        if (voidTd != null)
        {
            voidTd.Attributes = TypeAttributes.Public | TypeAttributes.SequentialLayout
                                | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;
            voidTd.BaseType = valueType;
            voidTd.Fields.Clear();
            Console.WriteLine($"[Sanitize] seeded System.Void : ValueType (empty) isValueType={voidTd.IsValueType}");
        }
    }
}


// --- Seed Delegate / Action hierarchy (Pass60/61 need MulticastDelegate + Invoke + op_Implicit) ---
{
    var mscorlib = assemblies.First(a => a.Name == "mscorlib");
    var mod = mscorlib.ManifestModule ?? mscorlib.Modules[0];
    var corlib = mod.CorLibTypeFactory;
    TypeDefinition? Find(string full) => mod.GetAllTypes().FirstOrDefault(t => t.FullName == full);

    var objectType = Find("System.Object") ?? throw new Exception("System.Object missing");
    var valueType = Find("System.ValueType")!;
    var del = Find("System.Delegate") ?? throw new Exception("System.Delegate missing");
    var mdel = Find("System.MulticastDelegate") ?? throw new Exception("System.MulticastDelegate missing");

    del.Attributes = TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit;
    del.BaseType = objectType;
    del.Interfaces.Clear();
    // Pass60 op_Addition/Subtraction need Il2CppSystem.Delegate.Combine/Remove on rewritten type
    foreach (var m in del.Methods.Where(m => {
        var n = m.Name?.ToString();
        return n == "Combine" || n == "Remove";
    }).ToList()) del.Methods.Remove(m);
    var delSig = del.ToTypeSignature();
    del.Methods.Add(new MethodDefinition("Combine",
        MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
        MethodSignature.CreateStatic(delSig, delSig, delSig)));
    del.Methods.Add(new MethodDefinition("Remove",
        MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
        MethodSignature.CreateStatic(delSig, delSig, delSig)));
    Console.WriteLine("[Sanitize] seeded System.Delegate.Combine/Remove");

    mdel.Attributes = TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit;
    mdel.BaseType = del;
    mdel.Interfaces.Clear();

    void MakeDelegate(string fullName, TypeSignature[] invokeParams, TypeSignature invokeRet)
    {
        var td = Find(fullName);
        if (td == null)
        {
            Console.WriteLine($"[Sanitize] WARN missing delegate {fullName}");
            return;
        }
        td.Attributes = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;
        td.BaseType = mdel;
        td.Interfaces.Clear();
        // Ensure Invoke exists for Pass60 AddDelegateConversions
        foreach (var m in td.Methods.Where(m => m.Name?.ToString() == "Invoke").ToList())
            td.Methods.Remove(m);
        var inv = new MethodDefinition("Invoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            MethodSignature.CreateInstance(invokeRet, invokeParams));
        td.Methods.Add(inv);
        Console.WriteLine($"[Sanitize] seeded delegate {fullName} : MulticastDelegate + Invoke({invokeParams.Length})");
    }

    MakeDelegate("System.Action", Array.Empty<TypeSignature>(), corlib.Void);
    MakeDelegate("System.AsyncCallback", new[] { corlib.Object /* IAsyncResult approx */ }, corlib.Void);

    // Harmony / Il2CppHarmonySupport needs Type.op_Equality for patch matching
    {
        var typeTd = Find("System.Type");
        if (typeTd != null)
        {
            var typeSig = typeTd.ToTypeSignature();
            foreach (var n in new[] { "op_Equality", "op_Inequality" })
            {
                foreach (var m in typeTd.Methods.Where(m => m.Name?.ToString() == n).ToList())
                    typeTd.Methods.Remove(m);
                typeTd.Methods.Add(new MethodDefinition(n,
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                    MethodSignature.CreateStatic(corlib.Boolean, typeSig, typeSig)));
            }
            foreach (var m in typeTd.Methods.Where(m => m.Name?.ToString() == "internal_from_handle").ToList())
                typeTd.Methods.Remove(m);
            typeTd.Methods.Add(new MethodDefinition("internal_from_handle",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                MethodSignature.CreateStatic(typeSig, corlib.IntPtr)));
            Console.WriteLine("[Sanitize] seeded System.Type.op_Equality/op_Inequality/internal_from_handle");
        }
    }


    // Action`1 .. Action`4, Func`1 .. Func`5 — use generic params already on type if present
    for (int n = 1; n <= 4; n++)
    {
        var name = n == 1 ? "System.Action`1" : $"System.Action`{n}";
        var td = Find(name);
        if (td == null) continue;
        // Ensure generic params
        while (td.GenericParameters.Count < n)
            td.GenericParameters.Add(new GenericParameter($"T{td.GenericParameters.Count}"));
        var paramSigs = Enumerable.Range(0, n)
            .Select(i => (TypeSignature)new GenericParameterSignature(GenericParameterType.Type, i))
            .ToArray();
        MakeDelegate(name, paramSigs, corlib.Void);
    }
    for (int n = 1; n <= 5; n++)
    {
        var name = $"System.Func`{n}";
        var td = Find(name);
        if (td == null) continue;
        while (td.GenericParameters.Count < n)
            td.GenericParameters.Add(new GenericParameter($"T{td.GenericParameters.Count}"));
        var paramSigs = Enumerable.Range(0, n - 1)
            .Select(i => (TypeSignature)new GenericParameterSignature(GenericParameterType.Type, i))
            .ToArray();
        var ret = new GenericParameterSignature(GenericParameterType.Type, n - 1);
        MakeDelegate(name, paramSigs, ret);
    }
}


foreach (var asm in assemblies)
{
    foreach (var mod in asm.Modules)
    {
        foreach (var t in mod.GetAllTypes().ToList())
        {
            if (t.Name == "<Module>") continue;

            foreach (var f in t.Fields.ToList())
            {
                // Never strip seeded m_value on primitives
                var tName = t.Name?.ToString() ?? "";
                var tNs = t.Namespace?.ToString() ?? "";
                if (f.Name?.ToString() == "m_value" && tNs == "System" &&
                    tName is "Boolean" or "Byte" or "SByte" or "Int16" or "UInt16"
                        or "Int32" or "UInt32" or "Int64" or "UInt64"
                        or "Single" or "Double" or "Char" or "IntPtr" or "UIntPtr")
                    continue;

                bool drop = stripAllFields || !FieldTypeOk(f.Signature?.FieldType);
                if (drop)
                {
                    t.Fields.Remove(f);
                    removedFields++;
                    if (samples.Count < 30)
                        samples.Add($"field {mod.Name}::{t.FullName}.{f.Name} type={f.Signature?.FieldType?.FullName ?? "<null>"}");
                }
            }

            foreach (var p in t.Properties.ToList())
            {
                var pt = p.Signature?.ReturnType;
                if (stripAllFields || p.Signature == null || (pt != null && !FieldTypeOk(pt)))
                {
                    t.Properties.Remove(p);
                    removedProps++;
                }
            }

            foreach (var e in t.Events.ToList())
            {
                TypeSignature? es = null;
                try { es = e.EventType?.ToTypeSignature(); } catch { }
                if (stripAllFields || es == null || !FieldTypeOk(es))
                {
                    t.Events.Remove(e);
                    removedEvents++;
                }
            }

            if (stripAllMethods)
            {
                var tNs = t.Namespace?.ToString() ?? "";
                var tName = t.Name?.ToString() ?? "";
                foreach (var m in t.Methods.ToList())
                {
                    var mn = m.Name?.ToString() ?? "";
                    bool keep = tNs == "System" && (
                        ((tName == "Delegate" || tName == "MulticastDelegate") && (mn == "Combine" || mn == "Remove"))
                        || (tName == "Type" && (mn == "op_Equality" || mn == "op_Inequality" || mn == "internal_from_handle"))
                        || (mn == "Invoke" && (tName == "Action" || tName.StartsWith("Action`", StringComparison.Ordinal)
                            || tName.StartsWith("Func`", StringComparison.Ordinal) || tName == "AsyncCallback")));
                    if (keep) continue;
                    t.Methods.Remove(m);
                    removedMethods++;
                }
            }
        }
    }
}


// --- Repair Dummy BaseType Object`1 (soft hierarchy wrote System.Object<System.Object>) ---
{
    int repaired = 0;
    foreach (var asm in assemblies)
    {
        foreach (var mod in asm.Modules)
        {
            var objectDef = mod.GetAllTypes().FirstOrDefault(td =>
                td.Name?.ToString() == "Object" && td.GenericParameters.Count == 0
                && (td.Namespace?.ToString() == "System" || string.IsNullOrEmpty(td.Namespace?.ToString())));
            // Prefer System.Object in mscorlib; for other assemblies use TypeRef later
            ITypeDefOrRef? objectRef = objectDef;
            if (objectRef == null)
            {
                var mscorlibAsm = assemblies.FirstOrDefault(a => a.Name == "mscorlib");
                var mscorlibObj = mscorlibAsm?.Modules.SelectMany(m => m.GetAllTypes())
                    .FirstOrDefault(td => td.FullName == "System.Object");
                if (mscorlibObj != null)
                {
                    var aref = mod.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib");
                    if (aref == null)
                    {
                        aref = new AssemblyReference("mscorlib", new Version(4, 0, 0, 0));
                        mod.AssemblyReferences.Add(aref);
                    }
                    objectRef = new TypeReference(mod, aref, "System", "Object");
                }
            }
            if (objectRef == null) continue;

            bool IsBogus(ITypeDefOrRef? bt)
            {
                if (bt == null) return false;
                try
                {
                    var s = bt.ToTypeSignature();
                    return s is GenericInstanceTypeSignature gi && gi.GenericType?.Name?.ToString() == "Object";
                }
                catch { return false; }
            }

            foreach (var td in mod.GetAllTypes())
            {
                if (td.Name?.ToString() == "<Module>") continue;
                if (IsBogus(td.BaseType))
                {
                    td.BaseType = objectRef;
                    repaired++;
                }
                foreach (var ii in td.Interfaces.ToList())
                {
                    if (IsBogus(ii.Interface))
                    {
                        td.Interfaces.Remove(ii);
                        repaired++;
                    }
                }
            }
        }
    }
    Console.WriteLine($"[Sanitize] repaired Object`1 bases/ifaces={repaired}");
}

var imageBuilder = new ManagedPEImageBuilder();
var fileBuilder = new ManagedPEFileBuilder();
int written = 0;
foreach (var asm in assemblies)
{
    var mod = asm.ManifestModule ?? asm.Modules[0];
    var name = (mod.Name?.ToString()) ?? (asm.Name + ".dll");
    if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) name += ".dll";
    var outPath = Path.Combine(output, name);
    var result = imageBuilder.CreateImage(mod);
    if (result.ConstructedImage == null)
        throw new Exception("CreateImage failed for " + name);
    fileBuilder.CreateFile(result.ConstructedImage).Write(outPath);
    written++;
}

Console.WriteLine($"[Sanitize] in={paths.Count} out={written} seededPrimitives={seeded}");
Console.WriteLine($"[Sanitize] removed fields={removedFields} props={removedProps} events={removedEvents} methods={removedMethods}");
foreach (var s in samples.Take(15)) Console.WriteLine("  " + s);
return 0;
