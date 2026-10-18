# FormWind

**Türkçe** | [English](README.en.md)

**Mevcut WinForms uygulamanızı yeniden yazmadan modernleştirin:** standart kontrollerle ve Krypton Toolkit ile çalışan, Tailwind tarzı bir stil katmanı. Kontroller değişmez; bir hazır görünüm (Fluent, Ant Design, Material) seçip `AutoStyle`'ı açmak formu class yazmadan giydirir, gerektiğinde kontrol başına class eklenir.

Forma bir `TailwindProvider` bırakın; her kontrolün property penceresinde `Classes` alanı belirir:

```
btnSave  →  Classes: "bg-blue-600 text-white px-4 py-2 rounded-md font-bold hover:bg-blue-700"
```

- Hedefler: .NET Framework 4.7.2+ ve .NET 8+ (`net472`, `net8.0-windows`); testler .NET 9 ve 10'da da çalışır
- Çekirdek paketin .NET dışında bağımlılığı yok (`net472` için yalnızca `System.Text.Json`)
- Durum varyantları: `hover:`, `focus:`, `active:`, `disabled:`, `checked:`, `dark:` ve birleşimleri (`dark:hover:`)
- Flex ve grid yerleşimi (`flex`, `gap-4`, `justify-between`, `flex-1`, `w-1/2`, `grid-cols-3`, `col-span-2`)
- Bileşen class'ları (`btn-primary`, `card`) ve JSON tema dosyası
- Hazır görünümler (Tailwind, Fluent, Ant Design, Material) ve class yazmadan stillendiren `AutoStyle`
- Yuvarlak köşeleri antialias çizen `TwButton`, `TwPanel`, `TwTextBox`
- Koyu temada native kaydırma çubukları ve ipuçları da koyulaşır
- WCAG kontrast denetimi (`TwContrast`) ve Windows Yüksek Karşıtlık desteği
- Krypton Toolkit entegrasyonu (`FormWind.Krypton`): Krypton kontrollerinde class'lar ve temayı izleyen Krypton paleti
- Gerçek uygulamalarda kaynak koduna dokunmadan deneme aracı (`tools/run-harness.ps1`)

| Açık tema | Koyu tema |
| --- | --- |
| ![Açık](docs/showcase-light.png) | ![Koyu](docs/showcase-dark.png) |

## FormWind ne zaman uygun?

| Durum | Öneri |
| --- | --- |
| Mevcut bir WinForms uygulamasını yeniden yazmadan modernleştirmek | FormWind: standart kontroller olduğu gibi kalır; `AutoStyle` ve bir hazır görünümle class yazmadan başlanır, gerektiğinde kontrol başına class eklenir. |
| Krypton Toolkit kullanan bir uygulama | FormWind + `FormWind.Krypton`: Krypton'un ribbon, docking ve gezgin bileşenleri yerinde kalır, palet temayı izler. |
| Koyu tema, tutarlı renkler, erişilebilirlik (kontrast, High Contrast, UI Automation) | FormWind. |
| Ribbon, docking, scheduler, rapor, gelişmiş grid gibi ağır bileşenler | Bunları Krypton'dan ya da kullandığınız kontrol paketinden alın; FormWind onların yanında standart kontrolleri stiller. |
| Tamamen yeni, çapraz platform bir uygulama | WinForms yerine Avalonia, WPF ya da web tabanlı bir arayüz daha uygun olabilir. |

## Hızlı başlangıç

