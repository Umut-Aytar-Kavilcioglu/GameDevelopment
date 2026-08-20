using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace SdlToolchain;

internal sealed class NativeVendor(
    ToolchainManifest manifest,
    SourceCheckout sources,
    NativePipeline nativePipeline,
    CancellationToken cancellationToken)
{
    public async Task<string> CreateAsync()
    {
        var artifacts = new List<ValidatedNativeArtifact>();
        var missingRids = new List<string>();

        foreach (var rid in manifest.Native.SupportedRids)
        {
            var artifact = await ValidateNativeArtifactAsync(rid);
            if (artifact is null)
            {
                missingRids.Add(rid);
            }
            else
            {
                artifacts.Add(artifact);
            }
        }

        if (missingRids.Count > 0)
        {
            throw new ToolchainException(
                "Tracked native ağacı yalnız bütün RID'ler aynı SDL commit'inden üretildiğinde oluşturulur. " +
                "Eksikler: " + string.Join(", ", missingRids));
        }

        BindingProvenance.Verify(manifest);

        var sdlRoot = await sources.GetSdlAsync();
        var license = Path.Combine(sdlRoot, "LICENSE.txt");
        if (!File.Exists(license))
        {
            throw new ToolchainException($"SDL lisans dosyası bulunamadı: {license}");
        }

        var destination = manifest.ResolvePath(manifest.Native.VendorDirectory);
        var parent = Path.GetDirectoryName(destination)
            ?? throw new ToolchainException($"Native vendor üst dizini çözülemedi: {destination}");
        Directory.CreateDirectory(parent);

        var stagedDestination = $"{destination}.tmp-{Guid.NewGuid():N}";
        try
        {
            await CreateVendorTreeAsync(stagedDestination, license, artifacts);
            ReplaceDirectory(stagedDestination, destination);
        }
        finally
        {
            if (Directory.Exists(stagedDestination))
            {
                Directory.Delete(stagedDestination, recursive: true);
            }
        }

        Console.WriteLine($"Tracked SDL native ağacı hazır: {destination}");
        return destination;
    }

    private async Task<ValidatedNativeArtifact?> ValidateNativeArtifactAsync(string rid)
    {
        var target = NativeTarget.Resolve(rid);
        var nativeRoot = nativePipeline.VersionedNativeRoot;
        var library = Path.Combine(nativeRoot, rid, "native", target.LibraryFileName);
        var provenancePath = Path.Combine(nativeRoot, rid, "provenance.json");
        var hasLibrary = File.Exists(library);
        var hasProvenance = File.Exists(provenancePath);

        if (!hasLibrary && !hasProvenance)
        {
            return null;
        }

        if (!hasLibrary || !hasProvenance)
        {
            throw new ToolchainException(
                $"Eksik native artifact çifti ({rid}). Library={hasLibrary}, provenance={hasProvenance}");
        }

        try
        {
            var provenanceText = await File.ReadAllTextAsync(provenancePath, cancellationToken);
            using var provenance = JsonDocument.Parse(provenanceText);
            var root = provenance.RootElement;
            RequireProvenance(root, "sdlVersion", manifest.Sdl.Version, rid);
            RequireProvenance(root, "sdlRef", manifest.Sdl.Ref, rid);
            RequireProvenance(root, "sdlCommit", manifest.Sdl.Commit, rid);
            RequireProvenance(root, "rid", rid, rid);
            RequireProvenance(root, "configuration", "Release", rid);
            RequireProvenance(root, "library", target.LibraryFileName, rid);

            await using var stream = File.OpenRead(library);
            var actualHash = Convert.ToHexString(
                    await SHA256.HashDataAsync(stream, cancellationToken))
                .ToLowerInvariant();
            RequireProvenance(root, "sha256", actualHash, rid);

            return new ValidatedNativeArtifact(target, library, provenanceText, actualHash);
        }
        catch (JsonException exception)
        {
            throw new ToolchainException($"Native provenance JSON geçersiz ({rid}): {provenancePath}", exception);
        }
    }

    private async Task CreateVendorTreeAsync(
        string root,
        string license,
        IReadOnlyList<ValidatedNativeArtifact> artifacts)
    {
        Directory.CreateDirectory(root);

        foreach (var artifact in artifacts)
        {
            var runtimeDirectory = Path.Combine(root, "runtimes", artifact.Target.Rid, "native");
            var provenanceDirectory = Path.Combine(root, "provenance");
            Directory.CreateDirectory(runtimeDirectory);
            Directory.CreateDirectory(provenanceDirectory);

            File.Copy(
                artifact.LibraryPath,
                Path.Combine(runtimeDirectory, artifact.Target.LibraryFileName),
                overwrite: false);
            await File.WriteAllTextAsync(
                Path.Combine(provenanceDirectory, $"{artifact.Target.Rid}.json"),
                artifact.Provenance,
                cancellationToken);
        }

        File.Copy(license, Path.Combine(root, "LICENSE.txt"), overwrite: false);
        await WriteBundleManifestAsync(root, artifacts);
        await File.WriteAllTextAsync(
            Path.Combine(root, "README.md"),
            CreateReadme(),
            cancellationToken);
        WriteMsBuildProps(root, artifacts);
    }

    private async Task WriteBundleManifestAsync(
        string root,
        IReadOnlyList<ValidatedNativeArtifact> artifacts)
    {
        var bundleManifest = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            sdlVersion = manifest.Sdl.Version,
            sdlRef = manifest.Sdl.Ref,
            sdlCommit = manifest.Sdl.Commit,
            runtimes = artifacts.Select(artifact => new
            {
                rid = artifact.Target.Rid,
                library = artifact.Target.LibraryFileName,
                sha256 = artifact.Sha256
            }).ToArray()
        };

        await File.WriteAllTextAsync(
            Path.Combine(root, "sdl-native-manifest.json"),
            JsonSerializer.Serialize(bundleManifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private void WriteMsBuildProps(string root, IReadOnlyList<ValidatedNativeArtifact> artifacts)
    {
        var itemGroup = new XElement("ItemGroup");
        foreach (var artifact in artifacts)
        {
            var target = artifact.Target;
            var condition =
                $"'$(RuntimeIdentifier)' == '{target.Rid}' Or " +
                $"('$(RuntimeIdentifier)' == '' And '$(_SdlHostPlatformRid)' == '{target.Rid}')";
            itemGroup.Add(new XElement(
                "_SdlProjectNativeLibrary",
                new XAttribute(
                    "Include",
                    $"$(MSBuildThisFileDirectory)runtimes/{target.Rid}/native/{target.LibraryFileName}"),
                new XAttribute("Condition", condition),
                new XElement("TargetFileName", target.LibraryFileName)));
        }

        var document = new XDocument(
            new XComment("Generated by Tools/SdlToolchain. Do not edit by hand."),
            new XElement(
                "Project",
                new XElement(
                    "PropertyGroup",
                    new XElement("_SdlNativeBundleVersion", manifest.Sdl.Version),
                    new XElement("_SdlNativeBundleCommit", manifest.Sdl.Commit)),
                itemGroup));
        document.Save(Path.Combine(root, "SdlNative.props"));
    }

    private string CreateReadme() =>
        $$"""
        # Project-owned SDL3 native runtimes

        This directory is generated by `Tools/SdlToolchain` from SDL {{manifest.Sdl.Version}}
        at commit `{{manifest.Sdl.Commit}}`.

        `runtimes/<rid>/native` contains the checked-in SDL shared libraries used by
        normal game builds and publishes. `provenance` and
        `sdl-native-manifest.json` record the exact source and SHA-256 for every file.

        Do not replace individual libraries by hand. Generate every supported RID from
        the same manifest, run `dotnet run --project Tools/SdlToolchain -- vendor`, then
        review and commit the resulting directory as one unit.
        """;

    private static void ReplaceDirectory(string staged, string destination)
    {
        if (!Directory.Exists(destination))
        {
            Directory.Move(staged, destination);
            return;
        }

        var backup = $"{destination}.backup-{Guid.NewGuid():N}";
        Directory.Move(destination, backup);
        try
        {
            Directory.Move(staged, destination);
        }
        catch
        {
            Directory.Move(backup, destination);
            throw;
        }

        try
        {
            Directory.Delete(backup, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"UYARI: Eski native vendor yedeği silinemedi: {backup} ({exception.Message})");
        }
    }

    private static void RequireProvenance(JsonElement root, string property, string expected, string rid)
    {
        if (!root.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase))
        {
            var actual = value.ValueKind == JsonValueKind.Undefined ? "<yok>" : value.ToString();
            throw new ToolchainException(
                $"Native provenance uyuşmazlığı ({rid}, {property}). Beklenen={expected}, bulunan={actual}");
        }
    }

    private sealed record ValidatedNativeArtifact(
        NativeTarget Target,
        string LibraryPath,
        string Provenance,
        string Sha256);
}
