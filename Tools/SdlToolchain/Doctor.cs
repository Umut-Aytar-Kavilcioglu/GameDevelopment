namespace SdlToolchain;

internal static class Doctor
{
    public static async Task<bool> RunAsync(
        ToolchainManifest manifest,
        CliInvocation invocation,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Manifest : {manifest.ManifestPath}");
        Console.WriteLine($"SDL      : {manifest.Sdl.Version} @ {manifest.Sdl.Commit}");
        Console.WriteLine($"RID      : {NativeTarget.Host().Rid}");
        Console.WriteLine();

        var git = ReportExecutable("git", required: true);
        var dotnet = ReportExecutable("dotnet", required: true);
        var cmake = ReportExecutable("cmake", required: false);
        var compiler = ProcessRunner.FindExecutable(OperatingSystem.IsWindows() ? "cl" : "clang")
            ?? ProcessRunner.FindExecutable(OperatingSystem.IsWindows() ? "clang-cl" : "cc")
            ?? ProcessRunner.FindExecutable("gcc");
        Report("C/C++ compiler", compiler, required: true);

        var explicitC2Ffi = invocation.Value("--c2ffi");
        var c2ffi = explicitC2Ffi is null
            ? FindCachedC2Ffi(manifest)
            : ProcessRunner.FindExecutable(explicitC2Ffi) ?? Path.GetFullPath(explicitC2Ffi, manifest.RepositoryRoot);
        Report("pinned c2ffi", c2ffi is not null && File.Exists(c2ffi) ? c2ffi : null, required: false);

        var unselectedPathC2Ffi = explicitC2Ffi is null ? ProcessRunner.FindExecutable("c2ffi") : null;
        if (unselectedPathC2Ffi is not null)
        {
            Console.WriteLine($"[bilgi] PATH c2ffi : {unselectedPathC2Ffi} (yalnız açık --c2ffi ile kullanılır)");
        }

        var llvm = await LlvmDiscovery.FindAsync(
            manifest.Bindings.C2FfiLlvmVersion,
            manifest.RepositoryRoot,
            cancellationToken);
        var llvmMatches = llvm?.ClangCmakeDirectory is not null;
        if (llvm is not null)
        {
            Console.WriteLine($"[bilgi] LLVM     : {llvm.Version} ({llvm.ConfigExecutable})");
            Console.WriteLine($"[bilgi] LLVM CMake: {llvm.LlvmCmakeDirectory}");
            if (llvm.ClangCmakeDirectory is null)
            {
                Console.WriteLine("[uyarı] Clang CMake geliştirme dosyaları aynı LLVM prefix'i altında bulunamadı.");
            }
            else
            {
                Console.WriteLine($"[bilgi] Clang CMake: {llvm.ClangCmakeDirectory}");
            }
        }
        else
        {
            Console.WriteLine(
                $"[uyarı] LLVM     : uyumlu llvm-config bulunamadı " +
                $"(c2ffi bootstrap LLVM {manifest.Bindings.C2FfiLlvmVersion} geliştirme dosyalarını ister)");
        }

        var baseReady = git is not null && dotnet is not null && compiler is not null;
        var bindingReady = c2ffi is not null || llvmMatches;
        var ready = baseReady && cmake is not null && bindingReady;
        Console.WriteLine();
        Console.WriteLine(ready
            ? "Binding ve native üretim araç zinciri çalıştırılabilir görünüyor."
            : "Tam üretim için eksik araçlar var; yukarıdaki satırlar hangi aşamanın etkilendiğini gösteriyor.");
        return ready;
    }

    private static string? ReportExecutable(string name, bool required)
    {
        var executable = ProcessRunner.FindExecutable(name);
        Report(name, executable, required);
        return executable;
    }

    private static void Report(string name, string? path, bool required)
    {
        var label = path is null ? (required ? "eksik" : "yok/otomatik hazırlanabilir") : "hazır";
        Console.WriteLine($"[{label}] {name,-14}: {path ?? "-"}");
    }

    private static string? FindCachedC2Ffi(ToolchainManifest manifest)
    {
        var executable = OperatingSystem.IsWindows() ? "c2ffi.exe" : "c2ffi";
        var root = Path.Combine(manifest.ArtifactsRoot, "build", "c2ffi", manifest.Bindings.C2FfiCommit[..12]);
        return new[]
        {
            Path.Combine(root, "bin", executable),
            Path.Combine(root, "bin", "Release", executable),
            Path.Combine(root, "Release", executable)
        }.FirstOrDefault(File.Exists);
    }
}
