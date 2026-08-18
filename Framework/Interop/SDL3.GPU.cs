using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Framework.Interop;

/// <summary>
/// Contains project-owned SDL GPU interop helpers that complement the generated bindings.
/// </summary>
public static unsafe partial class SDL3
{
    /// <summary>
    /// Begins a render pass without a depth-stencil target.
    /// </summary>
    internal static IntPtr SDL_BeginGPURenderPassWithoutDepth(
        IntPtr commandBuffer,
        SDL_GPUColorTargetInfo colorTargetInfo)
    {
        return SDL_BeginGPURenderPassWithoutDepthNative(
            commandBuffer,
            &colorTargetInfo,
            1,
            null);
    }

    [LibraryImport(nativeLibName, EntryPoint = "SDL_BeginGPURenderPass")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr SDL_BeginGPURenderPassWithoutDepthNative(
        IntPtr commandBuffer,
        SDL_GPUColorTargetInfo* colorTargetInfos,
        uint numberOfColorTargets,
        SDL_GPUDepthStencilTargetInfo* depthStencilTargetInfo);
}
