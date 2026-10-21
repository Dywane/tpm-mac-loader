using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.Builder;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Primitives;
using LibCpp2IL;
using LibCpp2IL.Metadata;

static int Usage()
{
    Console.Error.WriteLine(
        "Usage: EnrichDummyMethods --input <dummyDir> --output <outDir> --metadata <global-metadata.dat> [--unity 6000.0.63f1] [--only-zero-param] [--include-getters]");
    return 2;
}

var argsList = args.ToList();
string? GetOpt(string n)
{
    var i = argsList.IndexOf(n);
    return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : null;
}
bool Has(string n) => argsList.Contains(n);

var input = GetOpt("--input");
var output = GetOpt("--output");
var metaPath = GetOpt("--metadata");
if (input == null || output == null || metaPath == null) return Usage();
var onlyZeroParam = Has("--only-zero-param") || true; // v1: signatures without binary
var includeGetters = Has("--include-getters"); // default off — void would lie

Directory.CreateDirectory(output);

var unityStr = GetOpt("--unity") ?? "6000.0.63f1";
// 6000.0.63f1 → UnityVersion(6000,0,63,Final,1)
var unity = new UnityVersion(6000, 0, 63, UnityVersionType.Final, 1);
var metaBytes = File.ReadAllBytes(metaPath);
var metadata = Il2CppMetadata.ReadFrom(metaBytes, unity);
LibCpp2IlMain.TheMetadata = metadata;
Console.WriteLine($"[Enrich] metadata v{metadata.MetadataVersion} typeDefs={metadata.typeDefs.Length} methodDefs={metadata.methodDefs.Length}");

// Priority names always emitted (even if filters would skip)
var priority = new HashSet<string>(StringComparer.Ordinal)
{
    "OnBecomeActive", "OnBecomeInactive", "RestartItemPlaceState",
    "BecomeActive", "OnEnter", "Enter", "OnExit", "Exit",
};

var paths = Directory.EnumerateFiles(input, "*.dll").OrderBy(p => p).ToList();
var assemblies = paths.Select(AssemblyDefinition.FromFile).ToList();

// Index Dummy types by Namespace+Name and by FullName
var byNsName = new Dictionary<(string ns, string name), List<TypeDefinition>>();
var byFull = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
var typeModule = new Dictionary<TypeDefinition, ModuleDefinition>();
foreach (var asm in assemblies)
{
    foreach (var mod in asm.Modules)
    {
        foreach (var t in mod.GetAllTypes())
        {
            if (t.Name?.ToString() == "<Module>") continue;
            var ns = t.Namespace?.ToString() ?? "";
            var name = t.Name?.ToString() ?? "";
            var key = (ns, name);
            if (!byNsName.TryGetValue(key, out var list))
                byNsName[key] = list = new List<TypeDefinition>();
            list.Add(t);
            byFull[t.FullName] = t;
            typeModule[t] = mod;
        }
    }
}
Console.WriteLine($"[Enrich] dummy types indexed={byFull.Count}");

int typesTouched = 0, methodsAdded = 0, methodsSkipped = 0, typesMissing = 0;
var placementProof = new List<string>();

