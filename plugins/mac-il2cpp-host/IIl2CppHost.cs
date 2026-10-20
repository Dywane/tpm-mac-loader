namespace TpmMacIl2CppHost;

/// <summary>对外最小可调用面；Multiselect / 日后玩法只依赖此契约。</summary>
public interface IIl2CppHost
{
    bool Ready { get; }

    /// <summary>
    /// 按 image + namespace + name 解析 Il2CppClass*。
    /// imageHint: null/空/"corlib" → il2cpp_get_corlib()；
    /// 其它字符串 → 在 domain assemblies 中按 image 名子串匹配（与轨 B 探针一致）。
    /// </summary>
    IntPtr ClassFromName(string? imageHint, string namespaze, string name);
}
