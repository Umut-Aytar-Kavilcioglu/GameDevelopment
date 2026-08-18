using Framework.Graphics;
using Framework.Input;
using Framework.Interop;
using Framework.Time;

namespace Framework;

/// <summary>
/// Owns the application lifecycle and coordinates engine subsystems around game-defined hooks.
/// </summary>
public abstract class Engine
{
    private const double NanosecondsPerSecond = 1_000_000_000.0;

    private readonly string _windowTitle;
    private readonly int _windowWidth;
    private readonly int _windowHeight;

    private bool _isRunning;
    private bool _isRunActive;
    private IntPtr _windowHandle;
    private ulong _startTime;
    private ulong _previousTime;
    private ulong _frameCount;

    /// <summary>
    /// Creates an engine instance with the window configuration required by the game.
    /// </summary>
    protected Engine(string windowTitle, int windowWidth, int windowHeight)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
        {
            throw new ArgumentException("Window title cannot be empty.", nameof(windowTitle));
        }

        if (windowWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowWidth), "Window width must be greater than zero.");
        }

        if (windowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowHeight), "Window height must be greater than zero.");
        }

        _windowTitle = windowTitle;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
    }

    /// <summary>
    /// Initializes the engine, runs frames until stopped, and deterministically releases resources.
    /// </summary>
    public void Run()
    {
        if (_isRunActive)
        {
            throw new InvalidOperationException("The engine is already running.");
        }

        _isRunActive = true;
        var coreInitializationStarted = false;
        var gameInitializationStarted = false;

        try
        {
            // Core teardown is safe after partial initialization, so mark it before entering the method.
            coreInitializationStarted = true;
            InitializeCore();

            // OnShutdown must tolerate partially initialized game state if this hook throws.
            gameInitializationStarted = true;
            OnInitialize();

            // Exclude engine and game initialization work from the first frame's elapsed time.
            ResetTime();
            _isRunning = true;

            while (_isRunning)
            {
                ProcessEvents();

                if (_isRunning == false)
                {
                    break;
                }

                var gameTime = AdvanceTime();

                Keyboard.Update();
                OnUpdate(gameTime);
                RenderFrame();
            }
        }
        finally
        {
            _isRunning = false;

            try
            {
                if (gameInitializationStarted)
                {
                    OnShutdown();
                }
            }
            finally
            {
                // Engine-owned native resources must be released even when game cleanup fails.
                try
                {
                    if (coreInitializationStarted)
                    {
                        ShutdownCore();
                    }
                }
                finally
                {
                    _isRunActive = false;
                }
            }
        }
    }

    /// <summary>
    /// Allows the game to create its state and resources after all engine subsystems are available.
    /// </summary>
    /// <remarks>
    /// If this method throws, <see cref="OnShutdown"/> is still called. Implementations must support
    /// cleanup of partially initialized state.
    /// </remarks>
    protected virtual void OnInitialize() { }

    /// <summary>
    /// Advances game state once per frame after events and keyboard state have been processed.
    /// </summary>
    /// <param name="gameTime">Timing information for the current frame.</param>
    protected virtual void OnUpdate(GameTime gameTime) { }

    /// <summary>
    /// Allows the game to submit its rendering work for the current frame.
    /// </summary>
    protected virtual void OnRender() { }

    /// <summary>
    /// Allows the game to release its resources before engine-owned subsystems are destroyed.
    /// </summary>
    protected virtual void OnShutdown() { }

    /// <summary>
    /// Requests a clean exit from the main loop after the current operation completes.
    /// </summary>
    protected void Stop()
    {
        _isRunning = false;
    }

    private void InitializeCore()
    {
        // Ownership is established in dependency order: SDL, window, then the GPU renderer.
        Console.WriteLine("Initializing SDL...");

        if (!SDL3.SDL_Init(SDL3.SDL_InitFlags.SDL_INIT_VIDEO))
        {
            throw new InvalidOperationException($"SDL initialization failed: {SDL3.SDL_GetError()}");
        }
        Console.WriteLine("SDL initialized.");

        _windowHandle = SDL3.SDL_CreateWindow(_windowTitle, _windowWidth, _windowHeight, 0);

        if (_windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL window creation failed: {SDL3.SDL_GetError()}");
        }

        Console.WriteLine($"Window created: \"{_windowTitle}\" ({_windowWidth}x{_windowHeight}).");

        Renderer2D.Initialize(_windowHandle);
    }

    private void ShutdownCore()
    {
        // Teardown reverses initialization so no resource outlives a dependency it references.
        Renderer2D.Shutdown();

        if (_windowHandle != IntPtr.Zero)
        {
            SDL3.SDL_DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        SDL3.SDL_Quit();
    }

    private void ResetTime()
    {
        _startTime = SDL3.SDL_GetTicksNS();
        _previousTime = _startTime;
        _frameCount = 0;
    }

    private GameTime AdvanceTime()
    {
        var currentTime = SDL3.SDL_GetTicksNS();
        var deltaTime = (currentTime - _previousTime) / NanosecondsPerSecond;
        var totalTime = (currentTime - _startTime) / NanosecondsPerSecond;

        _previousTime = currentTime;
        _frameCount++;

        return new GameTime((float)deltaTime, totalTime, _frameCount);
    }

    private void RenderFrame()
    {
        if (!Renderer2D.BeginFrame())
        {
            return;
        }

        // Frame ownership stays inside the engine so game code only submits drawing commands.
        try
        {
            OnRender();
        }
        finally
        {
            Renderer2D.EndFrame();
        }
    }

    private void ProcessEvents()
    {
        // SDL events remain an engine detail; game code receives engine-level state and hooks instead.
        while (SDL3.SDL_PollEvent(out SDL3.SDL_Event sdlEvent))
        {
            var type = (SDL3.SDL_EventType)sdlEvent.type;

            if (type == SDL3.SDL_EventType.SDL_EVENT_QUIT)
            {
                Stop();
            }
        }
    }
}
