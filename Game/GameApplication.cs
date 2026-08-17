using Framework;
using Framework.Input;

namespace Game;

public sealed class GameApplication : Engine
{
    public GameApplication()
        : base("MyGame", 1280, 720)
    {
    }

    protected override void OnInitialize()
    {
        // Game initialization
    }

    protected override void OnUpdate()
    {
        if (Keyboard.IsKeyPressed(Key.Escape))
        {
            Stop();
        }
    }

    protected override void OnRender()
    {

    }

    protected override void OnShutdown()
    {
        // Game shutdown
    }
}
