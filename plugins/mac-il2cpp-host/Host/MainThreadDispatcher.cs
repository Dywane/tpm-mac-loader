using System.Runtime.InteropServices;
using System.Threading;

namespace TpmMacIl2CppHost.Host;

/// <summary>
/// Marshals work onto the Unity main thread without MonoBehaviour / ClassInjector /
/// <c>UnityEngine.Object.op_Equality</c> and without BepInEx <c>INativeDetour</c>.
/// </summary>
/// <remarks>
/// Mac Host Load runs on <c>DelayedExecuteForMacSmoke</c> (background Thread), not the Unity
/// game thread. Installing <c>INativeDetour.CreateAndApply</c> on <c>il2cpp_runtime_invoke</c>
/// from that path AVs inside <c>FollowExportThunks</c> (Host 0.2.1 / 7338967).
///
/// Strategy (0.2.3+; Find harden 0.2.4+):
/// 1. Detect main via <c>pthread_main_np</c> (never trust Load tid as main).
/// 2. Marshal with GCD <c>dispatch_async_f</c> on <c>_dispatch_main_q</c>.
///    IMPORTANT: <c>dispatch_get_main_queue</c> is a <b>macro</b> on modern macOS
///    (<c>#define … (&amp;_dispatch_main_q)</c>) — it is <b>not</b> an exported symbol.
///    Host 0.2.2 DllImport of that name → EntryPointNotFound → MainThreadOnly / no marshal.
///    Resolve via <c>dlsym(RTLD_DEFAULT)</c> / explicit dylibs instead.
/// 3. After first main-queue run, also capture <see cref="SynchronizationContext"/> for Post.
/// 4. Never call <c>INativeDetour</c> / <c>CreateAndApply</c> from Load or anywhere else.
/// </remarks>
internal static class MainThreadDispatcher
{
    const int RTLD_LAZY = 0x1;
    const int RTLD_NOLOAD = 0x10;

    /// <summary>macOS dyld: search already-loaded images (libdispatch is always present).</summary>
    static readonly IntPtr RTLD_DEFAULT = (IntPtr)(-2);

    const string LibSystem = "/usr/lib/libSystem.B.dylib";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void DispatchWorkFn(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void DispatchAsyncFFn(IntPtr queue, IntPtr context, IntPtr work);

    [DllImport(LibSystem)]
    static extern IntPtr dlopen(string path, int mode);

    [DllImport(LibSystem)]
    static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport(LibSystem)]
    static extern IntPtr dlerror();

    /// <summary>Non-zero when the calling thread is the process main thread (Unity game thread on Mac).</summary>
    [DllImport(LibSystem)]
    static extern int pthread_main_np();

    static readonly object InstallLock = new();

    // Keep native callback delegate rooted for the process lifetime.
    static readonly DispatchWorkFn DispatchCallback = OnDispatchAsync;

    static SynchronizationContext? _syncCtx;
    static int _mainThreadId; // set on first confirmed main-thread entry; 0 = unknown
    static bool _initialized;
    static bool _gcdAvailable;
    static IntPtr _mainQueue; // &_dispatch_main_q after resolve
    static DispatchAsyncFFn? _dispatchAsyncF;
    static string _gcdBinding = "none";
    static string _mode = "uninitialized";

    internal static bool IsInitialized => _initialized;
    internal static string Mode => _mode;
    internal static string GcdBinding => _gcdBinding;

    /// <summary>True after a main-queue entry captured <see cref="SynchronizationContext"/>.</summary>
    internal static bool HasSyncContext => _syncCtx != null;

    /// <summary>
    /// Residual: GCD <c>_dispatch_main_q</c> is process-main, which usually IS Unity's player
    /// thread on Mac — but not proven identical for every Find* call. Prefer SyncContext once
    /// captured; Find may still run via GCD-only with circuit-breaker hardening (0.2.4+).
    /// </summary>
    internal static bool FindMarshalDegraded => _gcdAvailable && _syncCtx is null;

    internal static bool IsMainThread
    {
        get
        {
            try
            {
                if (pthread_main_np() != 0)
                    return true;
            }
            catch
            {
                // P/Invoke failed — fall through to captured tid
            }

            return _mainThreadId != 0
                   && Environment.CurrentManagedThreadId == _mainThreadId;
        }
    }

