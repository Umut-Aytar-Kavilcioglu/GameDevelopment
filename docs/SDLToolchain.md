# Projeye ait SDL3 araç zinciri

Bu depo C# binding dosyasını yerelde, platform native SDL3 kütüphanelerini ise
yerelde veya GitHub Actions üzerinde aynı sabitlenmiş SDL kaynak commit'inden
üretir. Normal oyun derlemesi SDL'yi, binding üreticisini veya `c2ffi`yi
çalıştırmaz.

Tek doğruluk noktası [`eng/sdl-toolchain.json`](../eng/sdl-toolchain.json)
dosyasıdır. Manifest resmi SDL kaynağını, binding generator'ını ve uyumlu
`c2ffi`/LLVM serisini tam Git commit SHA'larıyla sabitler.

```text
eng/sdl-toolchain.json
          |
          +--> yerel: SDL headers --> c2ffi --> SDL3-CS generator --> Framework/Interop/SDL3.cs
          |
          +--> Actions: aynı SDL checkout --> yedi native RID
                                           --> doğrulanmış ThirdParty/SDL3 ağacı
                                           --> indirilebilir native bundle
```

## Sorumluluk ayrımı

- Binding yalnız SDL sürümü güncellendiğinde geliştirici tarafından yerelde
  üretilir ve kaynak kontrolünde tutulur.
- GitHub Actions binding üretmez; yalnız native SDL kütüphanelerini üretir.
- Onaylanan native bundle `ThirdParty/SDL3` altında kaynak kontrolünde tutulur.
- `dotnet build` host native dosyasını, `dotnet publish -r <RID>` seçilen hedef
  native dosyasını executable ile aynı klasöre otomatik koyar.
- Native NuGet paketi veya harici native package feed'i kullanılmaz.

## Yerel binding üretimi

İlk kontrol:

```bash
dotnet run --project Tools/SdlToolchain -- doctor
```

Arch Linux üzerinde gereken paketler:

```bash
sudo pacman -S --needed cmake llvm18 clang18
```

CLI `llvm-config-18` ile `/usr/lib/llvm18` altındaki LLVM ve Clang CMake
paketlerini otomatik bulur. `c2ffi` sistemden rastgele bir executable olarak
alınmaz; manifestte sabitlenen kaynak ilk kullanımda derlenip `.artifacts`
altında cache'lenir.

Binding üretmek:

```bash
dotnet run --project Tools/SdlToolchain -- bindings
```

Gerekirse var olan, açıkça seçilmiş bir executable kullanılabilir:

```bash
dotnet run --project Tools/SdlToolchain -- bindings --c2ffi /tam/yol/c2ffi
```

Yeni SDL sürümünde `WARN_*` sayıları değişirse araç üretilen dosyayı önce
`.artifacts` altında bırakır ve Framework binding'ini değiştirmez. Fark
incelendikten sonra bilinçli kabul için:

```bash
dotnet run --project Tools/SdlToolchain -- bindings --allow-binding-warnings
```

## SDL sürümünü değiştirmek

```bash
dotnet run --project Tools/SdlToolchain -- pin 3.4.15
dotnet run --project Tools/SdlToolchain -- bindings
```

`pin`, resmi release tag'ini çözüp tam commit SHA'sını manifeste yazar. Aynı
anda önceki sürüme ait `ThirdParty/SDL3/SdlNative.props` dosyasını devre dışı
bırakır; böylece yeni binding'in yanlışlıkla eski native SDL ile paketlenmesi
önlenir. Yeni sürüm için native bundle yeniden üretilmelidir.

## Yerel native derleme

Host RID için bir native staging çıktısı gerektiğinde:

```bash
dotnet run --project Tools/SdlToolchain -- native
```

Bu komut yalnız `.artifacts/sdl-toolchain/native` altına yazar. Tek bir yerel
RID, tracked runtime ağacına otomatik kopyalanmaz; tracked ağaç aynı SDL
commit'inden üretilmiş bütün desteklenen RID'leri içermek zorundadır.

`all` yerel binding ile host native staging çıktısını birlikte üretir:

```bash
dotnet run --project Tools/SdlToolchain -- all
```

## GitHub Actions ile native bundle üretimi

Workflow dosyası `.github/workflows/sdl-toolchain.yml` konumundadır ve yalnız
elle `workflow_dispatch` ile çalışır:

