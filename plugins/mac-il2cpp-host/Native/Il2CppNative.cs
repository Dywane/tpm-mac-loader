using System.Runtime.InteropServices;

namespace TpmMacIl2CppHost.Native;

/// <summary>
/// Thin Cdecl wrappers over Il2CppApiExports addresses.
/// Real il2cpp_class_from_name needs (Il2CppImage*, const char*, const char*).
/// Task 3: invoke; Field; Field-list; Find: class_get_type + type_get_object → Object.Find*.
/// </summary>
public static class Il2CppNative
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr DomainGetFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr GetCorlibFn();

    /// <summary>Il2CppClass* il2cpp_class_from_name(const Il2CppImage* image, const char* namespaze, const char* name)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassFromNameFn(IntPtr image, IntPtr namespaze, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassGetNameFn(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassGetNamespaceFn(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ImageGetNameFn(IntPtr image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr DomainGetAssembliesFn(IntPtr domain, out UIntPtr size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr AssemblyGetImageFn(IntPtr assembly);

    /// <summary>const MethodInfo* il2cpp_class_get_method_from_name(Il2CppClass* klass, const char* name, int argsCount)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassGetMethodFromNameFn(IntPtr klass, IntPtr name, int argsCount);

    /// <summary>Il2CppObject* il2cpp_runtime_invoke(const MethodInfo* method, void* obj, void** params, Il2CppException** exc)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr RuntimeInvokeFn(IntPtr method, IntPtr obj, IntPtr parameters, ref IntPtr exc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr MethodGetNameFn(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint MethodGetParamCountFn(IntPtr method);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ObjectGetClassFn(IntPtr obj);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ObjectUnboxFn(IntPtr obj);

    /// <summary>bool il2cpp_class_is_valuetype(Il2CppClass* klass) — returns 0/1</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte ClassIsValuetypeFn(IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ValueBoxFn(IntPtr klass, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr StringNewFn(IntPtr utf8);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr StringCharsFn(IntPtr str);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int StringLengthFn(IntPtr str);

    /// <summary>FieldInfo* il2cpp_class_get_field_from_name(Il2CppClass* klass, const char* name)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassGetFieldFromNameFn(IntPtr klass, IntPtr name);

    /// <summary>Il2CppObject* il2cpp_field_get_value_object(FieldInfo* field, Il2CppObject* obj) — obj null for static</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr FieldGetValueObjectFn(IntPtr field, IntPtr obj);

    /// <summary>void il2cpp_field_set_value_object(Il2CppObject* instance, FieldInfo* field, Il2CppObject* value)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FieldSetValueObjectFn(IntPtr instance, IntPtr field, IntPtr value);

    /// <summary>void il2cpp_field_static_get_value(FieldInfo* field, void* value)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FieldStaticGetValueFn(IntPtr field, IntPtr value);

    /// <summary>void il2cpp_field_static_set_value(FieldInfo* field, void* value)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FieldStaticSetValueFn(IntPtr field, IntPtr value);

    /// <summary>void il2cpp_field_get_value(Il2CppObject* obj, FieldInfo* field, void* value)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FieldGetValueFn(IntPtr obj, IntPtr field, IntPtr value);

    /// <summary>void il2cpp_field_set_value(Il2CppObject* obj, FieldInfo* field, void* value)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FieldSetValueFn(IntPtr obj, IntPtr field, IntPtr value);

    /// <summary>int il2cpp_field_get_flags(FieldInfo* field)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int FieldGetFlagsFn(IntPtr field);

    /// <summary>
    /// FieldInfo* il2cpp_class_get_fields(Il2CppClass* klass, void** iter) —
    /// pass iter=null first; returns null when exhausted.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassGetFieldsFn(IntPtr klass, ref IntPtr iter);

    /// <summary>const char* il2cpp_field_get_name(FieldInfo* field)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr FieldGetNameFn(IntPtr field);

    /// <summary>Il2CppClass* il2cpp_class_get_parent(Il2CppClass* klass)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassGetParentFn(IntPtr klass);

    /// <summary>const Il2CppType* il2cpp_class_get_type(Il2CppClass* klass)</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr ClassGetTypeFn(IntPtr klass);

    /// <summary>Il2CppObject* il2cpp_type_get_object(const Il2CppType* type) → System.Type</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr TypeGetObjectFn(IntPtr type);

    public static DomainGetFn? DomainGet { get; private set; }
    public static GetCorlibFn? GetCorlib { get; private set; }
    public static ClassFromNameFn? ClassFromName { get; private set; }
    public static ClassGetNameFn? ClassGetName { get; private set; }
    public static ClassGetNamespaceFn? ClassGetNamespace { get; private set; }
    public static ImageGetNameFn? ImageGetName { get; private set; }
    public static DomainGetAssembliesFn? DomainGetAssemblies { get; private set; }
    public static AssemblyGetImageFn? AssemblyGetImage { get; private set; }

    public static ClassGetMethodFromNameFn? ClassGetMethodFromName { get; private set; }
    public static RuntimeInvokeFn? RuntimeInvoke { get; private set; }

    /// <summary>Raw <c>il2cpp_runtime_invoke</c> export address. Retained for diagnostics; 0.2.2+ does not detour it (0.2.3+ GCD via dlsym).</summary>
    public static IntPtr RuntimeInvokeExport { get; private set; }
    public static MethodGetNameFn? MethodGetName { get; private set; }
    public static MethodGetParamCountFn? MethodGetParamCount { get; private set; }
    public static ObjectGetClassFn? ObjectGetClass { get; private set; }
    public static ObjectUnboxFn? ObjectUnbox { get; private set; }
    public static ClassIsValuetypeFn? ClassIsValuetype { get; private set; }
    public static ValueBoxFn? ValueBox { get; private set; }
    public static StringNewFn? StringNew { get; private set; }
    public static StringCharsFn? StringChars { get; private set; }
    public static StringLengthFn? StringLength { get; private set; }

    public static ClassGetFieldFromNameFn? ClassGetFieldFromName { get; private set; }
    public static FieldGetValueObjectFn? FieldGetValueObject { get; private set; }
    public static FieldSetValueObjectFn? FieldSetValueObject { get; private set; }
    public static FieldStaticGetValueFn? FieldStaticGetValue { get; private set; }
    public static FieldStaticSetValueFn? FieldStaticSetValue { get; private set; }
    public static FieldGetValueFn? FieldGetValue { get; private set; }
    public static FieldSetValueFn? FieldSetValue { get; private set; }
    public static FieldGetFlagsFn? FieldGetFlags { get; private set; }

    public static ClassGetFieldsFn? ClassGetFields { get; private set; }
    public static FieldGetNameFn? FieldGetName { get; private set; }
    public static ClassGetParentFn? ClassGetParent { get; private set; }

    public static ClassGetTypeFn? ClassGetType { get; private set; }
    public static TypeGetObjectFn? TypeGetObject { get; private set; }

    public static bool Ready { get; private set; }

    /// <summary>True when method lookup + runtime_invoke are bound (Task 3 TryInvoke).</summary>
    public static bool InvokeReady { get; private set; }

    /// <summary>True when field lookup + get_value_object are bound (TryGetField).</summary>
    public static bool FieldReady { get; private set; }

    /// <summary>True when class_get_type + type_get_object bound (TryFindObject needs these + InvokeReady).</summary>
    public static bool TypeObjectReady { get; private set; }

    /// <summary>True when object_get_class + class_get_name bound (TryGetTypeName). Namespace symbol optional.</summary>
    public static bool TypeNameReady => ObjectGetClass != null && ClassGetName != null;

    /// <summary>
    /// True when object_get_class + class_get_fields + field_get_name bound (TryListFields).
    /// Parent symbol optional (hierarchy walk degrades to declaring class only).
    /// </summary>
    public static bool FieldListReady =>
        ObjectGetClass != null && ClassGetFields != null && FieldGetName != null;

    /// <summary>True when Find path can run (InvokeReady + TypeObjectReady). Still requires Unity main thread.</summary>
    public static bool FindReady => InvokeReady && TypeObjectReady;

    /// <summary>FIELD_ATTRIBUTE_STATIC (0x10) bit from field_get_flags.</summary>
    public const int FieldAttributeStatic = 0x10;

    /// <summary>FIELD_ATTRIBUTE_LITERAL (0x40) — const; get_value_object unsafe / undefined.</summary>
    public const int FieldAttributeLiteral = 0x40;

    /// <summary>FIELD_ATTRIBUTE_HAS_FIELD_RVA (0x100) — often with literal; skip get_value_object.</summary>
    public const int FieldAttributeHasFieldRva = 0x100;

    public static void Bind(Il2CppApiExports e)
    {
        ClassFromName = Marshal.GetDelegateForFunctionPointer<ClassFromNameFn>(e.ClassFromName);
        if (e.DomainGet != IntPtr.Zero)
            DomainGet = Marshal.GetDelegateForFunctionPointer<DomainGetFn>(e.DomainGet);
        if (e.GetCorlib != IntPtr.Zero)
            GetCorlib = Marshal.GetDelegateForFunctionPointer<GetCorlibFn>(e.GetCorlib);
        if (e.ClassGetName != IntPtr.Zero)
            ClassGetName = Marshal.GetDelegateForFunctionPointer<ClassGetNameFn>(e.ClassGetName);
        if (e.ClassGetNamespace != IntPtr.Zero)
            ClassGetNamespace = Marshal.GetDelegateForFunctionPointer<ClassGetNamespaceFn>(e.ClassGetNamespace);
        if (e.ImageGetName != IntPtr.Zero)
            ImageGetName = Marshal.GetDelegateForFunctionPointer<ImageGetNameFn>(e.ImageGetName);
        if (e.DomainGetAssemblies != IntPtr.Zero)
            DomainGetAssemblies = Marshal.GetDelegateForFunctionPointer<DomainGetAssembliesFn>(e.DomainGetAssemblies);
        if (e.AssemblyGetImage != IntPtr.Zero)
            AssemblyGetImage = Marshal.GetDelegateForFunctionPointer<AssemblyGetImageFn>(e.AssemblyGetImage);

        if (e.ClassGetMethodFromName != IntPtr.Zero)
            ClassGetMethodFromName = Marshal.GetDelegateForFunctionPointer<ClassGetMethodFromNameFn>(e.ClassGetMethodFromName);
        RuntimeInvokeExport = e.RuntimeInvoke;
        if (e.RuntimeInvoke != IntPtr.Zero)
            RuntimeInvoke = Marshal.GetDelegateForFunctionPointer<RuntimeInvokeFn>(e.RuntimeInvoke);
        if (e.MethodGetName != IntPtr.Zero)
            MethodGetName = Marshal.GetDelegateForFunctionPointer<MethodGetNameFn>(e.MethodGetName);
        if (e.MethodGetParamCount != IntPtr.Zero)
            MethodGetParamCount = Marshal.GetDelegateForFunctionPointer<MethodGetParamCountFn>(e.MethodGetParamCount);
        if (e.ObjectGetClass != IntPtr.Zero)
            ObjectGetClass = Marshal.GetDelegateForFunctionPointer<ObjectGetClassFn>(e.ObjectGetClass);
        if (e.ObjectUnbox != IntPtr.Zero)
            ObjectUnbox = Marshal.GetDelegateForFunctionPointer<ObjectUnboxFn>(e.ObjectUnbox);
        if (e.ClassIsValuetype != IntPtr.Zero)
            ClassIsValuetype = Marshal.GetDelegateForFunctionPointer<ClassIsValuetypeFn>(e.ClassIsValuetype);
        if (e.ValueBox != IntPtr.Zero)
            ValueBox = Marshal.GetDelegateForFunctionPointer<ValueBoxFn>(e.ValueBox);
        if (e.StringNew != IntPtr.Zero)
            StringNew = Marshal.GetDelegateForFunctionPointer<StringNewFn>(e.StringNew);
        if (e.StringChars != IntPtr.Zero)
            StringChars = Marshal.GetDelegateForFunctionPointer<StringCharsFn>(e.StringChars);
        if (e.StringLength != IntPtr.Zero)
            StringLength = Marshal.GetDelegateForFunctionPointer<StringLengthFn>(e.StringLength);

        if (e.ClassGetFieldFromName != IntPtr.Zero)
            ClassGetFieldFromName = Marshal.GetDelegateForFunctionPointer<ClassGetFieldFromNameFn>(e.ClassGetFieldFromName);
        if (e.FieldGetValueObject != IntPtr.Zero)
            FieldGetValueObject = Marshal.GetDelegateForFunctionPointer<FieldGetValueObjectFn>(e.FieldGetValueObject);
        if (e.FieldSetValueObject != IntPtr.Zero)
            FieldSetValueObject = Marshal.GetDelegateForFunctionPointer<FieldSetValueObjectFn>(e.FieldSetValueObject);
        if (e.FieldStaticGetValue != IntPtr.Zero)
            FieldStaticGetValue = Marshal.GetDelegateForFunctionPointer<FieldStaticGetValueFn>(e.FieldStaticGetValue);
        if (e.FieldStaticSetValue != IntPtr.Zero)
            FieldStaticSetValue = Marshal.GetDelegateForFunctionPointer<FieldStaticSetValueFn>(e.FieldStaticSetValue);
        if (e.FieldGetValue != IntPtr.Zero)
            FieldGetValue = Marshal.GetDelegateForFunctionPointer<FieldGetValueFn>(e.FieldGetValue);
        if (e.FieldSetValue != IntPtr.Zero)
            FieldSetValue = Marshal.GetDelegateForFunctionPointer<FieldSetValueFn>(e.FieldSetValue);
        if (e.FieldGetFlags != IntPtr.Zero)
            FieldGetFlags = Marshal.GetDelegateForFunctionPointer<FieldGetFlagsFn>(e.FieldGetFlags);
        if (e.ClassGetFields != IntPtr.Zero)
            ClassGetFields = Marshal.GetDelegateForFunctionPointer<ClassGetFieldsFn>(e.ClassGetFields);
        if (e.FieldGetName != IntPtr.Zero)
            FieldGetName = Marshal.GetDelegateForFunctionPointer<FieldGetNameFn>(e.FieldGetName);
        if (e.ClassGetParent != IntPtr.Zero)
            ClassGetParent = Marshal.GetDelegateForFunctionPointer<ClassGetParentFn>(e.ClassGetParent);

        if (e.ClassGetType != IntPtr.Zero)
            ClassGetType = Marshal.GetDelegateForFunctionPointer<ClassGetTypeFn>(e.ClassGetType);
        if (e.TypeGetObject != IntPtr.Zero)
            TypeGetObject = Marshal.GetDelegateForFunctionPointer<TypeGetObjectFn>(e.TypeGetObject);

        Ready = ClassFromName != null;
        InvokeReady = ClassGetMethodFromName != null && RuntimeInvoke != null;
        FieldReady = ClassGetFieldFromName != null && FieldGetValueObject != null;
        TypeObjectReady = ClassGetType != null && TypeGetObject != null;
    }

    /// <summary>
    /// Legacy hook from 0.2.1 RuntimeInvoke drain. Unused in 0.2.2+ (detour removed — AV on Mac Load; 0.2.3 GCD dlsym).
    /// Kept so older call sites compile if resurrected.
    /// </summary>
    internal static void RebindRuntimeInvoke(IntPtr trampolinePtr)
    {
        if (trampolinePtr == IntPtr.Zero)
            return;
        RuntimeInvoke = Marshal.GetDelegateForFunctionPointer<RuntimeInvokeFn>(trampolinePtr);
        InvokeReady = ClassGetMethodFromName != null && RuntimeInvoke != null;
    }
}
