# Performans karşılaştırması

FormWind'in aynı formu düz WinForms, Krypton Toolkit ve MaterialSkin.2 ile kurarken ne kadar zaman, bellek ve GDI/USER nesnesi harcadığını
ölçer. Araç: `tools/FormWind.Benchmarks`.

## Çalıştırma

```powershell
dotnet build tools/FormWind.Benchmarks -c Release
tools/FormWind.Benchmarks/bin/Release/net8.0-windows/FormWind.Benchmarks.exe                 # tüm senaryolar, ~15 dk
tools/FormWind.Benchmarks/bin/Release/net8.0-windows/FormWind.Benchmarks.exe --other-dpi     # ikinci monitörde açılış
tools/FormWind.Benchmarks/bin/Release/net8.0-windows/FormWind.Benchmarks.exe --quick --only formwind,krypton --sizes 200
```

Sonuçlar `artifacts/benchmarks/results.json` ve `results.md` dosyalarına yazılır (`--out` ile başka klasöre). `report <results.json>`
kayıtlı bir ölçümden yeniden rapor üretir.

## Yöntem

- Her senaryo aynı altı kontrol rolünü sırayla tekrarlar: etiket, birincil buton, ikincil buton, metin kutusu, kart, onay kutusu.
  Kontroller `FlowLayoutPanel` içinde, ekran dışında, 1200×900 çerçevesiz bir formdadır.
- Senaryolar: düz WinForms (renkler elle atanır, koyu tema kontroller gezilerek uygulanır), FormWind standart kontroller + class,
  FormWind `Tw*` kontrolleri, FormWind AutoStyle + Fluent, Krypton Toolkit 100.25 (`KryptonForm`, Microsoft 365 White ↔ Black Dark
  Mode), Krypton + FormWind.Krypton (`TwKryptonTheme.Follow`), MaterialSkin.2 2.3.1 (`MaterialForm`, LIGHT ↔ DARK).
- Her senaryo ve boyut ayrı bir süreçte ölçülür (JIT, global paletler ve süreç geneli GDI sayaçları karışmasın diye). Ilık ölçümlerden
  önce iki form açılıp kapatılır; değerler medyan ve p90'dır.
- Form, en yakın ekranı ana ekran olan bir konumda açılır. `--other-dpi` ile, gösterilirken `WM_DPICHANGED` aldığı monitörde açılır
  (dizüstü + harici monitörde ikinci ekranda açılan form); WinForms bu durumda tüm kontrolleri yeniden ölçekler.
- Hover gerçek fare mesajlarıyla (`WM_MOUSEMOVE`, `WM_MOUSELEAVE`) yapılır; her 50 geçişte bekleyen çizimler de ölçüme katılır.
  MaterialSkin hover animasyonunu zamanlayıcıyla sonradan çizdiği için hover değeri o işi içermez.
- Soğuk başlangıç: süreç başlangıcından 200 kontrollü ilk formun çizilmesine kadar (7 koşunun medyanı).

## Düzeltilen darboğazlar

Profil (`dotnet-trace`) ile bulunan ve düzeltilen sorunlar:

1. **Gölge ve çerçeveler geçersiz bölgeye bakmadan çiziliyordu** (`TwOutset.ParentPainter.OnPaint`). Bir kontrolün yeniden çizimi
   ebeveynin tüm gölge ve çerçevelerini (örn. `input` sınıflı her metin kutusunun çerçevesini) yeniden çiziyordu. Artık
   `e.ClipRectangle` ile kesişmeyenler atlanır.
2. **Padding her yenilemede iki kez değişiyordu** (`WinFormsApplier.ApplySpacing` + `FitPadding`). İstenen padding yazılıp hemen
   yazıya sığdırılmış değerle değiştiriliyordu; her değişiklik ebeveynin yerleşimini tüm kardeşleriyle çalıştırdığından hover
   maliyeti kontrol sayısıyla büyüyordu. Artık doğrudan sığdırılmış değer yazılır.
3. **Yazı her yenilemede yeniden ölçülüyordu** (`FitToText`). Ölçüm, yazı ve font değişmedikçe önbellekten gelir.
4. **Formun varsayılan fontu ekranın DPI'ına ölçeklenmiyordu** (`WinFormsApplier.ApplyFont`). Kendi fontu olmayan formda temel font
   sistem DPI'ına göreydi; WinForms onu ekranın DPI'ına ölçekledikçe FormWind geri alıyordu. DPI'ı farklı monitörde açık kalan formda
   yazı büyük kalıyordu; her yenilemede font değişimi tüm alt kontrollere yayılıp yerleşimi yeniden çalıştırıyordu.
