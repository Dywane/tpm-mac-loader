using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using TpmMacIl2CppHost.Kernels;

namespace TpmMacIl2CppHost;

/// <summary>
/// ITHM thin Host: Interop bootstrap + IInteropHost delivery surface.
/// CAPI / Il2CppNative path removed from Load (files excluded from csproj).
/// Play-facing IRuntimeBridge is ARCHIVE — gate success criteria use IInteropHost only.
/// </summary>
[BepInPlugin(HostIds.Guid, HostIds.Name, HostIds.Version)]
public class Il2CppHostPlugin : BasePlugin, IIl2CppHost, IInteropHost
{
    internal static ManualLogSource LogSrc = null!;

    public bool Ready { get; private set; }

    /// <inheritdoc />
    public string Kind => HostKernelKind.Interop.ToString();

    /// <summary>Host-owned shared Harmony (Task2). Non-null after InitSharedHarmony.</summary>
    public HarmonyLib.Harmony SharedHarmony => _sharedHarmony!;

    HarmonyLib.Harmony? _sharedHarmony;

    /// <summary>
    /// Legacy CAPI surface. This branch has no C API table — always returns null.
    /// ARCHIVE — prefer Il2Cpp* direct bind from Multiselect.
    /// </summary>
    public IntPtr ClassFromName(string? imageHint, string namespaze, string name)
    {
        _ = imageHint;
        _ = namespaze;
        _ = name;
        return IntPtr.Zero;
    }

    /// <summary>This branch production default = Interop. Config may still say Interop explicitly.</summary>
    const HostKernelKind DefaultKernelKind = HostKernelKind.Interop;

    ConfigEntry<string>? _kernelKindEntry;

    public override void Load()
    {
        LogSrc = Log;
        Log.LogInfo($"{HostIds.Name} {HostIds.Version} loaded (GUID={HostIds.Guid}).");

        _kernelKindEntry = Config.Bind(
            "Kernel",
            "Kind",
            DefaultKernelKind.ToString(),
            "HostKernelKind: Interop (this branch production default). CApi path removed from this worktree build.");
        var kind = ParseKernelKind(_kernelKindEntry.Value);
        Log.LogInfo($"[HOST] Kernel.Kind config={_kernelKindEntry.Value} → {kind} (default={DefaultKernelKind})");

        if (kind != HostKernelKind.Interop)
        {
            Log.LogError(
                $"[HOST] Kind={kind} requested but this branch build has no CApiRuntimeKernel. " +
                "Forcing Interop. Rebuild main-tree Host for CAPI.");
            kind = HostKernelKind.Interop;
        }

        var interop = new InteropRuntimeKernel();
        HostFacade.SetRuntime(interop); // ARCHIVE wire-up for legacy Multiselect compile
        HostFacade.Set(this);           // ARCHIVE
        HostFacade.SetHost(this);       // IInteropHost delivery surface

        var bootOk = interop.TryBootstrap();
        Ready = interop.Ready;
        Log.LogInfo(
            $"[HOST] Runtime Kind={interop.Kind} Ready={interop.Ready} HostFacade.Ready={HostFacade.Ready} — {interop.Diagnostic}");

        if (!bootOk || !interop.Ready)
        {
            Log.LogError("[HOST] InteropRuntimeKernel bootstrap failed; Ready=false (honest).");
            InitSharedHarmony(ready: false);
            return;
        }

        Log.LogInfo($"[HOST] Runtime Kind=Interop Ready=true");
        InitSharedHarmony(ready: true);
        TrySmokeA10(interop);
    }

    /// <summary>
    /// Task2: create Host-owned SharedHarmony. When Ready, apply harmless smoke patch.
    /// Ready=false callers still get an instance but must not patch gameplay.
    /// </summary>
    void InitSharedHarmony(bool ready)
    {
        _sharedHarmony = new HarmonyLib.Harmony(HostIds.Guid + ".shared");
        Log.LogInfo($"[HOST] SharedHarmony id={_sharedHarmony.Id} Ready={ready}");
        if (!ready)
        {
            Log.LogWarning("[HOST][harmony] Ready=false — SharedHarmony created; gameplay must NOT patch.");
            return;
        }

        try
        {
            // See Il2CppHarmonySupportGate: Type.op_Equality hole breaks ResolvePatcher.
            Il2CppHarmonySupportGate.DetachIl2CppResolver();
            try
            {
                _sharedHarmony.CreateClassProcessor(typeof(HarmonySmokePatch)).Patch();
                HarmonySmokeTarget.Nop();
            }
            finally
            {
                Il2CppHarmonySupportGate.AttachIl2CppResolver();
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"[HOST][harmony] smoke patch failed: {ex}");
            try { Il2CppHarmonySupportGate.AttachIl2CppResolver(); } catch { /* best-effort */ }
        }
    }

