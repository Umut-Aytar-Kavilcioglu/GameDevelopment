using System.Runtime.InteropServices;

namespace SdlToolchain;

internal sealed record NativeTarget(
    string Rid,
    string Platform,
    Architecture Architecture,
    string CMakeArchitecture,
    string LibraryFileName)
{
    private static readonly IReadOnlyDictionary<string, NativeTarget> Targets =
        new Dictionary<string, NativeTarget>(StringComparer.OrdinalIgnoreCase)
        {
            ["win-x86"] = new("win-x86", "windows", Architecture.X86, "Win32", "SDL3.dll"),
            ["win-x64"] = new("win-x64", "windows", Architecture.X64, "x64", "SDL3.dll"),
            ["win-arm64"] = new("win-arm64", "windows", Architecture.Arm64, "ARM64", "SDL3.dll"),
            ["linux-x64"] = new("linux-x64", "linux", Architecture.X64, string.Empty, "libSDL3.so"),
            ["linux-arm64"] = new("linux-arm64", "linux", Architecture.Arm64, string.Empty, "libSDL3.so"),
            ["osx-x64"] = new("osx-x64", "macos", Architecture.X64, "x86_64", "libSDL3.dylib"),
            ["osx-arm64"] = new("osx-arm64", "macos", Architecture.Arm64, "arm64", "libSDL3.dylib")
        };

    public static NativeTarget Resolve(string rid)
    {
        return Targets.TryGetValue(rid, out var target)
            ? target
            : throw new ToolchainException(
                $"Desteklenmeyen RID: {rid}. Desteklenenler: {string.Join(", ", Targets.Keys.Order())}");
    }

    public static NativeTarget Host()
    {
        var operatingSystem = System.OperatingSystem.IsWindows()
            ? "windows"
            : System.OperatingSystem.IsLinux()
                ? "linux"
                : System.OperatingSystem.IsMacOS()
                    ? "macos"
                    : throw new ToolchainException("Bu işletim sistemi için SDL native hedefi tanımlı değil.");

        return Targets.Values.FirstOrDefault(target =>
                   target.Platform == operatingSystem &&
                   target.Architecture == RuntimeInformation.ProcessArchitecture)
               ?? throw new ToolchainException(
                   $"Host hedefi desteklenmiyor: {operatingSystem}/{RuntimeInformation.ProcessArchitecture}");
    }

    public bool IsHost =>
        Platform == Host().Platform && Architecture == RuntimeInformation.ProcessArchitecture;
}