foreach (var td in metadata.typeDefs)
{
    var ns = td.Namespace ?? "";
    var name = td.Name ?? "";
    if (string.IsNullOrEmpty(name)) continue;

    // Nested types in metadata: Name is short; DeclaringType chain → Dummy nested FullName uses +
    string dummyFull;
    if (td.DeclaringType != null)
    {
        var parts = new Stack<string>();
        parts.Push(name);
        var walk = td.DeclaringType;
        while (walk != null)
        {
            parts.Push(walk.Name ?? "?");
            ns = walk.Namespace ?? ns;
            walk = walk.DeclaringType;
        }
        dummyFull = (string.IsNullOrEmpty(ns) ? "" : ns + ".") + string.Join("+", parts);
    }
    else
    {
        dummyFull = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    if (!byFull.TryGetValue(dummyFull, out var dummyType))
    {
        // fallback ns+name (non-nested)
        if (!byNsName.TryGetValue((ns, name), out var cands) || cands.Count == 0)
        {
            typesMissing++;
            continue;
        }
        dummyType = cands[0];
    }

    var existing = new HashSet<string>(
        dummyType.Methods.Select(m => $"{m.Name}/{m.Parameters.Count}/{(m.IsStatic ? "S" : "I")}"),
        StringComparer.Ordinal);

    var mod = typeModule[dummyType];
    var corlib = mod.CorLibTypeFactory;
    int addedHere = 0;

    Il2CppMethodDefinition[]? methods;
    try { methods = td.Methods; }
    catch { continue; }
    if (methods == null) continue;

    foreach (var md in methods)
    {
        var mname = md.Name ?? "";
        if (mname is ".ctor" or ".cctor") { methodsSkipped++; continue; }

        bool isPriority = priority.Contains(mname);
        bool isGetter = mname.StartsWith("get_", StringComparison.Ordinal);
        bool isSetter = mname.StartsWith("set_", StringComparison.Ordinal);

        if (onlyZeroParam && md.parameterCount != 0 && !isPriority)
        {
            methodsSkipped++;
            continue;
        }
        if ((isGetter || isSetter) && !includeGetters && !isPriority)
        {
            methodsSkipped++;
            continue;
        }

        // Without binary we only trust void() for zero-param non-accessors.
        // Priority zero-param methods are emitted as void instance/static.
        if (md.parameterCount != 0)
        {
            methodsSkipped++;
            continue;
        }

        // Convert System.Reflection.MethodAttributes → AsmResolver MethodAttributes
        var raw = (uint)md.Attributes;
        raw &= ~(uint)System.Reflection.MethodAttributes.Abstract;
        if ((raw & (uint)System.Reflection.MethodAttributes.Virtual) != 0)
            raw |= (uint)System.Reflection.MethodAttributes.NewSlot;
        var attrs = (MethodAttributes)raw;

        bool isStatic = md.IsStatic;
        var key = $"{mname}/0/{(isStatic ? "S" : "I")}";
        if (existing.Contains(key))
        {
            methodsSkipped++;
            continue;
        }

        MethodSignature sig = isStatic
            ? MethodSignature.CreateStatic(corlib.Void)
            : MethodSignature.CreateInstance(corlib.Void);
        var method = new MethodDefinition(mname, attrs, sig);
        dummyType.Methods.Add(method);
        existing.Add(key);
        methodsAdded++;
        addedHere++;

        if (dummyType.Name?.ToString() == "CursorModePlaceItem" &&
            (mname.Contains("OnBecome") || mname.Contains("Restart") || isPriority))
            placementProof.Add($"{dummyType.FullName}::{mname} attrs={attrs}");
    }

    if (addedHere > 0) typesTouched++;
}

Console.WriteLine($"[Enrich] typesTouched={typesTouched} methodsAdded={methodsAdded} methodsSkipped={methodsSkipped} typesMissingInDummy={typesMissing}");
foreach (var p in placementProof)
    Console.WriteLine("[Enrich][proof] " + p);

var imageBuilder = new ManagedPEImageBuilder();
var fileBuilder = new ManagedPEFileBuilder();
int written = 0, fail = 0;
foreach (var asm in assemblies)
{
    var mod = asm.ManifestModule ?? asm.Modules[0];
    var name = mod.Name?.ToString() ?? (asm.Name + ".dll");
    if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) name += ".dll";
    var outPath = Path.Combine(output, name);
    try
    {
        var result = imageBuilder.CreateImage(mod);
        if (result.ConstructedImage == null) throw new Exception("CreateImage null");
        var tmp = outPath + ".tmp";
        fileBuilder.CreateFile(result.ConstructedImage).Write(tmp);
        if (File.Exists(outPath)) File.Delete(outPath);
        File.Move(tmp, outPath);
        written++;
    }
    catch (Exception ex)
    {
        fail++;
        Console.Error.WriteLine($"[Enrich] FAIL {name}: {ex.GetType().Name}: {ex.Message}");
    }
}
Console.WriteLine($"[Enrich] written={written} fail={fail}");
return fail == 0 ? 0 : 1;
