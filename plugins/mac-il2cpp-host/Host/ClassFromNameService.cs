using System.Runtime.InteropServices;
using TpmMacIl2CppHost.Native;

namespace TpmMacIl2CppHost.Host;

/// <summary>
/// Reusable ClassFromName with imageHint resolution.
/// Logic aligned with Track B ClassFromNameProbe, exposed as API rather than one-shot probe.
/// </summary>
public static class ClassFromNameService
{
    /// <summary>
    /// Resolve Il2CppClass* for (namespaze, name) under imageHint.
    /// imageHint null/empty/"corlib" → il2cpp_get_corlib();
    /// other → first domain assembly whose image name contains the hint (case-insensitive).
    /// Returns IntPtr.Zero on failure.
    /// </summary>
    public static IntPtr Resolve(string? imageHint, string namespaze, string name)
    {
        if (!Il2CppNative.Ready || Il2CppNative.ClassFromName == null)
            return IntPtr.Zero;
        if (string.IsNullOrEmpty(namespaze) || string.IsNullOrEmpty(name))
            return IntPtr.Zero;

        var image = ResolveImage(imageHint);
        if (image == IntPtr.Zero)
            return IntPtr.Zero;

        return CallClassFromName(image, namespaze, name);
    }

    static IntPtr ResolveImage(string? imageHint)
    {
        var useCorlib = string.IsNullOrEmpty(imageHint)
            || string.Equals(imageHint, "corlib", StringComparison.OrdinalIgnoreCase);

        if (useCorlib)
        {
            if (Il2CppNative.GetCorlib != null)
            {
                var corlib = Il2CppNative.GetCorlib();
                if (corlib != IntPtr.Zero)
                    return corlib;
            }
            // Fallback: scan for mscorlib / System.Private.CoreLib style images
            return FindImageBySubstring("mscorlib")
                ?? FindImageBySubstring("System.Private.CoreLib")
                ?? FindImageBySubstring("System")
                ?? IntPtr.Zero;
        }

        return FindImageBySubstring(imageHint!) ?? IntPtr.Zero;
    }

    static IntPtr? FindImageBySubstring(string hint)
    {
        if (Il2CppNative.DomainGet == null
            || Il2CppNative.DomainGetAssemblies == null
            || Il2CppNative.AssemblyGetImage == null)
            return null;

        var domain = Il2CppNative.DomainGet();
        if (domain == IntPtr.Zero)
            return null;

        var assemblies = Il2CppNative.DomainGetAssemblies(domain, out var size);
        var n = (int)size.ToUInt64();
        if (assemblies == IntPtr.Zero || n <= 0 || n >= 512)
            return null;

        for (var i = 0; i < n; i++)
        {
            var asm = Marshal.ReadIntPtr(assemblies, i * IntPtr.Size);
            if (asm == IntPtr.Zero) continue;
            var image = Il2CppNative.AssemblyGetImage(asm);
            if (image == IntPtr.Zero) continue;
            var imgName = PtrToUtf8(Il2CppNative.ImageGetName?.Invoke(image) ?? IntPtr.Zero) ?? "";
            if (imgName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                return image;
        }

        return null;
    }

    static IntPtr CallClassFromName(IntPtr image, string ns, string name)
    {
        var nsPtr = Marshal.StringToHGlobalAnsi(ns);
        var namePtr = Marshal.StringToHGlobalAnsi(name);
        try
        {
            return Il2CppNative.ClassFromName!(image, nsPtr, namePtr);
        }
        finally
        {
            Marshal.FreeHGlobal(nsPtr);
            Marshal.FreeHGlobal(namePtr);
        }
    }

    static string? PtrToUtf8(IntPtr p) =>
        p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
}
