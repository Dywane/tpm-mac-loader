using Mono.Cecil;
using Mono.Cecil.Cil;

// Mac + Rosetta: Dobby patches il2cpp_runtime_invoke in x86_64 GameAssembly,
// but Internal_ActiveSceneChanged never reaches OnInvokeMethod (AOT/translation).
// After Initialize patches the detour, also start a delayed Instance.Execute()
// so plugins still load. Idempotent: skips if DelayedExecuteForMacSmoke exists.
if (args.Length < 1) { Console.Error.WriteLine("usage: MacDelayedExecute <BepInEx.Unity.IL2CPP.dll>"); return 1; }
var dllPath = args[0];
var backup = dllPath + ".bak-unpatched";
if (!File.Exists(backup)) File.Copy(dllPath, backup);
File.Copy(backup, dllPath, overwrite: true);

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath)!);
using var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters {
    AssemblyResolver = resolver, ReadWrite = true
});

var type = asm.MainModule.GetType("BepInEx.Unity.IL2CPP.IL2CPPChainloader")
    ?? throw new Exception("IL2CPPChainloader not found");
if (type.Methods.Any(m => m.Name == "DelayedExecuteForMacSmoke"))
{
    Console.WriteLine("already patched");
    return 0;
}

var executeRef = asm.MainModule.ImportReference(new MethodReference(
    "Execute", asm.MainModule.TypeSystem.Void, type.BaseType) { HasThis = true });
var instanceProp = type.Properties.First(p => p.Name == "Instance").GetMethod;
var sleep = asm.MainModule.ImportReference(typeof(Thread).GetMethod("Sleep", new[] { typeof(int) })!);
var threadCtor = asm.MainModule.ImportReference(typeof(Thread).GetConstructor(new[] { typeof(ThreadStart) })!);
var threadStartCtor = asm.MainModule.ImportReference(
    typeof(ThreadStart).GetConstructor(new[] { typeof(object), typeof(IntPtr) })!);
var threadStartMethod = asm.MainModule.ImportReference(typeof(Thread).GetMethod("Start", Type.EmptyTypes)!);

var helper = new MethodDefinition("DelayedExecuteForMacSmoke",
    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
    asm.MainModule.TypeSystem.Void);
type.Methods.Add(helper);
{
    var il = helper.Body.GetILProcessor();
    // 25s: splash/main scene usually up; plugins need host only for Task 3 smoke
    helper.Body.Instructions.Add(il.Create(OpCodes.Ldc_I4, 25000));
    helper.Body.Instructions.Add(il.Create(OpCodes.Call, sleep));
    helper.Body.Instructions.Add(il.Create(OpCodes.Call, instanceProp));
    helper.Body.Instructions.Add(il.Create(OpCodes.Callvirt, executeRef));
    helper.Body.Instructions.Add(il.Create(OpCodes.Ret));
}

var initialize = type.Methods.First(m => m.Name == "Initialize" && m.Parameters.Count == 1);
var ilInit = initialize.Body.GetILProcessor();
var lastRet = initialize.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);
foreach (var insn in new[] {
    ilInit.Create(OpCodes.Ldnull),
    ilInit.Create(OpCodes.Ldftn, helper),
    ilInit.Create(OpCodes.Newobj, threadStartCtor),
    ilInit.Create(OpCodes.Newobj, threadCtor),
    ilInit.Create(OpCodes.Callvirt, threadStartMethod),
})
    ilInit.InsertBefore(lastRet, insn);

asm.Write();
Console.WriteLine("patched DelayedExecuteForMacSmoke into " + dllPath);
return 0;
