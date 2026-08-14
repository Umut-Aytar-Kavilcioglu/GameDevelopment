using Framework;
using Framework.Inputs;

namespace Game;

public sealed class Game : Engine
{
    protected override void Initialize()
    {
        base.Initialize();

        // Game initialization
    }

    protected override void Update()
    {
        if (Input.IsKeyPressed(Key.Escape))
        {
            Stop();
        }
    }

    protected override void Render()
    {

    }

    protected override void Shutdown()
    {
        // Game shutdown

        base.Shutdown();
    }
}
