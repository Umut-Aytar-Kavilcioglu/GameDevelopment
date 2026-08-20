namespace SdlToolchain;

internal sealed class SourceCheckout(ToolchainManifest manifest, CancellationToken cancellationToken)
{
    private readonly string git = ProcessRunner.FindExecutable("git")
        ?? throw new ToolchainException("git PATH üzerinde bulunamadı.");

    public Task<string> GetSdlAsync() => GetAsync("SDL", manifest.Sdl.Repository, manifest.Sdl.Commit, requireClean: true);

    public Task<string> GetBindingGeneratorAsync() =>
        GetAsync("SDL3-CS", manifest.Bindings.GeneratorRepository, manifest.Bindings.GeneratorCommit, requireClean: true);

    public Task<string> GetC2FfiAsync() =>
        GetAsync("c2ffi", manifest.Bindings.C2FfiRepository, manifest.Bindings.C2FfiCommit, requireClean: true);

    public async Task<string> ResolveRemoteRefAsync(string repository, string gitRef)
    {
        var output = await ProcessRunner.CaptureAsync(
            git,
            ["ls-remote", repository, gitRef, $"{gitRef}^{{}}"],
            manifest.RepositoryRoot,
            cancellationToken);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var peeled = lines.FirstOrDefault(line => line.EndsWith("^{}", StringComparison.Ordinal));
        var selected = peeled ?? lines.FirstOrDefault();
        if (selected is null)
        {
            throw new ToolchainException($"Uzak Git ref bulunamadı: {gitRef}");
        }

        var commit = selected.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (commit.Length != 40 || commit.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ToolchainException($"Uzak ref geçerli bir commit SHA üretmedi: {selected}");
        }

        return commit.ToLowerInvariant();
    }

    private async Task<string> GetAsync(string name, string repository, string commit, bool requireClean)
    {
        var shortCommit = commit[..12];
        var sourceParent = Path.Combine(manifest.ArtifactsRoot, "sources", name);
        var destination = Path.Combine(sourceParent, shortCommit);

        if (Directory.Exists(destination))
        {
            var actualCommit = await ProcessRunner.CaptureAsync(
                git,
                ["-C", destination, "rev-parse", "HEAD"],
                manifest.RepositoryRoot,
                cancellationToken);

            if (!string.Equals(actualCommit, commit, StringComparison.OrdinalIgnoreCase))
            {
                throw new ToolchainException(
                    $"Önbellekteki checkout beklenen commit değil: {destination}\n" +
                    $"Beklenen: {commit}\nBulunan: {actualCommit}");
            }

            if (requireClean)
            {
                var status = await ProcessRunner.CaptureAsync(
                    git,
                    ["-C", destination, "status", "--short", "--untracked-files=all"],
                    manifest.RepositoryRoot,
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    throw new ToolchainException(
                        $"Önbellekteki {name} checkout'u değiştirilmiş; aynı-kaynak garantisi korunamıyor: {destination}\n{status}");
                }
            }

            return destination;
        }

        Directory.CreateDirectory(sourceParent);
        var temporary = Path.Combine(sourceParent, $".tmp-{shortCommit}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporary);

        try
        {
            await ProcessRunner.RunAsync(git, ["init", "--quiet"], temporary, cancellationToken);
            await ProcessRunner.RunAsync(git, ["remote", "add", "origin", repository], temporary, cancellationToken);
            await ProcessRunner.RunAsync(
                git,
                ["fetch", "--depth", "1", "origin", commit],
                temporary,
                cancellationToken);
            await ProcessRunner.RunAsync(git, ["checkout", "--quiet", "--detach", "FETCH_HEAD"], temporary, cancellationToken);

            Directory.Move(temporary, destination);
            Console.WriteLine($"{name} kaynağı hazır: {destination}");
            return destination;
        }
        catch
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }

            throw;
        }
    }
}
