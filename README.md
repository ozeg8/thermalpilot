# ThermalPilot

**Windows için Türkçe sıcaklık ve performans izleyicisi.**

CPU ve RAM kullanımını, CPU/GPU sıcaklıklarını ve çalışan işlemleri tek arayüzde izleyin. Geri alınabilir güç modlarıyla Windows güç ayarlarını yönetin; PE/COFF dosyalarını çalıştırmadan inceleyin.

Bu depo **1.3** sürümünün kaynak kodunu ve kullanıma hazır Windows paketini içerir.

**[ThermalPilot 1.3 — Windows x64 ZIP indir](downloads/ThermalPilot-1.3-Windows-x64.zip?raw=true)**

[Kurulum](#kurulum) · [Özellikler](#özellikler) · [Kaynaktan derleme](#kaynaktan-derleme) · [Teknik belgeler](#teknik-belgeler)

## Kurulum

1. Yukarıdaki bağlantıdan uygulama ZIP'ini indirin.
2. Arşivin tamamını bir klasöre çıkarın.
3. `ThermalPilot.exe` dosyasını açıp Windows yönetici iznini onaylayın.

GitHub'ın **Code → Download ZIP** seçeneği kaynak kodunu indirir. Çalıştırılabilir uygulama için yukarıdaki indirme bağlantısını kullanın.

| Gereksinim | Açıklama |
| --- | --- |
| İşletim sistemi | Windows x64 |
| Ana uygulama | .NET Framework 4.7.2 veya üzeri |
| Varsayılan sensör yardımcısı | .NET 10 Windows Desktop Runtime x64 |
| Yetki | Uygulamanın açılması için yönetici erişimi |
| CPU sıcaklığı | Desteklenen donanım ve PawnIO sürücüsü |

Uygulama runtime bileşenlerini otomatik kurmaz. GPU sıcaklığı, destekleyen ekran sürücülerinde Windows WDDM üzerinden bağımsız okunur; NVIDIA için mevcut `nvidia-smi` aracı da kullanılabilir.

Sensör paketi EXE'ye gömülür; uygulama açılışta paket indirmez ve ayrı LibreHardwareMonitor arayüzü açmaz. Yönetici erişimi Windows tarafından açılışta istenir. Sürücü kurma işlemi, `Sensörler ve REA` sekmesinde kullanıcının seçimi ve onayıyla başlatılır. Eksik veya geçersiz sıcaklıklar ölçüm olarak gösterilmez.

## Özellikler

- Yaklaşık üç saniyede yenilenen CPU/RAM, işlem listesi ve sıcaklık grafikleri.
- CPU Package/Tdie/Tctl ve GPU Core sensörleri arasında öncelikli seçim; eski, boş veya sıfır ölçümleri reddetme.
- Sessiz / Serin, Dengeli ve Performans modları; önceki ayarlara geri dönme.
- İşlem önceliğini düşürme ve yapılan değişiklikleri geri alma.
- REA'dan uyarlanan okuyucularla PE/COFF başlıklarını dosyayı çalıştırmadan inceleme.

## Performans modları

Modlar, onaydan sonra mevcut Windows güç planının geçici bir kopyasını uygular.

| Mod | CPU üst sınırı (priz/pil) | Boost (priz/pil) | EPP (priz/pil) |
| --- | --- | --- | --- |
| Sessiz / Serin | %70 / %50 | Kapalı / kapalı | 80 / 90 |
| Dengeli | %100 / %100 | Açık / açık | 50 / 75 |
| Performans | %100 / %100 | Agresif tercih / açık | 0 / 25 |

CPU alt sınırı %5'tir. Bu değerler sabit frekans, CPU kullanım sınırı veya FPS artışı garantisi değildir. Desteklenmeyen isteğe bağlı ayarlar açıklanır; zorunlu ayarlar uygulanamazsa mod etkinleştirilmez. Performans modu ısıyı, fan sesini ve pil tüketimini artırabilir.

`Önceki ayarları geri al` önceki plana döner. Çıkışta geri alma varsayılan olarak açıktır. Kullanıcı Windows'tan başka bir plan seçmişse bu seçim korunur. Fan eğrisi, voltaj ve BIOS ayarları değiştirilmez.

## Kaynaktan derleme

Windows PowerShell ve Windows'un .NET Framework C# derleyicisi kullanılır. Derleme sırasında SDK kurulmaz veya dosya indirilmez. Gerekli sensör arşivi `vendor/LibreHardwareMonitor.NET.10.zip` içinde depoya dahildir; SHA-256 değeri derlemeden önce kontrol edilir.

Yalnızca derlemek için depo ana dizininde PowerShell açın:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Derleme ve regresyon testleri:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Çıktı: `dist/ThermalPilot.exe`. Test raporu: `self-test-results.json`.

Belgeler ve üçüncü taraf lisanslarıyla dağıtım ZIP'i oluşturmak için:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package
```

Bu komut `ThermalPilot-1.3-Windows-x64.zip` dosyasını üretir ve GitHub'daki indirme bağlantısı için aynı dosyanın `downloads/` kopyasını günceller. `-SensorArchive` ile hash'i doğrulanan resmî LibreHardwareMonitor 0.9.6 .NET Framework arşivi verilirse sensörler ana işlemde çalışır ve .NET 10 gereksinimi kalkar; bu alternatif gerçek donanımla doğrulanmamıştır.

İsteğe bağlı arayüz ve gerçek sensör kontrolleri:

```powershell
Start-Process .\dist\ThermalPilot.exe -ArgumentList '--ui-smoke-test', ('"' + "$PWD\verification-1.3" + '"') -Wait -WindowStyle Hidden
Start-Process .\dist\ThermalPilot.exe -ArgumentList '--sensor-report', ('"' + "$PWD\sensor-results.json" + '"') -Wait -WindowStyle Hidden
```

Test komutları da aynı EXE'yi çalıştırdığından yönetici erişimi gerektirir ve Windows UAC onayı isteyebilir; sürücü kurmaz. Regresyon testleri aktif Windows güç planını değiştirmez; işlem önceliği testi yalnızca kendisinin oluşturduğu alt işlemi kullanır. Sensör raporu CPU ve GPU ölçümü başarılıysa `0`, kısmi donanım sonucunda `2` döndürür. Regresyon testleri tüm donanımlarda çalışma veya performans artışı kanıtı değildir. Ayrıntılar: [sensör mimarisi](SENSOR-DIAGNOSIS.md) ve [güç modu doğrulaması](POWER-MODE-VALIDATION.md).

## Yerel veriler

Kayıtlar `%LOCALAPPDATA%\ThermalPilot` altında tutulur:

- `sensor-latest.json`: son sensör ölçümü ve tanılar.
- `sensor-events.jsonl`: sağlayıcı durum değişiklikleri; 1 MiB sonrasında döndürülür.
- `sensors/`: EXE'den çıkarılan sensör dosyaları.
- `changes.json`: geri alınabilir değişiklikler.

Ölçümler ve raporlar yerel kalır. Yerel raporlar, test ekran görüntüleri ve ara derleme çıktıları Git takibine alınmaz. Paylaşılacak hazır dağıtım ZIP'i `downloads/` altında tutulur.

## Depo yapısı

```text
ThermalPilot/
├── App.cs, MainWindow.xaml       # WPF arayüzü ve uygulama akışı
├── Telemetry.cs                  # CPU, RAM ve işlem ölçümleri
├── Sensor*.cs, HardwareSampler.cs
├── NativeGpuSensors.cs, NvidiaSmiSensors.cs
├── Optimization.cs, PowerProfiles.cs
├── ReaPeInspector.cs             # PE/COFF incelemesi
├── SelfTests.cs, PowerProfileTests.cs, UiSmokeTest.cs
├── build.ps1                     # Derleme, test ve uygulama paketi
├── prepare-github.ps1            # GitHub klasörü ve kaynak ZIP'i
├── downloads/                    # Kullanıma hazır uygulama ZIP'i
└── vendor/                       # Doğrulanan sensör bağımlılığı
```

Kaynakları, belgeleri ve hazır uygulamayı birlikte paketlemek için:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\prepare-github.ps1
```

Çıktılar `GITHUBA-YUKLE/` klasörü ve `ThermalPilot-1.3-GitHub.zip` arşividir. Paket dosyaları açık bir listeden seçilir; yerel raporlar ve ara derleme dosyaları eklenmez. Uygulama kodu veya belgeleri değiştiğinde önce `build.ps1 -Package`, ardından `prepare-github.ps1` çalıştırın.

## Teknik belgeler

- [Kullanım kılavuzu](KULLANIM.txt)
- [Sensör mimarisi ve donanım doğrulaması](SENSOR-DIAGNOSIS.md)
- [Güç modlarının doğrulama kapsamı](POWER-MODE-VALIDATION.md)
- [Sensör bağımlılığı ve SHA-256 bilgisi](vendor/README.md)
- [Üçüncü taraf bildirimleri](THIRD-PARTY-NOTICES.md)

## Üçüncü taraf lisansları

LibreHardwareMonitorLib **MPL-2.0**, REA'dan uyarlanan PE/COFF okuyucuları **MIT** lisanslıdır. PawnIO modüllerinin lisans metni ayrıca korunur. Bunlar ThermalPilot'un tamamı için bir lisans beyanı değildir.

- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
- [LIBREHARDWAREMONITOR-LICENSE.txt](LIBREHARDWAREMONITOR-LICENSE.txt)
- [PAWNIO-MODULES-COPYING.txt](PAWNIO-MODULES-COPYING.txt)
- [REA-LICENSE.txt](REA-LICENSE.txt)
