using TpmMacIl2CppHost.Contracts;

namespace TpmMacIl2CppHost;

/// <summary>门面：插件 Load 后写入；本刀玩法认 <see cref="Host"/> / <see cref="Ready"/> / <see cref="Kind"/> / <see cref="SharedHarmony"/>。</summary>
public static class HostFacade
{
    /// <summary>瘦 Host 交付面（ITHM Task1+）。</summary>
    public static IInteropHost? Host { get; private set; }

    /// <summary>Prefer <see cref="Host"/>.Ready；本刀成功标准只认此路径。</summary>
    public static bool Ready => Host is { Ready: true };

    public static string? Kind => Host?.Kind;

    public static HarmonyLib.Harmony? SharedHarmony => Host?.SharedHarmony;

    /// <summary>ARCHIVE: IRuntimeBridge；玩法禁止新增引用。</summary>
    public static IRuntimeBridge? Runtime { get; private set; }

    /// <summary>ARCHIVE: legacy ClassFromName surface。</summary>
    public static IIl2CppHost? Instance { get; private set; }

    internal static void SetHost(IInteropHost host) => Host = host;

    internal static void SetRuntime(IRuntimeBridge bridge) => Runtime = bridge;

    internal static void Set(IIl2CppHost host) => Instance = host;
}
