using System.Reflection;
using HarmonyLib;
using HarmonyLib.Public.Patching;

namespace TpmMacIl2CppHost;

/// <summary>Host-owned shared Harmony + harmless smoke patch target.</summary>
internal static class HarmonySmokeTarget
{
    internal static void Nop() { }
}

[HarmonyPatch(typeof(HarmonySmokeTarget), nameof(HarmonySmokeTarget.Nop))]
static class HarmonySmokePatch
{
    static void Prefix() => Il2CppHostPlugin.LogSrc.LogInfo("[HOST][harmony] smoke ok");
}

/// <summary>
/// Il2CppInterop.HarmonySupport.TryResolve compares Il2CppSystem.Type with ==,
/// which requires Type.op_Equality. Our interleaved stubs currently lack that operator,
/// so ANY ResolvePatcher invocation throws MissingMethodException — including pure managed
/// Host methods. Detach that resolver around managed smoke; restore afterward.
/// </summary>
internal static class Il2CppHarmonySupportGate
{
    static EventHandler<PatchManager.PatcherResolverEventArgs>? _il2CppTryResolve;

    internal static void DetachIl2CppResolver()
    {
        if (_il2CppTryResolve is not null)
        {
            PatchManager.ResolvePatcher -= _il2CppTryResolve;
            return;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.Equals(asm.GetName().Name, "Il2CppInterop.HarmonySupport", StringComparison.Ordinal))
                continue;
            var t = asm.GetType("Il2CppInterop.HarmonySupport.HarmonySupportComponent", throwOnError: false);
            var m = t?.GetMethod("TryResolve", BindingFlags.NonPublic | BindingFlags.Static);
            if (m is null)
                return;
            _il2CppTryResolve = (EventHandler<PatchManager.PatcherResolverEventArgs>)
                Delegate.CreateDelegate(typeof(EventHandler<PatchManager.PatcherResolverEventArgs>), m);
            PatchManager.ResolvePatcher -= _il2CppTryResolve;
            return;
        }
    }

    internal static void AttachIl2CppResolver()
    {
        if (_il2CppTryResolve is null)
            return;
        PatchManager.ResolvePatcher -= _il2CppTryResolve; // idempotent
        PatchManager.ResolvePatcher += _il2CppTryResolve;
    }
}
