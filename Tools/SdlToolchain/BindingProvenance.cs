namespace SdlToolchain;

internal static class BindingProvenance
{
    public static void Verify(ToolchainManifest manifest)
    {
        var bindingPath = manifest.ResolvePath(manifest.Bindings.Output);
        if (!File.Exists(bindingPath))
        {
            throw new ToolchainException($"Committed binding bulunamadı: {bindingPath}");
        }

        var binding = File.ReadAllText(bindingPath);
        var expectedSdl = $"// SDL source: {manifest.Sdl.Ref} ({manifest.Sdl.Commit})";
        var expectedGenerator =
            $"// Binding generator: flibitijibibo/SDL3-CS@{manifest.Bindings.GeneratorCommit}";

        if (!binding.Contains(expectedSdl, StringComparison.Ordinal) ||
            !binding.Contains(expectedGenerator, StringComparison.Ordinal))
        {
            throw new ToolchainException(
                "Committed C# binding manifestteki SDL/generator piniyle eşleşmiyor. " +
                "Önce yerelde `bindings` komutunu çalıştırıp oluşan binding'i commit edin.");
        }

        Console.WriteLine(
            $"Committed binding doğrulandı: SDL {manifest.Sdl.Version} @ {manifest.Sdl.Commit}");
    }
}
