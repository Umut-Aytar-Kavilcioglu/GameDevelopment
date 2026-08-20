using System.Security.Cryptography;
using System.Text.Json;

namespace SdlToolchain;

internal sealed class NativePipeline(
    ToolchainManifest manifest,
    SourceCheckout sources,
    CliInvocation invocation,
    CancellationToken cancellationToken)
{
    public string VersionedNativeRoot => Path.Combine(
        manifest.ArtifactsRoot,
        "native",
        $"{manifest.Sdl.Version}-{manifest.Sdl.Commit[..12]}");

    public async Task<NativeTarget> BuildAsync()
    {
        var cmake = ProcessRunner.FindExecutable("cmake")
            ?? throw new ToolchainException("SDL native kütüphanesini derlemek için cmake PATH üzerinde bulunamadı.");
        var target = NativeTarget.Resolve(invocation.Value("--rid") ?? NativeTarget.Host().Rid);
        if (!manifest.Native.SupportedRids.Contains(target.Rid, StringComparer.OrdinalIgnoreCase))
        {
            throw new ToolchainException($"RID manifestte etkin değil: {target.Rid}");
        }

        if (!target.IsHost && !invocation.Has("--allow-cross"))
        {
            throw new ToolchainException(
                $"{target.Rid}, mevcut hosttan farklı. Uygun toolchain sağladıysanız --allow-cross ve --cmake-arg kullanın.");
        }

        var configuration = invocation.Value("--configuration") ?? "Release";
        if (configuration is not ("Release" or "Debug" or "RelWithDebInfo" or "MinSizeRel"))
        {
            throw new ToolchainException($"Desteklenmeyen CMake configuration: {configuration}");
        }

        var sdlRoot = await sources.GetSdlAsync();
        var buildRoot = Path.Combine(
            manifest.ArtifactsRoot,
            "build",
            "SDL",
            manifest.Sdl.Commit[..12],
            target.Rid,
            configuration);
        var installRoot = Path.Combine(
            manifest.ArtifactsRoot,
            "install",
            "SDL",
            manifest.Sdl.Commit[..12],
            target.Rid,
            configuration);
        Directory.CreateDirectory(buildRoot);
        Directory.CreateDirectory(installRoot);

        var configureArguments = new List<string>
        {
            "-S", sdlRoot,
            "-B", buildRoot,
            $"-DCMAKE_BUILD_TYPE={configuration}",
            $"-DCMAKE_INSTALL_PREFIX={installRoot}",
            "-DSDL_SHARED=ON",
            "-DSDL_STATIC=OFF",
            "-DSDL_TESTS=OFF",
            "-DSDL_EXAMPLES=OFF",
            "-DSDL_TEST_LIBRARY=OFF",
            "-DSDL_INSTALL_TESTS=OFF",
            "-DSDL_DISABLE_INSTALL_DOCS=ON"
        };

        if (target.Platform == "windows")
        {
            configureArguments.Add("-A");
            configureArguments.Add(target.CMakeArchitecture);
        }
        else if (target.Platform == "macos")
        {
            configureArguments.Add($"-DCMAKE_OSX_ARCHITECTURES={target.CMakeArchitecture}");
            configureArguments.Add("-DCMAKE_OSX_DEPLOYMENT_TARGET=10.13");
        }

        configureArguments.AddRange(invocation.Values("--cmake-arg"));

        await ProcessRunner.RunAsync(cmake, configureArguments, manifest.RepositoryRoot, cancellationToken);
        await ProcessRunner.RunAsync(
            cmake,
            ["--build", buildRoot, "--config", configuration],
            manifest.RepositoryRoot,
            cancellationToken);
        await ProcessRunner.RunAsync(
            cmake,
            ["--install", buildRoot, "--config", configuration],
            manifest.RepositoryRoot,
            cancellationToken);

        var installedLibrary = ResolveInstalledLibrary(FindInstalledLibrary(installRoot, target.LibraryFileName));
        var versionedDirectory = Path.Combine(VersionedNativeRoot, target.Rid, "native");
        Directory.CreateDirectory(versionedDirectory);

        var versionedLibrary = Path.Combine(versionedDirectory, target.LibraryFileName);
        File.Copy(installedLibrary, versionedLibrary, overwrite: true);

        await WriteProvenanceAsync(target, configuration, cmake, versionedLibrary);
        Console.WriteLine($"Native SDL hazır: {versionedLibrary}");
        return target;
    }

    private static string FindInstalledLibrary(string installRoot, string fileName)
    {
        var exactMatches = Directory.EnumerateFiles(installRoot, fileName, SearchOption.AllDirectories).ToArray();
        if (exactMatches.Length == 1)
        {
            return exactMatches[0];
        }

        if (exactMatches.Length > 1)
        {
            throw new ToolchainException(
                $"Kurulum ağacında birden fazla {fileName} bulundu:\n{string.Join(Environment.NewLine, exactMatches)}");
        }

        throw new ToolchainException($"SDL kurulumu tamamlandı ancak {fileName} bulunamadı: {installRoot}");
    }

    private static string ResolveInstalledLibrary(string path)
    {
        var file = new FileInfo(path);
        if (file.LinkTarget is null)
        {
            return path;
        }

        return file.ResolveLinkTarget(returnFinalTarget: true)?.FullName
            ?? throw new ToolchainException($"Native SDL symlink hedefi çözülemedi: {path}");
    }

    private async Task WriteProvenanceAsync(
        NativeTarget target,
        string configuration,
        string cmake,
        string nativeLibrary)
    {
        var cmakeVersion = await ProcessRunner.CaptureAsync(
            cmake,
            ["--version"],
            manifest.RepositoryRoot,
            cancellationToken);
        await using var nativeStream = File.OpenRead(nativeLibrary);
        var provenance = new
        {
            builtAtUtc = DateTimeOffset.UtcNow,
            sdlVersion = manifest.Sdl.Version,
            sdlRef = manifest.Sdl.Ref,
            sdlCommit = manifest.Sdl.Commit,
            rid = target.Rid,
            configuration,
            cmake = cmakeVersion.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
            library = Path.GetFileName(nativeLibrary),
            sha256 = Convert.ToHexString(await SHA256.HashDataAsync(nativeStream, cancellationToken)).ToLowerInvariant()
        };
        var serialized = JsonSerializer.Serialize(provenance, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(VersionedNativeRoot, target.Rid, "provenance.json");
        await File.WriteAllTextAsync(
            path,
            serialized,
            cancellationToken);
    }
}
