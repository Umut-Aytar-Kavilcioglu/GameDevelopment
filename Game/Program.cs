namespace Game;

/// <summary>
/// Provides the process entry point and starts the concrete game application.
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        var game = new GameApplication();
        game.Run();
    }
}
