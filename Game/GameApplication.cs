using Framework;
using Framework.Input;
using Framework.Time;

namespace Game;

/// <summary>
/// Defines the game-specific behavior hosted by the reusable framework lifecycle.
/// </summary>
public sealed class GameApplication : Engine
{
    public GameApplication()
        : base("MyGame", 1280, 720)
    {
    }

    protected override void OnInitialize()
    {
        // Create game-owned worlds, entities, assets, and other persistent state here.
    }

    protected override void OnUpdate(GameTime gameTime)
    {
        if (Keyboard.IsKeyPressed(Key.Escape))
        {
            Stop();
        }
    }

    protected override void OnRender()
    {
        // Submit game-facing Renderer2D draw commands here; the engine owns the frame lifecycle.
    }

    protected override void OnShutdown()
    {
        // Release game-owned resources while engine subsystems are still available.
    }
}