5. **DPI değişiminde class'lar ölçeklemeden önce de uygulanıyordu** (`TwBinding.OnDpiChanged`). Formun `DpiChanged` olayı WinForms
   ölçeklemeden önce gelir: o anda atanan font bir kez daha ölçeklenip ertelenen yenilemede geri alınıyordu. Artık yalnızca ölçekleme
   bittikten sonra, bir kez uygulanır.

| FormWind | Önce | Sonra |
| --- | ---: | ---: |
| Hover geçişi, 500 kontrol (standart kontroller) | 1970 µs | 32 µs |
| Hover geçişi, 500 kontrol (AutoStyle) | 1901 µs | 36 µs |
| Tema geçişi + çizim, 200 kontrol | 61 ms | 29 ms |
| Tam yeniden çizim, 200 kontrol | 43 ms | 18 ms |
| İkinci monitörde açılış, 500 kontrol (standart kontroller) | 1493 ms | 450 ms |
| İkinci monitörde açılış, 500 kontrol (AutoStyle) | 1622 ms | 418 ms |
| İkinci monitörde ertelenen yenileme, 500 kontrol (AutoStyle) | 338 ms | 12 ms |
| Soğuk başlangıç, ikinci monitörde (ilk form, 200 kontrol) | 1000 ms | 496 ms |

"Önce" ölçümleri ikinci monitörde yapıldı (ilk düzenek formu orada açıyordu), "Sonra" sütunu da aynı koşulda. FormWind.Krypton
senaryosunda maliyetin çoğu Krypton'da olduğundan fark küçüktür.

## Özet (ana ekranda, 200 kontrol)

- **Formu açma:** FormWind 185–228 ms, düz WinForms 236 ms, Krypton 346 ms, MaterialSkin 365 ms. FormWind'in kendi işi (class çözümleme
  ve uygulama) 500 kontrolde ~30 ms; kalan süre handle oluşturma, köşe Region'ları ve ilk çizimdir. Düz WinForms'un yavaşlığı WinForms'un
  kendisinden: `BorderStyle.FixedSingle` metin kutusunun handle'ı oluşurken yüksekliği değişir ve `FlowLayoutPanel` tüm kontrolleri yeniden
  ölçer (500 kontrolde Show 653 ms). FormWind'in `input` sınıfı bu yolu kullanmadığından daha hızlı açılır; bu, FormWind'in genel olarak
  düz WinForms'tan hızlı olduğu anlamına gelmez.
- **Tema geçişi:** FormWind 16–29 ms, MaterialSkin 61 ms, Krypton 556 ms. Krypton'un palet değişimi her kontrolü yeniden düzenler ve
  geçiş başına ~9 MB bellek ayırır.
- **Çizim:** `Tw*` kontrolleri ve AutoStyle düz WinForms düzeyinde (~7–8 ms). Standart kontrollerde class'lar (Region, kenarlık çizimi)
  18 ms; Krypton 40 ms, MaterialSkin 55 ms.
- **Hover:** FormWind 25–41 µs, Krypton 104 µs. Düz WinForms ve MaterialSkin ~10 µs (MaterialSkin işin çoğunu animasyon zamanlayıcısında
  yapar).
- **Bellek:** Açık form kontrol başına FormWind'de ~1,1 KB, MaterialSkin'de ~6,5 KB, Krypton'da ~11 KB tutar. FormWind'in GDI/USER nesne
  sayısı düz WinForms ile aynıdır (`Tw*` kontrollerinde iç pencere nedeniyle kontrol başına bir USER nesnesi fazla).
- **Soğuk başlangıç:** FormWind 423–498 ms, MaterialSkin 524 ms, Krypton 693 ms, düz WinForms 323 ms. FormWind 5 derleme ve ~6 MB
  özel bellek ekler.
- **Sızıntı:** 500 aç/kapatta FormWind'in GDI/USER sayısı sabittir. Bu düzenekte Krypton'un yönetilen belleği ~218 MB büyüdü (GDI
  yalnızca ikinci monitörde arttı: 54 → 566); MaterialSkin'in GDI sayısı 57 → 3057 ve belleği ~94 MB büyüdü (formlar
  `RemoveFormToManage` ile bırakıldığı halde); Krypton + FormWind.Krypton'da GDI 66 → 2579. Son ikisi ayrıntılı incelenmedi: bu
  düzeneğe (örn. her formda paletin yeniden kurulmasına) özgü olabilir.

