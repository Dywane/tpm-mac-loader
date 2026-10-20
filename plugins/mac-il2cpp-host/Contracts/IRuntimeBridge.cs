namespace TpmMacIl2CppHost.Contracts;

/// <summary>
/// Contract A — stable runtime facade. Multiselect / gameplay depend only on this surface
/// (plus <c>IItemSelectionBridge</c>); never on Native / dlsym / Interop DLL names.
/// </summary>
public interface IRuntimeBridge
{
    bool Ready { get; }

    bool TryResolveType(string? imageHint, string namespaze, string name, out RuntimeTypeHandle handle);

    /// <param name="instance">Default (invalid) = static invoke.</param>
    bool TryInvoke(
        RuntimeTypeHandle type,
        string methodName,
        RuntimeObjectHandle instance,
        object?[]? args,
        out object? result);

    /// <summary>
    /// Unwrap a TryInvoke result (or prior handle) into RuntimeObjectHandle without Multiselect touching IntPtr.
    /// </summary>
    bool TryAsObjectHandle(object? value, out RuntimeObjectHandle handle);

    /// <summary>
    /// Read a managed field. Reference fields map to <see cref="RuntimeObjectHandle"/> (same as TryInvoke).
    /// Default / invalid <paramref name="instance"/> = static field on <paramref name="type"/>.
    /// For instance fields, <paramref name="type"/> may be default — class is taken from the object.
    /// Missing field / unbound symbols → false. Null ref field → true with value=null.
    /// Value-type fields beyond common boxed mapping are out of scope (false).
    /// </summary>
    bool TryGetField(RuntimeTypeHandle type, RuntimeObjectHandle instance, string fieldName, out object? value);

    /// <summary>
    /// Write a managed field. Supports null / <see cref="RuntimeObjectHandle"/> / IntPtr refs;
    /// bool/int value types when symbols allow. Same instance/static convention as TryGetField.
    /// </summary>
    bool TrySetField(RuntimeTypeHandle type, RuntimeObjectHandle instance, string fieldName, object? value);


    /// <summary>
    /// Runtime type name of a live object (il2cpp_object_get_class + class_get_namespace/name).
    /// Success → e.g. <c>TPS.Game.CursorModePlaceItem</c> or bare <c>Name</c> when namespace empty.
    /// False when invalid handle / unbound symbols / class lookup failed.
    /// Safe from any thread (metadata only; no Find / no Unity scene walk).
    /// </summary>
    bool TryGetTypeName(RuntimeObjectHandle instance, out string? fullName);

    /// <summary>
    /// Enumerate managed field names on a live object (il2cpp_object_get_class +
    /// il2cpp_class_get_fields + il2cpp_field_get_name). Includes instance fields,
    /// static fields declared on the class, and compiler backing fields
    /// (<c>&lt;X&gt;k__BackingField</c>).
    /// <para>
    /// <b>Scope:</b> walks declaring class then parents via <c>il2cpp_class_get_parent</c>
    /// (derived → base). Caps: 32 parent levels, 512 names. Does <b>not</b> read values
    /// (use <see cref="TryGetField"/>). Metadata only — any thread; no Find / no GCD.
    /// If parent symbol unbound, lists declaring class only. False when invalid handle /
    /// unbound symbols / empty result after soft miss.
    /// </para>
    /// </summary>
    bool TryListFields(RuntimeObjectHandle instance, out string[]? names);

    /// <summary>
    /// Find a live Unity object of <paramref name="type"/> (FindFirstObjectByType / FindObjectOfType style).
    /// Success → valid <see cref="RuntimeObjectHandle"/> (opaque; no public FromNative).
    /// False when not found / type invalid / symbols unbound / invoke failed / main-thread marshal timeout /
    /// Find hard-fail circuit open.
    /// <para>
    /// <b>Host marshals to Unity main thread</b> (GCD <c>_dispatch_main_q</c> via dlsym + optional
    /// <c>SynchronizationContext</c>). Multiselect may call from a worker — <b>do not</b> use
    /// ClassInjector / <c>AddComponent</c> / <c>op_Equality</c>. Prefer not hammering Find every frame.
    /// </para>
    /// <para>
    /// <b>0.2.4 harden:</b> default overload uses <c>includeInactive:false</c> only — it does
    /// <b>not</b> escalate to inactive after failures. On first Find* il2cpp exception Host logs
    /// <c>FindIl2CppException</c> + <c>FindHardFailStopped</c>, trips a process-wide circuit, and
    /// returns false without more Find* variants. Subsequent calls (including
    /// <c>includeInactive:true</c>) refuse immediately. Residual: GCD-only marshal without
    /// SyncContext may not be Unity player thread — prefer Instance polling until Find proven safe.
    /// No <c>INativeDetour</c>.
    /// </para>
    /// </summary>
    bool TryFindObject(RuntimeTypeHandle type, out RuntimeObjectHandle instance)
        => TryFindObject(type, includeInactive: false, out instance);

    /// <param name="includeInactive">
    /// When true, tries inactive-inclusive Find* only (never auto-selected by the default overload).
    /// After a prior Find hard-fail, returns false without invoking Find*.
    /// </param>
    bool TryFindObject(RuntimeTypeHandle type, bool includeInactive, out RuntimeObjectHandle instance);
}
