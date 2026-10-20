namespace TpmMacIl2CppHost;

/// <summary>瘦 Host 对外交付面。玩法只依赖此面 + Il2Cpp* 生成类型。</summary>
public interface IInteropHost
{
    /// <summary>Interop bootstrap 成功且关键程序集可读。</summary>
    bool Ready { get; }

    /// <summary>固定日志/门禁关键字：必须为 "Interop"（或 Kind 枚举.ToString()==Interop）。</summary>
    string Kind { get; }

    /// <summary>Host 创建并持有的共享 Harmony 实例；Ready=false 时仍可非 null，但玩法不得 patch。</summary>
    HarmonyLib.Harmony SharedHarmony { get; }
}