    /// <summary>
    /// Call once from Host.Load after native bind. Safe on DelayedExecute bg thread.
    /// Does <b>not</b> install native detours. <paramref name="runtimeInvokeExport"/> is ignored
    /// (kept so call sites stay stable; 0.2.1 used it for CreateAndApply — that path is removed).
    /// </summary>
    internal static void Initialize(IntPtr runtimeInvokeExport)
    {
        _ = runtimeInvokeExport; // intentionally unused — never detour from Load/bg

        lock (InstallLock)
        {
            if (_initialized)
                return;

            // Do NOT set _mainThreadId from Load — Mac Load is DelayedExecute bg.
            TryResolveGcd();

            // Rare: if Load somehow already on main, capture SyncContext now.
            if (IsMainThread)
                CaptureMainAffinity();

            _initialized = true;
            _mode = DescribeMode();

            Il2CppHostPlugin.LogSrc?.LogInfo(
                $"[HOST][main] dispatcher ready mode={_mode} mainTid={(_mainThreadId == 0 ? "pending" : _mainThreadId.ToString())} "
                + $"gcd={_gcdAvailable} binding={_gcdBinding} queue=0x{_mainQueue.ToInt64():X} "
                + $"syncCtx={(_syncCtx is null ? "null" : _syncCtx.GetType().FullName)} "
                + "detour=disabled (0.2.1 FollowExportThunks AV)");
        }
    }

    /// <summary>
    /// Resolve <c>_dispatch_main_q</c> + <c>dispatch_async_f</c> without relying on the
    /// non-exported <c>dispatch_get_main_queue</c> macro symbol.
    /// </summary>
    static void TryResolveGcd()
    {
        _gcdAvailable = false;
        _mainQueue = IntPtr.Zero;
        _dispatchAsyncF = null;
        _gcdBinding = "none";

        // 1) RTLD_DEFAULT (preferred — covers dyld shared cache)
        if (TryBindFromHandle(RTLD_DEFAULT, "dlsym(RTLD_DEFAULT)"))
            return;

        // 2) Explicit dylibs (NOLOAD first so we do not force-load; then LAZY)
        foreach (var path in new[]
                 {
                     "/usr/lib/system/libdispatch.dylib",
                     "/usr/lib/libSystem.B.dylib",
                     "libSystem.B.dylib",
                     "libdispatch.dylib"
                 })
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = dlopen(path, RTLD_NOLOAD | RTLD_LAZY);
                if (h == IntPtr.Zero)
                    h = dlopen(path, RTLD_LAZY);
            }
            catch (Exception ex)
            {
                Il2CppHostPlugin.LogSrc?.LogWarning(
                    $"[HOST][main] dlopen({path}) EX {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if (h == IntPtr.Zero)
                continue;

            if (TryBindFromHandle(h, $"dlopen({path})"))
                return;
        }

        // Clear dyld error state for diagnostics
        try { _ = dlerror(); } catch { /* ignore */ }

        Il2CppHostPlugin.LogSrc?.LogWarning(
            "[HOST][main] libdispatch resolve failed: _dispatch_main_q / dispatch_async_f not found "
            + "(dispatch_get_main_queue is a macro — do not DllImport it)");
    }

