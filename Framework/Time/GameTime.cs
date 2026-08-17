namespace Framework.Time;

/// <summary>
/// Describes the timing information for a single game frame.
/// </summary>
public readonly struct GameTime
{
    internal GameTime(float deltaTime, double totalTime, ulong frameCount)
    {
        DeltaTime = deltaTime;
        TotalTime = totalTime;
        FrameCount = frameCount;
    }

    /// <summary>
    /// Gets the elapsed time in seconds since the previous frame.
    /// </summary>
    public float DeltaTime { get; }

    /// <summary>
    /// Gets the total elapsed game time in seconds since the game loop started.
    /// </summary>
    public double TotalTime { get; }

    /// <summary>
    /// Gets the number of update frames processed by the engine, including the current frame.
    /// </summary>
    public ulong FrameCount { get; }
}
