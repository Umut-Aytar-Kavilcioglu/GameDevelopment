namespace SdlToolchain;

internal sealed record LlvmInstallation(
    string Version,
    string ConfigExecutable,
    string LlvmCmakeDirectory,
    string? ClangCmakeDirectory,
    string? ClangExecutable,
    string? ClangCxxCompiler);

internal static class LlvmDiscovery
{
    public static async Task<LlvmInstallation?> FindAsync(
        string expectedVersion,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var major = expectedVersion.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        var candidates = new[]
        {
            ProcessRunner.FindExecutable($"llvm-config-{major}"),
            ProcessRunner.FindExecutable("llvm-config"),
            ProcessRunner.FindExecutable($"/usr/lib/llvm{major}/bin/llvm-config"),
            ProcessRunner.FindExecutable($"/usr/lib/llvm-{major}/bin/llvm-config")
        };

        foreach (var executable in candidates.Where(path => path is not null).Distinct(StringComparer.Ordinal))
        {
            var version = await ProcessRunner.CaptureAsync(
                executable!,
                ["--version"],
                workingDirectory,
                cancellationToken);
            if (!version.StartsWith(expectedVersion, StringComparison.Ordinal))
            {
                continue;
            }

            var cmakeDirectory = await ProcessRunner.CaptureAsync(
                executable!,
                ["--cmakedir"],
                workingDirectory,
                cancellationToken);
            var prefix = await ProcessRunner.CaptureAsync(
                executable!,
                ["--prefix"],
                workingDirectory,
                cancellationToken);
            if (!Directory.Exists(cmakeDirectory))
            {
                continue;
            }

            var clangCmake = ExistingDirectory(Path.Combine(prefix, "lib", "cmake", "clang"));
            var clang = ExistingFile(Path.Combine(prefix, "bin", OperatingSystem.IsWindows() ? "clang.exe" : "clang"));
            var clangCxx = ExistingFile(Path.Combine(prefix, "bin", OperatingSystem.IsWindows() ? "clang++.exe" : "clang++"));

            return new LlvmInstallation(
                version,
                executable!,
                Path.GetFullPath(cmakeDirectory),
                clangCmake,
                clang,
                clangCxx);
        }

        return null;
    }

    private static string? ExistingDirectory(string path) => Directory.Exists(path) ? Path.GetFullPath(path) : null;

    private static string? ExistingFile(string path) => File.Exists(path) ? Path.GetFullPath(path) : null;
}