    static HostKernelKind ParseKernelKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultKernelKind;
        if (Enum.TryParse<HostKernelKind>(raw.Trim(), ignoreCase: true, out var kind))
            return kind;
        return DefaultKernelKind;
    }

    /// <summary>
    /// TaskA10 smoke retained for continuity; not an ITHM acceptance criterion.
    /// Find stays NotSupported. No InteropProbe.
    /// </summary>
    static void TrySmokeA10(InteropRuntimeKernel kernel)
    {
        try
        {
            if (!kernel.TryResolveType(null, "System", "String", out var stringHandle) || !stringHandle.IsValid)
            {
                LogSrc.LogWarning("[RT] smoke TryResolveType System.String FAILED");
                return;
            }

            LogSrc.LogInfo(
                $"[RT] smoke TryResolveType ok ns=System name=String → Il2CppSystem.String handle valid; {kernel.Diagnostic}");

            if (!kernel.TryInvoke(stringHandle, "op_Implicit", default, new object?[] { "a10" }, out var invokeResult)
                || invokeResult is null)
            {
                LogSrc.LogWarning("[RT] smoke TryInvoke op_Implicit(\"a10\") → false");
            }
            else if (!kernel.TryAsObjectHandle(invokeResult, out var objHandle) || !objHandle.IsValid)
            {
                LogSrc.LogWarning(
                    $"[RT] smoke TryInvoke op_Implicit ok but TryAsObjectHandle failed (resultType={invokeResult.GetType().FullName})");
            }
            else
            {
                LogSrc.LogInfo("[RT] smoke TryInvoke op_Implicit(\"a10\") → true (RuntimeObjectHandle)");

                if (kernel.TryGetField(stringHandle, objHandle, "isWrapped", out var fieldVal))
                    LogSrc.LogInfo($"[RT] smoke TryGetField isWrapped → true value={fieldVal}");
                else
                    LogSrc.LogWarning("[RT] smoke TryGetField isWrapped → false");

                if (fieldVal is bool b)
                {
                    if (kernel.TrySetField(stringHandle, objHandle, "isWrapped", b))
                        LogSrc.LogInfo($"[RT] smoke TrySetField isWrapped={b} → true (idempotent rewrite)");
                    else
                        LogSrc.LogWarning("[RT] smoke TrySetField isWrapped → false");
                }

                if (kernel.TryGetTypeName(objHandle, out var typeName) && !string.IsNullOrEmpty(typeName))
                    LogSrc.LogInfo($"[RT] smoke TryGetTypeName → true name={typeName}");
                else
                    LogSrc.LogWarning("[RT] smoke TryGetTypeName → false");
            }

            if (kernel.TryFindObject(stringHandle, includeInactive: false, out _))
                LogSrc.LogWarning("[RT] smoke TryFindObject unexpectedly succeeded");
            else
                LogSrc.LogInfo($"[RT] smoke TryFindObject → false ({InteropRuntimeKernel.FindNotSupportedDiagnostic})");

            (string Image, string Ns, string Name)[] candidates =
            {
                ("TPS.Game", "TPS.Game", "BuildMode"),
                ("TPS.Game", "TPS.Game", "BuildLogic"),
                ("TPS.Game", "TPS.Game", "CursorManager"),
                ("TPS.Game", "TPS.Game", "GameItemInstance"),
                ("TPS.Game", "TPS.Game", "ECGameItem"),
                ("TPS.Game", "TPS.Game.UI", "WorldCursorState"),
            };

            foreach (var (image, ns, name) in candidates)
            {
                if (kernel.TryResolveType(image, ns, name, out var h) && h.IsValid)
                {
                    LogSrc.LogInfo($"[RT] smoke TryResolveType ok image={image} ns={ns} name={name}");
                    return;
                }

                LogSrc.LogInfo($"[RT] smoke TryResolveType miss image={image} ns={ns} name={name}");
            }

            LogSrc.LogWarning("[RT] smoke: no TPS.Game candidate resolved (String still OK; Ready stays true)");
        }
        catch (Exception ex)
        {
            LogSrc.LogError($"[RT] smoke failed: {ex}");
        }
    }
}