### Uyarılar

- Tek makine, tek koşu; değerler ±%10–15 oynar. Göreli sıralama koşular arasında değişmedi.
- Senaryolar görünüm olarak birebir aynı değildir: her kütüphane kendi bileşenlerini kullanır (örn. `KryptonForm`, `MaterialForm`).

## Sonuçlar: ana ekranda açılış

Ölçüm: 2026-10-14 20:09 · 12th Gen Intel(R) Core(TM) i5-12450HX (12 çekirdek) · Microsoft Windows 10.0.26200 · .NET 8.0.11 · 120 DPI

#### Formu kurma + stillendirme + ilk çizim (ms, medyan)

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 74.1 <sub>p90 81.5</sub> | 235.5 <sub>p90 292.2</sub> | 682.0 <sub>p90 705.3</sub> |
| FormWind (standart kontroller + class) | 86.9 <sub>p90 94.6</sub> | 228.0 <sub>p90 256.7</sub> | 439.3 <sub>p90 523.8</sub> |
| FormWind (Tw* kontrolleri) | 62.6 <sub>p90 72.2</sub> | 185.0 <sub>p90 206.0</sub> | 331.9 <sub>p90 434.3</sub> |
| FormWind AutoStyle + Fluent | 72.5 <sub>p90 75.7</sub> | 205.1 <sub>p90 219.3</sub> | 361.2 <sub>p90 438.9</sub> |
| Krypton Toolkit 100.25 | 162.6 <sub>p90 184.1</sub> | 346.4 <sub>p90 407.5</sub> | 507.7 <sub>p90 696.0</sub> |
| Krypton + FormWind.Krypton (Fluent) | 198.8 <sub>p90 214.4</sub> | 449.3 <sub>p90 519.5</sub> | 719.2 <sub>p90 831.0</sub> |
| MaterialSkin.2 2.3.1 | 137.8 <sub>p90 155.4</sub> | 364.5 <sub>p90 383.3</sub> | 631.1 <sub>p90 1280.2</sub> |

Isınmış süreçte; `new Form` → `Show` → `Refresh`.

#### Kurma aşamaları (ms, medyan): kurma · Show · ilk çizim · ertelenen mesajlar

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 3 · 63 · 6 · 1 | 10 · 216 · 11 · 1 | 17 · 653 · 11 · 1 |
| FormWind (standart kontroller + class) | 8 · 62 · 15 · 1 | 28 · 169 · 30 · 1 | 35 · 370 · 31 · 2 |
| FormWind (Tw* kontrolleri) | 7 · 47 · 8 · 1 | 24 · 149 · 12 · 1 | 37 · 282 · 11 · 2 |
| FormWind AutoStyle + Fluent | 7 · 57 · 6 · 1 | 24 · 166 · 12 · 1 | 32 · 318 · 10 · 1 |
| Krypton Toolkit 100.25 | 28 · 102 · 28 · 3 | 87 · 197 · 62 · 4 | 153 · 294 · 56 · 8 |
| Krypton + FormWind.Krypton (Fluent) | 53 · 114 · 31 · 2 | 154 · 220 · 64 · 4 | 331 · 314 · 59 · 7 |
| MaterialSkin.2 2.3.1 | 41 · 35 · 62 · 2 | 151 · 99 · 98 · 2 | 315 · 215 · 87 · 2 |

#### Tam yeniden çizim (ms, medyan)

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 3.0 <sub>p90 4.2</sub> | 7.4 <sub>p90 8.4</sub> | 7.2 <sub>p90 8.0</sub> |
| FormWind (standart kontroller + class) | 8.7 <sub>p90 10.4</sub> | 18.2 <sub>p90 20.0</sub> | 16.5 <sub>p90 19.4</sub> |
| FormWind (Tw* kontrolleri) | 3.2 <sub>p90 3.8</sub> | 8.0 <sub>p90 8.7</sub> | 7.6 <sub>p90 8.4</sub> |
| FormWind AutoStyle + Fluent | 2.7 <sub>p90 3.2</sub> | 7.0 <sub>p90 7.8</sub> | 7.3 <sub>p90 8.3</sub> |
| Krypton Toolkit 100.25 | 20.7 <sub>p90 23.9</sub> | 40.0 <sub>p90 44.0</sub> | 39.9 <sub>p90 42.7</sub> |
| Krypton + FormWind.Krypton (Fluent) | 24.1 <sub>p90 27.2</sub> | 47.9 <sub>p90 57.8</sub> | 52.2 <sub>p90 58.8</sub> |
| MaterialSkin.2 2.3.1 | 37.7 <sub>p90 39.8</sub> | 54.9 <sub>p90 59.9</sub> | 76.1 <sub>p90 101.8</sub> |

