using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Builder;
using AsmResolver.PE.Builder;
using AsmResolver.PE.DotNet.Metadata.Tables;

static int Usage()
{
    Console.Error.WriteLine("Usage: DeduplicateInteropTypes --input <dir> [--output <dir>] [--inplace]");
    return 2;
}

var argsList = args.ToList();
string? GetOpt(string n) { var i = argsList.IndexOf(n); return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : null; }
bool Has(string n) => argsList.Contains(n);

var input = GetOpt("--input");
if (input == null) return Usage();
var output = GetOpt("--output");
var inplace = Has("--inplace") || output == null;
if (inplace) output = input;
Directory.CreateDirectory(output!);

int totalRenamed = 0, totalRepaired = 0, totalVis = 0, totalHandleBases = 0, totalSealedBases = 0, filesTouched = 0, filesOk = 0, filesFail = 0;
var imageBuilder = new ManagedPEImageBuilder();
var fileBuilder = new ManagedPEFileBuilder();

foreach (var path in Directory.EnumerateFiles(input, "*.dll").OrderBy(p => p))
{
    try
    {
        var asm = AssemblyDefinition.FromFile(path);
        var mod = asm.ManifestModule ?? asm.Modules[0];
        int renamed = DedupModule(mod);
        int repaired = RepairInvalidObjectUsages(mod);
        int vis = FixOrphanNestedVisibility(mod);
        int handleBases = RepairBogusHandleBases(mod);
        int sealedBases = RepairSealedOrPrimitiveBases(mod);
        if (renamed > 0 || repaired > 0 || vis > 0 || handleBases > 0 || sealedBases > 0) filesTouched++;
        totalRenamed += renamed;
        totalRepaired += repaired;
        totalVis += vis;
        totalHandleBases += handleBases;
        totalSealedBases += sealedBases;
        if (repaired > 0)
            Console.WriteLine($"[Repair] {Path.GetFileName(path)}: objectUsagesFixed={repaired}");
        if (vis > 0)
            Console.WriteLine($"[Vis] {Path.GetFileName(path)}: orphanNestedVisibilityFixed={vis}");
        if (handleBases > 0)
            Console.WriteLine($"[HandleBase] {Path.GetFileName(path)}: bogusHandleBasesFixed={handleBases}");
        if (sealedBases > 0)
            Console.WriteLine($"[SealedBase] {Path.GetFileName(path)}: sealedOrPrimitiveBasesFixed={sealedBases}");
        if (renamed > 0)
            Console.WriteLine($"[Dedup] {Path.GetFileName(path)}: renamed={renamed}");

        var name = mod.Name?.ToString() ?? Path.GetFileName(path);
        if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) name += ".dll";
        var outPath = Path.Combine(output!, name);

        var tmp = outPath + ".tmp";
        var result = imageBuilder.CreateImage(mod);
        if (result.ConstructedImage == null)
            throw new Exception("CreateImage failed");
        fileBuilder.CreateFile(result.ConstructedImage).Write(tmp);
        if (File.Exists(outPath)) File.Delete(outPath);
        File.Move(tmp, outPath);
        filesOk++;
    }
    catch (Exception ex)
    {
        filesFail++;
        Console.Error.WriteLine($"[Dedup] FAIL {Path.GetFileName(path)}: {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine($"[Dedup] done filesOk={filesOk} filesFail={filesFail} filesTouched={filesTouched} renamed={totalRenamed} objectRepaired={totalRepaired} visFixed={totalVis} handleBasesFixed={totalHandleBases} sealedBasesFixed={totalSealedBases}");
return filesFail == 0 ? 0 : 1;

static int DedupModule(ModuleDefinition mod)
{
    int renamed = 0;
    foreach (var group in mod.GetAllTypes()
                 .Where(t => t.Name?.ToString() != "<Module>")
                 .GroupBy(t => t.FullName)
                 .Where(g => g.Count() > 1))
    {
        int i = 0;
        foreach (var t in group)
        {
            if (i == 0) { i++; continue; }
            var old = t.Name?.ToString() ?? "T";
            t.Name = $"{old}_d{i}";
            renamed++;
            i++;
        }
    }

    foreach (var group in mod.GetAllTypes()
                 .Where(t => t.Name?.ToString() != "<Module>")
                 .GroupBy(t => (t.DeclaringType?.FullName ?? "<root>") + "\0" + (t.Name?.ToString() ?? ""))
                 .Where(g => g.Count() > 1))
    {
        int i = 0;
        foreach (var t in group)
        {
            if (i == 0) { i++; continue; }
            var old = t.Name?.ToString() ?? "T";
            t.Name = $"{old}_s{i}";
            renamed++;
            i++;
        }
    }

    int guard = 0;
    while (guard++ < 5)
    {
        var dups = mod.GetAllTypes()
            .Where(t => t.Name?.ToString() != "<Module>")
            .GroupBy(t => t.FullName)
            .Where(g => g.Count() > 1)
            .ToList();
        if (dups.Count == 0) break;
        foreach (var g in dups)
        {
            int i = 0;
            foreach (var t in g)
            {
                if (i++ == 0) continue;
                t.Name = $"{t.Name}_x{guard}_{i}";
                renamed++;
            }
        }
    }
    return renamed;
}

static bool IsBogusObjectGenericSig(TypeSignature? s)
{
    if (s is GenericInstanceTypeSignature gi)
        return gi.GenericType?.Name?.ToString() == "Object";
    return false;
}

static bool IsBogusObjectGenericRef(ITypeDefOrRef? bt)
{
    if (bt == null) return false;
    try { return IsBogusObjectGenericSig(bt.ToTypeSignature()); }
    catch { return false; }
}

static TypeSignature? RewriteSig(TypeSignature? s, ITypeDefOrRef objectRef)
{
    if (s == null) return null;
    if (IsBogusObjectGenericSig(s))
        return objectRef.ToTypeSignature();
    return s switch
    {
        SzArrayTypeSignature a => new SzArrayTypeSignature(RewriteSig(a.BaseType, objectRef)!),
        ByReferenceTypeSignature b => new ByReferenceTypeSignature(RewriteSig(b.BaseType, objectRef)!),
        PointerTypeSignature p => new PointerTypeSignature(RewriteSig(p.BaseType, objectRef)!),
        PinnedTypeSignature pin => new PinnedTypeSignature(RewriteSig(pin.BaseType, objectRef)!),
        GenericInstanceTypeSignature gi => RewriteGenericInstance(gi, objectRef),
        _ => s
    };
}

static GenericInstanceTypeSignature RewriteGenericInstance(GenericInstanceTypeSignature gi, ITypeDefOrRef objectRef)
{
    // If the generic type itself is Object — should already be caught; still rebuild args
    var args = gi.TypeArguments.Select(a => RewriteSig(a, objectRef) ?? a).ToArray();
    bool changed = false;
    for (int i = 0; i < args.Length; i++)
        if (!ReferenceEquals(args[i], gi.TypeArguments[i])) changed = true;
    if (!changed && !IsBogusObjectGenericSig(gi)) return gi;
    if (IsBogusObjectGenericSig(gi))
        return objectRef.ToTypeSignature() as GenericInstanceTypeSignature ?? gi; // shouldn't happen
    var neu = new GenericInstanceTypeSignature(gi.GenericType, gi.IsValueType);
    foreach (var a in args) neu.TypeArguments.Add(a);
    return neu;
}

static ITypeDefOrRef ResolveObjectRef(ModuleDefinition mod)
{
    var localObject = mod.GetAllTypes()
        .FirstOrDefault(t => t.Name?.ToString() == "Object" && t.GenericParameters.Count == 0
                             && (t.Namespace?.ToString() == "Il2CppSystem" || t.Namespace?.ToString() == "System"));
    if (localObject != null) return localObject;

    var asmRef = mod.AssemblyReferences.FirstOrDefault(a => a.Name == "Il2Cppmscorlib")
                 ?? mod.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib");
    if (asmRef == null)
    {
        asmRef = new AssemblyReference("Il2Cppmscorlib", new Version(4, 0, 0, 0));
        mod.AssemblyReferences.Add(asmRef);
    }
    return new TypeReference(mod, asmRef, "Il2CppSystem", "Object");
}

static int RepairInvalidObjectUsages(ModuleDefinition mod)
{
    // Detect any bogus Object`1 in bases/ifaces/constraints/field/method/prop
    bool AnyBogus()
    {
        foreach (var t in mod.GetAllTypes())
        {
            if (IsBogusObjectGenericRef(t.BaseType)) return true;
            if (t.Interfaces.Any(ii => IsBogusObjectGenericRef(ii.Interface))) return true;
            foreach (var gp in t.GenericParameters)
                foreach (var c in gp.Constraints)
                    if (IsBogusObjectGenericRef(c.Constraint)) return true;
            foreach (var f in t.Fields)
                if (IsBogusObjectGenericSig(f.Signature?.FieldType)) return true;
            foreach (var m in t.Methods)
            {
                var sig = m.Signature; if (sig == null) continue;
                if (IsBogusObjectGenericSig(sig.ReturnType)) return true;
                if (sig.ParameterTypes.Any(IsBogusObjectGenericSig)) return true;
            }
        }
        return false;
    }
    if (!AnyBogus()) return 0;

    var objectRef = ResolveObjectRef(mod);
    int fixedCount = 0;

    foreach (var t in mod.GetAllTypes())
    {
        if (t.Name?.ToString() == "<Module>") continue;

        if (IsBogusObjectGenericRef(t.BaseType))
        {
            t.BaseType = objectRef;
            fixedCount++;
        }
        foreach (var ii in t.Interfaces.ToList())
        {
            if (IsBogusObjectGenericRef(ii.Interface))
            {
                t.Interfaces.Remove(ii);
                fixedCount++;
            }
        }
        foreach (var gp in t.GenericParameters)
        {
            foreach (var c in gp.Constraints.ToList())
            {
                if (IsBogusObjectGenericRef(c.Constraint))
                {
                    c.Constraint = objectRef;
                    fixedCount++;
                }
            }
        }
        foreach (var f in t.Fields)
        {
            if (f.Signature == null) continue;
            var neu = RewriteSig(f.Signature.FieldType, objectRef);
            if (neu != null && !ReferenceEquals(neu, f.Signature.FieldType))
            {
                f.Signature.FieldType = neu;
                fixedCount++;
            }
        }
        foreach (var m in t.Methods)
        {
            var sig = m.Signature; if (sig == null) continue;
            var ret = RewriteSig(sig.ReturnType, objectRef);
            if (ret != null && !ReferenceEquals(ret, sig.ReturnType))
            {
                sig.ReturnType = ret;
                fixedCount++;
            }
            for (int i = 0; i < sig.ParameterTypes.Count; i++)
            {
                var p = RewriteSig(sig.ParameterTypes[i], objectRef);
                if (p != null && !ReferenceEquals(p, sig.ParameterTypes[i]))
                {
                    sig.ParameterTypes[i] = p;
                    fixedCount++;
                }
            }
        }
        foreach (var p in t.Properties)
        {
            if (p.Signature == null) continue;
            var rt = RewriteSig(p.Signature.ReturnType, objectRef);
            if (rt != null && !ReferenceEquals(rt, p.Signature.ReturnType))
            {
                // PropertySignature return type may be settable via reconstructing — try
                try
                {
                    typeof(PropertySignature).GetProperty("ReturnType")?.SetValue(p.Signature, rt);
                    fixedCount++;
                }
                catch { /* ignore */ }
            }
        }
    }
    return fixedCount;
}

/// <summary>
/// Root types with Nested* visibility but no DeclaringType → CLR TypeLoadException.
/// Demote NestedPublic/NestedPrivate/... to Public/NotPublic.
/// </summary>
static int FixOrphanNestedVisibility(ModuleDefinition mod)
{
    int fixedCount = 0;
    foreach (var t in mod.GetAllTypes())
    {
        if (t.Name?.ToString() == "<Module>") continue;
        if (t.DeclaringType != null) continue; // truly nested — OK

        var vis = t.Attributes & TypeAttributes.VisibilityMask;
        TypeAttributes neu = vis switch
        {
            TypeAttributes.NestedPublic => TypeAttributes.Public,
            TypeAttributes.NestedPrivate => TypeAttributes.NotPublic,
            TypeAttributes.NestedFamily => TypeAttributes.NotPublic,
            TypeAttributes.NestedAssembly => TypeAttributes.NotPublic,
            TypeAttributes.NestedFamilyAndAssembly => TypeAttributes.NotPublic,
            TypeAttributes.NestedFamilyOrAssembly => TypeAttributes.Public,
            _ => vis
        };
        if (neu != vis)
        {
            t.Attributes = (t.Attributes & ~TypeAttributes.VisibilityMask) | neu;
            fixedCount++;
        }
    }
    return fixedCount;
}

/// <summary>
/// Dummy/Il2CppInterop hierarchy artifact: a class gets BaseType = Runtime*Handle
/// (often sealed, e.g. Il2CppMono.RuntimePropertyHandle). CLR TypeLoadException.
/// Rewrite to Il2CppSystem.Object. Bounded to Runtime*Handle / Mono.*Handle names on
/// non-valuetype/non-enum/non-interface types. Does NOT touch Enum→ValueType.
/// Idempotent.
/// </summary>
static bool IsBogusHandleBase(ITypeDefOrRef? bt)
{
    if (bt == null) return false;
    var name = bt.Name?.ToString() ?? "";
    if (name is "RuntimePropertyHandle" or "RuntimeTypeHandle" or "RuntimeMethodHandle"
        or "RuntimeFieldHandle" or "RuntimeArgumentHandle")
        return true;
    var ns = bt.Namespace?.ToString() ?? "";
    if (name.EndsWith("Handle", StringComparison.Ordinal)
        && (ns.Contains("Mono", StringComparison.Ordinal)
            || ns.Contains("Il2CppMono", StringComparison.Ordinal)))
        return true;
    return false;
}

static int RepairBogusHandleBases(ModuleDefinition mod)
{
    int fixedCount = 0;
    ITypeDefOrRef? objectRef = null;
    foreach (var t in mod.GetAllTypes())
    {
        if (t.Name?.ToString() == "<Module>") continue;
        if (t.IsValueType || t.IsEnum || t.IsInterface) continue;
        if (!IsBogusHandleBase(t.BaseType)) continue;
        objectRef ??= ResolveObjectRef(mod);
        Console.WriteLine($"[HandleBase] {t.FullName}: {t.BaseType?.FullName} -> Il2CppSystem.Object");
        t.BaseType = objectRef;
        fixedCount++;
    }
    return fixedCount;
}

/// <summary>
/// Dummy/dedup artifact: a class gets BaseType = sealed type (BCL primitive like
/// System.Byte, or wrong sealed peer after FullName/_dN collision). CLR TypeLoadException
/// "because the parent type is sealed." Rewrite to Il2CppObjectBase when the module
/// already references Il2CppInterop.Runtime; else Il2CppSystem.Object.
/// Skips valuetype/enum/interface. Idempotent. Does NOT unseal legitimate parents.
/// </summary>
static bool IsPrimitiveBaseName(string name) => name is
    "Byte" or "SByte" or "Int16" or "UInt16" or "Int32" or "UInt32" or "Int64" or "UInt64"
    or "Boolean" or "Char" or "Single" or "Double" or "Decimal" or "String" or "IntPtr" or "UIntPtr";

static bool IsPrimitiveBase(ITypeDefOrRef? bt)
{
    if (bt == null) return false;
    var name = bt.Name?.ToString() ?? "";
    if (!IsPrimitiveBaseName(name)) return false;
    var ns = bt.Namespace?.ToString() ?? "";
    // System / Il2CppSystem primitives only — avoid false positives on game types named Byte etc.
    return ns is "" or "System" or "Il2CppSystem";
}

static bool IsSealedBase(ITypeDefOrRef? bt)
{
    if (bt == null) return false;
    TypeDefinition? def = bt as TypeDefinition;
    if (def == null)
    {
        try { def = bt.Resolve(); } catch { /* unresolved ref */ }
    }
    return def?.IsSealed == true;
}

static ITypeDefOrRef ResolveSafeClassBase(ModuleDefinition mod)
{
    // Prefer Il2CppObjectBase — matches healthy Il2CppInterop stubs in TPS.Game
    var iio = mod.AssemblyReferences.FirstOrDefault(a => a.Name == "Il2CppInterop.Runtime");
    if (iio != null)
        return new TypeReference(mod, iio, "Il2CppInterop.Runtime.InteropTypes", "Il2CppObjectBase");
    return ResolveObjectRef(mod);
}

static int RepairSealedOrPrimitiveBases(ModuleDefinition mod)
{
    int fixedCount = 0;
    ITypeDefOrRef? safeBase = null;
    foreach (var t in mod.GetAllTypes())
    {
        if (t.Name?.ToString() == "<Module>") continue;
        if (t.IsValueType || t.IsEnum || t.IsInterface) continue;
        if (t.BaseType == null) continue;
        if (!IsPrimitiveBase(t.BaseType) && !IsSealedBase(t.BaseType)) continue;
        // Already pointing at our safe base — skip
        var bn = t.BaseType.Name?.ToString() ?? "";
        if (bn is "Il2CppObjectBase" or "Object") continue;
        safeBase ??= ResolveSafeClassBase(mod);
        Console.WriteLine($"[SealedBase] {t.FullName}: {t.BaseType.FullName} -> {safeBase.FullName}");
        t.BaseType = safeBase;
        fixedCount++;
    }
    return fixedCount;
}

