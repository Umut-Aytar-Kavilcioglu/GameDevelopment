using Framework.Interop;

namespace Framework.Graphics;

/// <summary>
/// Owns the SDL GPU device and window claim used by the framework's 2D rendering layer.
/// </summary>
public static class Renderer2D
{
    // These flags describe shader formats supplied by the framework, not formats detected on the GPU.
    private const SDL3.SDL_GPUShaderFormat ShaderFormats =
        SDL3.SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV;

    // GPU validation is valuable during development but should not affect release performance.
#if DEBUG
    private const bool EnableGpuDebugMode = true;
#else
    private const bool EnableGpuDebugMode = false;
#endif

    private static IntPtr _device;
    private static IntPtr _window;

    /// <summary>
    /// Creates the GPU device and claims the supplied window for presentation.
    /// </summary>
    internal static void Initialize(IntPtr window)
    {
        if (_device != IntPtr.Zero)
        {
            throw new InvalidOperationException("Renderer2D is already initialized.");
        }

        if (window == IntPtr.Zero)
        {
            throw new ArgumentException("Window handle cannot be zero.", nameof(window));
        }

        // Keep the device local until every initialization step succeeds.
        var device = SDL3.SDL_CreateGPUDevice(ShaderFormats, EnableGpuDebugMode, null!);

        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to create GPU device: {SDL3.SDL_GetError()}");
        }

        // Claiming the window creates the swapchain state required for future frame presentation.
        if (SDL3.SDL_ClaimWindowForGPUDevice(device, window) == false)
        {
            var error = SDL3.SDL_GetError();
            SDL3.SDL_DestroyGPUDevice(device);

            throw new InvalidOperationException($"Failed to claim window for GPU device: {error}");
        }

        _device = device;
        _window = window;

        var driver = SDL3.SDL_GetGPUDeviceDriver(_device);
        var shaderFormats = SDL3.SDL_GetGPUShaderFormats(_device);

        Console.WriteLine($"Renderer2D initialized using {driver}. Shader formats: {shaderFormats}.");
    }

    /// <summary>
    /// Releases the window claim and destroys the GPU device after pending work completes.
    /// </summary>
    internal static void Shutdown()
    {
        if (_device == IntPtr.Zero) { return; }

        // Native resources must not be destroyed while submitted GPU work can still reference them.
        if (SDL3.SDL_WaitForGPUIdle(_device) == false)
        {
            Console.Error.WriteLine($"Failed to wait for GPU idle: {SDL3.SDL_GetError()}");
        }

        if (_window != IntPtr.Zero)
        {
            SDL3.SDL_ReleaseWindowFromGPUDevice(_device, _window);
            _window = IntPtr.Zero;
        }

        SDL3.SDL_DestroyGPUDevice(_device);
        _device = IntPtr.Zero;
    }
}
