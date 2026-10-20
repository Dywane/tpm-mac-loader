namespace TpmMacIl2CppHost.Contracts;

/// <summary>
/// Opaque type handle for contract A. Public API exposes only <see cref="IsValid"/>;
/// raw native class pointers stay internal for Kernels.
/// </summary>
public readonly struct RuntimeTypeHandle : IEquatable<RuntimeTypeHandle>
{
    internal readonly IntPtr NativeClass;

    public bool IsValid => NativeClass != IntPtr.Zero;

    internal RuntimeTypeHandle(IntPtr nativeClass) => NativeClass = nativeClass;

    internal static RuntimeTypeHandle FromNative(IntPtr nativeClass) => new(nativeClass);

    public bool Equals(RuntimeTypeHandle other) => NativeClass == other.NativeClass;

    public override bool Equals(object? obj) => obj is RuntimeTypeHandle other && Equals(other);

    public override int GetHashCode() => NativeClass.GetHashCode();

    public static bool operator ==(RuntimeTypeHandle left, RuntimeTypeHandle right) => left.Equals(right);

    public static bool operator !=(RuntimeTypeHandle left, RuntimeTypeHandle right) => !left.Equals(right);
}
