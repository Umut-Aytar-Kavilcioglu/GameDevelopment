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
    private static IntPtr _commandBuffer;
    private static IntPtr _renderPass;
    private static bool _frameActive;

    /// <summary>
    /// Gets or sets the color used to clear the window at the beginning of each rendered frame.
    /// </summary>
    public static Color ClearColor { get; set; } = new(0.05f, 0.06f, 0.09f);

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
    /// Acquires the current swapchain texture and begins its render pass.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the window can be rendered; otherwise,
    /// <see langword="false"/> when no swapchain texture is currently available.
    /// </returns>
    internal static bool BeginFrame()
    {
        EnsureInitialized();

        if (_frameActive)
        {
            throw new InvalidOperationException("A Renderer2D frame is already active.");
        }

        var commandBuffer = SDL3.SDL_AcquireGPUCommandBuffer(_device);

        if (commandBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to acquire GPU command buffer: {SDL3.SDL_GetError()}");
        }

        if (SDL3.SDL_WaitAndAcquireGPUSwapchainTexture(
                commandBuffer,
                _window,
                out var swapchainTexture,
                out _,
                out _) == false)
        {
            var acquireError = SDL3.SDL_GetError();

            if (SDL3.SDL_CancelGPUCommandBuffer(commandBuffer) == false)
            {
                var cancelError = SDL3.SDL_GetError();

                throw new InvalidOperationException(
                    $"Failed to acquire swapchain texture: {acquireError} " +
                    $"The command buffer also could not be cancelled: {cancelError}");
            }

            throw new InvalidOperationException($"Failed to acquire swapchain texture: {acquireError}");
        }

        // A minimized or otherwise non-presentable window can temporarily have no swapchain texture.
        if (swapchainTexture == IntPtr.Zero)
        {
            SubmitCommandBuffer(commandBuffer);
            return false;
        }

        var clearColor = ClearColor;
        var colorTargetInfo = new SDL3.SDL_GPUColorTargetInfo
        {
            texture = swapchainTexture,
            clear_color = new SDL3.SDL_FColor
            {
                r = clearColor.R,
                g = clearColor.G,
                b = clearColor.B,
                a = clearColor.A
            },
            load_op = SDL3.SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL3.SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE
        };

        var renderPass = SDL3.SDL_BeginGPURenderPassWithoutDepth(commandBuffer, colorTargetInfo);

        if (renderPass == IntPtr.Zero)
        {
            var renderPassError = SDL3.SDL_GetError();

            // A command buffer cannot be cancelled after acquiring a swapchain texture.
            if (SDL3.SDL_SubmitGPUCommandBuffer(commandBuffer) == false)
            {
                var submitError = SDL3.SDL_GetError();

                throw new InvalidOperationException(
                    $"Failed to begin GPU render pass: {renderPassError} " +
                    $"The command buffer also could not be submitted: {submitError}");
            }

            throw new InvalidOperationException($"Failed to begin GPU render pass: {renderPassError}");
        }

        _commandBuffer = commandBuffer;
        _renderPass = renderPass;
        _frameActive = true;

        return true;
    }

    /// <summary>
    /// Ends and submits the active render pass, which presents its swapchain texture.
    /// </summary>
    internal static void EndFrame()
    {
        if (!_frameActive)
        {
            throw new InvalidOperationException("No Renderer2D frame is active.");
        }

        var renderPass = _renderPass;
        var commandBuffer = _commandBuffer;

        SDL3.SDL_EndGPURenderPass(renderPass);

        // Clear recording state before submission because the command buffer becomes invalid afterward.
        _renderPass = IntPtr.Zero;
        _commandBuffer = IntPtr.Zero;
        _frameActive = false;

        SubmitCommandBuffer(commandBuffer);
    }

    /// <summary>
    /// Releases the window claim and destroys the GPU device after pending work completes.
    /// </summary>
    internal static void Shutdown()
    {
        if (_device == IntPtr.Zero) { return; }

        if (_frameActive)
        {
            try
            {
                EndFrame();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Failed to finish the active Renderer2D frame: {exception.Message}");
            }
        }

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
        _commandBuffer = IntPtr.Zero;
        _renderPass = IntPtr.Zero;
        _frameActive = false;
    }

    private static void EnsureInitialized()
    {
        if (_device == IntPtr.Zero || _window == IntPtr.Zero)
        {
            throw new InvalidOperationException("Renderer2D is not initialized.");
        }
    }

    private static void SubmitCommandBuffer(IntPtr commandBuffer)
    {
        if (SDL3.SDL_SubmitGPUCommandBuffer(commandBuffer) == false)
        {
            throw new InvalidOperationException($"Failed to submit GPU command buffer: {SDL3.SDL_GetError()}");
        }
    }
}
