using System.Runtime.InteropServices;
using System.Text;

namespace TpmMacIl2CppHost.Native;

/// <summary>
/// Resolve GameAssembly il2cpp_* export addresses via dlopen/dlsym.
/// Does not invoke; addresses only. No Win PE / abolished RVA.
/// Promoted from TpmMacCapiProbe (Track B); Task 3 invoke; Field; Field-list; Find via class_get_type + type_get_object.
/// </summary>
public sealed class Il2CppApiExports
{
    const int RTLD_NOLOAD = 0x10;
    const int RTLD_LAZY = 0x1;

    [DllImport("libSystem.B.dylib")]
    static extern IntPtr dlopen(string path, int mode);

    [DllImport("libSystem.B.dylib")]
    static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("libSystem.B.dylib")]
    static extern IntPtr dlerror();

    public IntPtr Handle { get; private init; }
    public IntPtr ClassFromName { get; private init; }
    public IntPtr DomainGet { get; private init; }
    public IntPtr GetCorlib { get; private init; }
    public IntPtr ClassGetName { get; private init; }
    public IntPtr ClassGetNamespace { get; private init; }
    public IntPtr ImageGetName { get; private init; }
    public IntPtr DomainGetAssemblies { get; private init; }
    public IntPtr AssemblyGetImage { get; private init; }

    // Task 3 — invoke surface (required for TryInvoke)
    public IntPtr ClassGetMethodFromName { get; private init; }
    public IntPtr RuntimeInvoke { get; private init; }

    // Task 3 — optional helpers (smoke / simple return mapping)
    public IntPtr MethodGetName { get; private init; }
    public IntPtr MethodGetParamCount { get; private init; }
    public IntPtr ObjectGetClass { get; private init; }
    public IntPtr ObjectUnbox { get; private init; }
    public IntPtr ClassIsValuetype { get; private init; }
    public IntPtr ValueBox { get; private init; }
    public IntPtr StringNew { get; private init; }
    public IntPtr StringChars { get; private init; }
    public IntPtr StringLength { get; private init; }

    // Field surface (TryGetField / TrySetField)
    public IntPtr ClassGetFieldFromName { get; private init; }
    public IntPtr FieldGetValueObject { get; private init; }
    public IntPtr FieldSetValueObject { get; private init; }
    public IntPtr FieldStaticGetValue { get; private init; }
    public IntPtr FieldStaticSetValue { get; private init; }
    public IntPtr FieldGetValue { get; private init; }
    public IntPtr FieldSetValue { get; private init; }
    public IntPtr FieldGetFlags { get; private init; }

    // Field enumeration (TryListFields)
    public IntPtr ClassGetFields { get; private init; }
    public IntPtr FieldGetName { get; private init; }
    public IntPtr ClassGetParent { get; private init; }

    // Type object surface (TryFindObject: Il2CppClass* → System.Type)
    public IntPtr ClassGetType { get; private init; }
    public IntPtr TypeGetObject { get; private init; }

    /// <summary>Required: ClassFromName non-zero. Invoke symbols preferred but optional for Resolve-only Ready.</summary>
    public static bool TryResolve(out Il2CppApiExports exports, out string diagnostics)
    {
        exports = null!;
        var sb = new StringBuilder();

        var abs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/Application Support/Steam/steamapps/common/Two Point Museum/TPM.app/Contents/Frameworks/GameAssembly.dylib");

        IntPtr h = IntPtr.Zero;
        string pathUsed = "";
        foreach (var c in new[] { abs, "GameAssembly.dylib", "@rpath/GameAssembly.dylib" })
        {
            h = dlopen(c, RTLD_NOLOAD | RTLD_LAZY);
            if (h == IntPtr.Zero && c == abs)
                h = dlopen(c, RTLD_LAZY);
            if (h == IntPtr.Zero)
            {
                sb.AppendLine($"dlopen miss: {c} err={ReadDlError()}");
                continue;
            }
            pathUsed = c;
            break;
        }

        if (h == IntPtr.Zero)
        {
            diagnostics = sb.Append("dlopen GameAssembly failed").ToString();
            return false;
        }

        sb.AppendLine($"dlopen ok path={pathUsed} handle=0x{h.ToInt64():X}");

        var classFromName = Sym(h, "il2cpp_class_from_name", sb);
        if (classFromName == IntPtr.Zero)
        {
            diagnostics = sb.Append("required dlsym il2cpp_class_from_name=0").ToString();
            return false;
        }

        exports = new Il2CppApiExports
        {
            Handle = h,
            ClassFromName = classFromName,
            DomainGet = Sym(h, "il2cpp_domain_get", sb),
            GetCorlib = Sym(h, "il2cpp_get_corlib", sb),
            ClassGetName = Sym(h, "il2cpp_class_get_name", sb),
            ClassGetNamespace = Sym(h, "il2cpp_class_get_namespace", sb),
            ImageGetName = Sym(h, "il2cpp_image_get_name", sb),
            DomainGetAssemblies = Sym(h, "il2cpp_domain_get_assemblies", sb),
            AssemblyGetImage = Sym(h, "il2cpp_assembly_get_image", sb),
            ClassGetMethodFromName = Sym(h, "il2cpp_class_get_method_from_name", sb),
            RuntimeInvoke = Sym(h, "il2cpp_runtime_invoke", sb),
            MethodGetName = Sym(h, "il2cpp_method_get_name", sb),
            MethodGetParamCount = Sym(h, "il2cpp_method_get_param_count", sb),
            ObjectGetClass = Sym(h, "il2cpp_object_get_class", sb),
            ObjectUnbox = Sym(h, "il2cpp_object_unbox", sb),
            ClassIsValuetype = Sym(h, "il2cpp_class_is_valuetype", sb),
            ValueBox = Sym(h, "il2cpp_value_box", sb),
            StringNew = Sym(h, "il2cpp_string_new", sb),
            StringChars = Sym(h, "il2cpp_string_chars", sb),
            StringLength = Sym(h, "il2cpp_string_length", sb),
            ClassGetFieldFromName = Sym(h, "il2cpp_class_get_field_from_name", sb),
            FieldGetValueObject = Sym(h, "il2cpp_field_get_value_object", sb),
            FieldSetValueObject = Sym(h, "il2cpp_field_set_value_object", sb),
            FieldStaticGetValue = Sym(h, "il2cpp_field_static_get_value", sb),
            FieldStaticSetValue = Sym(h, "il2cpp_field_static_set_value", sb),
            FieldGetValue = Sym(h, "il2cpp_field_get_value", sb),
            FieldSetValue = Sym(h, "il2cpp_field_set_value", sb),
            FieldGetFlags = Sym(h, "il2cpp_field_get_flags", sb),
            ClassGetFields = Sym(h, "il2cpp_class_get_fields", sb),
            FieldGetName = Sym(h, "il2cpp_field_get_name", sb),
            ClassGetParent = Sym(h, "il2cpp_class_get_parent", sb),
            ClassGetType = Sym(h, "il2cpp_class_get_type", sb),
            TypeGetObject = Sym(h, "il2cpp_type_get_object", sb),
        };
        sb.AppendLine("TryResolve ok (ClassFromName non-zero)");
        if (exports.ClassGetMethodFromName == IntPtr.Zero || exports.RuntimeInvoke == IntPtr.Zero)
            sb.AppendLine("WARN invoke symbols incomplete (TryInvoke will fail until present)");
        else
            sb.AppendLine("invoke symbols OK (class_get_method_from_name + runtime_invoke)");
        if (exports.ClassGetFieldFromName == IntPtr.Zero || exports.FieldGetValueObject == IntPtr.Zero)
            sb.AppendLine("WARN field symbols incomplete (TryGetField will fail until present)");
        else
            sb.AppendLine("field symbols OK (class_get_field_from_name + field_get_value_object)");
        if (exports.ClassGetFields == IntPtr.Zero || exports.FieldGetName == IntPtr.Zero)
            sb.AppendLine("WARN field-list symbols incomplete (TryListFields will fail until present)");
        else
            sb.AppendLine("field-list symbols OK (class_get_fields + field_get_name; parent optional)");
        if (exports.ClassGetType == IntPtr.Zero || exports.TypeGetObject == IntPtr.Zero)
            sb.AppendLine("WARN type-object symbols incomplete (TryFindObject will fail until present)");
        else
            sb.AppendLine("type-object symbols OK (class_get_type + type_get_object)");
        diagnostics = sb.ToString().TrimEnd();
        return true;
    }

    static IntPtr Sym(IntPtr handle, string name, StringBuilder sb)
    {
        var p = dlsym(handle, name);
        if (p == IntPtr.Zero)
            p = dlsym(handle, "_" + name);
        if (p == IntPtr.Zero)
        {
            sb.AppendLine($"dlsym FAIL {name} err={ReadDlError()}");
            return IntPtr.Zero;
        }
        sb.AppendLine($"dlsym OK {name} -> 0x{p.ToInt64():X}");
        return p;
    }

    static string ReadDlError()
    {
        var e = dlerror();
        return e == IntPtr.Zero ? "" : (Marshal.PtrToStringUTF8(e) ?? "");
    }
}