    static bool TryBindFromHandle(IntPtr handle, string label)
    {
        IntPtr queue;
        IntPtr asyncF;
        try
        {
            queue = dlsym(handle, "_dispatch_main_q");
            // Some toolchains export with leading underscore already stripped by dlsym — try both.
            if (queue == IntPtr.Zero)
                queue = dlsym(handle, "dispatch_main_q");

            asyncF = dlsym(handle, "dispatch_async_f");
            if (asyncF == IntPtr.Zero)
                asyncF = dlsym(handle, "_dispatch_async_f");
        }
        catch (Exception ex)
        {
            Il2CppHostPlugin.LogSrc?.LogWarning(
                $"[HOST][main] dlsym via {label} EX {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        if (queue == IntPtr.Zero || asyncF == IntPtr.Zero)
            return false;

        try
        {
            _dispatchAsyncF = Marshal.GetDelegateForFunctionPointer<DispatchAsyncFFn>(asyncF);
        }
        catch (Exception ex)
        {
            Il2CppHostPlugin.LogSrc?.LogWarning(
                $"[HOST][main] GetDelegateForFunctionPointer(dispatch_async_f) EX {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        _mainQueue = queue;
        _gcdAvailable = true;
        _gcdBinding = $"{label}+_dispatch_main_q+dispatch_async_f";
        Il2CppHostPlugin.LogSrc?.LogInfo(
            $"[HOST][main] GCD bound via {_gcdBinding} queue=0x{queue.ToInt64():X} async_f=0x{asyncF.ToInt64():X}");
        return true;
    }

    static string DescribeMode()
    {
        if (_gcdAvailable && _syncCtx != null)
            return "GcdMainQueue+SyncContext";
        if (_gcdAvailable)
            return "GcdMainQueue";
        if (_syncCtx != null)
            return "SyncContext";
        return "MainThreadOnly (no marshal — caller must already be on main / pthread_main_np)";
    }

    static void CaptureMainAffinity()
    {
        _mainThreadId = Environment.CurrentManagedThreadId;
        var ctx = SynchronizationContext.Current;
        if (ctx != null)
            _syncCtx = ctx;
        _mode = DescribeMode();
    }

    static void OnDispatchAsync(IntPtr context)
    {
        Action? work = null;
        try
        {
            var handle = GCHandle.FromIntPtr(context);
            work = handle.Target as Action;
            handle.Free();
        }
        catch
        {
            return;
        }

        try
        {
            CaptureMainAffinity();
            work?.Invoke();
        }
        catch (Exception ex)
        {
            try
            {
                Il2CppHostPlugin.LogSrc?.LogWarning(
                    $"[HOST][main] gcd work EX {ex.GetType().Name}: {ex.Message}");
            }
            catch
            {
                // ignore logging failures
            }
        }
    }

    /// <summary>
    /// Post <paramref name="body"/> to GCD main queue. Caller owns exactly-once semantics.
    /// On failure the GCHandle is freed and false is returned.
    /// </summary>
    static bool TryPostGcd(Action body)
    {
        if (!_gcdAvailable || _dispatchAsyncF is null || _mainQueue == IntPtr.Zero)
            return false;

        var handle = GCHandle.Alloc(body);
        try
        {
            var workPtr = Marshal.GetFunctionPointerForDelegate(DispatchCallback);
            _dispatchAsyncF(_mainQueue, GCHandle.ToIntPtr(handle), workPtr);
            // Ownership of handle transfers to OnDispatchAsync (frees there).
            return true;
        }
        catch (Exception ex)
        {
            handle.Free();
            Il2CppHostPlugin.LogSrc?.LogWarning(
                $"[HOST][main] dispatch_async_f failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Run <paramref name="func"/> on the Unity main thread. Inline when already there.
    /// </summary>
    internal static T Run<T>(Func<T> func, int timeoutMs = 8000)
    {
        if (!_initialized)
            return func(); // best-effort; Load path / Fake kernel

        if (IsMainThread)
        {
            CaptureMainAffinity();
            return func();
        }

        T result = default!;
        Exception? error = null;
        using var done = new ManualResetEventSlim(false);

        void Body()
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        }

        var posted = false;

        // Prefer SyncContext once captured (after first GCD main entry).
        var ctx = _syncCtx;
        if (ctx != null)
        {
            try
            {
                ctx.Post(_ =>
                {
                    CaptureMainAffinity();
                    Body();
                }, null);
                posted = true;
            }
            catch (Exception ex)
            {
                Il2CppHostPlugin.LogSrc?.LogWarning(
                    $"[HOST][main] SyncContext.Post failed: {ex.GetType().Name}: {ex.Message}; trying GCD");
            }
        }

        if (!posted)
            posted = TryPostGcd(Body);

        if (!posted)
        {
            Il2CppHostPlugin.LogSrc?.LogWarning(
                $"[HOST][main] no marshal path (mode={_mode} binding={_gcdBinding}) — Find requires main thread");
            return default!;
        }

        if (!done.Wait(timeoutMs))
        {
            Il2CppHostPlugin.LogSrc?.LogWarning(
                $"[HOST][main] Run timed out after {timeoutMs}ms (mode={_mode} binding={_gcdBinding}) — Find not marshaled");
            return default!;
        }

        if (error != null)
            throw error;

        return result;
    }

    /// <summary>Boolean convenience wrapper.</summary>
    internal static bool Run(Func<bool> func, int timeoutMs = 8000) =>
        Run<bool>(func, timeoutMs);
}
