using Framework.Graphics;
using Framework.Input;
using Framework.Interop;

namespace Framework;

public abstract class Engine
{
    private readonly string _windowTitle;
    private readonly int _windowWidth;
    private readonly int _windowHeight;

    private bool _isRunning;
    private bool _isRunActive;
    private IntPtr _windowHandle;

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
            coreInitializationStarted = true;
            InitializeCore();

            gameInitializationStarted = true;
            OnInitialize();

            _isRunning = true;

            while (_isRunning)
            {
                ProcessEvents();

                if (_isRunning == false)
                {
                    break;
                }

                Keyboard.Update();
                OnUpdate();
                OnRender();
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

    protected virtual void OnInitialize() { }
    protected virtual void OnUpdate() { }
    protected virtual void OnRender() { }
    protected virtual void OnShutdown() { }

    protected void Stop()
    {
        _isRunning = false;
    }

    private void InitializeCore()
    {
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
        Renderer2D.Shutdown();

        if (_windowHandle != IntPtr.Zero)
        {
            SDL3.SDL_DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        SDL3.SDL_Quit();
    }

    private void ProcessEvents()
    {
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
