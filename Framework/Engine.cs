using Framework.Binding;
using Framework.Inputs;

namespace Framework;

public abstract class Engine
{
    private bool _isRunning;
    private IntPtr _windowHandle;

    public void Run()
    {
        try
        {
            Initialize();
            _isRunning = true;

            while (_isRunning)
            {
                ProcessEvents();
                if (_isRunning == false) { break; }
                Input.Update();
                Update();
                Render();
            }
        }
        finally
        {
            Shutdown();
        }
    }

    protected virtual void Initialize()
    {
        Console.WriteLine("Initializing SDL...");

        if (!SDL3.SDL_Init(SDL3.SDL_InitFlags.SDL_INIT_VIDEO))
        {
            throw new InvalidOperationException($"SDL initialization failed: {SDL3.SDL_GetError()}");
        }
        Console.WriteLine("SDL initialized.");

        _windowHandle = SDL3.SDL_CreateWindow("MyGame", 1280, 710, 0);

        if (_windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL window creation failed: {SDL3.SDL_GetError()}");
        }

        Console.WriteLine($"Window created: {_windowHandle}");
    }

    protected virtual void Update() { }
    protected virtual void Render() { }

    protected virtual void Shutdown()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            SDL3.SDL_DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        SDL3.SDL_Quit();
    }

    protected void Stop()
    {
        _isRunning = false;
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