1. Paketi ekleyin (bkz. [Paketleme](#paketleme)) ya da `src/FormWind` projesine proje referansı verin.
2. `Program.Main` içinde, formlar açılmadan önce temayı yükleyin (dosya yoksa varsayılan tema kullanılır):

   ```csharp
   TwTheme.Current = TwTheme.LoadIfExists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tw.config.json"));
   ```

3. Toolbox'tan `TailwindProvider`'ı forma sürükleyin. Görünmüyorsa Toolbox → sağ tık → **Choose Items** → `FormWind.dll`.
4. Kontrolleri seçip `Classes on tailwindProvider1` alanını doldurun. Designer şunu üretir:

   ```csharp
   this.tailwindProvider1.SetClasses(this.btnSave, "btn-primary");
   ```

5. Koyu tema: `tailwindProvider1.DarkMode = true;`

### Proje şablonu

`FormWind.Templates` paketi hazır ekranlarla bir uygulama oluşturur: giriş (doğrulamalı alanlar), liste–detay (aranabilir `TwDataList` ve ayrıntı kartı) ve ayarlar; açık/koyu tema anahtarı formlar arasında paylaşılır. Şablon metinleri İngilizcedir.

```bash
dotnet new install FormWind.Templates
```

```bash
dotnet new formwind-app -n MyApp --Framework net8.0-windows --Preset fluent --Krypton true
```

`--Framework`: `net8.0-windows` (varsayılan), `net9.0-windows`, `net10.0-windows`, `net472`; `--Preset`: `tailwind` (varsayılan), `fluent`, `antd`, `material` (bkz. [Hazır görünümler](#hazır-görünümler-ve-autostyle)); `--Palette`: `v3` (varsayılan), `v4`; `--Krypton true`: `FormWind.Krypton` paketini ekler, `KryptonApplier`'ı kaydeder ve Krypton paletini ana formun temasına bağlar (bkz. [Krypton Toolkit](#krypton-toolkit)); `--FormWindVersion`: paket sürümü (varsayılan: şablon paketiyle aynı sürüm). Önizleme sürümü için kurulumda sürümü açıkça verin: `dotnet new install FormWind.Templates::0.1.0-preview.1`. Formlar Designer'sız, kodla kurulur ve `AutoScaleMode.Dpi` kullanır. Paket yayımlanana kadar `artifacts/packages` klasörü paket kaynağı olarak eklenmelidir; CI şablonu her çalışmada üç biçimde (varsayılan; `net472` ve `v4`; `--Krypton true` ve `material`) üretip derler.

## Hazır görünümler ve AutoStyle

Class yazmadan başlamak için bir hazır görünüm seçip `AutoStyle`'ı açın:

```jsonc
// tw.config.json
{ "preset": "fluent" }   // tailwind (varsayılan), fluent, antd, material
```

```csharp
tailwindProvider1.AutoStyle = true;   // Designer'da: Properties → FormWind → AutoStyle
```

| Tailwind | Fluent | Ant Design | Material |
| --- | --- | --- | --- |
| ![Tailwind](docs/preset-tailwind.png) | ![Fluent](docs/preset-fluent.png) | ![Ant Design](docs/preset-antd.png) | ![Material](docs/preset-material.png) |

**Hazır görünümler** (`TwPreset`, kodda `new TwTheme(TwPreset.Fluent)`) renkleri, fontları ve bileşenlerin şeklini (köşe, boşluk, kalınlık) belirler. Görünümler adı geçen tasarım sistemlerinden **esinlenir**; o sistemlerin kendisi değildir ve logo ya da varlık içermez. Orijinali WCAG AA kontrastını sağlamayan yerlerde renk koyulaştırılır: Ant Design'ın `#1677ff` mavisi beyaz yazıyla 4,1:1 olduğundan dolu butonlar `primary-600` (`#0958d9`) kullanır. Fluent, Windows 11'de Segoe UI Variable ile, Material ise kuruluysa Roboto ile çizilir; ikisi de yoksa Segoe UI kullanılır. Her görünüm açık ve koyu temada kontrast denetiminden ve görsel regresyon testlerinden geçer.

Bütün görünümler **aynı bileşen adlarını** tanımlar; görünüm değiştirmek class'lara ya da Designer.cs'e dokunmadan uygulamanın tamamını yeniden giydirir:

| Grup | Bileşenler |
| --- | --- |
| Butonlar | `btn`, `btn-primary`, `btn-secondary` (dolgulu nötr), `btn-ghost` (kenarlıklı varsayılan), `btn-danger`, `link` |
| Form | `input`, `select` (input ve açılır liste), `field`, `toggle`, `range` (slider, ilerleme) |
| Yüzey ve yazı | `page` (form zemini, yazı rengi ve font), `card`, `sidebar`, `nav-item`, `title`, `muted`, `badge`, `alert-info`, `alert-success`, `alert-warning`, `alert-danger` |
| Veri ve gezinme | `table`, `list`, `tabs`, `tab-control`, `tab-page`, `menu`, `properties` |

**Anlamsal renkler.** Bileşenler `primary`, `surface` (nötr zemin ve yazı), `danger`, `success` ve `warning` renklerini kullanır (`bg-primary-600`, `text-surface-500` …). Varsayılan temada bunlar sırasıyla `blue`, `gray`, `red`, `green` ve `amber`'ın kopyasıdır ve palet sürümünü izler. Tema dosyasında bir rampa tanımlamak (`"colors": { "primary": { "50": "…", …, "950": "…" } }`) bütün bileşenleri birlikte yeniden renklendirir. Kendi class'larınızda da anlamsal adları kullanırsanız onlar da görünümle değişir.

**Görünümü özelleştirme.** Tema dosyası görünümün üzerine uygulanır; yalnızca farklı olanı yazın (örneğin `"preset": "antd"` ile birlikte kendi `primary` rampanız ya da `btn` bileşeniniz). Bir görünümü baştan sona değiştirmek için [`src/FormWind/Presets/`](src/FormWind/Presets) klasöründeki dosyasını `tw.config.json` olarak kopyalayın; dosyalar aynı biçimdedir.

**AutoStyle.** `TailwindProvider.AutoStyle` açıkken `ContainerControl` formundaki kontroller tipine eşlenen bileşeni alır; sonradan eklenen kontroller de. Designer'da `ContainerControl` formun kendisine ayarlanır (ErrorProvider gibi); kodla kurulan formlarda `provider.ContainerControl = this;` yazın.

| Kontrol | Bileşen |
| --- | --- |
| `Form` | `page` |
| `Button`, `TwButton` | `btn-ghost` |
| `TextBox`, `RichTextBox`, `MaskedTextBox`, `TwTextBox`, `TwNumberBox` | `input` |
| `ComboBox`, `TwSelect`, `TwDatePicker` | `select` |
| `CheckBox`, `RadioButton`, `TwCheckBox`, `TwRadioButton`, `TwSwitch` | `toggle` |
| `TwSlider`, `TwProgress`, `ProgressBar` | `range` |
| `TwField` | `field` |
| `DataGridView` | `table` |
| `ListBox`, `ListView`, `TreeView`, `TwDataList`, `TwListBox`, `TwTreeView` | `list` |
| `TwTabStrip` (ve `TwTabControl`'ün şeridi) | `tabs` |
| `MenuStrip`, `ToolStrip`, `StatusStrip` | `menu` |
| `LinkLabel` | `link-label` |
| `TabControl`, `TabPage` | `tab-control`, `tab-page` |
| `PropertyGrid` | `properties` |

- Etiketler ve paneller listede yoktur: formun yazı ve zemin rengini alırlar.
- **Kopyalanan zeminler.** Mevcut uygulamaların tema motorları formun zeminini çoğu zaman her kontrole açıkça yazar. FormWind formu yeniden boyayınca bu kontroller eski zeminde kalır ve yalnızca yazı rengi alan etiketler okunmaz olur (koyu zemin üzerinde koyu yazı). `AutoStyle`, bir konteyneri ilk gezişinde zemini ebeveyninkiyle aynı olan kontrolleri ebeveyni izlemeye bırakır (`FollowParentBackColor`, varsayılan açık; Designer'da yapılmaz). Sıfırlanınca ebeveynden farklı bir varsayılana dönen kontroller (örn. TextBox) rengini korur; sonradan eklenen kontroller ebeveynin özgün zeminiyle karşılaştırılır. Class'ları kendiniz atıyorsanız aynı işi stillemeden önce `TailwindProvider.InheritCopiedBackColors(form)` yapar.
- Kontrolün kendi class'ları otomatik bileşenin **üzerine** eklenir: `flex-1`, `mt-2` ya da `bg-red-50` görünümü korur. Kendi class'larında bir bileşen adı varsa (`btn-primary`, `card` …) otomatik bileşen kullanılmaz.
- Otomatik class'lar Designer.cs'e yazılmaz; akıllı etikette `AutoStyle: input` olarak görünür ve class oluşturucu onların üzerine yazar.
- Eşleme tema dosyasında değiştirilir: `"autoStyle": { "Button": "btn-primary", "Label": "muted", "KryptonButton": "btn-ghost", "ListView": "" }`. Anahtar tipin kısa adıdır, temel tipler de aranır (`TextBoxBase` tüm metin kutularını kapsar); boş değer o tipi stillemez. Kodda `theme.SetAutoStyle("Button", "btn-primary")`.
- `LinkLabel` `link-label` bileşenini alır: bağlantı dışındaki yazı `text-*`, bağlantılar (`LinkColor`, `ActiveLinkColor`, `VisitedLinkColor`) `accent-*`; devre dışı bağlantı yazı rengiyle zeminin ortasıdır. Kontrast denetimi bağlantı rengini `link` bölümü olarak ölçer.
- İç kontrolleri olan bileşik kontrollerin (`TwTextBox`'ın metin kutusu, `NumericUpDown`, `PropertyGrid`) içine girilmez. `PropertyGrid` kendi renk özellikleriyle stillenir: `bg-*`/`text-*` görünüm, yardım ve komut alanları, `divide-*` aralarındaki çizgiler, `header:` kategori satırları, `selected:` seçili özellik; koyu temada iç kaydırma çubuğu da koyulaşır.
- Standart `TabControl` renkleri yok saydığından renk veren class'larda sekme şeridini FormWind, Windows'un çiziminin üzerine yeniden çizer (`DrawMode` değişmez; değişimi sayfalardaki bütün kontrollerin handle'larını yeniden oluştururdu): `bg-*`/`text-*` sekmeler, `selected:` seçili sekme, `selected:border-*` seçili sekmenin dış kenarında 2 px çizgi; şeridin boş kısmı ebeveynin, sayfa çerçevesi seçili sayfanın zeminini alır. Uygulamanın kendisi çizdiği (`OwnerDrawFixed`) sekmelere dokunulmaz.
- Standart `ProgressBar` temalı çizildiğinde renk almaz; `accent-*` (çubuk) ya da `fill-*`/`bg-*` (iz) verilince görsel stili kapatılır ve renkler native kontrole gönderilir (düz, klasik çubuk; `Blocks` stili bu sürede `Continuous` olur). Class'lar kaldırılınca tema ve stil geri gelir.

Vitrinde görünümler arasında geçiş yapan bir form vardır: `FormWind.Showcase.exe --presets` (ilk görünüm için `--preset fluent`).

## Class sözlüğü

| Kategori | Kalıp | Örnek | WinForms karşılığı |
| --- | --- | --- | --- |
| Arka plan | `bg-{renk}-{ton}`, `bg-[#hex]` | `bg-blue-600`, `bg-white` | `BackColor` |
| Yazı rengi | `text-{renk}-{ton}` | `text-gray-900` | `ForeColor` |
| Yazı boyutu | `text-xs … text-4xl` | `text-sm` = 14 px | `Font` (px × 0,75 = pt) |
| Yazı stili | `font-thin` … `font-black`, `font-sans`, `font-mono`, `italic`, `underline` | `font-semibold` | `Font` (bkz. [Font kalınlıkları](#font-kalınlıkları)) |
| İç boşluk | `p`, `px`, `py`, `pt`, `pr`, `pb`, `pl` + `-{n}` / `-px` / `-[12px]` | `px-4` = 16 px | `Padding` |
| Dış boşluk | `m`, `mx`, `my`, `mt`, `mr`, `mb`, `ml` + `-{n}` | `mt-2` = 8 px | `Margin` (layout panellerinde) |
| Boyut | `w-{n}`, `h-{n}` | `h-10` = 40 px | `Width`, `Height` |
| Kenarlık | `border`, `border-{0,2,4,8}`, `border-{renk}-{ton}` | `border border-gray-300` | Çizim |
| Köşe | `rounded`, `rounded-{none,sm,md,lg,xl,2xl,3xl,full}`, `rounded-[10px]` | `rounded-md` = 6 px | `Region` veya owner-draw |
| Hizalama | `text-left`, `text-center`, `text-right` | | `TextAlign` |
| Diğer | `cursor-pointer`, `cursor-text`, `hidden`, `visible`, `truncate` | | `Cursor`, `Visible`, `AutoEllipsis` |

- Boşluk birimi 4 px'tir (`spacingUnit` ile değiştirilebilir). Sayılar her kültürde nokta ile yazılır: `p-0.5`.
- Palet: Tailwind v3'ün 22 rengi × 11 tonu (50–950), ayrıca `white`, `black`, `transparent`. Tailwind v4 paleti de seçilebilir (bkz. [Tema dosyası](#tema-dosyası-twconfigjson)).
- Aynı varyant içinde sonra gelen class kazanır: `p-2 px-4` → dikey 8 px, yatay 16 px.
- Tanınmayan class'lar `TailwindProvider.UnknownClasses` olayına (dinleyici yoksa Debug çıktısına) bildirilir.
- CSS'te padding kutuyu büyütür; WinForms'ta boyutu sabit (`AutoSize` kapalı) bir buton ya da etikette ise içeriği daraltır. Bu kontrollerde padding, yazı tek satıra sığacak kadar kısılır (örn. 25 px yüksekliğindeki bir butona `btn` verildiğinde `py-2` küçülür). Kontrol büyüyünce, fontu ya da metni değişince yeniden hesaplanır ve yer varsa tam değere döner.
- Padding sığdırma fontu küçültemez: sabit boyutlu bir kontrole fontunu büyüten bir class (örn. `btn`'deki `text-sm font-semibold`) verilirse padding sıfıra inse de yazı sığmayabilir; böyle kontrollerde font class'ı vermeyin ya da kontrolü büyütün.
- `hidden` kaldırıldığında FormWind'in gizlediği kontrol yeniden görünür olur.
- **MDI:** `IsMdiContainer` bir formun `bg-*` rengi çocuk pencerelerin arkasındaki alana (`MdiClient`) da verilir; yoksa bu alan sistemin gri rengiyle kalır, koyu temada açık bir boşluk olurdu. Uygulamanın `MdiClient`'a atadığı renk korunur. Büyütülmüş çocuğun menüye eklenen küçült/geri yükle/kapat düğmeleri şeridin yazı rengiyle çizilir. Windows MDI çocuklarının başlık çubuğunu koyu çizmez (klasik çerçeve) ve `MdiClient`'ın çökük kenarı koyu temada açık bir çizgi olarak görünür; ikisi de FormWind'in dışında.
- **Şeride gömülü kutular:** `ToolStripTextBox` ve `ToolStripComboBox` şeridin renderer'ıyla çizilmez; koyu şeritte beyaz kalırlardı. Şeridin yazı rengini ve şeritten türetilen bir zemin (açık şeritte beyaz, koyu şeritte zeminin biraz açığı) alırlar; açılır kutunun çerçevesi ve ok düğmesi de bu renklerle çizilir. Şeridin renderer'ını paylaşan alt menülerdeki kutular ve sonradan eklenenler dahildir. Uygulamanın bu kutulara atadığı renkler korunur.
- **Devre dışı native metin kutuları** (`TextBox`, `RichTextBox`, `MaskedTextBox`): Windows devre dışı metin kutusunda zemin rengine güvenilir biçimde uymaz (rengin ne zaman atandığına göre sistem zeminini çizebilir), yazı rengine ise uyar; koyu temada açık zeminde açık yazı çıkardı. Bu yüzden devre dışıyken yalnızca `disabled:` varyantlarının renkleri uygulanır, yoksa Windows'un kendi devre dışı görünümü (açık zeminde gri yazı) kalır. FormWind'in kendi çizdiği kontroller (`TwTextBox` dahil) bundan etkilenmez.
- **Devre dışı butonlar:** Windows devre dışı düz butonun yazısını `ForeColor` yerine zeminin koyulaştırılmış tonuyla çizer; koyu zeminde yazı görünmez olurdu. FormWind'in stillediği butonlarda yazı, devre dışıyken `disabled:text-*` rengiyle, o yoksa yazı renginin zemine doğru yarı yarıya karıştırılmış tonuyla yeniden çizilir (`TwButton` da aynı kuralı izler). Resimli native butonlarda Windows'un çizimi kalır. Varsayılan `btn-primary`, `btn-ghost` ve `btn-danger` bileşenleri açık ve koyu temada `disabled:` renkleri tanımlar; `disabled:` varyantı `dark:`'tan önceliklidir, bu yüzden kendi bileşenlerinizde koyu tema için `dark:disabled:` de verin.
- Class rengi ebeveynden kalıtılan renkle aynı olsa da kontrole açıkça atanır; ebeveynin rengi sonradan değişince class rengi değişmez.
- Class'lar kaldırıldığında (`SetClasses(control, "")`) renk, font, padding, margin, imleç, görünürlük, köşe ve ToolStrip renderer'ı class'lardan önceki değerlerine döner. Boyutlar (`w-*`, `h-*`) geri alınmaz; yerleşim kontrolü sonradan yeniden boyutlandırmış olabilir.

### Uygulama kodunun atadığı renkler

Class'lar uygulandıktan sonra uygulama kodu bir kontrolün `BackColor` ya da `ForeColor` değerini değiştirirse (örn. bir durum göstergesini yeşile boyamak), FormWind o renk kanalını uygulamaya bırakır: `hover:`, `dark:` ve diğer varyantlar o rengi ezmez. Diğer kanal FormWind'de kalır.

```csharp
tw.SetClasses(lblStatus, "bg-white text-gray-700 dark:bg-gray-800 dark:text-gray-200");
lblStatus.BackColor = Color.LightGreen;   // koyu temaya geçince de yeşil kalır; yazı rengi class'lardan gelir
tw.SetClasses(lblStatus, "bg-red-600 text-white");   // yeni class'lar: arka plan yeniden FormWind'in
```

- Ebeveynden kalıtılan renk değişimleri (örn. form koyu temaya geçti) uygulamanın ataması sayılmaz.
- Kanalı geri almak için farklı class'lar atanmalıdır; `Refresh(control)` yetmez.
- Uygulama yalnızca zemini değiştirip yazı rengini class'lara bırakırsa koyu temada okunmayan bir çift (açık yazı, açık zemin) çıkabilir. [Kontrast denetimi](#kontrast-denetimi) bunu raporlar ve nedenini `Cause = TwContrastCause.KeptBackground` olarak verir.

**Kendi çizen kod.** `CellPainting` işleyicisi ya da owner-draw bir ListView gibi renkleri uygulamanın çizdiği yerlerde class renkleri sağlayıcıdan istenir; sonuç sağlayıcının o anki modunu (koyu tema, yüksek karşıtlık) izler:

```csharp
var selected = tw.GetColors("list", "selected");            // bileşenler ve part'lar açılır
var hover = tw.GetColors("list", "item", TwState.Hover);
var own = tw.GetCurrentColors(lvUploads, "selected");          // kontrolün class'ları ve o anki durumu
e.Graphics.FillRectangle(new SolidBrush(selected.Back ?? SystemColors.Highlight), e.Bounds);
```

## Yerleşim (flex ve grid)

`flex` ya da `grid` class'ı verilen bir **`TwPanel`** çocuklarını kendisi yerleştirir; çocukların `Location`'ı, `Dock` ve `Anchor` değerleri yok sayılır. Standart `Panel` ve `Form`'un yerleşim motoru değiştirilemediğinden flex/grid yalnızca `TwPanel` (ya da `ITwLayoutHost` uygulayan bir konteyner) üzerinde çalışır.

```csharp
// Kart: dikey dizilim, öğeler genişliği doldurur
this.tailwindProvider1.SetClasses(this.pnlCard, "card flex flex-col gap-1");
// Buton satırı: sağa yaslı, 8 px aralıklı
this.tailwindProvider1.SetClasses(this.pnlActions, "flex justify-end gap-2");
// Boşluk doldurucu: kalan alanı kaplar ve buton satırını alta iter
this.tailwindProvider1.SetClasses(this.pnlSpacer, "flex-1");
```

| Konteyner (TwPanel) | Öğe (çocuk kontrol) |
| --- | --- |
| `flex`, `grid`, `block` | `flex-1`, `flex-auto`, `flex-initial`, `flex-none` |
| `flex-row`, `flex-col`, `flex-wrap`, `flex-nowrap` | `grow`, `grow-0`, `shrink`, `shrink-0` |
| `gap-{n}`, `gap-x-{n}`, `gap-y-{n}` | `basis-{n}`, `basis-1/2`, `basis-full`, `basis-auto` |
| `justify-start/end/center/between/around/evenly` | `w-1/2`, `w-full`, `h-full` (oranlar konteynere göre) |
| `items-start/end/center/stretch` | `min-w-{n}`, `max-w-{n}`, `max-w-md` …, `min-h-{n}`, `max-h-{n}` |
| `grid-cols-{1–12}`, `grid-rows-{1–12}` | `self-start/end/center/stretch`, `col-span-{1–12}`, `col-span-full`, `row-span-{1–12}`, `row-span-full`, `m-*` |

CSS'ten farklar:
- Öğelerin "doğal" boyutu Designer'daki `Size`'dır (AutoSize kontrollerde tercih edilen boyut). Boyutu belirtilmemiş öğeler doğal boyutlarının altına küçülmez.
- Öğe aralığı yalnızca `m-*` class'larından gelir; WinForms'un varsayılan 3 px `Margin`'i kullanılmaz.
- Varsayılan `items-stretch`: satırdaki öğeler konteyner yüksekliğine, sütundaki öğeler genişliğine uzar.
- Grid öğeleri CSS'in otomatik yerleştirmesiyle (seyrek) sırayla konur; `row-span-*`'ın kapladığı hücreler atlanır. `grid-rows-N` kabın yüksekliğini N eşit satıra böler (`repeat(N, minmax(0, 1fr))`); `row-span-full` bu açık satırların tümünü kaplar (`grid-rows` yoksa tek satırdır). Diğer satırların yüksekliği içerikten gelir; birden çok satır kaplayan bir öğe sığmazsa eksik yükseklik kapladığı satırlara eşit dağıtılır. `col-start-*`, `row-start-*`, `grid-flow-*`, `margin: auto` ve `order-*` yok.
- Öğeler satırın çapraz boyutunu aşmaz (CSS'te taşabilir; WinForms'ta taşan kısım kesilip kaybolurdu). Daralan Label'larda `truncate` "…" gösterir.
- `TwPanel`'in içeriği kenarlığın içinde başlar (CSS kutu modeli); `AutoScroll` panelde genişlik görünen alandan alınır ve dikey kaydırma çubuğunun yeri önceden ayrılır.
- `AutoSize = true` olan flex `TwPanel` içeriğine göre boyutlanır (grid için henüz yok).
- Responsive varyantlar (`sm:`, `md:` …): bkz. [Animasyonlar ve responsive varyantlar](#animasyonlar-ve-responsive-varyantlar).

## Font kalınlıkları

`font-thin` (100), `font-extralight` (200), `font-light` (300), `font-normal` (400), `font-medium` (500), `font-semibold` (600), `font-bold` (700), `font-extrabold` (800), `font-black` (900).

WinForms yalnızca normal ve kalın stilleri bilir; ara kalınlıklar Windows'ta **ayrı font aileleri** olarak kuruludur. FormWind kalınlığı bu ailelere CSS'in eşleme kuralıyla çözer:

| Class | Segoe UI'da kullanılan |
| --- | --- |
| `font-thin`, `font-extralight`, `font-light` | Segoe UI Light |
| `font-normal`, `font-medium` | Segoe UI |
| `font-semibold` | Segoe UI Semibold |
| `font-bold` | Segoe UI + kalın stil |
| `font-extrabold`, `font-black` | Segoe UI Black |

Başka bir font için ağırlık aileleri tema dosyasında tanımlanır; kurulu olmayan aile atlanır ve en yakın kurulu kalınlık seçilir:

```jsonc
"fontWeights": { "Inter": { "300": "Inter Light", "600": "Inter SemiBold" } }
```

## İkonlar

Windows'la gelen ikon fontu kullanılır: Windows 11'de **Segoe Fluent Icons**, Windows 10'da **Segoe MDL2 Assets** (Windows 7/8'de ikon çizilmez).

- **`TwIcon`**: tek başına ikon. `IconName` property'si Designer'da açılır listeden seçilir. Renk `text-*`, boyut font boyutuyla verilir: `text-xl` = 20×20 px. AutoSize varsayılan olarak açıktır.
- **`TwButton.IconName`**: metnin yanında ikon. `IconPosition` (Left/Right) ve `IconSpacing` (varsayılan 8 px) ayarlanabilir.

```csharp
this.icoCustomer.IconName = "user";
this.tailwindProvider1.SetClasses(this.icoCustomer, "text-xl text-blue-600 dark:text-blue-400");
this.btnSave.IconName = "save";
```

Yerleşik adlar: `arrow-down`, `arrow-left`, `arrow-right`, `arrow-up`, `attach`, `bell`, `calculator`, `calendar`, `camera`, `cart`, `check`, `chevron-down`, `chevron-left`, `chevron-right`, `chevron-up`, `clock`, `copy`, `cut`, `download`, `edit`, `error`, `eye`, `eye-off`, `file`, `filter`, `flag`, `folder`, `folder-open`, `fullscreen`, `globe`, `grid`, `heart`, `heart-filled`, `help`, `history`, `home`, `image`, `info`, `key`, `link`, `list`, `location`, `lock`, `mail`, `map-pin`, `menu`, `minus`, `more`, `paste`, `phone`, `pin`, `plus`, `power`, `print`, `redo`, `refresh`, `save`, `search`, `send`, `settings`, `share`, `shield`, `sort`, `star`, `star-filled`, `sync`, `tag`, `trash`, `undo`, `unlock`, `upload`, `user`, `users`, `warning`, `x`, `zoom-in`, `zoom-out`.

Fontun herhangi bir glyph'i kod noktasıyla da kullanılabilir (`IconName = "#E8A5"`). Ek adlar ve farklı bir ikon fontu tema dosyasından verilir:

```jsonc
"iconFont": "Segoe Fluent Icons",
"icons": { "invoice": "E8A5" }
```

Glyph'ler font metriklerine güvenilmeden, gerçek mürekkep sınırlarına göre ortalanarak çizilir (ikon fontlarının satır yüksekliği glyph boyutuyla uyuşmaz).

### SVG ikonlar

Lucide, Heroicons, Tabler gibi tek renkli SVG setleri de kullanılabilir. `icons` değeri bir `.svg` yolu (tema dosyasına göre) ya da doğrudan `<svg>` metni olabilir; kodda `TwTheme.SetSvgIcon(ad, svg)`:

```jsonc
"icons": {
  "invoice": "E8A5",                         // ikon fontu glifi
  "package": "Assets/icons/package.svg",     // dosya
  "dot": "<svg viewBox='0 0 24 24'><circle cx='12' cy='12' r='4'/></svg>"
}
```

SVG ikon aynı adlı glifi ezer, böylece yerleşik ikonlar (`check`, `chevron-down` …) tüm kontrollerde (`TwButton`, `TwSelect`, takvim, grid) tek tek değiştirilebilir. Renk her zaman çizildiği yerin metin rengidir (`currentColor` gibi). Desteklenenler: `path` (tüm komutlar, yaylar dahil), `circle`, `ellipse`, `rect` (`rx`), `line`, `polyline`, `polygon`, `g`; `fill`, `stroke`, `stroke-width`, `stroke-linecap`, `stroke-linejoin`. Gradient, maske, metin ve CSS desteklenmez.

## Kenarlık, gölge ve focus halkası

| Class | Etki |
| --- | --- |
| `border-t`, `border-b-2`, `border-x`, `border-y` | Kenar bazlı kenarlık (köşesiz çizilir) |
| `shadow-sm`, `shadow`, `shadow-md`, `shadow-lg`, `shadow-xl`, `shadow-2xl`, `shadow-none` | Tailwind v3 gölge katmanları (bulanık) |
| `shadow-{renk}`, `shadow-blue-500/50` | Renkli gölge |
| `ring`, `ring-0/1/2/4/8`, `ring-{renk}`, `ring-offset-2`, `ring-offset-{renk}` | Halka; genelde `focus:ring-2 focus:ring-blue-500/30` |
| `bg-black/50`, `text-white/75` … | Saydamlık: tüm renk class'larında `/0–100` |

Gölge ve halka kontrolün **ebeveyninde** çizilir (kontroller kendi sınırlarının dışına çizemez); Form, Panel ve TwPanel ebeveynlerinde çalışır. Bu sayede `TextBox` gibi native kontrollerin de focus halkası olabilir.

## Gradient

`bg-gradient-to-{r,l,t,b,tr,tl,br,bl} from-{renk} via-{renk} to-{renk}` — `TwPanel`, `TwButton` ve standart `Panel`'de çizilir. Eksik uç diğer ucun saydam halidir.

## Etkileşim

- **`transition`** (`transition-colors`, `duration-75` … `duration-1000`, `duration-[250ms]`): arka plan ve metin rengi değişimleri yumuşak geçişle (ease-out, varsayılan 150 ms) uygulanır. Gölge değişimleri de (`shadow-sm hover:shadow-lg transition`) aynı sürede, eski gölge solarken yenisi belirerek geçer; bulanık gölgeler önbellekten gelir, geçiş sırasında yeniden hesaplanmaz.
  - İlk uygulama geçişsizdir: class'lar atandığında kontrol hemen son rengini alır (geçiş uygulamanın kendi renginden başlayıp açılışta renk kaydırırdı). Tema, durum (hover, focus, disabled …) ve class değişimleri geçişlidir.
  - Geçişler bir WinForms zamanlayıcısıyla (yaklaşık 60 kare/sn) ilerler. Windows `WM_TIMER`'ı yalnızca kuyrukta başka mesaj yokken üretir; her `OnPaint`'te kendini yeniden geçersiz kılan bir pencere açıkken (örn. ShareX'in bölge yakalaması) ya da UI iş parçacığı meşgulken geçişler durur ve o sırada ara renkte kalır, pencere kapanınca tamamlanır. Kontrast denetimi bu sürede de varılacak rengi ölçer.
- **`hover:` / `active:`**: yalnızca bu varyantları olan kontroller fare girip çıkınca yeniden stillenir; diğerlerinde fare hareketi stil uygulamasını tetiklemez. Class'lar imleç zaten kontrolün üzerindeyken atanırsa (örn. tıklayınca class'ı değişen bir buton) hover durumu hemen uygulanır.
- **`group` / `group-hover:`**: `group` class'lı konteynerin üzerine gelindiğinde içindeki kontrollerin `group-hover:` varyantları uygulanır. Hover'a tepki veren konteynerler, imleç doğrudan bir çocuk kontrolün üzerinden girse de hover sayılır.
- **Placeholder**: `TwTextBox.PlaceholderText`; renk `placeholder:text-gray-400`.
- **`focus-visible:`**: yalnızca odak klavyeyle (Tab, oklar) geldiğinde uygulanır; fareyle tıklanan kontrolde uygulanmaz (`focus-visible:ring-2 focus-visible:ring-blue-500/40`). Owner-draw kontrollerin (`TwButton`, `TwTabStrip`, `TwDataList`, `TwCalendar`, `TwSlider`) odak halkaları da aynı kuralla çizilir. Son girişin türü `TwFocusVisible.LastInputWasKeyboard` ile okunabilir.

## CheckBox, Radio, Switch

`TwCheckBox`, `TwRadioButton`, `TwSwitch` owner-draw çizilir. Seçili renk `accent-*`, seçili değilken dolgu `fill-*`, kutu kenarlığı `border-*`, köşe `rounded-*`, metin `text-*`. `TwSwitch` (44×24 px) düğmesi 150 ms içinde kayar.

Devre dışı standart `Label` da (resimsiz, `LinkLabel` olmayan) yazı rengi class'lardan geliyorsa aynı şekilde yeniden çizilir: Windows onu koyu temada görünmeyen kabartma griyle çizer.

Standart `CheckBox` ve `RadioButton` devre dışıyken `ForeColor`'ı yok sayar ve yazıyı zeminden türettiği koyu bir renkle çizer; koyu temada yazı görünmez olur. Class'lar yazı rengini veriyorsa FormWind devre dışı yazıyı işaretin yanında yeniden çizer: `disabled:text-*` varsa o renk, yoksa yazı rengiyle zeminin ortası. İşaret solda ya da sağda, `Appearance.Normal`, resimsiz ve `FlatStyle.System` olmayan kontrollerde yapılır.

## Menü ve araç çubukları

MenuStrip, ToolStrip, StatusStrip ve ContextMenuStrip için tema renderer'ı. Temel class'lar şeride, `menu:` açılan menülere, `item:` öğelere uygulanır:

```csharp
SetClasses(menuStrip1, "bg-white text-gray-700 border-b menu:bg-white menu:border-gray-200 item:hover:bg-gray-100 item:rounded-md divide-gray-200 dark:bg-gray-900 dark:text-gray-200");
```

- `item:hover:`, ayırıcı (`divide-*`) ve menü kenarlığı (`menu:border-*`) verilmezse varsayılanlar açık şerit içindir. Şeridin zemini koyuysa bu renkler şeridin kendi zemin ve yazı renginden türetilir; böylece koyu şeritte hover kutusunda yazı kaybolmaz.
- Öğelere elle atanmış açılır menüler (`menuItem.DropDown = contextMenuStrip1`) WinForms'ta sahibinin renderer'ını devralmaz. FormWind bu menülere şeridin renderer'ını verir (iç içe menüler dahil). Kendi renderer'ı atanmış ya da kendi class'ları olan menülere dokunulmaz. Şeride sonradan atanan bir menü, şerit bir sonraki uygulanışında (örn. tema değişimi) renk alır.
- Renkler değişmedikçe renderer yeniden atanmaz (atama şeridi yeniden yerleştirir). Class'lar kaldırılınca şeridin özgün renderer'ı geri yüklenir.

## Başlık çubuğu

Form'a class verildiğinde koyu tema açıkken başlık çubuğu otomatik koyulaşır (Windows 10 20H1+). Windows 11'de `caption:bg-*`, `caption:text-*`, `caption:border-*` ile renklendirilir.

`backdrop-mica`, `backdrop-acrylic`, `backdrop-tabbed` (Mica Alt) ve `backdrop-none` Windows 11 22H2+ sistem zeminini seçer. Zemin başlık çubuğunda ve pencere çerçevesinde görünür; WinForms istemci alanını opak çizdiği için form içinde görünmez. `caption:bg-*` verilirse başlık rengi zeminin önüne geçer.

## Koyu tema: kaydırma çubukları ve ipuçları

`DarkMode = true` iken FormWind'in çizmediği native parçalar da Windows'un koyu temasına geçer:

| Özellik (varsayılan açık) | Etki |
| --- | --- |
| `TailwindProvider.DarkScrollbars` | Panel, TextBox, ListBox, TreeView, ListView ve DataGridView kaydırma çubukları; ListView'da sütun ve grup başlıkları da (Explorer'ın koyu liste teması; başlık yazısı listenin yazı rengiyle çizilir, `OwnerDraw` listeler hariç) |
| `TailwindProvider.DarkToolTips` | Native ipucu pencereleri: `ToolTip` bileşenleri, ToolStrip öğe ipuçları, ListView/TreeView ipuçları |

- Windows devre dışı ListView, ComboBox ve metin kutularını sistem renkleriyle çizer; koyu temada açık kalırlar ve `BackColor` bunu değiştirmez (örn. KeePass'te veritabanı açık değilken girdi listesi ve arama kutusu).
- İpuçları uygulamanın kendi bileşenlerine ait olduğundan `DarkToolTips` iş parçacığı geneline etki eder: koyu temada en az bir provider varsa o iş parçacığının ipuçları koyudur. Tema değişince açık ipucu pencereleri de güncellenir.
- İkisi de Windows Gezgini'nin kullandığı belgelenmemiş `DarkMode_Explorer` temasını kullanır (Windows 10 1809+); desteklenmeyen sistemlerde etkisizdir, sorun olursa kapatılabilir.
- Class'larla stillenen ipucu için `TwToolTip` kullanılabilir.

## DataGridView

DataGridView'un bölümleri **parça önekleriyle** stillenir; durum önekleriyle birleştirilebilir (`dark:header:bg-gray-900`, `row:hover:bg-gray-50`). Hazır `table` bileşeni tek class'la açık ve koyu temalı bir tablo verir:

```csharp
this.tailwindProvider1.SetClasses(this.gridOrders, "table");
```

| Açık tema | Koyu tema |
| --- | --- |
| ![Tablo, açık](docs/grid-light.png) | ![Tablo, koyu](docs/grid-dark.png) |

Vitrin örneğinin tablo formu (`FormWind.Showcase --grid`): arama, süzme, gruplama, alt toplam, satır detayları, yeni hücre tipleri ve düzen kaydetme.

| Önek | Hedef | Desteklenen class'lar |
| --- | --- | --- |
| (yok) | Kontrol alanı ve tüm hücreler | `bg-*`, `text-*`, font, `border`, `divide-x`, `divide-y`, `divide-{renk}` (ızgara çizgileri) |
| `cell:` | `DefaultCellStyle` | renk, font, `px-*`/`py-*`, `text-left/center/right`, `whitespace-nowrap` |
| `alt:` | `AlternatingRowsDefaultCellStyle` | renk, font |
| `selected:` | Seçim renkleri | `bg-*`, `text-*` |
| `header:` | Kolon başlıkları | renk, font, padding, hizalama, `h-*`, `border-0`, `whitespace-nowrap` |
| `row:` | Satırlar | `h-*` (satır yüksekliği), `row:hover:bg-*` / `row:hover:text-*` (satır hover'ı) |
| `row-header:` | Satır başlıkları | `hidden`, renk |
| `match:` | Arama eşleşmeleri ([Arama](#arama)) | `bg-*`, `text-*` |
| `group:` | Grup başlıkları ([Gruplama](#gruplama-ve-alt-toplam)) | `bg-*`, `text-*`, font |
| `footer:` | Alt toplam satırı (`TwGridFooter`) | `bg-*`, `text-*`, font |

**`table` varyantları** ondan sonra yazılır: `table-sm` (sık, 32 px satır), `table-lg` (48 px satır), `table-striped` (ardışık satırlar), `table-bordered` (dikey ve yatay çizgiler) — `"table table-sm table-striped"`. Fluent, Ant Design ve Material görünümleri kendi `table-sm` ve `table-lg` yüksekliklerini tanımlar.

- Başlık renkleri verildiğinde `EnableHeadersVisualStyles` kapatılır (Windows teması başlık renklerini yok sayar).
- Class'lar kaldırıldığında grid'in orijinal stillerine dönülür.
- Kaydırma çubukları koyu temada Windows'un koyu çubuklarına geçer (`TailwindProvider.DarkScrollbars`, varsayılan açık).
- Parça önekleri DataGridView dışındaki kontrollerde yok sayılır.
- `row:h-*`, `VirtualMode` ya da `DataSource` kullanan grid'lerde `RowHeightInfoNeeded` olayıyla verilir: satırlar paylaşımdan çıkarılmaz, kaydırma konumu ve seçili hücre korunur (17.000 satırda ~12 ms; satır satır atamada ~17 sn). Bağlı olmayan grid'lerde yalnızca yüksekliği farklı satırlar güncellenir.
- Grid'in zemini (`BackgroundColor`) saydamlığı desteklemez; saydam bir `bg-*` zeminde özgün renge düşer (hücrelere yine uygulanır).
- Satır eklenince grid yalnızca boş tablo ya da yükleniyor görünümü değişiyorsa yeniden çizilir.
- **Kendi çizen grid'ler:** hücreleri kendisi çizen grid'ler (örn. Git Extensions'ın commit listesi) hücre stillerini yok sayar ve yapısal özelliklere bağımlı olabilir. `divide-x` / `divide-y` `CellBorderStyle`'ı, `row:h-*` satır yüksekliğini değiştirir; böyle bir grid `CellBorderStyle.None` ya da kendi satır yüksekliğini varsayıyorsa bu class'ları kullanmayın. Git Extensions'ın commit listesi `SingleHorizontal` kenarlıkla kaydırırken kilitlenir; FormWind olmadan da aynısı olur.
- **Kendi çizen grid'lerde koyu tema:** `TwGrid.EnableDarkInversion(grid)` böyle bir grid'i koyu temada açık class'larıyla stiller ve görüntüsünü tonu koruyarak çevirir (tarayıcıların zorunlu koyu modu gibi); sabit açık renkler de koyulaşır. Resimler ve renkli işaretler de çevrilir; kaydırma çubukları, düzenleme kontrolü ve satır detayları ayrı penceredir, onlara koyu renkler verilir.

### Kolon class'ları

Her kolonun Properties penceresinde **ColumnClasses on tailwindProvider1** özelliği vardır (kodda `SetColumnClasses`). Temel class'lar kolonun hücrelerine, `header:` başlığa uygulanır; `w-32`, `min-w-20`, `flex-1` (Fill ağırlığı) ve `hidden` kolonun kendisini etkiler. Font boyutu verilmezse grid'in fontu korunur.

```csharp
tw.SetColumnClasses(colTotal, "text-right font-semibold w-28 header:text-right");
tw.SetColumnClasses(colName, "flex-1 min-w-40");
```

### Hücre tipleri

| Kolon | Değer | Ayarlar |
| --- | --- | --- |
| `TwBadgeColumn` | Metin; değere göre renkli rozet | `Variants = "Alive: bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100; Dead: bg-red-100 text-red-800; *: bg-gray-100 text-gray-700"` |
| `TwAvatarColumn` | `Image` (yoksa `Placeholder` rengi) | `AvatarSize`, `Radius` |
| `TwIconColumn` | İkon adı (`check`, `#E710`) | Renk hücrenin metin rengi |
| `TwProgressColumn` | 0–`Maximum` sayı | `ShowText`, `BarColor`, `TrackColor`, `DarkTrackColor` |
| `TwCheckColumn` | `bool` / `CheckState` | Yuvarlak köşeli kutu, `AccentColor`, `BoxSize`; `ThreeState` belirsiz durumu çizgiyle gösterir |
| `TwButtonColumn` | Metin (ya da `Text` + `UseColumnTextForButtonValue`) | `Classes = "rounded-md bg-red-600 text-white hover:bg-red-700"`; tıklama `CellContentClick` ile |
| `TwSparklineColumn` | Sayılar (`IEnumerable` ya da `;`/boşlukla ayrılmış metin) | `Kind` (`Line`, `Area`, `Bar`), `LineColor`, `DarkLineColor`, `NegativeColor` |
| `TwDateColumn` | `DateTime` / `DateTimeOffset` | Kolon biçimi (varsayılan `g`); `Relative = true` son haftayı "5 dk önce" gösterir (`TwTexts.MinutesAgo` …); düzenlerken tarihin kendisi görünür |
| `TwNumberColumn` | Sayı | Sağa hizalı, `N2` biçimi (para için `C2`), `Culture = "tr-TR"`, `NegativeClasses` (varsayılan `text-red-600 dark:text-red-400`), `PositiveClasses` |
| `TwLinkColumn` | Metin | Tema mavisi (koyu temada `DarkLinkColor`), üzerine gelince altı çizili; tıklama `CellContentClick` ile |
| `TwTagsColumn` | Metinler (ya da `Separator` ile bölünen tek metin) | `Variants` ile birden çok rozet; sığmayanlar `+N` olarak sayılır |
| `TwActionsColumn` | — | Menülü "⋯" düğmesi: `Actions = "Düzenle; -; Sil"`, `ActionClicked`, `MenuOpening` (satıra göre öğeleri kapatma), `Icon` |
| `TwExpanderColumn` | — | Satır detayını açan ok ([Satır detayları](#satır-detayları)); tıklama ya da Boşluk |

Hücre tipleri koyu temayı grid'in sağlayıcısından öğrenir; `dark:` varyantları ayrıca ayar gerektirmez.

### Satır kuralları

```csharp
tw.SetRowClasses(gridOrders, row =>
    row.Cells["colStatus"].Value as string == "İptal" ? "text-gray-400 dark:text-gray-500" : null);
```

Kural her satır için class döndürür (`null` = değişiklik yok); `CellFormatting` sırasında uygulanır, veri değişince kendiliğinden güncellenir. `SetRowClasses(grid, null)` kuralı kaldırır.

### Sıralama, boş tablo ve yükleniyor durumu

Class verilmiş grid'lerde başlıktaki sıralama oku tema ikonuyla (`arrow-up` / `arrow-down`) ve `accent-*` rengiyle çizilir. `List<T>` ya da düz `BindingList<T>` ile bağlanan grid başlığa tıklanınca sıralamaz; `TwSortableBindingList<T>` ile `SortMode = Automatic` kolonlar sıralanır (metinler geçerli kültürle, Türkçe harf sırası dahil; eşit değerler yerini korur):

```csharp
gridOrders.DataSource = new TwSortableBindingList<Order>(orders);
TwGrid.SetEmptyText(gridOrders, "Kayıt bulunamadı");   // satır yokken ortada ikon + metin (TwGrid.SetEmptyIcon)
TwGrid.SetLoading(gridOrders, true);                   // satır yoksa iskelet satırlar, varsa soluk katman + "Yükleniyor…"
```

Hücre düzenlenirken açılan TextBox/ComboBox kenarlıksız ve hücre renkleriyle gösterilir; koyu temada kaydırma çubukları da koyulaşır.

### Süzme, kolon seçimi ve sayfalama

```csharp
TwGrid.EnableFilterMenu(gridOrders);     // başlıkta süzme düğmesi: kolonun değerleri onay kutularıyla
TwGrid.EnableColumnChooser(gridOrders);  // başlığa sağ tık: kolonları göster/gizle
```

- Süzme menüsü veri kaynağı `ITwFilterableList` olan grid'lerde (`TwSortableBindingList<T>`) ve `DataPropertyName`'i olan kolonlarda görünür. Süzülen kolonun düğmesi `accent-*` renginde çizilir. Kodla: `list.SetColumnFilter("City", new[] { "İzmir" })`, `ClearFilters()`.
- Sıralamalar üst üste uygulanır (önce ada, sonra tarihe göre sıralanan listede aynı tarihliler ada göre kalır); süzme sıralamayı korur, eklenen ve silinen satırlar tüm listeye yansır.
- Kolon menüsünde son görünen kolon gizlenemez.
- Sekiz ve daha çok değeri olan kolonlarda menünün üstünde arama kutusu vardır; "tümünü seç" o zaman görünen değerlere uygulanır.
- `ITwGridSource` (`TwSortableBindingList<T>`) kaynaklı grid'lerin sayı, tarih ve süre kolonlarında **En az** ve **En çok** kutuları vardır; geçerli kültürle okunur (`1.250,50`, `31.12.2025`), saatsiz bir tarih üst sınır olarak o günün tamamını kapsar. Kodla: `list.SetRangeFilter("Amount", 100, null)`. `ClearFilters()` değer ve aralık süzmelerini kaldırır.

### Arama

```csharp
TwGrid.AttachSearchBox(gridOrders, txtSearch);   // yazdıkça arar (200 ms bekleme); Escape kutuyu temizler
TwGrid.SetSearchText(gridOrders, "izmir");        // kodla; null aramayı bitirir
```

- Eşleşen metin düz metin hücrelerinde vurgulanır (`match:` renkleri; varsayılan sarı). Arama geçerli kültürle büyük/küçük harfi yok sayar (`İZMİR` `izmir`'i bulur).
- `TwSortableBindingList<T>` (`ITwGridSource`) ile yalnızca görünen bağlı bir kolonda eşleşen satırlar kalır; bağlı olmayan grid'lerde diğer satırlar gizlenir; diğer bağlı grid'lerde yalnızca vurgulanır.

### Gruplama ve alt toplam

```csharp
TwGrid.SetGroupBy(gridOrders, colCity);           // her şehir için satır sayılı bir başlık; tıklayınca grup kapanır
TwGrid.SetGroupText(gridOrders, g => $"{g.Key} — {g.RowCount} sipariş");   // isteğe bağlı

var footer = TwGrid.AddFooter(gridOrders);        // grid'in altında, aynı kapta alt toplam satırı
footer.SetText(colCustomer, "Toplam");
footer.SetAggregate(colAmount, TwAggregate.Sum, "C2");   // Sum, Average, Count, Min, Max
```

- `TwSortableBindingList<T>` satırları önce gruba göre dizer, grup içinde geçerli sıralamayı korur; kodla eklenen satırlar kendi grubunun sonuna girer. Diğer veri kaynaklarında aynı değerli ardışık satırlar bir grup olur. `TwGrid.GetGroups`, `SetGroupExpanded`, `ExpandAllGroups`, `CollapseAllGroups`. VirtualMode'da desteklenmez.
- Grup başlığı grubun ilk satırının üstüne eklenir (satır uzar, içeriği aşağı kayar); satırlar paylaşımda kalır, kaydırma akıcı kalır.
- `TwGridFooter` grid'in kolon genişliklerini, sırasını ve yatay kaydırmasını izler; satırlar ya da değerler değişince yeniden hesaplar. Süzülen ya da aranan listede yalnızca görünen satırlar sayılır. `footer.SetValue(column, values => ...)` özel bir hücre hesaplar. `AddFooter` doldurmaya (ya da üste/alta) yerleşen grid'in altına yerleşir; aksi halde grid kısalır. Designer'da da konup `Grid` ile bağlanabilir.

### Satır detayları

```csharp
gridOrders.Columns.Insert(0, new TwExpanderColumn());
TwGrid.SetRowDetails(gridOrders, row => new OrderLinesView((Order)row.DataBoundItem), height: 160);
```

- Açılan satır kontrolü hücrelerinin altında gösterir; kontrol kaydırmayla birlikte hareket eder, satır kapanınca dispose edilir. Açık satırlar bağlı öğeleriyle hatırlanır; sıralama ve süzme onları korur. Kodla: `SetRowExpanded`, `ToggleRowDetails`, `IsRowExpanded`, `GetRowDetails`.

### Düzeni kaydetme ve dışa aktarma

```csharp
Settings.Default.OrdersLayout = TwGrid.SaveLayout(gridOrders);   // JSON: kolon sırası, genişlikler, görünürlük, sıralama, gruplama, arama, süzmeler
TwGrid.LoadLayout(gridOrders, Settings.Default.OrdersLayout);

TwGrid.ExportCsv(gridOrders, path);                 // BOM'lu UTF-8 (Excel); ayırıcı: kültürün liste ayırıcısı (Türkçede ;)
TwGrid.CopyToClipboard(gridOrders);                 // seçili hücreler (ya da tüm satırlar), başlıklı ve sekmeyle ayrılmış
string text = TwGrid.ToDelimitedText(gridOrders, ';', selectedOnly: false);
```

- Kolonlar `Name` ile eşlenir; grid'de olmayan kolonlar ve geçersiz değerler atlanır, formun eski sürümünde kaydedilmiş düzen de yüklenir.
- Dışa aktarma görünen kolonları görünüm sırasıyla ve görünen satırları, her kolonun biçimiyle yazar; ayırıcı, tırnak ya da satır sonu içeren alanlar tırnaklanır. Buton, resim, açma ve işlem kolonları atlanır.

**Büyük tablolar (VirtualMode):** `TwVirtualRows<T>` grid'i VirtualMode'a alır ve satırları görüldükçe sayfa sayfa yükler; son kullanılan sayfalar önbellekte tutulur, milyon satırda yalnızca görünen sayfalar bellektedir:

```csharp
var rows = new TwVirtualRows<Order>(gridOrders, (start, count) => db.Orders.OrderBy(o => o.Id).Skip(start).Take(count).ToList(), pageSize: 100);
rows.Reset(db.Orders.Count());              // veri ya da sıralama değişince yeniden
```

- Hücre değeri kolonun `DataPropertyName`'indeki (yoksa adındaki) özellikten okunur; `rows.CellValue = (order, column) => ...` ile değiştirilir. `rows.GetItem(rowIndex)` satırın verisini verir.
- `loadPage` UI iş parçacığında çağrılır; hızlı olmalı (veritabanında OFFSET/FETCH). VirtualMode'da grid kendisi sıralayamaz: sıralamayı sorguda yapıp `Reset` çağırın. Kullanıcının satır eklemesi kapatılır.

`TwPager` sayfalama düğmeleri çizer (‹ 1 … 9 10 11 … 20 ›); veriyi sayfaya göre siz bağlarsınız:

```csharp
pager.TotalItems = orders.Count;
pager.PageSize = 20;
pager.PageChanged += (s, e) => grid.DataSource = orders.Skip(pager.FirstItemIndex).Take(pager.PageSize).ToList();
tw.SetClasses(pager, "text-gray-700 selected:bg-blue-600 selected:text-white item:hover:bg-gray-100");
```

### Varyantların uygulanma sırası

Sonraki, öncekini ezer: temel → `dark:` → `checked:` → `focus:` → `focus-visible:` → `group-hover:` → `hover:` → `dark:hover:` → `active:` → `disabled:` → `invalid:` → `sm:` → `md:` → `lg:` → `xl:` → `2xl:` → `forced-colors:` (Tailwind'deki gibi genişlik varyantları en sonda).
Kontrol devre dışıyken `focus`, `hover` ve `active` uygulanmaz.

## Tema dosyası (`tw.config.json`)

```jsonc
{
  "version": 1,                                          // dosya biçiminin sürümü (isteğe bağlı)
  "preset": "fluent",                                     // hazır görünüm; dosyanın geri kalanı üzerine uygulanır
  "fontFamily": "Segoe UI",
  "spacingUnit": 4,
  "colors": {
    "brand": { "500": "#2b579a", "600": "#234a85" },   // bg-brand-500
    "canvas": "#f3f6fb"                                   // bg-canvas
  },
  "components": {
    "btn-primary": "btn bg-brand-500 text-white hover:bg-brand-600"
  }
}
```

`fontFamily` virgülle ayrılmış bir yığın olabilir (`"Inter, Segoe UI"`): kurulu ya da kayıtlı ilk aile kullanılır. Uygulamayla dağıtılan fontlar `"fonts": ["Assets/fonts/Inter-Regular.ttf", "Assets/fonts/Inter-SemiBold.ttf"]` ile kurulum gerektirmeden kaydedilir (yol tema dosyasına göredir, bulunamayan dosya atlanır); kodda `TwFonts.Register(path)`. Fontlar hem GDI+ (`PrivateFontCollection`) hem GDI (`AddFontResourceEx`, süreç içi) tarafına kaydedildiğinden `TextRenderer` ile çizilen standart kontrollerde de çalışır. Font dosyalarının lisansını (örn. SIL OFL) uygulamayla birlikte dağıtın.

**Tailwind v4 paleti.** `"palette": "v4"` (kodda `new TwTheme(TwPaletteVersion.V4)` ya da `theme.UsePalette(...)`) aynı 22 rengin v4 tonlarını kullanır. v4 renkleri OKLCH olarak tanımlıdır (`TwPaletteV4`); sRGB'ye sığmayanlar tarayıcıların yaptığı gibi CSS Color 4 gamut eşlemesiyle yaklaştırılır, böylece `blue-500` Tailwind sitesindeki gibi `#2b7fff` olur. Tema dosyasındaki renkler `oklch(62.3% 0.214 259.8)` biçiminde de yazılabilir; kodda `TwOklch.TryParse`. Varsayılan bileşenler iki paletle de kontrast denetiminden geçer.

Dosya varsayılan palet ve bileşenlerin (ya da `preset` ile seçilen görünümün) üzerine eklenir. Bileşenlerin listesi için bkz. [Hazır görünümler](#hazır-görünümler-ve-autostyle) ve `TwTheme.DefaultComponents`; `autoStyle` anahtarı AutoStyle eşlemesini değiştirir. Designer önizlemesi ve listeleri de projenin tema dosyasını kullanır (bkz. [Designer](#designer)). `version` dosya biçiminin sürümüdür (bkz. [API kararlılığı](#api-kararlılığı)).

## Kontroller

| Kontrol | Ne zaman |
| --- | --- |
| `TwPictureBox` | Pürüzsüz yuvarlak köşeli resim (`rounded-lg`, `rounded-full` avatar); `object-cover` / `object-contain` / `object-fill`. |
| `TwSelect` | Stillenebilir açılır liste (native ComboBox'ın yerine): kutu temel class'larla, menü `menu:` / `item:` ile. Genişlik varsayılan olarak seçili öğeye göredir (`SizeToSelection`). Harf yazmak o harflerle başlayan öğeyi seçer; `Searchable = true` menünün üstüne öğeleri süzen bir arama kutusu ekler (büyük/küçük harf, aksan ve ı/i farkı gözetilmez). |
| `TwIcon` | Tek başına ikon (bkz. [İkonlar](#i̇konlar)). |
| `TwCheckBox`, `TwRadioButton`, `TwSwitch` | Bkz. [CheckBox, Radio, Switch](#checkbox-radio-switch). |
| Standart kontroller | Renk, yazı ve boşluk her kontrolde çalışır. Köşe `Region` ile kırpılır (antialias yok), kenarlık `Paint` olayında çizilir. `TextBox`'ta (`TextBoxBase`) native kenarlık kapatılır; `border-*` ve `rounded-*` ebeveynde, kontrolün 3 px dışına zemin rengiyle doldurulmuş bir çerçeve olarak çizilir (çerçeve kontrolün sınırlarından taşar, yerleşimde pay bırakın). `DropDownList` modundaki `ComboBox`'ın kapalı kutusu (zemin, kenarlık, köşe, metin, ok) FormWind tarafından çizilir; açılır liste native kalır. Düzenlenebilir `ComboBox`'ta ve diğer native kontrollerde (`ListBox` …) köşe ve kenarlık uygulanmaz; `TwSelect`, `TwListBox` kullanın. |
| `TwButton` | Pürüzsüz köşeli buton. `Image` henüz çizilmez, `Paint` olayı tetiklenmez. |
| `TwPanel` | Pürüzsüz köşeli, kenarlıklı panel (kart). |
| `TwTextBox` | Kenarlıksız TextBox'ı yuvarlak bir panel içine alır; `input` stili için. |
| `TwNumberBox` | Sayı kutusu (`TwTextBox` + sağda ▲▼). `Minimum`, `Maximum`, `Increment`, `DecimalPlaces`; ok tuşları, fare tekeri ve kültüre göre ayrıştırma. |
| `TwDatePicker` | Tarih alanı + açılır `TwCalendar`. Tarih yazılabilir (`Culture`/`Format`'a göre çözülür), F4 / Alt+↓ takvimi açar. Takvim `menu:bg-* menu:border-*`, `selected:bg-* selected:text-*`, `item:hover:bg-*` ile stillenir. |
| `TwCalendar` | Tek başına ay takvimi: kültürün ilk günü ve gün adları, bugün halkası, `MinDate`/`MaxDate`, klavye (oklar, PageUp/PageDown, Home/End, Enter, Esc). |
| `TwTabStrip` | Sekme şeridi: `Underline`, `Pills`, `Segmented`. `selected:text-* selected:border-*` / `selected:bg-*`, `item:hover:*`, `fill-*` (segmented zemini), `gap-*`. Yalnızca şerittir; sayfalı kullanım için `TwTabControl`. |
| `TwTabControl` | Sayfalı sekme kontrolü (TabControl yerine): `AddPage("Genel")` ya da `TwTabPage` ekleyin, seçili sayfa gösterilir. Şerit `Strip` üzerinden stillenir (`provider.SetClasses(tabs.Strip, "...")`); Ctrl+Tab / Ctrl+PageDown sayfalar arasında gezer. |
| `TwSlider` | Kaydırıcı (TrackBar yerine): `Minimum`, `Maximum`, `Value`, `SmallChange`, `LargeChange`. Ray `fill-*`, dolu kısım `accent-*`, tutamaç `item:bg-*`. Klavye, fare tekeri ve sürükleme. |
| `TwProgress` | İlerleme çubuğu (ProgressBar yerine): ray `fill-*`, dolgu `accent-*`, köşe `rounded-*`. `Indeterminate = true` süresi bilinmeyen iş için kayan bir parça çizer. |
| `TwDataList` | Sanal liste (bkz. aşağı). |
| `TwListBox` | ListBox yerine: `Items` (+ `DisplayMember`), `SelectedItem(s)`, `FindString`; TwDataList'in sanal çizimi, çoklu seçimi ve stilleriyle. |
| `TwCheckedListBox` | CheckedListBox yerine: satır başında yuvarlak onay kutusu (`accent-*`), `CheckedIndices`/`CheckedItems`, `ItemCheck`; kutuya tık ya da Boşluk. |
| `TwTreeView` | TreeView yerine sanal ağaç: `Nodes`, ikonlu düğümler, `SelectedNode`, `ExpandAll`; ←/→ ile aç/kapa, tembel yükleme için `HasLazyChildren` + `BeforeExpand`. Ekran okuyucuya Outline olarak görünür. |
| `TwPager` | Sayfalama (bkz. [DataGridView](#datagridview)). |
| `TwField` | Etiket + düzenleyici + ipucu/hata metni; doğrulama kuralları (bkz. [Formlar](#formlar-doğrulama-maske-ve-diyaloglar)). |
| `TwToolTip` | Stillenebilir ipucu: `Classes`; `Provider` verilirse koyu temayı izler. |

### TwDataList

Yalnızca görünen satırları çizen sanal liste; veri listede tutulmaz, satırlar dizinle istenir. Milyon satırda da çizim süresi görünen satır sayısına bağlıdır.

```csharp
list.ItemCount = orders.Count;
list.ItemHeight = 52;                       // 96 DPI pikseli
list.DrawItem += (s, e) =>
{
    var o = orders[e.Index];
    var c = e.ContentBounds;
    int left = e.DrawBadge(o.Status, c.Right, badgeBack, badgeFore);
    e.DrawText(o.Customer, new Rectangle(c.X, c.Y + 6, left - c.X - 12, 20), font: e.FontWithWeight(600));
    e.DrawText(o.Number, new Rectangle(c.X, c.Y + 26, left - c.X - 12, 20), e.MutedColor);
};
tw.SetClasses(list, "p-1 bg-white text-gray-900 item:hover:bg-gray-100 selected:bg-blue-50 selected:text-blue-900 "
                  + "row:border-gray-100 fill-gray-300 dark:bg-gray-800 dark:text-gray-100 dark:selected:bg-blue-950");
```

- `DrawItem` yoksa `ItemText` (ya da dizin) tek satır metin olarak çizilir.
- Fare tekeri yumuşak kaydırır (`SmoothScrolling`); kaydırma çubuğu owner-draw, sürüklenebilir ve `fill-*` ile renklenir.
- Klavye: ↑/↓, PageUp/PageDown, Home/End; Enter ve çift tık `ItemActivate`.
- `item:rounded-*` satır köşesi, `item:text-*` ikincil metin, `row:border-*` satır ayırıcısı, `ItemPadding` satır içi boşluk (`e.ContentBounds`).
- **Çoklu seçim:** `SelectionMode = MultiExtended`; Ctrl+tık ekler/çıkarır, Shift+tık ve Shift+ok aralık seçer, Ctrl+A tümünü seçer (milyon satırda da dizin tutmadan). `SelectedIndices`, `IsSelected`, `SetSelected`, `SelectionChanged`.
- **Gruplar:** `SetGroups(new[] { (0, "Bugün"), (12, "Dün") })`; kaydırırken üstteki grubun başlığı yapışık kalır. Başlık `header:bg-* header:text-*` ile ya da `DrawGroupHeader` olayıyla çizilir.
- **Farklı yükseklikte satırlar:** `ItemHeights = i => orders[i].HasNote ? 72 : 52` (96 DPI pikseli); satır başlangıçları bir kez hesaplanır, yükseklikler değişince `InvalidateItemHeights()`. Verilmezse bütün satırlar `ItemHeight` boyundadır ve milyon satırda ek maliyet yoktur; verildiğinde her satırın yüksekliği bir kez sorulur.
- **Parça parça yükleme:** `IsItemLoaded` yüklenmemiş satırları iskelet olarak çizer (hafifçe nabız atar) ve `ItemsNeeded(first, last)` görünür aralıkla tetiklenir; veri gelince `Invalidate()`.

## Grafikler

FormWind'in kendi çizdiği, ek bağımlılığı olmayan hafif grafikler: `TwChart` (çizgi, alan, çubuk), `TwDonutChart`, `TwSparkline` ve KPI kartı `TwStat`. Diğer kontroller gibi koyu temayı, DPI'ı ve temayı izler; `AutoStyle` ile `chart` ve `stat` bileşenlerini alır.

| Açık | Koyu |
| --- | --- |
| ![Grafikler, açık](docs/charts-light.png) | ![Grafikler, koyu](docs/charts-dark.png) |

Bu pano örnekte hazır: `FormWind.Showcase --charts` (bir aya, çeyreğe ya da dilime tıklamak `ItemClick` ile durum satırını günceller).

```csharp
chart.Kind = TwChartKind.Bar;                    // Line, Area, Bar; Stacked = true çubukları üst üste koyar
chart.SetData(new[] { "Oca", "Şub", "Mar" },
    new TwChartSeries("Gelir", new[] { 120.0, 150, 170 }),
    new TwChartSeries("Hedef", new[] { 130.0, 140, 160 }) { Kind = TwChartKind.Line });   // çubukların üstünde çizgi
chart.ValueFormat = "C0";                        // ipucu; eksen kısa sayılarla yazılır (1,2B), AxisFormat verilmedikçe

donut.SetData(new[] { "Web", "Mağaza", "Telefon" }, new[] { 52.0, 28, 20 });

stat.Title = "Gelir"; stat.Value = "₺1,24 Mn"; stat.Delta = 0.124; stat.Caption = "geçen aya göre";
stat.Trend = new[] { 820.0, 940, 910, 1040, 1240 };
stat.PositiveIsGood = false;                     // maliyet ve hata oranında artış kırmızı gösterilir
```

| Class | Etkisi |
| --- | --- |
| `bg-*`, `text-*`, font | Zemin, metin rengi (değerler, gösterge), font |
| `label:text-*` | Eksen etiketleri, gösterge değerleri, başlık ve açıklama (ikincil metin) |
| `divide-*` | Izgara çizgileri |
| `accent-*` | İlk serinin ve sparkline'ların rengi |
| `stat`: `border`, `rounded-*`, `p-*` | `TwStat`'ın kart görünümü |

- **Renkler** sabit, doğrulanmış bir kategorik paletten gelir (renk körlüğünde ayırt edilebilir, açık ve koyu tema için ayrı tonlar); seriler paleti sırayla alır, başka seriler eklenip çıkınca renklerini korur. Dokuzuncu seri yeniden kullanılan bir ton değil gri olur; `TwDonutChart` sekizden sonraki dilimleri `TwTexts.Other`'da birleştirir. `TwChartSeries.Color` ve `TwChartSlice.Color` rengi açıkça verir.
- **İşaretler:** çubuklar en çok 24 px genişlikte, ucu 4 px yuvarlak, tabanı köşeli; çubuklar, yığın segmentleri ve dilimler arasında 2 px boşluk; çizgiler 2 px; alanlar %10 opaklıkta; işaret yalnızca son noktada ve vurgulanan kategoride. Tek değer ekseni; çubuk ve alan grafikleri sıfırı içerir. `double.NaN` eksik değerdir (çizgide boşluk).
- **Gösterge:** iki ve daha çok seride görünür (`ShowLegend`); anahtar serinin işaretini taşır (çizgi için kısa çizgi, çubuk için kare). Metin hiçbir zaman seri rengini almaz.
- **Etkileşim:** fareyle ya da ok tuşlarıyla (Home/End) bir kategori vurgulanır ve her serinin değeri ipucunda görünür; Enter ya da tıklama `ItemClick`'i tetikler (`TwChartItemEventArgs.Index`, `Label`), `HoverIndexChanged` vurguyu izler. Halka grafik vurgulanan dilimi ortada gösterir.
- **Ekran okuyucular:** grafikler `Chart` rolündedir, her kategori ya da dilim bir öğedir (`"Mar: Gelir 170, Hedef 160"`, `"Web: 52 (52%)"`); grafiğin değeri aralıkların özetidir. `TwStat` "başlık: değer, değişim, açıklama" olarak okunur; değişim her zaman ok ve işaretle verilir, yalnızca renkle değil.
- `TwStat`'ın değişimi temanın `success` ve `danger` renklerini kullanır; hazır görünümler ve tema dosyaları onu da yeniden renklendirir.

## Formlar: doğrulama, maske ve diyaloglar

```csharp
var field = new TwField { LabelText = "T.C. kimlik no", Required = true, Validator = TwValidators.TcKimlikNo };
field.Controls.Add(new TwTextBox { Mask = TwMasks.TcKimlikNo });
tw.SetClasses(field, "field");                 // label:, hint:, error: parçaları
// Kaydet'te:
if (!tw.Validate(this)) return;                // hataları gösterir, ilk hatalı alana odaklanır
```

- **`TwField`**: etiket (zorunluysa " *"), tek bir düzenleyici ve altında ipucu ya da hata metni. Kurallar: `Required`, `Pattern` (düzenli ifade), `Validator` (özel), maskeli alanda tamlık. Etiket düzenleyicinin `AccessibleName`'i, hata metni `AccessibleDescription`'ı olur.
- **`tw.SetError(control, "mesaj")`**: elle hata (sunucudan gelen gibi). Hata varken kontrole `invalid:` varyantı uygulanır; `input` bileşeni kırmızı kenarlık ve halka içerir. Kullanıcı değeri değiştirince hata kendiliğinden temizlenir. `HasErrors`, `ClearErrors()`, `ErrorsChanged`.
- **Hazır kurallar** (`TwValidators`): `TcKimlikNo` (kontrol basamakları), `Iban` (mod 97), `Email`, `TurkishMobile`, `MinLength(n)`.
- **Maske** (`TwTextBox.Mask`): `0` rakam, `9` isteğe bağlı rakam, `L` harf; hazır maskeler `TwMasks.Phone`, `Iban`, `TcKimlikNo`, `Date`, `PostalCode`. Yalnızca uygun karakterler girilir, yapıştırma sabit karakterleri atlar. `UnmaskedText`, `MaskCompleted`.

```csharp
if (TwDialog.Show(this, "Sipariş silinsin mi?", "Silme onayı", TwDialogKind.Warning, TwDialogButtons.YesNo, danger: true) == DialogResult.Yes) …
TwToast.Show(this, "Sipariş kaydedildi", TwDialogKind.Success);
```

- **`TwDialog`** (MessageBox yerine): ikon, başlık, mesaj, sağa dayalı düğmeler; Enter varsayılan, Esc iptal/hayır. Sahip formun koyu temasını izler. Düğme metinleri [`TwTexts`](#yerelleştirme)'ten gelir, tek tek `TwDialog.Texts[DialogResult.Yes] = "Sil"` ile ezilebilir; `TwDialog.Create(...)` göstermeden özelleştirmeye izin verir.
- **`TwToast`**: sahip formun sağ alt köşesinde üst üste dizilen, odak çalmayan, fare üzerindeyken bekleyen ve kendiliğinden kapanan bildirim (`DefaultDuration` 4 sn). Ekran okuyucuya Alert olarak bildirilir.

## Yerelleştirme

Kullanıcıya görünen metinler (diyalog düğmeleri, doğrulama mesajları, grid süzme menüsü ve "yükleniyor" metni, arama kutusu ipucu, ekran okuyucu adları) `TwTexts` içindedir. Varsayılan dil İngilizcedir; uygulamanın kullanıcı arayüzü dili (`CultureInfo.CurrentUICulture`) Türkçeyse Türkçe metinler seçilir.

```csharp
TwTexts.Current = TwTexts.Turkish;                                   // dili zorla
TwTexts.Current = new TwTexts { Ok = "OK", Cancel = "Abbrechen", Required = "Pflichtfeld." };   // başka bir dil
TwTexts.Current.Loading = "Veriler alınıyor…";                       // tek bir metin
```

- Kontrollerin kendi metin özellikleri (`TwField.RequiredMessage`, `PatternMessage`, `IncompleteMessage`, `TwSelect.SearchPlaceholder`) atanmışsa `TwTexts`'in önüne geçer; atanmamışsa Designer.cs'e yazılmaz.
- Biçimli metinlerde `{0}` yer tutucuları kullanılır (`MinLength`: karakter sayısı, `Page`: sayfa ve sayfa sayısı).
- Grid süzmesinde boş değerlerin anahtarı dilden bağımsızdır (`TwSortableBindingList<T>.EmptyValue`); menüde `TwTexts.Blank` görünür.
- Geliştiriciye görünen metinler (istisnalar, Designer komutları, çözümleyici uyarıları, Debug çıktısı) İngilizcedir.

## Erişilebilirlik

Owner-draw kontroller ekran okuyuculara ve UI otomasyon araçlarına (Narrator, NVDA, FlaUI, Appium/WinAppDriver) öğeleriyle görünür:

| Kontrol | Rol | Öğeler |
| --- | --- | --- |
| `TwDataList` | List | Her satır ListItem; ad `AccessibleItemText` (yoksa `ItemText`, o da yoksa "Öğe N"). Sanal: öğe nesneleri istendiğinde üretilir. |
| `TwTabStrip` | PageTabList | Her sekme PageTab (seçili durumu ve varsayılan eylemi ile) |
| `TwCalendar` | Table | "Önceki ay" / "Sonraki ay" düğmeleri ve günler (ad: "15 Ekim 2026 Perşembe"; aralık dışı günler Unavailable) |
| `TwSelect` | ComboBox | Değer seçili öğe; varsayılan eylem menüyü açar |
| `TwSlider` / `TwProgress` | Slider / ProgressBar | Değer |
| `TwTextBox`, `TwNumberBox`, `TwDatePicker` | (içteki TextBox) | Ad ve açıklama sarmalayıcının `AccessibleName` / `AccessibleDescription` değerinden, yoksa ipucu metninden |

Seçim ve odak değişiklikleri ekran okuyuculara bildirilir. Kontrole `AccessibleRole` verilmişse o kullanılır. Görünen bir etiketi olmayan alanlara `AccessibleName` verin (bkz. `ControlsForm`). `TwTreeView` Outline/OutlineItem (açık/kapalı durumu ve düzeyiyle), `TwPager` ToolBar ("Sayfa 3 / 20"), `TwToast` Alert, `TwDialog` Dialog olarak görünür.

UI Automation testleri (`Category=UIAutomation`) listeyi, sekmeleri, açılır listeyi ve takvimi ekran okuyucuların kullandığı istemciyle dışarıdan doğrular. Bu istemci süreç genelinde ayar değiştirdiğinden (sonraki testlerde metin farklı çizilir) varsayılan çalıştırmada atlanır; ayrı çalıştırın:

```powershell
$env:FORMWIND_UIA = "1"; dotnet test tests/FormWind.Tests -f net472 --filter Category=UIAutomation
```

### Yüksek Karşıtlık modu

Windows'ta Yüksek Karşıtlık (Ayarlar → Erişilebilirlik → Karşıtlık temaları) açıkken FormWind class renkleri yerine sistem renklerini kullanır:

- Kontrollerin zemini ve yazısı özgün (sistem) değerlerine döner; seçili ve hover parçaları (`selected:`, `item:hover:`, `row:hover:`) vurgu rengini (`Highlight`) alır.
- Class'lardaki kenarlık ve ayırıcılar yazı rengiyle çizilir; gölge, gradient ve renk geçişleri çizilmez. Kenarlığı olmayan `TwButton`'lar 1 px kenarlık alır.
- Boşluk, yazı boyutu, köşe ve yerleşim değişmez. Mod uygulama çalışırken değişirse stiller yeniden uygulanır.
- Bu moda özel renkler `forced-colors:` varyantıyla verilir (Tailwind'deki gibi): `forced-colors:border-2`.
- Kapatmak için `TailwindProvider.RespectHighContrast = false`. Modun etkin olup olmadığı `HighContrastActive` ile okunur; `DrawItem` gibi uygulamanın kendi çizdiği yerlerde (örn. rozet renkleri) buna bakılmalıdır.
- Grid hücre tipleri (`TwBadgeColumn`, `TwProgressColumn` …) kendi renklerini korur.

### Kontrast denetimi

`TwContrast` bir formun o anki renklerini WCAG 2 kontrast oranlarına göre denetler. Testlerde ya da geliştirme sırasında temanın açık ve koyu hâlini doğrulamak için kullanılır:

```csharp
tw.DarkMode = true;
foreach (var issue in TwContrast.Audit(this, tw))
    Debug.WriteLine(issue);   // TwButton 'btnSave' hover: 2,54:1 < 4,5:1 (#FFFFFF on #60A5FA)
```

| Denetlenen | Bölüm adı |
| --- | --- |
| Metin taşıyan kontroller (Label, buton, TextBox, ComboBox, liste, ağaç, GroupBox …) | `text` |
| Class'lardaki `hover:` varyantı (provider verilirse; fareyi gezdirmeye gerek yok) | `hover` |
| ToolStrip ve menüler: şerit, öğe hover'ı, açılır menü, menü öğesi hover'ı | `strip`, `item:hover`, `menu`, `menu item:hover` |
| DataGridView hücreleri, seçili hücreler ve başlıklar | `cell`, `selected`, `header` |

- Eşik normal metinde 4,5:1, büyük metinde (18 pt ya da 14 pt kalın) 3:1 (WCAG AA). `minimum` parametresiyle değiştirilebilir.
- Saydam zeminler ebeveynin zeminiyle birleştirilerek hesaplanır. Gizli kontroller atlanır.
- Devre dışı kontroller WCAG'ın kapsamı dışında olduğundan varsayılan olarak atlanır. `includeDisabled: true` onları da denetler (bölüm adı `disabled text`); bu, devre dışı bir kontrolün içerik göstermek için kullanıldığı durumları yakalar (örn. imleç görünmesin diye devre dışı bırakılmış bir açıklama kutusu). Devre dışı native metin kutularında zemin Windows'un çizdiği sistem zemini sayılır. Harness denetimi bu seçenekle çalıştırır. Devre dışı butonlarda ve yazısını FormWind'in yeniden çizdiği onay kutularında ekrana çizilen yazı rengi 3:1 eşiğiyle denetlenir: amaç kontrast değil, görünmez yazıyı yakalamak.
- `transition` geçişi süren renklerde varılacak renk denetlenir; denetim tema değişiminin hemen ardından çağrılabilir.
- `issue.Cause` bilinen nedeni verir: `KeptBackground`, class'lar yalnızca yazı rengini verip zemin uygulamanın açıkça atadığı (ebeveyninkinden farklı) renkte kaldığında; çözüm bir `bg-*` class'ı ya da [kopyalanan zeminlerin](#hazır-görünümler-ve-autostyle) ebeveyni izlemesidir. `ToString()` nedeni de yazar.
- `TwContrast.Ratio(ön, zemin)` iki renk arasındaki oranı verir.
- `TwContrast.FindOwnerDrawn(root)` renklerini uygulamanın kendisi çizen kontrolleri listeler (`CellPainting` aboneli ya da `OnPaint`'i ezen grid'ler, `OwnerDraw`/`DrawMode` açık liste, ağaç, sekme ve açılır kutular). Denetim bunların ekrandaki renklerini bilemez; koyu temada elle bakılmalıdır. Çizim kodu renkleri `tw.GetColors(…)` ile alabilir.
- Varsayılan bileşenler (`btn-primary`, `btn-ghost`, `card`, `input`, `table`) açık ve koyu temada bu denetimden geçer (`ContrastTests`). `btn-primary` koyu temada da `blue-600`/`blue-700` kullanır; beyaz yazı `blue-500` üzerinde 3,7:1, `blue-400` üzerinde 2,5:1 ile eşiğin altında kalır.

## Hata yönetimi

Stil uygulanırken çıkan bir hata uygulamayı düşürmez: istisna yakalanır, kontrol o ana kadar uygulanan stille kalır ve hata `TailwindProvider.StyleError` olayıyla bildirilir (dinleyici yoksa Debug çıktısına yazılır). Özel bir `IStyleApplier`'ın ya da `SetRowClasses` kuralının fırlattığı istisnalar bu yolla raporlanır; gölge, kenarlık ve ComboBox çizimindeki hatalar da çizimi durdurmaz.

```csharp
tw.StyleError += (s, e) => logger.LogWarning(e.Exception, "{Target} stillenemedi: {Classes}", e.Target, e.Classes);
```

- Dinleyici istisnayı yeniden fırlatabilir (örn. testlerde hataları görünür tutmak için).
- Bellek yetersizliği gibi kritik istisnalar yakalanmaz.
- Hatalı kontrol bir sonraki durum değişikliğinde (hover, tema …) yeniden denenir.

## Sağdan sola (RTL)

Kontrolün kalıtılan `RightToLeft` değeri `Yes` ise:

- `rtl:` ve `ltr:` varyantları yöne göre uygulanır (Tailwind'deki gibi): `pl-4 rtl:pl-0 rtl:pr-4`. Yön sonradan değişirse stiller yeniden uygulanır.
- `TwPanel` flex ve grid yerleşimi yatayda aynalanır (CSS'teki `dir="rtl"` gibi): ilk öğe sağdadır, `justify-start` sağa yaslar. Kenar boşlukları da aynayla yer değiştirir (`ml-*` başlangıç tarafı gibi davranır).
- `TwButton` (ikon metnin öbür yanına geçer, metin hizası aynalanır), `TwCheckBox`, `TwRadioButton`, `TwSwitch` (gösterge sağda, açık konum solda) ve `TwSelect` (ok solda) aynalanır.
- Formda `RightToLeftLayout = true` ise Windows pencereleri kendisi aynalar; FormWind ayrıca aynalamaz.
- Henüz aynalanmayanlar: `TwNumberBox`, `TwDatePicker`, `TwSlider`, `TwProgress`, `TwTabStrip` (sekme sırası), `TwDataList`, `TwTreeView`, `TwCalendar`, `DropDownList` ComboBox çizimi. `ps-*`/`pe-*` gibi mantıksal boşluk class'ları yok; `rtl:` ile yazılır.

## .NET 9+ renk modu (`Application.SetColorMode`)

.NET 9 ve sonrasında `Application.SetColorMode(SystemColorMode.Dark)` sistem renklerini ve native kontrolleri koyulaştırır. FormWind bununla birlikte çalışır:

- Class renkleri sistem renklerinin önüne geçer; class'lar kaldırılınca kontrol koyu sistem rengine döner.
- `dark:` varyantları için `DarkMode` uygulamanın renk moduna bağlanmalıdır: `tw.DarkMode = Application.IsDarkModeEnabled;`
- Bu davranış .NET 9 ve 10'da `ColorModeTests` ile doğrulanır (süreç geneline etki ettiğinden ayrı çalışır):

```powershell
$env:FORMWIND_COLORMODE = "1"; dotnet test tests/FormWind.Tests -f net10.0-windows --filter Category=ColorMode
```

## Krypton Toolkit

FormWind, Krypton Toolkit kullanan uygulamalarda iki yoldan çalışır; ikisi birlikte de kullanılabilir.

| Krypton + FormWind (Fluent) | Krypton + FormWind (Material, koyu) |
| --- | --- |
| ![Fluent](docs/krypton-fluent.png) | ![Material koyu](docs/krypton-material-dark.png) |

**1. Tema köprüsü: class yazmadan.** `TwKryptonTheme` FormWind temasından bir Krypton paleti üretir ve Krypton'un genel paleti yapar. Class'ı olmayan bütün Krypton kontrolleri seçili görünümü (`preset`) ve koyu temayı izler:

```csharp
using FormWind.Krypton;

KryptonApplier.Register();                                   // Program.Main içinde
var palette = TwKryptonTheme.Follow(tailwindProvider1);      // ana formda; kapanırken palette.Dispose()
```

Palet, temanın bileşenlerinden kurulur: `page` → form ve panel zemini, form başlık çubuğu; `card` → başlık grupları ve kontrol zeminleri; `btn-ghost` → butonlar (`btn-primary` → işaretli `KryptonCheckButton`); `input` → metin kutuları, açılır listeler, tarih ve sayı kutuları; `list` → liste öğeleri; `table` → `KryptonDataGridView`; temanın fontu. Sağlayıcının teması ya da `TwTheme.Current` değişince veya `DarkMode` açılıp kapanınca palet yeniden kurulur. Palet Krypton'un Microsoft 365 White (açık) ya da Black dark mode (koyu) paletinden başlar; FormWind'in eşlemediği parçalar (ribbon, gezgin sekmeleri, Krypton'un menü çizimi) o paletin renklerinde kalır. Sabit paleti kendiniz yönetmek isterseniz `TwKryptonTheme.CreatePalette(theme, dark)` bir `KryptonCustomPaletteBase` döndürür.

**2. Kontrol başına class'lar.** `KryptonApplier` class'ları Krypton'un durum paletlerine yazar: temel → `StateCommon`, `hover:` → `StateTracking`, `active:` → `StatePressed`, `focus:` → `OverrideFocus` (giriş kontrollerinde `StateActive`), `disabled:` → `StateDisabled`, `checked:` → `StateChecked*`.

| Kontrol | Parçalar |
| --- | --- |
| `KryptonButton`, `KryptonCheckButton` | `checked:` (işaretli durumlar) |
| `KryptonPanel`, `KryptonLabel`, `KryptonLinkLabel` | |
| `KryptonCheckBox`, `KryptonRadioButton` | yazı rengi ve fontu |
| `KryptonTextBox`, `KryptonMaskedTextBox`, `KryptonRichTextBox`, `KryptonNumericUpDown`, `KryptonDateTimePicker` | odak ve fare üzeri → `StateActive` |
| `KryptonComboBox` | `item:` (öğeler, `item:hover:`), `menu:` (açılır liste zemini) |
| `KryptonGroupBox` | `caption:` (başlık) |
| `KryptonHeaderGroup` | `header:` (birincil başlık) |
| `KryptonListBox`, `KryptonCheckedListBox` | `item:`, `item:hover:`, `selected:` |
| `KryptonForm` | `caption:` (başlık çubuğu) |
| `KryptonDataGridView` | aşağıya bakın |

- Sabit boyutlu butonlarda padding, yazı sığacak kadar kısılır (Krypton yazıyı `…` ile kırpmasın diye); `rounded-full` köşe yarıçapı kontrol yüksekliğinin yarısıyla (palette 16 px ile) sınırlanır.
- `KryptonDataGridView` yapısal olarak (satır yüksekliği, ızgara çizgileri, `row-header:hidden`, sıralama oku) FormWind'in DataGridView uygulayıcısıyla stillenir. Krypton hücreleri ve başlıkları kendi paletiyle çizdiği için renkler ayrıca palete yazılır: temel class'lar ve `cell:` → veri hücreleri, `header:` → kolon başlıkları, `row-header:` → satır başlıkları (yoksa `header:`), `selected:` → seçili hücreler, `bg-*` → boş alan, `divide-*` → hücre kenarlıkları. Kendi uygulayıcınızda aynı yolu izlemek için `TwAppliers.FindBuiltIn(control)` FormWind'in yerleşik uygulayıcısını verir.
- AutoStyle Krypton tiplerini varsayılan olarak eşlemez (köprü onları zaten stiller); isterseniz tema dosyasında ekleyin: `"autoStyle": { "KryptonButton": "btn-ghost" }`.

**Sürümler.** `FormWind.Krypton` Krypton Toolkit 100.25.11.328 ile derlenir (paketin bağımlılığı bu sürüm ve üstü) ve CI'da hem bu sürümle hem 105.26.7.201 ile test edilir. .NET Framework uygulamalarında daha yeni bir Krypton kullanılıyorsa derleme yönlendirmeleri (binding redirect) gerekir; SDK biçimli projeler bunları kendisi üretir.

**Örnek.** `samples/FormWind.Krypton.Showcase` (net8.0-windows): Krypton kontrolleriyle kurulu, görünüm seçicili bir form. `--preset material`, `--dark`; `--snapshot çıktı.png` görüntü alır. Şablonda: `dotnet new formwind-app --Krypton true`.

## Diğer kontrol kütüphaneleriyle kullanım

Kendi skin ya da tema motoru olan kontrol kütüphaneleri (ticari kontrol süitleri dahil) kontrollerini kendileri çizer. FormWind tanımadığı kontrollere standart WinForms uygulayıcısını uygular: boyut, boşluk ve yerleşim genellikle çalışır; renkler, durum varyantları (`hover:`, `focus:` …) ve köşeler ise kütüphanenin çizimiyle çakışabilir ya da yok sayılabilir. Bu kütüphanelerle FormWind üç şekilde kullanılabilir:

- **Karma kullanım:** Kütüphanenin güçlü olduğu bileşenler (gelişmiş grid'ler, ribbon, zamanlayıcı …) kendi motorunda kalır; FormWind standart kontrollerde, yeni ekranlarda ve form iskeletinde kullanılır. Kütüphanenin kontrolleri de bir `TwPanel` flex/grid yerleşiminin öğesi olabilir.
- **Uygulayıcı yazmak:** `IStyleApplier` uygulayan ve `TwAppliers.Register` ile kaydedilen bir sınıf, class'ları kütüphanenin kendi görünüm nesnelerine aktarır; temel stil ve her varyant kütüphanedeki karşılığına (normal, üzerinde, basılı, odaklı, devre dışı) eşlenir. [Krypton Toolkit](#krypton-toolkit) entegrasyonu bu yolun örneğidir. Çoğu skin motorunda renklerin birebir uygulanması için ilgili kontrolde skin'in kapatılması (düz stile geçilmesi) gerekir; yoksa renkler skin tarafından ezilir ya da yalnızca ton olarak kullanılır. `DataGridView`'den türeyen grid'lerde yapısal stil `TwAppliers.FindBuiltIn` ile FormWind'e bırakılabilir; kendi grid'i olan kütüphanelerde eşlemenin tümü uygulayıcıda yapılır. Uygulayıcı türleri deneyseldir (FW9001, bkz. [API kararlılığı](#api-kararlılığı)).
- **Yalnızca tema senkronu:** Kütüphanenin kontrollerine class verilmez; `DarkMode` değişince kütüphanenin açık/koyu skin'ine de geçilir ve istenirse `tw.config.json` renkleri kütüphanenin paletine aktarılır. Her kontrol kendi motoruyla çizilir, iki taraf aynı renkleri kullanır.

Lisanslı kütüphanelere yönelik uygulayıcılar bu repoda yer almaz; uygulamanın kendi projesinde tutulmaları önerilir. FormWind'in mevcut bir uygulamadaki standart kontrollere etkisi kaynak koduna dokunmadan [harness](#gerçek-uygulamalarla-deneme-harness) ile görülebilir.

## DPI

Önerilen kurulum: formlarda `AutoScaleMode = AutoScaleMode.Dpi` ve `AutoScaleDimensions = new SizeF(96F, 96F)` (kodla kurulan formlarda yapıcıyı `SuspendLayout`/`ResumeLayout` arasına alın) ve uygulamayı `PerMonitorV2` çalıştırın. `samples/FormWind.Showcase/ControlsForm.cs` bu şekilde kuruludur; %125'te (120 DPI) içerik ölçeklenip sığar. Ölçümler:

| Durum | Sonuç |
| --- | --- |
| DPI-unaware (4.7.2 varsayılanı) | Windows bitmap olarak büyütür; yerleşim doğru, metin bulanık. |
| PerMonitorV2 + `AutoScaleMode.Dpi` | Yerleşim, font ve FormWind boşlukları birlikte ölçeklenir. |
| PerMonitorV2 + `AutoScaleMode.Font` | Kodla kurulan formlar ve (.NET Framework'te) fontu `AutoScaleDimensions`'tan sonra atanan Designer formları ölçeklenmedi; boşluklar ve fontlar büyüdü, kontroller büyümedi. |

- 4.7.2'de PerMonitorV2 `app.config` içindeki `System.Windows.Forms.ApplicationConfigurationSection` (`DpiAwareness`) ile açılır. Bu dosyayı elle düzenlerken derlemenin ürettiği `bindingRedirect`'leri silmeyin; `System.Text.Json` bağımlılıkları yüklenemez ve uygulama açılışta düşer.
- .NET Framework'te WinForms DPI ayarını ilk `Control` oluşturulurken uygular; GDI+ ise ekran DPI'ını başladığı anda bir kez okur. Bundan önce bir `Font` oluşturulursa (örn. tema yüklenirken) GDI+ 96 DPI'da kalır ve native kontrollerin (TextBox) fontları küçük çizilir. FormWind GDI+'ya dokunmadan önce WinForms'un DPI başlatmasını tetikler; kendi kodunuzda `Program.Main` içinde form oluşturmadan önce `Font` üretiyorsanız önce `new Control().Dispose()` çağırın.
- PerMonitorV2 yalnızca uygulama manifestinde Windows 10 uyumluluğu (`supportedOS`) bildirildiyse açılır; yoksa süreç sessizce system-aware kalır.
- **Ekran değişimi (%150, %200):** form başka DPI'lı bir ekrana taşınınca FormWind class'ları WinForms ölçeklemeyi bitirdikten sonra bir kez yeniden uygular. `AutoScaleMode.Dpi` ile kurulan formlarda fontlar, boşluklar, owner-draw kontroller ve DataGridView (font ve kolon genişlikleri) doğru ölçeklenir; bu `ControlsForm` ve bir grid formu için %150 ve %200'de doğrulandı.
- **`AutoScaleMode.Font` ve .NET Framework:** Designer formun `Font`'unu `AutoScaleDimensions`'tan sonra yazar. .NET Framework WinForms, PerMonitorV2 açıkken bu font atamasında ölçeklemeyi atlar ve `AutoScaleDimensions`'ı ölçeklenmiş gibi günceller: kontroller 96 DPI boyutunda kalır, fontlar (ve FormWind boşlukları) ekran DPI'ıyla büyür; yazılar kırpılır, grid kolonları dar kalır. Bu FormWind'siz de olur (aynı form `Font` önce atanınca doğru ölçeklenir) ve .NET 8'de yoktur. Çalışma anında düzeltilemez: ezilen değerden formun hangi DPI'da tasarlandığı anlaşılamaz. **FW002** çözümleyicisi bu formları uyarır; çözüm `AutoScaleMode.Dpi` ve `AutoScaleDimensions = new SizeF(96F, 96F)`.
- Font modundaki formlarda .NET Framework ekran değişiminde fontları olay tetiklemeden değiştirir; `TwTextBox`'ın iç kutusu silinmiş font handle'ını tutup yazıyı sistem fontuyla küçük çiziyordu. `TwTextBox` artık formun `DpiChanged` olayında iç fontu yeniler (`Text_boxes_keep_a_valid_native_font_after_moving_to_200_percent`).
- Test: `Controls_*_at_150/200_percent` görsel testleri vitrini ayrı bir PerMonitorV2 sürecinde açıp WM_DPICHANGED ile ölçekler (`FormWind.Showcase.exe --snapshot x.png --controls --dpi 192`; yalnızca PerMonitorV2 yapılandırmasıyla etkili). Referans adı sürecin sistem DPI'ını da içerir.

Piksel değerleri kontrolün `DeviceDpi / 96` oranıyla ölçeklenir; class'lardaki değerler 96 DPI birimidir (`p-2` %125'te 10 px). Uygulamanın kendisinin DPI'a göre ölçeklemediği kontrollerde (örn. AutoScale'i çalışmayan UserControl'ler) bu, özgün değerlerden farklı sonuç verir.

- **Formun AutoScale'i:** `InitializeComponent` içindeki `SetClasses` ölçeklenmiş değeri atar; formun AutoScale'i (sondaki `ResumeLayout`) aynı padding'i bir kez daha ölçeklerdi (%125'te 8 → 10 → 12 px). Kendi `AutoScaleMode`'u olan iç içe UserControl'lerde bu ölçekleme handle oluştuktan sonra da olur. FormWind bunu iki yolla önler:
  - Class'lar padding ya da margin tanımlıyorsa değer değiştiğinde (`PaddingChanged`/`MarginChanged`) FormWind'in değeri geri yazılır.
  - Class'lar kontrolün ilk handle'ı oluşunca (ölçeklemeden sonra, `Load`'dan önce) bir kez yeniden uygulanır; boyut ve font da düzelir.
- **Sonuç:** class'lar padding/margin'i yönettiği sürece uygulama kodunun bu özelliklere çalışma anındaki atamaları geri alınır (FW001 bu atamaları uyarır); uygulamanın değeri için class'tan padding/margin'i kaldırın. Class'lar kaldırılınca izleme durur. Renklerde tersi geçerlidir: uygulamanın ataması [korunur](#uygulama-kodunun-atadığı-renkler). Designer'da izleme yapılmaz. Ekran değişiminde WinForms'un ölçeklemesi bir an geri alınır, ardından DPI yenilemesi yeni ölçekle uygular.
- Doğrulama: Git Extensions'ın dönüştürülmüş formlarında %125'te özgün uygulamayla karşılaştırıldı; kalan farklar 1 px yuvarlama ve uygulamanın hiç ölçeklemediği kontrollerden geliyor.
- Ölçeklenen pikseller WinForms gibi yuvarlanır (.5 yukarı).

Uygulama DPI-aware (örn. `PerMonitorV2`) çalışıyor ama formların AutoScale'i yerleşimi büyütmüyorsa boşluklar büyür, kontroller büyümez ve metinler sığmaz. Bu durumda AutoScale'i düzeltin ya da `TailwindProvider.ScaleWithDpi = false` yapın. DPI-unaware uygulamalarda (çoğu 4.7.2 projesinin varsayılanı) Windows ölçeklemeyi kendisi yapar ve ek ayar gerekmez.

## Designer

- `TwTabControl`: "Sayfa ekle" / "Seçili sayfayı kaldır" komutları ve akıllı etiket (görünüm seçimi), tasarım zamanında sekmeye tıklayınca sayfa değişir; kontrolün içine yalnızca `TwTabPage` bırakılabilir, diğer kontroller sayfaya bırakılır. Araç kutusundan bırakılan kontrol iki sayfayla başlar.
- **Akıllı etiket:** formda bir `TailwindProvider` varken her kontrolün akıllı etiketinde (standart kontroller dahil) bir FormWind bölümü görünür: `Classes` alanı, `Component` listesi, kontrol tipine uygun bileşenler (buton → `btn-primary`/`btn-ghost`/`btn-danger`, metin kutusu → `input`, panel → `card`, grid → `table`; temada tanımlıysa) ve "Class'ları temizle". Bileşen seçmek yalnızca mevcut bileşenin yerine geçer, diğer class'lar korunur (`btn-ghost px-6` → `btn-primary px-6`). Değişiklikler `Classes` özelliği üzerinden yapıldığından Designer.cs'e yazılır ve geri alınabilir.
- **Class oluşturucu:** akıllı etiketin alt bölümlerinde gruplara ayrılmış açılır listeler vardır: renkler (arka plan, yazı, kenarlık), boşluk ve şekil (padding, kenarlık, köşe), yazı (boyut, kalınlık), hover ve koyu tema (`hover:bg-*`, `dark:bg-*`, `dark:text-*`). Her alan yalnızca kendi grubundaki class'ları okur ve değiştirir; `px-4` seçmek `px-2`, `pl-1` gibi ezdiği class'ları kaldırır, `py-2`'yi korur. Alanı boşaltmak o grubun class'larını siler. Tanınmayan class'lar akıllı etikette uyarı olarak listelenir. Renk listeleri temanın kendi renkleriyle (`brand-500` …) başlar; serbest metin de yazılabilir.
- **Önizleme ve Designer.cs:** önizlemenin (`DesignTimePreview`) kontrole uyguladığı ve class'ların yönettiği özellikler (BackColor, ForeColor, Font, Padding, Margin, Cursor) `Designer.cs`'e yazılmaz; class'ların yönetmediği, elle atanmış değerler yazılmaya devam eder.
- `TwTabControl` tasarımcısı .NET Framework ve .NET 8 için derlenir; .NET 8'in süreç dışı Designer'ında tasarımcı sunucu sürecinde çalışır (komutlar `DesignSurface` testleriyle iki hedefte doğrulanır).
- **.NET 8+ (süreç dışı) Designer:** akıllı etiket, class oluşturucu, açılır listeler ve önizleme iki Designer'da da çalışır (hepsi `TypeConverter`'lı özellikler ve `DesignerActionList` üzerindedir). Yalnızca `Classes` alanının "…" penceresi (`ClassesEditor`; çok satırlı düzenleme) .NET Framework Designer'ı ile sınırlıdır. .NET 8'de `Classes` alanı temadaki bileşenleri öneren bir açılır listedir (`ClassesConverter`); liste mevcut bileşenin yerine geçen hali gösterir (`btn-primary px-6`), serbest metin de yazılabilir.
- **Tema dosyası:** paketle gelen `build/FormWind.targets`, proje klasöründeki `tw.config.json` dosyasını uygulamanın derlemesine gömer. Designer tasarlanan formun derlemesini bulur; derleme klasöründeki `tw.config.json` dosyasını (fontlar ve SVG ikonlarla birlikte), yoksa gömülü kopyayı okur. Böylece önizleme, bileşen ve renk listeleri projenin temasıyla gelir. Tema dosyası değişince projeyi derleyip formu yeniden açın. Proje henüz derlenmemişse varsayılan tema kullanılır. Dosya başka bir yerdeyse `<FormWindThemeFile>yol/tw.config.json</FormWindThemeFile>`, gömmeyi kapatmak için `<FormWindEmbedTheme>false</FormWindEmbedTheme>`. Çalışma zamanı dosyayı yine `TwTheme.LoadIfExists` ile yükler; gömülü kopyayı yalnızca Designer okur. Aynı Visual Studio sürecinde farklı temalı birden çok proje açıksa sağlayıcıya bağlı olmayan çizimler (ikonlar, grid kolonları) son açılan formun temasını kullanır.

### FW001 çözümleyicisi

Paketle gelen Roslyn çözümleyicisi, class'ların yönettiği bir özelliğe kodda değer atanınca uyarır:

```csharp
tw.SetClasses(btnSave, "btn-primary");
btnSave.BackColor = Color.Red;   // FW001: 'btnSave' has classes; assigning 'BackColor' conflicts with them. …
```

Çalışma zamanında renk ataması kazanır: FormWind o renk kanalını uygulamaya bırakır (bkz. [Uygulama kodunun atadığı renkler](#uygulama-kodunun-atadığı-renkler)); durum rengi gibi bilinçli atamalarda uyarı bastırılabilir. Padding ve margin atamaları ise geri alınır, class kazanır (bkz. [DPI](#dpi)); bu durumda uyarı gerçek bir çakışmayı gösterir.

Yalnızca class metni sabit olan çağrılar ve class'ların gerçekten yönettiği özellikler denetlenir (`bg-*` → BackColor, `text-{renk}` → ForeColor, `text-{boyut}` → Font, `p-*` → Padding, `m-*` → Margin, `cursor-*` → Cursor; varsayılan bileşenler dahil). `font-sans` ya da `font-semibold` varken temel `Font` ataması uyarı vermez. Kapatmak için `.editorconfig`'te `dotnet_diagnostic.FW001.severity = none`.

### FW002 çözümleyicisi

.NET Framework projelerinde, `SetClasses` kullanan bir `InitializeComponent` `AutoScaleMode.Font` kuruyor ve formun `Font`'unu `AutoScaleDimensions`'tan sonra atıyorsa uyarır (bkz. [DPI](#dpi)): PerMonitorV2'de form ölçeklenmez. Hedef çerçeve `build_property.TargetFramework`'ten, o yoksa referans verilen WinForms'un sürümünden anlaşılır. Uygulama PerMonitorV2 çalışmıyorsa uyarı `.editorconfig`'te `dotnet_diagnostic.FW002.severity = none` ile kapatılabilir.

## Animasyonlar ve responsive varyantlar

- Sekme göstergesi (alt çizgi, hap, bölme) yeni sekmeye kayar; liste satırlarının hover zemini yumuşakça belirir; iskelet satırlar (liste ve grid) hafifçe nabız atar. `TwMotion.Enabled` Windows'un "animasyonları göster" ayarını izler, kodla kapatılabilir; tasarım zamanında ve görünmeyen kontrollerde animasyon yapılmaz.
- `sm:`, `md:`, `lg:`, `xl:`, `2xl:` varyantları formun istemci genişliğine göre uygulanır (640 / 768 / 1024 / 1280 / 1536 px, 96 DPI; `TwBreakpoints` ile değiştirilebilir). Form yeniden boyutlanıp eşik değişince yalnızca bu varyantları kullanan kontroller yeniden uygulanır:

```csharp
tw.SetClasses(pnlForm, "flex flex-col gap-4 md:flex-row lg:gap-8");
tw.SetClasses(lblTitle, "text-lg lg:text-2xl");
```

## Proje yapısı

```
src/FormWind/             Çekirdek: parser, tema, durum motoru, WinFormsApplier, Tw* kontrolleri, Designer editörü
src/FormWind.Krypton/     KryptonApplier, TwKryptonTheme
src/FormWind.Analyzers/   FW001 Roslyn çözümleyicisi (FormWind paketine eklenir)
tests/FormWind.Tests/     xUnit testleri (net472, net8.0, net9.0, net10.0-windows)
samples/FormWind.Showcase/ Vitrin uygulaması (net472)
samples/FormWind.Krypton.Showcase/ Krypton Toolkit ile örnek (net8.0-windows)
tools/FormWind.Convert/   Designer dosyalarını class'lara çeviren komut satırı aracı (net8.0-windows)
tools/FormWind.Harness/   Gerçek uygulamalarda deneme için startup hook ve harness (net8.0-windows)
tools/FormWind.Docs/      Önizlemeli class referansı sitesini üretir (net8.0-windows)
website/                  Tanıtım ve belge sitesi, Türkçe ve İngilizce (statik HTML; website/build.ps1)
templates/                dotnet new şablonu (formwind-app)
tools/run-harness.ps1     Bir .NET WinForms uygulamasını harness'le çalıştırıp log özetini yazar
.github/workflows/ci.yml  Derleme, test, paket ve görsel çizimlerin yapıt olarak yüklenmesi
```

## Geliştirme

Katkı kuralları, test kategorileri, public API ve sürümleme politikası: [CONTRIBUTING.md](CONTRIBUTING.md). Kodun yapısı (class dizgisinden ekrana giden yol, modüller, tasarım kararları): [ARCHITECTURE.md](ARCHITECTURE.md). Değişiklikler: [CHANGELOG.md](CHANGELOG.md). Güvenlik açıkları: [SECURITY.md](SECURITY.md).

CI'daki adımların tamamı (derleme, dört hedefte test, kapsam denetimi, paketleme) tek komutla çalışır; `-Quick` yalnızca derleyip .NET 8'de test eder. SDK sürümü `global.json` ile sabitlenmiştir (.NET 10 SDK):

```powershell
.\build.ps1
```

Tek tek komutlar:

```bash
dotnet build FormWind.sln
```

```bash
dotnet test FormWind.sln
```

Performans ve GDI testleri (`Category=Performance`) varsayılan çalıştırmada atlanır (Skipped). Toplam ~1 dakika sürerler ve süreç genelindeki GDI sayaçlarını ölçtükleri için diğer testlerle paralel çalışmazlar. Çalıştırmak ve ölçümleri görmek için `FORMWIND_PERF=1` ortam değişkenini ayarlayın (PowerShell):

```powershell
$env:FORMWIND_PERF = "1"; dotnet test FormWind.sln --filter Category=Performance --logger "console;verbosity=detailed"
```

Visual Studio Test Explorer'da çalıştırmak için değişkeni Visual Studio'yu açmadan önce sistemde tanımlayın.

### Public API

`src/FormWind/PublicAPI.Shipped.txt` ve `PublicAPI.Unshipped.txt` kütüphanenin public yüzeyini listeler. Listede olmayan bir public üye eklenirse ya da listedeki bir üye kaldırılırsa derleme uyarı verir (RS0016/RS0017); yeni üyeler `Unshipped` dosyasına eklenir, sürüm yayımlanınca `Shipped` dosyasına taşınır. Kod düzeltmesiyle toplu ekleme:

```bash
dotnet format analyzers src/FormWind/FormWind.csproj --diagnostics RS0016 --severity warn
```

`src/FormWind.Krypton`'ın kendi dosya çifti vardır. Yalnızca kütüphanenin kendi kullandığı yardımcılar (çizim, parser, iç uygulayıcılar, Designer türleri) `internal`'dır; genişletme noktaları (`IStyleApplier`, `TwApplyContext`, `TwStyle`, `ITwChrome`) public'tir, henüz değişebilecek olanlar deneysel olarak işaretlidir (bkz. [API kararlılığı](#api-kararlılığı)).

### Kod kapsamı

Kapsam, test SDK'sıyla gelen "Code Coverage" toplayıcısıyla ölçülür (net472 ve net8.0-windows; yalnızca `FormWind` ve `FormWind.Analyzers`):

```powershell
dotnet test tests/FormWind.Tests -f net8.0-windows --settings tests/coverage.runsettings --collect "Code Coverage" --results-directory TestResults/net8.0-windows
```

```powershell
.\tools\check-coverage.ps1 -Results TestResults -Minimum 0.80
```

CI her iki hedefin raporunu iş akışı özetine tablo olarak yazar ve `FormWind` satır kapsamı %80'in altına düşerse düşer (şu an ~%85–87). Coverlet, WinForms çerçeve derlemelerini çözemediği için kullanılmaz.

### Değişmez testleri

`InvariantTests` rastgele bir kontrole (16 tip: standart kontroller, ToolStrip, DataGridView, `Tw*` kontrolleri) rastgele bir işlem dizisi uygular: class atama, koyu tema, devre dışı bırakma, hover, ebeveyn rengini değiştirme, uygulamanın renk ataması, handle'ı yeniden oluşturma. Her adımdan sonra renklerin çözülen stile (ya da uygulamanın atadığı renge) eşit olduğu, sonda class'lar kaldırılınca özgün değerlerin döndüğü denetlenir.

Varsayılan çalıştırmada 300 senaryo çalışır. Senaryolar seed'lidir; hata mesajı seed'i ve işlem dizisini yazar:

```powershell
$env:FORMWIND_INVARIANT_RUNS = "3000"; dotnet test tests/FormWind.Tests --filter FullyQualifiedName~InvariantTests
```

```powershell
$env:FORMWIND_INVARIANT_SEED = "162"; dotnet test tests/FormWind.Tests --filter FullyQualifiedName~InvariantTests
```

Bu testlerin bulduğu her hata için `ApplierTests` içinde ayrıca açık bir regresyon testi tutulur; seed'ler üretim değişirse aynı senaryoyu vermeyebilir.

### Gerçek uygulamalarla deneme (harness)

`tools/run-harness.ps1` bir .NET (Core) WinForms uygulamasını **kaynak kodunu değiştirmeden** FormWind'le çalıştırır. `DOTNET_STARTUP_HOOKS` ile yüklenen `FormWind.Harness`, açılan her forma kontrol tipine göre class'lar uygular (butonlara `btn-ghost`, metin kutularına açık/koyu renkler, ToolStrip'lere şerit renkleri …) ve sonuçları kaydeder:

```powershell
.\tools\run-harness.ps1 -Exe C:\apps\MyApp.exe -SelfTest -ExitAfter 60
```

| Parametre / ortam değişkeni | Etki |
| --- | --- |
| `-Dark` / `FORMWIND_DARK=1` | Koyu temayla başlar. Çalışırken **Ctrl+Alt+Shift+D** temayı değiştirir. |
| `-SelfTest` / `FORMWIND_SELFTEST=1` | Her yeni formun açık ve koyu temada görüntüsünü alır ve kontrastını hem class renklerinden hem görüntüden denetler. |
| `-ExitAfter 60` / `FORMWIND_EXIT_AFTER` | Verilen saniye sonra süreci sonlandırır (gözetimsiz çalıştırma). Süre arka plan iş parçacığında tutulur; UI zamanlayıcıları dursa da süreç sonlanır. |
| `-Skip DataGridView,ToolStrip` / `FORMWIND_SKIP` | Bu tipleri stillemez; bir sorunu bir kontrol tipine indirmek için. |
| `FORMWIND_DISABLE=1` | Harness'i kapatır; aynı senaryoyu FormWind'siz karşılaştırmak için. |
| `-Dump` / `FORMWIND_DUMP=1` | Hiç stil uygulamaz; her formun kontrollerinin renk, font ve padding değerlerini `dump-<form>.txt` dosyasına yazar. Dönüştürme aracından önce ve sonra çalıştırılıp karşılaştırılır. |
| `FORMWIND_CLOSE=FormSettings` | Adı verilen formları işlendikten sonra kapatır; açılıştaki modal pencerelerin arkasındaki formlara gözetimsiz ulaşmak için. |
| `FORMWIND_WALK=Launch` | İlk formdaki bu yazıya sahip butonlara sırayla basar; açılan her formu işler, kapatır ve sonrakine geçer; bitince süreci sonlandırır (örn. bir demo uygulamasının bütün örnekleri). `FORMWIND_WALK_WAIT` form başına bekleme (ms, varsayılan 4000), `FORMWIND_WALK_SKIP=33,40` atlanacak butonların sırası. |
| `FORMWIND_OPEN=SettingsForm,AboutForm` | Bu form tiplerini yüklü derlemelerde bulup parametresiz constructor'la sırayla açar, işler ve kapatır; menüden ya da tepsiden açılan formlara gözetimsiz ulaşmak için (test amaçlı yansıma). Kapanmayı reddeden formlar zorla dispose edilir. |
| `-Skip '*'` / `FORMWIND_SKIP=*` | Hiçbir kontrolü stillemez ama ölçer; aynı senaryonun FormWind'siz karşılaştırması için (donma, çizim sayısı, uygulama hataları). |

Log ve görüntüler `%TEMP%\FormWind-Harness\<süreç>-<zaman>\` altına yazılır:

| Satır | Anlamı |
| --- | --- |
| `FORM` | Form, stillenen kontrol sayısı ve süre; `unstyled` tanınmayan kontrol tipleri |
| `ADDED` | Sonradan eklenen ve stillenen kontroller |
| `CONTRAST` | Kontrast denetiminin bulduğu çiftler (açık ve koyu tema) |
| `SCREEN` | Self-test görüntüsünde 3:1'in altında kalan yazılar: kontrolün alanında en sık renk zemin, zemine en zıt piksel yazı sayılır. Class renklerinin göremediğini yakalar (Windows'un çizdiği devre dışı yazı, kendi paletiyle çizen kontroller); ölçüm yazı yumuşatması nedeniyle yaklaşıktır |
| `OWNERDRAW` | Renklerini uygulamanın kendisi çizen kontroller (`TwContrast.FindOwnerDrawn`): kontrast denetimi bunları ölçemez, class'ların hücre/öğe renkleri yok sayılabilir; koyu temada elle bakılmalı |
| `UI STALL` | UI iş parçacığının 250 ms'den uzun donması |
| `PAINT STORM` | 3 saniye üst üste saniyede 100'den fazla çizim: çizim sırasında yeniden geçersiz kılma döngüsü |
| `TIMER STARVATION` | UI iş parçacığı mesaj işliyor ama WinForms zamanlayıcıları 2 saniyedir çalışmıyor: Windows `WM_TIMER`'ı kuyrukta başka mesaj yokken üretir, her `OnPaint`'te `Invalidate` eden bir pencere (örn. ShareX'in bölge yakalaması) onu süresiz bekletir. Bu sürede harness yeni formları bulamaz ve `PAINT STORM` ölçülemez; zamanlayıcılar dönünce `TIMER STARVATION ended` yazılır |
| `UI BLOCKED` | UI iş parçacığı 2 saniyedir hiç mesaj işlemiyor; bitince süresi `UI STALL` olarak da yazılır |
| `EXCEPTION`, `UNKNOWN` | Stil uygulanırken çıkan hata; tanınmayan class |

- Startup hook yalnızca .NET (Core) uygulamalarında vardır; trim edilmiş ya da hook'u kapatılmış uygulamalarda yüklenmez. .NET Framework uygulamalarında projeye `FormWind.Harness`'teki `FormWindHarness.cs` (`LangVersion` `latest`) ve FormWind'in net472 derlemesi eklenir, `Main`'in başında `FormWindHarness.Install()` çağrılır. `Install` mesaj döngüsü yokken pencere oluşturmaz (uygulamanın sonraki `SetCompatibleTextRenderingDefault` çağrısı düşerdi); kurulum ilk form açılınca yapılır. FormWind'in bağımlılıkları için projede `AutoGenerateBindingRedirects` açılmalıdır.
- Pencerelerini ayrı UI iş parçacıklarında açan uygulamalarda (Open Live Writer her düzenleyiciyi ve açılış ekranını) harness her iş parçacığına ayrı kurulur: her iş parçacığı kendi formlarını tarar, donma ve zamanlayıcı açlığı iş parçacığı başına ölçülür, tema kısayolu hepsine geçer.
- Uygulamanın hata diyaloğu (`ThreadExceptionDialog`) açılınca `APP EXCEPTION` satırının altına hatanın ilk satırları yazılır.
- Harness yalnızca içerik taşıyan konteynerlere (form, panel, GroupBox, sekme, SplitContainer, UserControl, ToolStrip konteynerleri) iner. Bileşik kontrollerin iç parçaları (örn. `KryptonTextBox`'ın içindeki `TextBox`, `PropertyGrid`'in yardım paneli, `NumericUpDown`'ın düzenleme kutusu) kontrolün kendisine aittir ve stillenmez.
- Self-test görüntüleri üst düzey pencerelerde `PrintWindow` ile alınır: başlık çubuğu, `RichTextBox` ve MDI çocukları ekrandaki gibi çıkar, pencere başka bir pencerenin altında kalsa da çalışır. MDI çocuklarında (`PrintWindow` üstteki kardeş pencereleri de yakaladığından) ve `PrintWindow` başarısız olursa `DrawToBitmap` kullanılır; orada başlık çubuğu Windows'un çizdiği gibi görünmez ve `RichTextBox` yanlış çizilir.
- Formlar bir zamanlayıcıyla bulunur; uygulama WinForms ya da WPF mesaj döngüsüyle çalışsa da (örn. Greenshot) olur. Class'lar form gösterildikten sonra uygulandığından açılışta kısa bir an stilsiz görünebilir; o anda uygulamanın atadığı renkler özgün değer sayılır.
- Bir sorun bulunduğunda önce `FORMWIND_DISABLE=1` ile FormWind'siz tekrarlayın; sorun yine oluyorsa uygulamanındır. Sonra `-Skip` ile sorunu bir kontrol tipine indirin.
- Kendi tema motoru olan uygulamalar ebeveynin zeminini çoğu zaman her çocuğa açıkça yazar (örn. ShareX). Harness, form stillenmeden önce zemini ebeveyninkiyle aynı olan çocukların `BackColor`'ını sıfırlar; böylece yalnızca yazı rengi verilen etiketler ve checkbox'lar ebeveynin yeni zeminini izler (log'da `inherited`). Sıfırlanınca ebeveynden farklı bir varsayılana dönen kontroller (örn. TextBox) eski renklerini korur.
- Denenen uygulamalar: Git Extensions (.NET 10, startup hook), Greenshot (.NET Framework 4.8, WPF döngüsü), Greenshot 1.3 (.NET Framework 4.7.2, WinForms döngüsü, harness kod olarak), mRemoteNG (.NET 10, DockPanelSuite), Krypton Toolkit örnekleri (.NET 9: grid, giriş formu, ribbon, docking), ScottPlot WinForms demosu (.NET 10, 45 demo, `FORMWIND_WALK` ile), bir MDI kabuğu (.NET 8: birleşen menüler, pencere listesi, büyütülmüş çocuk, sonradan açılan çocuklar), KeePass 2.61.1 (.NET Framework 4.8, harness kod olarak), Open Live Writer (.NET Framework 4.7.2, harness kod olarak, birden çok UI iş parçacığı; C++ Ribbon olmadan yalnızca açılış ekranı ve sihirbaz), ShareX 19.0.2 (.NET 9, kendi koyu teması, `FORMWIND_OPEN` ile 8 form; sonraki sürümlerin arayüzü Avalonia'dır). Bulunan sorunlar bu README'deki davranışlara ve testlere dönüştü. Kendi tema motoru olan uygulamalarda (mRemoteNG) uygulamanın atadığı renkler korunduğu için koyu tema kısmen görünür; bu beklenen davranıştır.

### Görsel regresyon testleri

`tests/FormWind.Tests/Visual` vitrin formlarını (`MainForm`, `ControlsForm`) ve takvimi açık/koyu temada çizip `tests/FormWind.Tests/Baselines` içindeki PNG'lerle karşılaştırır (yalnızca net472). Bir pikselin RGB farkı toplamı 48'i aşarsa farklı sayılır; oran %0,2'yi geçerse test düşer ve çıktı klasöründeki `VisualDiffs` altına `*.actual.png` ile kırmızı işaretli `*.diff.png` yazılır.

Font çizimi ortama bağlı olduğundan referanslar ortam başına ayrı klasörde durur: `Baselines/win26200-96dpi-cleartype/` (Windows sürümü, DPI, font yumuşatma). Bu ortamın klasörü yoksa testler atlanır. Bilerek yapılan görsel değişikliklerden sonra referansları yeniden üretin ve PNG farklarını gözden geçirerek commit'leyin:

```powershell
$env:FORMWIND_UPDATE_BASELINES = "1"; dotnet test tests/FormWind.Tests -f net472 --filter Category=Visual; $env:FORMWIND_UPDATE_BASELINES = ""
```

**CI.** `.github/workflows/ci.yml` her push ve PR'da derler, dört hedefte (net472, .NET 8, 9, 10) test eder, kapsamı denetler, paketler. `FORMWIND_VISUAL_EXPORT` ayarlı olduğundan görsel testler çizimlerini `visual-snapshots` yapıtına yazar; CI ortamının referansı henüz yoksa testler yalnızca dışa aktarır ve iş akışı bir uyarı yazar. CI `windows-2022` imajına sabitlidir; imaj değişirse referans klasörünün adı da değişir. Yapıttaki `win…` klasörünü `tests/FormWind.Tests/Baselines/` altına ekleyip commit'lemek, o ortam için karşılaştırmayı açar.

### Web sitesi ve belge sitesi

`website/` tanıtım sayfasını ve belgeleri (başlangıç, hazır görünümler, class'lar, yerleşim, kontroller, DataGridView, grafikler, Krypton, Designer, DPI, erişilebilirlik, araçlar, performans, API kararlılığı) içerir. Statik HTML'dir, derleme aracı gerekmez; `website/build.ps1` sayfaları ortak şablona yerleştirip `docs/` altındaki ekran görüntüleriyle birlikte `artifacts/website`'a yazar. `-Reference` ile aşağıdaki class referansını da `reference/` altına üretir. Site İngilizce (kök) ve Türkçedir (`tr/`): her sayfanın `website/en/` ve `website/tr/` altında aynı adlı bir karşılığı vardır, başlıktaki düğme sayfanın diğer dildeki karşılığına gider; ortak metinler `website/strings.json`'dadır. README'deki bir davranış değiştiğinde iki dildeki ilgili sayfa da güncellenmelidir.

```powershell
.\website\build.ps1 -Reference
```

`tools/FormWind.Docs`, her örneği gerçek FormWind ile ekran dışında çizip (PNG; bileşen ve kontrollerde açık ve koyu tema) önizlemeli bir class referansı üretir; renk paleti (v3 ve v4) ve ikon adları da sitededir. Site İngilizcedir:

```bash
dotnet run --project tools/FormWind.Docs -c Release -- artifacts/docs-site
```

Örnekler `tools/FormWind.Docs/Catalog.cs` içinde tanımlıdır. `.github/workflows/docs.yml` web sitesini class referansıyla birlikte her push'ta üretip `website` yapıtı olarak yükler; GitHub Pages'e yayımlamak için depo ayarlarında Pages kaynağını "GitHub Actions" yapıp `DEPLOY_DOCS=true` depo değişkenini tanımlayın.

### Dönüştürme aracı

Mevcut formlardaki sabit stil atamalarını class'lara çevirir:

```bash
dotnet run --project tools/FormWind.Convert -- src/MyApp --report donusum.md
```

```bash
dotnet run --project tools/FormWind.Convert -- src/MyApp --write
```

| Designer satırı | Class |
| --- | --- |
| `BackColor` / `ForeColor` | `bg-*` / `text-*`: paletteki renk, en fazla 10 birim farkla en yakın palet rengi (not düşülür), değilse `bg-[#hex]` |
| `Font` | `text-sm` … `text-4xl` ya da `text-[13px]`; `Semibold`/`Bold` ailesi ya da `FontStyle.Bold` → `font-semibold`/`font-bold`; `italic`, `underline` |
| `Padding` | `p-*`, `px-*`/`py-*` ya da kenar bazlı (4 px = 1 birim; `[7px]`) |
| `FlatAppearance` | `BorderSize` → `border-0`/`border`, `BorderColor` → `border-*`, `MouseOverBackColor` → `hover:bg-*`, `MouseDownBackColor` → `active:bg-*` |

- Varsayılan kuru çalıştırmadır: ne değişeceğini ve notları yazar. `--write` dosyaları yerinde günceller; önce commit'leyin. Dosyaların BOM'u korunur.
- Çevrilen satırlar silinir, yerine `SetClasses` yazılır; kontrolün mevcut `SetClasses` çağrısı varsa class'lar ona eklenir. Dosyada `TailwindProvider` yoksa eklenir ve `components` kapsayıcısına eklenerek formla birlikte dispose edilir. Sınıfta `components` alanı yoksa eklenir; `Dispose` onu çağırmıyorsa rapora not düşülür.
- Visual Studio'nun yeni Designer'ının `this.` yazmadığı kod (`buttonOk.BackColor = ...`) ve yerel değişken olarak üretilen kontroller (`GenerateMember = false`) desteklenir; kontrol kaynakta nasıl yazılmışsa `SetClasses`'a öyle verilir.
- Yalnızca `InitializeComponent`'te oluşturulan **Control**'ler çevrilir. ToolStrip öğeleri, kolonlar ve yerel hücre stilleri (`dataGridViewCellStyle1`) `SetClasses` alamaz; satırları kalır. Uygulamanın kendi tipleri, hedef klasör ve üst klasöründeki (kardeş projeler) `class X : Y` bildirimleri izlenerek bir WinForms tipine çözülür (örn. `ToolStripComboBox`'tan türeyen bir sınıf çevrilmez); çözülemeyenler notla çevrilir.
- Eski Designer biçimi (`this.` önekli, süslü parantez metotla aynı satırda, sekmeyle girinti) de desteklenir.
- **Visual Basic:** `.Designer.vb` dosyaları da çevrilir (klasör verilince ikisi birlikte aranır; dil dosyanın içeriğinden anlaşılır). Çıktı VB sözdizimindedir: `Me.TailwindProvider1.SetClasses(btnSave, "...")`, alan `Friend WithEvents TailwindProvider1 As FormWind.TailwindProvider`; alan yoksa `End Class`'tan önce eklenir. `10.5!` biçimindeki font boyutları, `CType(CType(37, Byte), Integer)` ve `CByte(37)` renk bileşenleri, `Me.` önekli ve öneksiz (yeni Designer) satırlar tanınır. Uygulamanın kendi tipleri `Class X` / `Inherits Y` bildirimlerinden çözülür. `dotnet new winforms -lang VB` ile oluşturulup stillenen bir formda dönüştürülen proje uyarı ve hata olmadan derlendi, renk ve fontlar `-Dump` karşılaştırmasında birebir aynı çıktı.
- Sistem renkleri (`SystemColors.*`), uygulamanın kendi renk ifadeleri (`AppColor.X.GetThemeColor()`), türetilmiş fontlar (`new Font(Font, FontStyle.Bold)`) ve temadakinden farklı font aileleri çevrilmez; satır kalır ve raporda not edilir.
- **Gerçek projede doğrulama (Greenshot 1.3, .NET Framework 4.7.2):** dönüştürülen proje Visual Studio MSBuild'iyle uyarı ve hata olmadan derlendi; 7 formun 311 kontrolünde renk ve fontlar birebir aynı, padding farkları 1 px (DPI yuvarlaması).
- **Gerçek projede doğrulama:** Git Extensions'ın 74 Designer dosyasında 360 class üretildi; sonuç uyarı ve hata olmadan derlendi (uyarıları hata sayan, StyleCop kullanan bir projede). Harness'in `-Dump` moduyla dönüşümden önce ve sonra alınan değerlerde 270 kontrolün renk ve fontları birebir aynıydı; padding farkları DPI davranışından geliyordu ve iki kütüphane hatasını ortaya çıkardı (çift ölçekleme; bkz. [DPI](#dpi)).
- `--config tw.config.json` uygulamanın kendi paletini kullanır; `--tolerance` en yakın renk eşiğini değiştirir.

Vitrin formunun ekran görüntüsü (`--controls` kontroller formunu açar):

```bash
samples/FormWind.Showcase/bin/Debug/net472/FormWind.Showcase.exe --snapshot light.png
```

## API kararlılığı

FormWind ön sürümdedir (0.x): minor sürümler kırıcı değişiklik içerebilir; bunlar [değişiklik günlüğünde](CHANGELOG.md) *Changed* ya da *Removed* altında listelenir. Üç sözleşme ayrı tutulur:

- **Public API:** `TailwindProvider`, kontroller, `TwTheme`, hazır görünümler, olaylar ve class'lar. Her public üye izlenir (bkz. [Public API](#public-api)); 1.0'dan sonra değişiklikler [Semantic Versioning](CONTRIBUTING.md#versioning) kurallarına uyar.
- **Class söz dizimi:** bir class'ın anlamı derleyicinin yakalayamadığı bir sözleşmedir ve testlerle sabitlenir: `tests/FormWind.Tests/Golden/parse.txt` yaklaşık 70 örnek class dizisinin çözümlenmiş stilini (varyantları, parçaları, hazır görünümleri ve v4 paletiyle) tutar; biri değişirse `GoldenParseTests` düşer. Bilinçli bir değişiklik `FORMWIND_UPDATE_BASELINES=1` ile yazılır ve değişiklik günlüğüne eklenir.
- **Tema dosyası:** `tw.config.json` içindeki `"version"` dosya biçiminin sürümüdür (`TwTheme.FileVersion`, şu an 1; alan yoksa 1 sayılır). Daha yeni bir biçim için yazılmış dosya yanlış okunmak yerine `NotSupportedException` ile reddedilir.

**Deneysel genişletme noktaları (FW9001).** `IStyleApplier`, `TwApplyContext`, `TwAppliers`, `WinFormsApplier`, `TwStyle`, `TwParsed`, `TwOriginal`, `TwTheme.Parse` ve `TwStyle` alan yardımcılar (`TwFonts.Create`, `TwChrome.FromStyle`, `TwGradient.FromStyle`, `TwLayout.Apply`, `TwToolTip.ResolveStyle`) System.Drawing ve WinForms türlerine bağlıdır; platformdan bağımsız çekirdek ayrıldığında değişebilirler. Kullanımları, tanılama gizlenene kadar derleme hatasıdır (FW9001):

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);FW9001</NoWarn>
</PropertyGroup>
```

## Paketleme

```bash
dotnet pack FormWind.sln -c Release
```

Paketler `artifacts/packages` klasörüne yazılır (`FormWind`, `FormWind.Krypton` ve `.snupkg` sembol paketleri). `FormWind` paketi FW001 çözümleyicisini `analyzers/dotnet/cs` altında içerir. Paketler SourceLink bilgisi taşır; hata ayıklayıcı FormWind'in kaynağına GitHub'dan girebilir. Derlemeler strong-name ile imzalıdır (`PublicKeyToken=1cea47bdc82de2bf`, anahtar depodaki `FormWind.snk`); imzalı .NET Framework uygulamaları da paketlere başvurabilir. NuGet sayfalarında `docs/nuget/` altındaki kısa İngilizce README'ler kullanılır. Yerel kullanım için bu klasörü Visual Studio'da Package Sources'a ekleyin. `v*` etiketi itildiğinde `.github/workflows/release.yml` paketleri nuget.org'a gönderir ve GitHub Release oluşturur (bkz. [CONTRIBUTING](CONTRIBUTING.md#releasing)).

## Bilinen sınırlamalar ve yapılacaklar

- [x] Önizlemede uygulanan renk, font ve boşluklar `Designer.cs`'e yazılmaz (class'ların yönettiği özellikler serileştirilmez)
- [x] Smart tag ile hızlı bileşen seçimi (standart kontroller dahil)
- [x] Mevcut formlardaki sabit renk ve font atamalarını class'a çeviren dönüştürme aracı (`tools/FormWind.Convert`)
- [x] Flex ve grid yerleşimi (`TwPanel`)
- [x] DataGridView uygulayıcısı ve `table` bileşeni
- [x] Font kalınlıkları (`font-thin` … `font-black`) ve ikonlar (`TwIcon`, `TwButton.IconName`)
- [x] Gölge, kenar bazlı kenarlık, focus halkası, renk saydamlığı
- [x] `TwCheckBox`, `TwRadioButton`, `TwSwitch`; menü renderer'ı; başlık çubuğu
- [x] Geçişler, gradient, `group-hover`, placeholder
- [x] Koyu kaydırma çubukları; DataGridView kolon class'ları, hücre tipleri ve satır kuralları
- [x] Uygulamayla dağıtılan özel fontlar (`fonts`, font yığını)
- [x] `TwTabStrip`, `TwNumberBox`, `TwToolTip`, `TwDatePicker`/`TwCalendar`, `TwDataList`
- [x] DPI ölçümleri ve görsel regresyon testleri
- [x] SVG ikonlar, Windows 11 zemini (`backdrop-*`)
- [x] Erişilebilirlik (owner-draw kontrollerin öğeleri), `focus-visible:`
- [x] `TwTabControl`, `TwSlider`, `TwProgress`, aranabilir `TwSelect`
- [x] Grid: sıralama oku, `TwSortableBindingList`, boş/yükleniyor durumu, `TwCheckColumn`, `TwButtonColumn`
- [x] PerMonitorV2'de native kontrol fontlarının küçük kalması (GDI+ başlatma sırası)
- [x] CI ve ortam başına görsel referanslar
- [x] %150/%200 DPI doğrulaması ve ekran değişiminde yeniden ölçekleme (`AutoScaleMode.Dpi` formları)
- [x] Formlar: `TwField`, doğrulama, `invalid:`, maske, `TwDialog`, `TwToast`
- [x] `TwDataList` çoklu seçim, gruplar, parça parça yükleme; grid süzme menüsü, kolon seçimi, `TwPager`
- [x] `TwListBox`, `TwCheckedListBox`, `TwTreeView`
- [x] `TwTabControl` Designer desteği; FW001 çözümleyicisi
- [x] Sekme kayması, hover belirmesi, iskelet nabzı; responsive varyantlar (`sm:` … `2xl:`)
- [x] WCAG kontrast denetimi (`TwContrast`), koyu native ipuçları (`DarkToolTips`)
- [x] Değişmez testleri; gerçek uygulamalarda deneme aracı (`tools/run-harness.ps1`)
- [ ] Kendi çizen kontroller hücre stillerini yok sayar; koyu temada açık kalabilirler (owner-draw grid'ler: `TwGrid.EnableDarkInversion`)
- [x] Grafikler: `TwChart` (çizgi, alan, çubuk, yığılmış), `TwDonutChart`, `TwSparkline`, `TwStat`
- [x] Grid: arama, aralık süzmesi, gruplama, alt toplam satırı, satır detayları, düzen kaydetme, CSV'ye aktarma, yoğunluk varyantları; sparkline, tarih, sayı, bağlantı, etiket ve işlem kolonları
- [x] Grid için `row-span`, `grid-rows`; farklı yükseklikte liste satırları (`ItemHeights`); DataGridView VirtualMode yardımcısı (`TwVirtualRows<T>`)
- [x] .NET 8+ Designer: akıllı etikette class oluşturucu, bileşeni koruyarak değiştirme, Designer'da projenin teması
- [ ] .NET 8+ out-of-process Designer için `ClassesEditor` penceresinin Designer SDK ile taşınması
- [x] Hazır görünümler (Tailwind, Fluent, Ant Design, Material), anlamsal renkler ve `AutoStyle`
- [x] Krypton: CheckBox, RadioButton, CheckButton, giriş kontrolleri, ComboBox, GroupBox, HeaderGroup, ListBox, KryptonForm; temayı izleyen palet (`TwKryptonTheme`), sürüm matrisi, örnek ve `--Krypton` şablon seçeneği
- [x] Lisans: MIT

## Lisans

FormWind [MIT lisansı](LICENSE) ile dağıtılır. Üçüncü taraf bileşenler (Tailwind CSS renk paleti, test fontu): [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
