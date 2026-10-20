using System.Collections.Generic;
using RuntimeObjectHandle = TpmMacIl2CppHost.Contracts.RuntimeObjectHandle;
using RuntimeTypeHandle = TpmMacIl2CppHost.Contracts.RuntimeTypeHandle;

namespace TpmMacIl2CppHost.Kernels;

/// <summary>
/// In-memory kernel for contract isolation / compile proof. Zero DllImport.
/// Resolves corlib System.String; TryInvoke returns a fixed sentinel.
/// Kind reports CApi so gate docs can treat Fake as a stand-in (not a production bind).
/// </summary>
public sealed class FakeRuntimeKernel : IRuntimeKernel
{
    public const string InvokeSentinel = "FakeRuntimeKernel.TryInvoke.sentinel";

    // Opaque fake native ids (never passed to real IL2CPP).
    static readonly IntPtr FakeStringClass = new(1);
    static readonly IntPtr FakeEmptyString = new(2);
    static readonly IntPtr FakeFoundObject = new(3);

    readonly Dictionary<(string Image, string Namespace, string Name), RuntimeTypeHandle> _types;
    readonly Dictionary<(IntPtr Klass, string Field), object?> _staticFields;

    public FakeRuntimeKernel()
    {
        _types = new Dictionary<(string, string, string), RuntimeTypeHandle>
        {
            [("corlib", "System", "String")] = RuntimeTypeHandle.FromNative(FakeStringClass),
        };
        // Stand-in for System.String.Empty (static ref field) — compile/unit proof only.
        _staticFields = new Dictionary<(IntPtr, string), object?>
        {
            [(FakeStringClass, "Empty")] = RuntimeObjectHandle.FromNative(FakeEmptyString),
        };
    }

    public HostKernelKind Kind => HostKernelKind.CApi;

    public bool Ready => true;

    public bool TryResolveType(string? imageHint, string namespaze, string name, out RuntimeTypeHandle handle)
    {
        var image = NormalizeImageHint(imageHint);
        if (_types.TryGetValue((image, namespaze, name), out handle))
            return true;

        handle = default;
        return false;
    }

    public bool TryInvoke(
        RuntimeTypeHandle type,
        string methodName,
        RuntimeObjectHandle instance,
        object?[]? args,
        out object? result)
    {
        _ = methodName;
        _ = instance;
        _ = args;

        if (!type.IsValid)
        {
            result = null;
            return false;
        }

        result = InvokeSentinel;
        return true;
    }

    public bool TryAsObjectHandle(object? value, out RuntimeObjectHandle handle)
    {
        if (value is RuntimeObjectHandle h && h.IsValid)
        {
            handle = h;
            return true;
        }

        handle = default;
        return false;
    }

    public bool TryGetField(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        string fieldName,
        out object? value)
    {
        value = null;
        _ = instance;
        if (!type.IsValid || string.IsNullOrEmpty(fieldName))
            return false;
        if (_staticFields.TryGetValue((type.NativeClass, fieldName), out value))
            return true;
        return false;
    }

    public bool TrySetField(
        RuntimeTypeHandle type,
        RuntimeObjectHandle instance,
        string fieldName,
        object? value)
    {
        _ = instance;
        if (!type.IsValid || string.IsNullOrEmpty(fieldName))
            return false;
        _staticFields[(type.NativeClass, fieldName)] = value;
        return true;
    }


    /// <summary>Synthetic names for in-memory handles (compile/unit proof).</summary>
    public bool TryGetTypeName(RuntimeObjectHandle instance, out string? fullName)
    {
        fullName = null;
        if (!instance.IsValid)
            return false;
        var p = instance.NativeObject;
        if (p == FakeEmptyString || p == FakeStringClass)
        {
            fullName = "System.String";
            return true;
        }
        if (p == FakeFoundObject)
        {
            fullName = "Fake.FoundObject";
            return true;
        }
        return false;
    }

    /// <summary>Synthetic field names for in-memory handles (compile/unit proof).</summary>
    public bool TryListFields(RuntimeObjectHandle instance, out string[]? names)
    {
        names = null;
        if (!instance.IsValid)
            return false;
        var p = instance.NativeObject;
        if (p == FakeEmptyString || p == FakeStringClass)
        {
            names = new[] { "_stringLength", "_firstChar", "<Empty>k__BackingField" };
            return true;
        }
        if (p == FakeFoundObject)
        {
            names = new[] { "_mode", "<Item>k__BackingField", "_rotateControl" };
            return true;
        }
        return false;
    }

    /// <summary>In-memory stand-in: any valid type yields a synthetic found handle (compile/unit proof).</summary>

    public bool TryFindObject(RuntimeTypeHandle type, bool includeInactive, out RuntimeObjectHandle instance)
    {
        _ = includeInactive;
        instance = default;
        if (!type.IsValid)
            return false;
        instance = RuntimeObjectHandle.FromNative(FakeFoundObject);
        return true;
    }

    static string NormalizeImageHint(string? imageHint)
    {
        if (string.IsNullOrEmpty(imageHint) || imageHint == "corlib")
            return "corlib";
        return imageHint;
    }
}