`Form.Refresh()`: tüm kontroller yeniden çizilir.

#### Açık ↔ koyu tema geçişi + yeniden çizim (ms, medyan)

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 3.4 <sub>p90 4.1</sub> | 7.4 <sub>p90 8.5</sub> | 7.6 <sub>p90 8.9</sub> |
| FormWind (standart kontroller + class) | 13.5 <sub>p90 14.6</sub> | 29.4 <sub>p90 33.1</sub> | 42.1 <sub>p90 46.7</sub> |
| FormWind (Tw* kontrolleri) | 7.5 <sub>p90 8.1</sub> | 16.1 <sub>p90 17.2</sub> | 22.3 <sub>p90 24.7</sub> |
| FormWind AutoStyle + Fluent | 6.9 <sub>p90 8.6</sub> | 15.7 <sub>p90 16.9</sub> | 26.7 <sub>p90 28.7</sub> |
| Krypton Toolkit 100.25 | 75.9 <sub>p90 88.9</sub> | 556.0 <sub>p90 594.7</sub> | 1248.8 <sub>p90 1279.9</sub> |
| Krypton + FormWind.Krypton (Fluent) | 93.5 <sub>p90 103.9</sub> | 588.1 <sub>p90 632.7</sub> | 1587.9 <sub>p90 1682.2</sub> |
| MaterialSkin.2 2.3.1 | 34.4 <sub>p90 37.6</sub> | 61.2 <sub>p90 62.6</sub> | 72.8 <sub>p90 90.3</sub> |

#### Hover geçişi (µs / giriş veya çıkış)

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 17.1 | 12.6 | 9.8 |
| FormWind (standart kontroller + class) | 46.5 | 30.3 | 31.7 |
| FormWind (Tw* kontrolleri) | 36.2 | 24.7 | 20.3 |
| FormWind AutoStyle + Fluent | 57.0 | 41.4 | 35.8 |
| Krypton Toolkit 100.25 | 137.5 | 103.7 | 46.1 |
| Krypton + FormWind.Krypton (Fluent) | 196.8 | 150.4 | 83.6 |
| MaterialSkin.2 2.3.1 | 9.9 | 8.4 | 12.7 |

`WM_MOUSEMOVE` + `WM_MOUSELEAVE` mesajları; bekleyen çizimler dahil.

#### Form başına ayrılan bellek (KB)

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 152 | 963 | 3661 |
| FormWind (standart kontroller + class) | 620 | 2407 | 5944 |
| FormWind (Tw* kontrolleri) | 616 | 2407 | 5973 |
| FormWind AutoStyle + Fluent | 490 | 1870 | 4609 |
| Krypton Toolkit 100.25 | 1210 | 3840 | 8515 |
| Krypton + FormWind.Krypton (Fluent) | 2205 | 7614 | 22474 |
| MaterialSkin.2 2.3.1 | 556 | 2217 | 5533 |

Kurma sırasında UI iş parçacığında ayrılan toplam yönetilen bellek.

#### Form açıkken tutulan yönetilen bellek (KB)

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 13 | 42 | 100 |
| FormWind (standart kontroller + class) | 60 | 229 | 567 |
| FormWind (Tw* kontrolleri) | 61 | 232 | 574 |
| FormWind AutoStyle + Fluent | 41 | 155 | 382 |
| Krypton Toolkit 100.25 | 704 | 2229 | 5267 |
| Krypton + FormWind.Krypton (Fluent) | 1078 | 2655 | 5820 |
| MaterialSkin.2 2.3.1 | 324 | 1318 | 3256 |

