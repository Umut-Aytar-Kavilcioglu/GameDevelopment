using System.Diagnostics;

namespace SdlToolchain;

internal static class ProcessRunner
{
    public static string? FindExecutable(string name)
    {
        if (Path.IsPathFullyQualified(name) || name.Contains(Path.DirectorySeparatorChar))
        {
            return File.Exists(name) ? Path.GetFullPath(name) : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var candidates = OperatingSystem.IsWindows() && Path.GetExtension(name).Length == 0
            ? new[] { name, $"{name}.exe", $"{name}.cmd", $"{name}.bat" }
            : new[] { name };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidate in candidates)
            {
                var fullPath = Path.Combine(directory, candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    public static async Task RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = CreateProcess(executable, arguments, workingDirectory);
        WriteCommand(process.StartInfo);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new ToolchainException($"Komut {process.ExitCode} koduyla başarısız oldu: {executable}");
        }
    }

    public static async Task<string> CaptureAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = CreateProcess(executable, arguments, workingDirectory, streamOutput: false);
        WriteCommand(process.StartInfo);

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.Error.WriteLine(output.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.Error.WriteLine(error.TrimEnd());
            }

            throw new ToolchainException($"Komut {process.ExitCode} koduyla başarısız oldu: {executable}");
        }

        return output.Trim();
    }

    private static Process CreateProcess(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        bool streamOutput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo };
        if (streamOutput)
        {
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    Console.WriteLine(eventArgs.Data);
                }
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    Console.Error.WriteLine(eventArgs.Data);
                }
            };
        }

        return process;
    }

    private static void WriteCommand(ProcessStartInfo startInfo)
    {
        var arguments = string.Join(' ', startInfo.ArgumentList.Select(QuoteForDisplay));
        Console.WriteLine($"> {Path.GetFileName(startInfo.FileName)} {arguments}");
    }

    private static string QuoteForDisplay(string value) =>
        value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and Kill.
        }
    }
}
