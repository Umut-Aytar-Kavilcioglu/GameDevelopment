using System.Text.RegularExpressions;

namespace SdlToolchain;

internal static partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        try
        {
            var invocation = CliInvocation.Parse(args);
            if (invocation.Command is "help" or "--help" or "-h" || invocation.Has("--help") || invocation.Has("-h"))
            {
                PrintHelp();
                return 0;
            }

            var manifestPath = invocation.Value("--manifest") ?? FindDefaultManifest();
            var manifest = ToolchainManifest.Load(manifestPath);
            var sources = new SourceCheckout(manifest, cancellationSource.Token);
            var bindings = new BindingPipeline(manifest, sources, invocation, cancellationSource.Token);
            var native = new NativePipeline(manifest, sources, invocation, cancellationSource.Token);
            var vendor = new NativeVendor(manifest, sources, native, cancellationSource.Token);

            switch (invocation.Command)
            {
                case "doctor":
                    return await Doctor.RunAsync(manifest, invocation, cancellationSource.Token) ? 0 : 1;
                case "pin":
                    await PinAsync(manifest, sources, invocation);
                    return 0;
                case "bootstrap":
                    var c2ffi = await bindings.BootstrapC2FfiAsync();
                    Console.WriteLine($"c2ffi hazır: {c2ffi}");
                    return 0;
                case "bindings":
                    await bindings.GenerateAsync();
                    return 0;
                case "verify-binding":
                    BindingProvenance.Verify(manifest);
                    return 0;
                case "native":
                    await native.BuildAsync();
                    return 0;
                case "vendor":
                    await vendor.CreateAsync();
                    return 0;
                case "all":
                    await bindings.GenerateAsync();
                    await native.BuildAsync();
                    return 0;
                default:
                    throw new ToolchainException($"Bilinmeyen komut: {invocation.Command}. `help` ile seçenekleri görebilirsiniz.");
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("İşlem iptal edildi.");
            return 130;
        }
        catch (ToolchainException exception)
        {
            Console.Error.WriteLine($"HATA: {exception.Message}");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"BEKLENMEYEN HATA: {exception}");
            return 1;
        }
    }

    private static async Task PinAsync(
        ToolchainManifest manifest,
        SourceCheckout sources,
        CliInvocation invocation)
    {
        if (invocation.Arguments.Count != 1)
        {
            throw new ToolchainException("Kullanım: pin <SDL-sürümü> [--ref <git-ref>] [--commit <SHA>]");
        }

        var version = invocation.Arguments[0];
        if (!SdlVersionRegex().IsMatch(version))
        {
            throw new ToolchainException($"Geçersiz SDL sürümü: {version}");
        }

        var gitRef = invocation.Value("--ref") ?? $"refs/tags/release-{version}";
        var resolvedCommit = await sources.ResolveRemoteRefAsync(manifest.Sdl.Repository, gitRef);
        var assertedCommit = invocation.Value("--commit");
        if (assertedCommit is not null && !string.Equals(assertedCommit, resolvedCommit, StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolchainException(
                $"--commit uzak ref ile eşleşmiyor. Beklenen/uzak: {resolvedCommit}, verilen: {assertedCommit}");
        }

        manifest.Sdl.Version = version;
        manifest.Sdl.Ref = gitRef;
        manifest.Sdl.Commit = resolvedCommit;
        manifest.Save();

        var vendorProps = Path.Combine(manifest.ResolvePath(manifest.Native.VendorDirectory), "SdlNative.props");
        if (File.Exists(vendorProps))
        {
            File.Delete(vendorProps);
        }

        Console.WriteLine($"SDL pini güncellendi: {version} @ {resolvedCommit}");
        Console.WriteLine("Önceki SDL sürümüne ait tracked native seçim dosyası devre dışı bırakıldı.");
        Console.WriteLine("Yeni native bundle üretilip `vendor` ile doğrulanana kadar proje-owned runtime kopyalanmaz.");
        Console.WriteLine("Yeni SDL sürümünde binding warning baseline ve proje-owned SDL3.GPU.cs ayrıca gözden geçirilmelidir.");
    }

    private static string FindDefaultManifest()
    {
        foreach (var startingPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startingPath); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "eng", "sdl-toolchain.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new ToolchainException("eng/sdl-toolchain.json bulunamadı; --manifest ile yolu belirtin.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Project-owned SDL3 toolchain

            Kullanım:
              dotnet run --project Tools/SdlToolchain -- <komut> [seçenekler]

            Komutlar:
              doctor                 Gerekli araçları ve sabitlenmiş sürümleri denetler.
              pin <sürüm>            SDL tag/commit pinini manifestte günceller.
              bootstrap              Sabitlenmiş c2ffi kaynağını derler.
              bindings               Aynı SDL checkout'undan C# binding üretir.
              verify-binding         Committed binding ile manifest pinlerinin eşleştiğini doğrular.
              native                 Aynı SDL checkout'undan seçilen RID için SDL3 derler.
              vendor                 Tüm RID staging çıktılarını doğrulayıp ThirdParty ağacını üretir.
              all                    Binding + host native üretir.

            Genel seçenekler:
              --manifest <yol>       Varsayılan: eng/sdl-toolchain.json
              --c2ffi <yol>          Var olan c2ffi executable kullanılır.
              --no-bootstrap         c2ffi yoksa otomatik derlemeyi kapatır.
              --c2ffi-cmake-arg <x>  c2ffi CMake configure'a ek argüman (tekrarlanabilir).
              --allow-binding-warnings
                                     Değişen WARN_* baseline'ına rağmen bindingi yazar.
              --rid <RID>            Native hedef; varsayılan host RID.
              --configuration <cfg>  Release, Debug, RelWithDebInfo veya MinSizeRel.
              --cmake-arg <x>        SDL CMake configure'a ek argüman (tekrarlanabilir).
              --allow-cross          Cross build sorumluluğunu kullanıcıya bırakır.

            Pin seçenekleri:
              --ref <git-ref>        Varsayılan: refs/tags/release-<sürüm>
              --commit <SHA>         Uzak ref için beklenen commit assertion'ı.
            """);
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SdlVersionRegex();
}
