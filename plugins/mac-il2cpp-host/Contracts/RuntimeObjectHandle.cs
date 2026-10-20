namespace TpmMacIl2CppHost.Contracts;

/// <summary>
/// Opaque object/instance handle. Default (invalid) means static invoke.
/// Raw pointers stay internal for Kernels.
/// </summary>
public readonly struct RuntimeObjectHandle : IEquatable<RuntimeObjectHandle>
{
    internal readonly IntPtr NativeObject;

    public bool IsValid => NativeObject != IntPtr.Zero;

    internal RuntimeObjectHandle(IntPtr nativeObject) => NativeObject = nativeObject;

    internal static RuntimeObjectHandle FromNative(IntPtr nativeObject) => new(nativeObject);

    public bool Equals(RuntimeObjectHandle other) => NativeObject == other.NativeObject;

    public override bool Equals(object? obj) => obj is RuntimeObjectHandle other && Equals(other);

    public override int GetHashCode() => NativeObject.GetHashCode();

    public static bool operator ==(RuntimeObjectHandle left, RuntimeObjectHandle right) => left.Equals(right);

    public static bool operator !=(RuntimeObjectHandle left, RuntimeObjectHandle right) => !left.Equals(right);
}
