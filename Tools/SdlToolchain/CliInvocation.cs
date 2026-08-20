namespace SdlToolchain;

internal sealed class CliInvocation
{
    private static readonly HashSet<string> BooleanOptions = new(StringComparer.Ordinal)
    {
        "--help",
        "-h",
        "--allow-binding-warnings",
        "--no-bootstrap",
        "--allow-cross"
    };

    private static readonly HashSet<string> ValueOptions = new(StringComparer.Ordinal)
    {
        "--manifest",
        "--c2ffi",
        "--c2ffi-cmake-arg",
        "--rid",
        "--configuration",
        "--cmake-arg",
        "--ref",
        "--commit"
    };

    private Dictionary<string, List<string>> options = new(StringComparer.Ordinal);

    public string Command { get; private init; } = "help";
    public IReadOnlyList<string> Arguments { get; private init; } = [];

    public static CliInvocation Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliInvocation();
        }

        var command = string.Empty;
        var positional = new List<string>();
        var parsedOptions = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("-", StringComparison.Ordinal))
            {
                if (command.Length == 0)
                {
                    command = token;
                }
                else
                {
                    positional.Add(token);
                }

                continue;
            }

            if (BooleanOptions.Contains(token))
            {
                Add(parsedOptions, token, "true");
                continue;
            }

            if (!ValueOptions.Contains(token))
            {
                throw new ToolchainException($"Bilinmeyen seçenek: {token}");
            }

            if (index + 1 >= args.Length)
            {
                throw new ToolchainException($"{token} için bir değer gerekli.");
            }

            Add(parsedOptions, token, args[++index]);
        }

        return new CliInvocation
        {
            Command = command.Length == 0 ? "help" : command,
            Arguments = positional,
            options = parsedOptions
        };
    }

    public bool Has(string name) => options.ContainsKey(name);

    public string? Value(string name) => options.TryGetValue(name, out var values) ? values[^1] : null;

    public IReadOnlyList<string> Values(string name) => options.TryGetValue(name, out var values) ? values : [];

    private static void Add(Dictionary<string, List<string>> target, string name, string value)
    {
        if (!target.TryGetValue(name, out var values))
        {
            values = [];
            target.Add(name, values);
        }

        values.Add(value);
    }
}
