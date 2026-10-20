using AsmResolver.DotNet;
using Il2CppInterop.Common;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using Microsoft.Extensions.Logging;

static int Usage()
{
    Console.Error.WriteLine("Usage: GenerateIl2CppInterop --input <dummyDir> --output <outDir> --unity-libs <dir> [--game-assembly <path>] [--no-xref-cache]");
    return 2;
}

var argsList = args.ToList();
string? GetOpt(string name)
{
    var i = argsList.IndexOf(name);
    return i >= 0 && i + 1 < argsList.Count ? argsList[i + 1] : null;
}
bool HasFlag(string name) => argsList.Contains(name);

var input = GetOpt("--input");
var output = GetOpt("--output");
var unityLibs = GetOpt("--unity-libs");
var gameAsm = GetOpt("--game-assembly");
if (input == null || output == null || unityLibs == null) return Usage();

Directory.CreateDirectory(output);
var dlls = Directory.EnumerateFiles(input, "*.dll").OrderBy(p => p).ToList();
Console.WriteLine($"[A4] input={input} dlls={dlls.Count}");
Console.WriteLine($"[A4] output={output}");
Console.WriteLine($"[A4] unity-libs={unityLibs}");
var unityCount = Directory.Exists(unityLibs) ? Directory.EnumerateFiles(unityLibs, "*.dll").Count() : 0;
Console.WriteLine($"[A4] unity-libs dlls={unityCount}" + (unityCount == 0 ? " (Unstrip skipped — recommended for Mac Dummy)" : ""));
Console.WriteLine($"[A4] game-assembly={gameAsm ?? "(none)"}");

List<AssemblyDefinition> source;
try
{
    source = dlls.Select(AssemblyDefinition.FromFile).ToList();
}
catch (Exception ex)
{
    Console.Error.WriteLine("[A4] FAIL load Dummy: " + ex);
    return 1;
}
Console.WriteLine($"[A4] loaded assemblies={source.Count}");

using var loggerFactory = LoggerFactory.Create(b =>
{
    b.SetMinimumLevel(LogLevel.Information);
    b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
});
var logger = loggerFactory.CreateLogger("Il2CppInterop");

var opts = new GeneratorOptions
{
    Source = source,
    OutputDir = output,
    UnityBaseLibsDir = unityLibs,
    GameAssemblyPath = gameAsm,
    Verbose = true,
    NoXrefCache = HasFlag("--no-xref-cache") || true, // default true for first Mac pass
    Parallel = true,
    TypeDeobfuscationCharsPerUniquifier = 3,
    TypeDeobfuscationMaxUniquifiers = 20,
};

try
{
    Console.WriteLine("[A4] running Il2CppInteropGenerator.AddInteropAssemblyGenerator…");
    Il2CppInteropGenerator.Create(opts)
        .AddLogger(logger)
        .AddInteropAssemblyGenerator()
        .Run();
    var outCount = Directory.EnumerateFiles(output, "*.dll").Count();
    Console.WriteLine($"[A4] DONE outDlls={outCount}");
    return outCount > 0 ? 0 : 3;
}
catch (Exception ex)
{
    Console.Error.WriteLine("[A4] FAIL generate: " + ex);
    return 1;
}
