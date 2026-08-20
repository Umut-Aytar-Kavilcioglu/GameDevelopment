using System.Text.Json;
using System.Text.Json.Serialization;

namespace SdlToolchain;

internal sealed class ToolchainManifest
{
    public int SchemaVersion { get; set; }
    public string ArtifactsDirectory { get; set; } = ".artifacts/sdl-toolchain";
    public SdlSettings Sdl { get; set; } = new();
    public BindingSettings Bindings { get; set; } = new();
    public NativeSettings Native { get; set; } = new();

    [JsonIgnore]
    public string ManifestPath { get; private set; } = string.Empty;

    [JsonIgnore]
    public string RepositoryRoot { get; private set; } = string.Empty;

    [JsonIgnore]
    public string ArtifactsRoot => ResolvePath(ArtifactsDirectory);

    public static ToolchainManifest Load(string manifestPath)
    {
        var fullPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(fullPath))
        {
            throw new ToolchainException($"Manifest bulunamadı: {fullPath}");
        }

        var options = JsonOptions();
        var manifest = JsonSerializer.Deserialize<ToolchainManifest>(File.ReadAllText(fullPath), options)
            ?? throw new ToolchainException($"Manifest okunamadı: {fullPath}");

        manifest.ManifestPath = fullPath;
        manifest.RepositoryRoot = FindRepositoryRoot(Path.GetDirectoryName(fullPath)!);
        manifest.Validate();
        return manifest;
    }

    public void Save()
    {
        Validate();
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(this, JsonOptions()));
    }

    public string ResolvePath(string path) => Path.GetFullPath(path, RepositoryRoot);

    private void Validate()
    {
        if (SchemaVersion != 1)
        {
            throw new ToolchainException($"Desteklenmeyen manifest şeması: {SchemaVersion}");
        }

        Require(Sdl.Version, "sdl.version");
        Require(Sdl.Repository, "sdl.repository");
        Require(Sdl.Ref, "sdl.ref");
        RequireCommit(Sdl.Commit, "sdl.commit");
        Require(Bindings.GeneratorRepository, "bindings.generatorRepository");
        RequireCommit(Bindings.GeneratorCommit, "bindings.generatorCommit");
        Require(Bindings.C2FfiRepository, "bindings.c2ffiRepository");
        RequireCommit(Bindings.C2FfiCommit, "bindings.c2ffiCommit");
        Require(Bindings.C2FfiLlvmVersion, "bindings.c2ffiLlvmVersion");
        Require(Bindings.Namespace, "bindings.namespace");
        Require(Bindings.ClassName, "bindings.className");
        Require(Bindings.Output, "bindings.output");
        Require(Native.VendorDirectory, "native.vendorDirectory");

        if (Bindings.Headers.Count == 0)
        {
            throw new ToolchainException("bindings.headers en az bir başlık içermeli.");
        }

        if (Native.SupportedRids.Count == 0)
        {
            throw new ToolchainException("native.supportedRids en az bir RID içermeli.");
        }

        if (Sdl.Version.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '+')))
        {
            throw new ToolchainException($"Güvenli olmayan SDL sürüm metni: {Sdl.Version}");
        }

        if (!IsIdentifier(Bindings.ClassName) ||
            Bindings.Namespace.Split('.').Any(segment => !IsIdentifier(segment)))
        {
            throw new ToolchainException("bindings.namespace veya bindings.className geçerli bir C# tanımlayıcısı değil.");
        }

        if (Native.SupportedRids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Native.SupportedRids.Count)
        {
            throw new ToolchainException("native.supportedRids yinelenen RID içeremez.");
        }

        foreach (var rid in Native.SupportedRids)
        {
            _ = NativeTarget.Resolve(rid);
        }

        if (Bindings.WarningBaseline.Any(entry => entry.Value < 0))
        {
            throw new ToolchainException("bindings.warningBaseline değerleri negatif olamaz.");
        }

        EnsureRepositoryPath(ArtifactsDirectory, "artifactsDirectory");
        EnsureRepositoryPath(Bindings.Output, "bindings.output");
        EnsureRepositoryPath(Native.VendorDirectory, "native.vendorDirectory");

        var artifactsRoot = Path.TrimEndingDirectorySeparator(ArtifactsRoot);
        var vendorRoot = Path.TrimEndingDirectorySeparator(ResolvePath(Native.VendorDirectory));
        if (IsSameOrNestedPath(artifactsRoot, vendorRoot) || IsSameOrNestedPath(vendorRoot, artifactsRoot))
        {
            throw new ToolchainException("native.vendorDirectory ile artifactsDirectory birbirini içeremez.");
        }

        foreach (var header in Bindings.Headers)
        {
            if (header.Contains("..", StringComparison.Ordinal) ||
                header.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or '/')))
            {
                throw new ToolchainException($"Güvenli olmayan binding header yolu: {header}");
            }
        }
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ToolchainException($"Manifest alanı boş olamaz: {name}");
        }
    }

    private static void RequireCommit(string value, string name)
    {
        Require(value, name);
        if (value.Length != 40 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ToolchainException($"{name} tam 40 karakterlik Git commit SHA'sı olmalı.");
        }
    }

    private void EnsureRepositoryPath(string path, string name)
    {
        var fullPath = ResolvePath(path);
        var relative = Path.GetRelativePath(RepositoryRoot, fullPath);
        if (relative.Equals(".", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ToolchainException($"{name} depo kökünün dışına çıkamaz: {path}");
        }
    }

    private static bool IsIdentifier(string value)
    {
        return value.Length > 0 &&
               (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
               value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
    }

    private static bool IsSameOrNestedPath(string parent, string candidate)
    {
        return candidate.Equals(parent, StringComparison.Ordinal) ||
               candidate.StartsWith($"{parent}{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot(string startingDirectory)
    {
        for (var directory = new DirectoryInfo(startingDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GameDevelopment.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new ToolchainException("GameDevelopment.slnx üst dizinlerde bulunamadı; depo kökü çözülemedi.");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed class SdlSettings
{
    public string Version { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Ref { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
}

internal sealed class BindingSettings
{
    public string GeneratorRepository { get; set; } = string.Empty;
    public string GeneratorCommit { get; set; } = string.Empty;
    [JsonPropertyName("c2ffiRepository")]
    public string C2FfiRepository { get; set; } = string.Empty;
    [JsonPropertyName("c2ffiCommit")]
    public string C2FfiCommit { get; set; } = string.Empty;
    [JsonPropertyName("c2ffiLlvmVersion")]
    public string C2FfiLlvmVersion { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = [];
    public Dictionary<string, int> WarningBaseline { get; set; } = [];
}

internal sealed class NativeSettings
{
    public string VendorDirectory { get; set; } = string.Empty;
    public List<string> SupportedRids { get; set; } = [];
}

internal sealed class ToolchainException(string message, Exception? innerException = null)
    : Exception(message, innerException);