1. SDL sürümü değiştiyse önce yerelde `pin` ve `bindings` komutlarını çalıştırın;
   manifest ile binding'i birlikte inceleyip commit/push edin.
2. GitHub deposunda `Actions` sekmesini açın.
3. `SDL native runtimes` workflow'unu ve üretilecek commit'i içeren branch'i
   seçin.
4. `Run workflow` seçeneğiyle üretimi başlatın.

Workflow ayrı bir sürüm girdisi kabul etmez. Her native dosya, çalıştırılan Git
commit'indeki manifestten üretilir; böylece yerelde üretilip commit edilmiş
binding ile CI native'lerinin SDL pini birbirinden ayrılamaz. `preflight` işi
binding kaynak başlığındaki SDL ve generator commitlerini manifestle doğrular;
eski binding varsa platform derlemeleri başlamadan workflow durur.

Workflow şu hedefleri ayrı gerçek işletim sistemi runner'larında üretir:

- `win-x86`, `win-x64`, `win-arm64`
- `linux-x64`, `linux-arm64`
- `osx-x64`, `osx-arm64`

Son iş bütün kütüphanelerin SDL sürümünü, ref'ini, commit'ini, Release
configuration'ını, dosya adını ve SHA-256 değerini doğrular. Bir hedef eksik ya
da farklı kaynaktan geliyorsa bundle oluşmaz.

Başarılı çalışmanın `sdl-native-bundle` artifact'i içinde
`sdl3-native-bundle.tar.gz` bulunur. GitHub web arayüzü artifact'i önce bir ZIP
olarak indirir; ZIP'i açtıktan sonra içindeki tar arşivini depo kökünde açın:

```bash
tar -xzf /indirilen/yol/sdl3-native-bundle.tar.gz
```

GitHub CLI kullananlar aynı çıktıyı doğrudan indirebilir:

```bash
gh run download RUN_ID --name sdl-native-bundle --dir .artifacts/download
tar -xzf .artifacts/download/sdl3-native-bundle.tar.gz
```

Buradaki `RUN_ID`, Actions çalışmasının sayısal kimliğidir.

Arşiv doğrudan `ThirdParty/SDL3` ağacını içerir. Oluşan binary, provenance,
manifest, lisans ve `SdlNative.props` değişiklikleri birlikte incelenip commit
edilmelidir.

Ara workflow artifact'leri bir gün, son bundle on dört gün saklanır. Workflow
depoya commit atmaz ve harici bir package feed'ine yayın yapmaz.

## Build ve publish davranışı

Tracked bundle mevcutken:

```bash
dotnet build GameDevelopment.slnx
```

host RID'i otomatik seçer. Örneğin Linux x64 çıktısında:

```text
Game/bin/Debug/net10.0/Game
Game/bin/Debug/net10.0/libSDL3.so
```

Belirli bir dağıtım hedefi:

```bash
dotnet publish Game/Game.csproj \
  --configuration Release \
  --runtime osx-x64 \
  --self-contained true
```

publish klasörüne `libSDL3.dylib` eklenir. Self-contained publish, hedef
makinenin ayrıca .NET kurmasını gerektirmez. Hazır Finder `.app` paketi,
code-signing/notarization ve Metal shader çıktıları oyun dağıtım katmanının
ayrı sorumluluğudur.

## Tracked runtime ağacı

```text
ThirdParty/SDL3/
├── LICENSE.txt
├── README.md
├── SdlNative.props
├── sdl-native-manifest.json
├── provenance/
│   └── <rid>.json
└── runtimes/
    └── <rid>/native/<platform-library>
```

Normal build yalnız seçilen RID dosyasını kopyalar; yedi platform binary'sinin
tamamı oyun çıktısına taşınmaz.

## `SDL3.GPU.cs`

`SDL3.GPU.cs`, depth-stencil hedefi olmadan render pass başlatmak için null
native pointer geçirir. Bunun gerekip gerekmediği yalnız SDL sürümüne değil,
generator'ın `SDL_BeginGPURenderPass` pointer parametresine verdiği marshalling
kararına da bağlıdır.

Her binding üretiminden sonra CLI yeni imzayı denetler ve helper'ın hâlâ
gerekli olup olmadığını raporlar; helper otomatik silinmez.