#### Form başına GDI / USER nesnesi

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 14 / 67 | 39 / 217 | 89 / 517 |
| FormWind (standart kontroller + class) | 14 / 67 | 39 / 217 | 89 / 517 |
| FormWind (Tw* kontrolleri) | 14 / 75 | 39 / 250 | 89 / 600 |
| FormWind AutoStyle + Fluent | 14 / 67 | 39 / 217 | 89 / 517 |
| Krypton Toolkit 100.25 | 17 / 68 | 53 / 244 | 103 / 594 |
| Krypton + FormWind.Krypton (Fluent) | 27 / 81 | 88 / 256 | 188 / 604 |
| MaterialSkin.2 2.3.1 | 23 / 78 | 73 / 252 | 173 / 603 |

#### Tema geçişi başına ayrılan bellek (KB)

| Senaryo | 50 kontrol | 200 kontrol | 500 kontrol |
| --- | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 5 | 15 | 34 |
| FormWind (standart kontroller + class) | 138 | 526 | 1293 |
| FormWind (Tw* kontrolleri) | 141 | 541 | 1344 |
| FormWind AutoStyle + Fluent | 96 | 365 | 896 |
| Krypton Toolkit 100.25 | 994 | 8732 | 23925 |
| Krypton + FormWind.Krypton (Fluent) | 1552 | 9556 | 25164 |
| MaterialSkin.2 2.3.1 | 11 | 39 | 94 |

#### Soğuk başlangıç (200 kontrol, medyan)

| Senaryo | Main'e kadar (ms) | İlk form (ms) | Toplam (ms) | Çalışma kümesi (MB) | Özel bellek (MB) | GDI | USER | Derleme |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 274 | 323 | 601 | 54.0 | 14.1 | 57 | 230 | 31 |
| FormWind (standart kontroller + class) | 262 | 498 | 760 | 64.5 | 20.2 | 61 | 230 | 36 |
| FormWind (Tw* kontrolleri) | 259 | 423 | 683 | 63.8 | 19.9 | 60 | 263 | 36 |
| FormWind AutoStyle + Fluent | 268 | 448 | 737 | 63.9 | 19.7 | 60 | 230 | 36 |
| Krypton Toolkit 100.25 | 313 | 693 | 1054 | 83.2 | 29.0 | 104 | 269 | 39 |
| Krypton + FormWind.Krypton (Fluent) | 303 | 1009 | 1334 | 92.0 | 34.4 | 139 | 270 | 42 |
| MaterialSkin.2 2.3.1 | 276 | 524 | 812 | 72.7 | 26.2 | 112 | 267 | 35 |

#### Aç / kapat (30 kontrollü form)

