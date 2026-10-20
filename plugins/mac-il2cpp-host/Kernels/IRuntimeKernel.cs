using TpmMacIl2CppHost.Contracts;

namespace TpmMacIl2CppHost.Kernels;

/// <summary>Kernel implementation of contract A; exposes which backend is bound.</summary>
public interface IRuntimeKernel : IRuntimeBridge
{
    HostKernelKind Kind { get; }
}