| Senaryo | Tekrar | ms / form | GDI önce → sonra | USER önce → sonra | Yönetilen bellek artışı (KB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 500 | 77.0 | 18 → 18 | 14 → 14 | 497 |
| FormWind (standart kontroller + class) | 500 | 86.6 | 22 → 22 | 14 → 14 | 638 |
| FormWind (Tw* kontrolleri) | 500 | 77.6 | 21 → 21 | 14 → 14 | 625 |
| FormWind AutoStyle + Fluent | 500 | 80.1 | 20 → 21 | 14 → 14 | 770 |
| Krypton Toolkit 100.25 | 500 | 164.8 | 46 → 46 | 39 → 37 | 223189 |
| Krypton + FormWind.Krypton (Fluent) | 500 | 199.7 | 66 → 2579 | 27 → 27 | 403607 |
| MaterialSkin.2 2.3.1 | 500 | 128.5 | 57 → 3057 | 18 → 16 | 95813 |


## Sonuçlar: ikinci monitörde (DPI değişimli) açılış

Ölçüm: 2026-10-14 20:20 · 12th Gen Intel(R) Core(TM) i5-12450HX (12 çekirdek) · Microsoft Windows 10.0.26200 · .NET 8.0.11 · 120 DPI · DPI değişimli monitörde açılış

#### Formu kurma + stillendirme + ilk çizim (ms, medyan)

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 240.5 <sub>p90 283.6</sub> | 688.4 <sub>p90 710.6</sub> |
| FormWind (standart kontroller + class) | 242.5 <sub>p90 271.2</sub> | 450.2 <sub>p90 560.1</sub> |
| FormWind (Tw* kontrolleri) | 203.7 <sub>p90 267.1</sub> | 357.1 <sub>p90 485.3</sub> |
| FormWind AutoStyle + Fluent | 219.7 <sub>p90 256.8</sub> | 417.7 <sub>p90 551.8</sub> |
| Krypton Toolkit 100.25 | 385.0 <sub>p90 494.3</sub> | 753.7 <sub>p90 872.5</sub> |
| Krypton + FormWind.Krypton (Fluent) | 483.7 <sub>p90 585.2</sub> | 845.4 <sub>p90 1118.4</sub> |
| MaterialSkin.2 2.3.1 | 344.3 <sub>p90 384.9</sub> | 638.4 <sub>p90 775.8</sub> |

Isınmış süreçte; `new Form` → `Show` → `Refresh`.

#### Kurma aşamaları (ms, medyan): kurma · Show · ilk çizim · ertelenen mesajlar

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 7 · 217 · 11 · 1 | 16 · 658 · 11 · 2 |
| FormWind (standart kontroller + class) | 24 · 173 · 34 · 8 | 34 · 365 · 36 · 11 |
| FormWind (Tw* kontrolleri) | 23 · 143 · 13 · 20 | 30 · 290 · 14 · 24 |
| FormWind AutoStyle + Fluent | 23 · 178 · 13 · 8 | 31 · 360 · 12 · 12 |
| Krypton Toolkit 100.25 | 89 · 254 · 55 · 4 | 203 · 489 · 57 · 7 |
| Krypton + FormWind.Krypton (Fluent) | 145 · 266 · 57 · 18 | 288 · 464 · 61 · 29 |
| MaterialSkin.2 2.3.1 | 150 · 106 · 86 · 2 | 343 · 215 · 82 · 3 |

#### Tam yeniden çizim (ms, medyan)

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 7.6 <sub>p90 8.4</sub> | 7.6 <sub>p90 8.5</sub> |
| FormWind (standart kontroller + class) | 17.0 <sub>p90 22.3</sub> | 18.7 <sub>p90 22.4</sub> |
| FormWind (Tw* kontrolleri) | 8.3 <sub>p90 9.3</sub> | 8.5 <sub>p90 9.4</sub> |
| FormWind AutoStyle + Fluent | 7.2 <sub>p90 7.7</sub> | 7.3 <sub>p90 7.7</sub> |
| Krypton Toolkit 100.25 | 44.0 <sub>p90 45.5</sub> | 46.4 <sub>p90 49.7</sub> |
| Krypton + FormWind.Krypton (Fluent) | 45.3 <sub>p90 48.1</sub> | 45.4 <sub>p90 48.7</sub> |
| MaterialSkin.2 2.3.1 | 61.6 <sub>p90 64.5</sub> | 59.5 <sub>p90 74.5</sub> |

`Form.Refresh()`: tüm kontroller yeniden çizilir.

#### Açık ↔ koyu tema geçişi + yeniden çizim (ms, medyan)

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 7.6 <sub>p90 8.5</sub> | 8.1 <sub>p90 10.1</sub> |
| FormWind (standart kontroller + class) | 28.3 <sub>p90 30.8</sub> | 41.9 <sub>p90 43.7</sub> |
| FormWind (Tw* kontrolleri) | 15.4 <sub>p90 16.5</sub> | 23.0 <sub>p90 24.9</sub> |
| FormWind AutoStyle + Fluent | 15.8 <sub>p90 17.0</sub> | 28.1 <sub>p90 31.9</sub> |
| Krypton Toolkit 100.25 | 596.0 <sub>p90 636.0</sub> | 1374.7 <sub>p90 1398.4</sub> |
| Krypton + FormWind.Krypton (Fluent) | 563.8 <sub>p90 579.0</sub> | 1793.3 <sub>p90 1978.0</sub> |
| MaterialSkin.2 2.3.1 | 62.7 <sub>p90 66.2</sub> | 173.7 <sub>p90 287.8</sub> |

#### Hover geçişi (µs / giriş veya çıkış)

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 13.2 | 10.3 |
| FormWind (standart kontroller + class) | 34.0 | 30.0 |
| FormWind (Tw* kontrolleri) | 25.9 | 21.6 |
| FormWind AutoStyle + Fluent | 41.7 | 41.6 |
| Krypton Toolkit 100.25 | 109.3 | 45.8 |
| Krypton + FormWind.Krypton (Fluent) | 137.4 | 87.7 |
| MaterialSkin.2 2.3.1 | 8.7 | 14.4 |

`WM_MOUSEMOVE` + `WM_MOUSELEAVE` mesajları; bekleyen çizimler dahil.

#### Form başına ayrılan bellek (KB)

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 1142 | 4106 |
| FormWind (standart kontroller + class) | 3031 | 7480 |
| FormWind (Tw* kontrolleri) | 3023 | 7503 |
| FormWind AutoStyle + Fluent | 2413 | 5953 |
| Krypton Toolkit 100.25 | 4836 | 11060 |
| Krypton + FormWind.Krypton (Fluent) | 8839 | 25458 |
| MaterialSkin.2 2.3.1 | 2218 | 5533 |

Kurma sırasında UI iş parçacığında ayrılan toplam yönetilen bellek.

#### Form açıkken tutulan yönetilen bellek (KB)

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 67 | 162 |
| FormWind (standart kontroller + class) | 246 | 609 |
| FormWind (Tw* kontrolleri) | 249 | 616 |
| FormWind AutoStyle + Fluent | 165 | 407 |
| Krypton Toolkit 100.25 | 2261 | 5346 |
| Krypton + FormWind.Krypton (Fluent) | 2689 | 5885 |
| MaterialSkin.2 2.3.1 | 1318 | 3257 |

#### Form başına GDI / USER nesnesi

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 41 / 217 | 91 / 517 |
| FormWind (standart kontroller + class) | 39 / 217 | 89 / 517 |
| FormWind (Tw* kontrolleri) | 39 / 250 | 89 / 600 |
| FormWind AutoStyle + Fluent | 41 / 217 | 91 / 517 |
| Krypton Toolkit 100.25 | 56 / 244 | 106 / 594 |
| Krypton + FormWind.Krypton (Fluent) | 88 / 256 | 188 / 606 |
| MaterialSkin.2 2.3.1 | 73 / 253 | 173 / 603 |

#### Tema geçişi başına ayrılan bellek (KB)

| Senaryo | 200 kontrol | 500 kontrol |
| --- | ---: | ---: |
| Düz WinForms (elle renk) | 15 | 34 |
| FormWind (standart kontroller + class) | 526 | 1294 |
| FormWind (Tw* kontrolleri) | 541 | 1344 |
| FormWind AutoStyle + Fluent | 364 | 897 |
| Krypton Toolkit 100.25 | 8732 | 23925 |
| Krypton + FormWind.Krypton (Fluent) | 9556 | 25164 |
| MaterialSkin.2 2.3.1 | 39 | 94 |

#### Soğuk başlangıç (200 kontrol, medyan)

| Senaryo | Main'e kadar (ms) | İlk form (ms) | Toplam (ms) | Çalışma kümesi (MB) | Özel bellek (MB) | GDI | USER | Derleme |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 385 | 325 | 714 | 56.2 | 14.9 | 66 | 230 | 31 |
| FormWind (standart kontroller + class) | 386 | 496 | 893 | 67.0 | 21.2 | 68 | 230 | 36 |
| FormWind (Tw* kontrolleri) | 401 | 457 | 861 | 66.4 | 20.9 | 67 | 263 | 36 |
| FormWind AutoStyle + Fluent | 411 | 479 | 887 | 66.4 | 20.9 | 70 | 230 | 36 |
| Krypton Toolkit 100.25 | 397 | 771 | 1157 | 86.0 | 30.4 | 108 | 270 | 39 |
| Krypton + FormWind.Krypton (Fluent) | 391 | 1104 | 1465 | 96.2 | 37.3 | 143 | 270 | 42 |
| MaterialSkin.2 2.3.1 | 433 | 526 | 961 | 73.7 | 26.6 | 118 | 267 | 35 |

#### Aç / kapat (30 kontrollü form)

| Senaryo | Tekrar | ms / form | GDI önce → sonra | USER önce → sonra | Yönetilen bellek artışı (KB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Düz WinForms (elle renk) | 500 | 80.6 | 25 → 40 | 14 → 14 | 487 |
| FormWind (standart kontroller + class) | 500 | 91.7 | 23 → 24 | 14 → 14 | 628 |
| FormWind (Tw* kontrolleri) | 500 | 81.5 | 22 → 23 | 14 → 14 | 625 |
| FormWind AutoStyle + Fluent | 500 | 87.0 | 29 → 42 | 14 → 14 | 601 |
| Krypton Toolkit 100.25 | 500 | 174.2 | 54 → 566 | 39 → 38 | 223735 |
| Krypton + FormWind.Krypton (Fluent) | 500 | 226.3 | 72 → 3082 | 27 → 27 | 404021 |
| MaterialSkin.2 2.3.1 | 500 | 115.2 | 57 → 3058 | 18 → 16 | 95836 |

