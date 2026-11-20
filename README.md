
# ThermalPilot

**ThermalPilot 1.3 — Windows Donanım İzleme ve Performans Yönetimi**

ThermalPilot; işlemci (CPU), ekran kartı (GPU), RAM kullanımı, donanım sıcaklıkları ve çalışan işlemleri gerçek zamanlı izlemek için geliştirilmiş hafif bir Windows masaüstü uygulamasıdır.

**C#, WPF ve yapay zekâ destekli yazılım geliştirme teknolojileri** kullanılarak oluşturulan ThermalPilot; donanım izleme, sensör tanılama, geri alınabilir performans optimizasyonu ve sistem analizini tek bir arayüzde birleştirir.

### Öne Çıkan Özellikler

- Gerçek zamanlı CPU, GPU ve RAM takibi
- İşlemci ve ekran kartı sıcaklıklarının izlenmesi
- Donanım sensörlerinin otomatik algılanması
- Windows güç ve performans yönetimi
- Çalışan işlemlerin ve kaynak tüketiminin görüntülenmesi
- Sessiz / Serin, Dengeli ve Performans modları
- Önceki sistem ayarlarını geri yükleme
- Bulut servisi veya ücretli API gerektirmeyen yerel çalışma
- AI Agent'lar ve REA destekli geliştirme yaklaşımı

### [ThermalPilot 1.3 — Windows x64 İndir](downloads/ThermalPilot-1.3-Windows-x64.zip?raw=true)

[Kurulum](#kurulum) · [Özellikler](#özellikler) · [Performans Modları](#performans-modları) · [Kaynaktan Derleme](#kaynaktan-derleme) · [Teknik Belgeler](#teknik-belgeler)

---

## Uygulama Görselleri

### Genel Sistem Görünümü

CPU ve GPU sıcaklıklarını, donanım performansını, sistem kaynaklarının kullanımını ve genel sistem durumunu tek bir panel üzerinden takip edin.

![ThermalPilot Genel Sistem Görünümü](https://plain-eeur-prod-public.komododecks.com/202611/20/Dt0IuTRkblNAXyOENXlX/image.png)

### İşlem ve Kaynak İzleme

Bilgisayarınızda çalışan uygulamaların işlemci ve bellek tüketimini görüntüleyin. Sistem kaynaklarını yoğun kullanan işlemleri kolayca belirleyin.

![ThermalPilot İşlem İzleme](https://plain-eeur-prod-public.komododecks.com/202611/20/vV4gnxFxs2Myx9T5V9iR/image.png)

### Performans Optimizasyonu

Sessiz / Serin, Dengeli ve Performans modları arasında geçiş yaparak Windows güç ayarlarını yönetin.

Yapılan değişiklikler gerektiğinde önceki durumuna geri alınabilir.

![ThermalPilot Performans Optimizasyonu](https://plain-eeur-prod-public.komododecks.com/202611/20/DjxeQfFtSt0bYdYyJR5J/image.png)

### Donanım Sensörleri ve Tanılama

İşlemci ve ekran kartı sensörlerini, sıcaklık ölçümlerini, donanım uyumluluğunu ve tanılama bilgilerini inceleyin.

![ThermalPilot Donanım Sensörleri](https://plain-eeur-prod-public.komododecks.com/202611/20/rlK9lpw9bbF6vSIuyVx0/image.png)

> Görseller Kommodo üzerinden barındırılmaktadır. Kalıcı erişim için görsellerin doğrudan GitHub deposunda saklanması önerilir.

---

## Kurulum

ThermalPilot, Windows x64 sistemler için hazırlanmıştır.

### Nasıl Kurulur?

1. [ThermalPilot 1.3 Windows x64 ZIP](downloads/ThermalPilot-1.3-Windows-x64.zip?raw=true) dosyasını indirin.
2. İndirilen ZIP arşivini bir klasöre çıkarın.
3. `ThermalPilot.exe` dosyasını çalıştırın.
4. Windows yönetici izni istediğinde onaylayın.

**Önemli:** GitHub üzerindeki **Code → Download ZIP** seçeneği uygulamanın kaynak kodlarını indirir. Çalıştırılabilir Windows sürümü için yukarıdaki indirme bağlantısını kullanın.

### Sistem Gereksinimleri

| Gereksinim | Açıklama |
|---|---|
| İşletim Sistemi | Windows x64 |
| Ana Uygulama | .NET Framework 4.7.2 veya üzeri |
| Sensör Yardımcısı | .NET 10 Windows Desktop Runtime x64 |
| Yetki | Yönetici erişimi |
| CPU Sıcaklık Takibi | Desteklenen donanım ve PawnIO sürücüsü |
| GPU Sıcaklık Takibi | Uyumlu ekran kartı ve sürücüler |

ThermalPilot gerekli çalışma zamanı bileşenlerini otomatik yüklemez.

Uyumlu sistemlerde GPU sıcaklıkları Windows WDDM üzerinden okunabilir. NVIDIA ekran kartlarında mevcut `nvidia-smi` aracı da kullanılabilir.

Sensör bileşenleri uygulamaya gömülüdür. Ayrı bir LibreHardwareMonitor arayüzü açılması gerekmez.

İsteğe bağlı sürücü kurulumu yalnızca kullanıcının açık onayıyla gerçekleştirilir.

---

## Özellikler

### Gerçek Zamanlı Donanım İzleme

ThermalPilot, bilgisayarınızın temel performans verilerini tek bir arayüzde sunar.

- Anlık CPU kullanım oranı
- RAM tüketimi
- İşlemci sıcaklığı
- Ekran kartı sıcaklığı
- Donanım kullanım ve sıcaklık grafikleri
- Yaklaşık üç saniyede bir yenilenen ölçümler
- Çalışan işlemlerin görüntülenmesi
- Sensör durumu ve tanılama raporları

### Akıllı Sensör Seçimi

ThermalPilot, mevcut sensörler arasından uygun sıcaklık kaynaklarını öncelik sırasına göre değerlendirir.

Desteklenen sensör türleri:

- CPU Package
- CPU Tdie
- CPU Tctl
- GPU Core

Geçersiz, eski, boş veya sıfır değerli ölçümler gerçek sıcaklık bilgisi olarak gösterilmez.

Sensörlerin kullanılabilirliği bilgisayarın donanımına ve yüklü sürücülere bağlıdır.

### Windows Performans Yönetimi

Uygulama üç farklı performans profili sunar.

**Sessiz / Serin**

CPU güç ayarlarını sınırlandırarak daha düşük sıcaklık ve enerji tüketimini hedefler.

**Dengeli**

Performans ve enerji tüketimi arasında dengeli bir kullanım sunmayı amaçlar.

**Performans**

Desteklenen Windows güç ayarları üzerinden işlemci performansına öncelik verir.

Tüm modlarda önceki ayarları geri yükleme desteği bulunur.

### İşlem Yönetimi

- Çalışan işlemleri görüntüleme
- CPU ve bellek tüketimini inceleme
- Seçilen işlemlerin önceliğini düşürme
- Önceki işlem önceliğini geri yükleme

### PE/COFF Dosya İncelemesi

ThermalPilot, REA bileşenlerinden uyarlanan PE/COFF inceleme işlevlerini içerir.

Desteklenen çalıştırılabilir dosyaların başlık bilgileri, dosya çalıştırılmadan analiz edilebilir.

---

## Performans Modları

ThermalPilot, kullanıcı onayından sonra mevcut Windows güç planının geçici bir kopyasını kullanarak performans ayarlarını uygular.

| Mod | Maksimum CPU Durumu (Priz / Pil) | Boost (Priz / Pil) | EPP (Priz / Pil) |
|---|---|---|---|
| Sessiz / Serin | %70 / %50 | Kapalı / Kapalı | 80 / 90 |
| Dengeli | %100 / %100 | Açık / Açık | 50 / 75 |
| Performans | %100 / %100 | Agresif tercih / Açık | 0 / 25 |

Minimum CPU durumu %5 olarak ayarlanır.

### Önemli Bilgiler

- Performans profilleri sabit işlemci frekansı veya FPS artışı garantisi vermez.
- Performans modu sıcaklıkları, fan sesini ve pil tüketimini artırabilir.
- Desteklenmeyen güç ayarları kullanıcıya bildirilir.
- Zorunlu ayarlar uygulanamazsa seçilen mod etkinleştirilmez.
- Fan eğrileri, voltaj değerleri ve BIOS ayarları değiştirilmez.

**Önceki Ayarları Geri Yükle** özelliği, daha önce kullanılan Windows güç planına dönülmesini sağlar.

Uygulama kapanırken otomatik geri yükleme varsayılan olarak aktiftir.

Kullanıcı Windows üzerinden farklı bir güç planı seçmişse bu seçim korunur.

---

## Kaynaktan Derleme

ThermalPilot, Windows PowerShell ve Windows .NET Framework C# derleyicisi kullanılarak derlenebilir.

Derleme sırasında otomatik SDK indirme veya kurulum işlemi gerçekleştirilmez.

Gerekli sensör arşivi şu konumda bulunur:

`vendor/LibreHardwareMonitor.NET.10.zip`

Derleme öncesinde arşivin SHA-256 bütünlük kontrolü yapılır.

### Uygulamayı Derleme

Projenin ana klasöründe PowerShell açın:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

### Regresyon Testlerini Çalıştırma

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Oluşturulan uygulama:

`dist/ThermalPilot.exe`

Test sonuçları:

`self-test-results.json`

### Windows Dağıtım Paketi Oluşturma

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package
```

Bu komut aşağıdaki dosyayı oluşturur:

`ThermalPilot-1.3-Windows-x64.zip`

Aynı zamanda dağıtım paketinin `downloads/` klasöründeki kopyasını günceller.

Alternatif `-SensorArchive` parametresiyle SHA-256 doğrulaması yapılan resmî LibreHardwareMonitor 0.9.6 .NET Framework arşivi kullanılabilir.

Bu alternatifte desteklenen sensör işlemleri ana uygulama sürecinde çalışır ve .NET 10 sensör yardımcısı gereksinimi ortadan kalkar.

Ancak bu alternatif gerçek donanım üzerinde doğrulanmamıştır.

---

## Test ve Doğrulama

ThermalPilot; uygulama işlevlerini, sistem davranışını ve performans ayarlarını kontrol etmek için regresyon testleri içerir.

### Arayüz Kontrol Testi

```powershell
Start-Process .\dist\ThermalPilot.exe -ArgumentList '--ui-smoke-test', ('"' + "$PWD\verification-1.3" + '"') -Wait -WindowStyle Hidden
```

### Donanım Sensör Raporu

```powershell
Start-Process .\dist\ThermalPilot.exe -ArgumentList '--sensor-report', ('"' + "$PWD\sensor-results.json" + '"') -Wait -WindowStyle Hidden
```

Bu işlemler Windows UAC üzerinden yönetici izni isteyebilir.

Regresyon testleri mevcut Windows güç planını değiştirmez.

İşlem önceliği testleri, yalnızca test için oluşturulan alt süreç üzerinde gerçekleştirilir.

### Sensör Raporu Çıkış Kodları

- `0` — CPU ve GPU sıcaklık ölçümleri başarılı.
- `2` — Donanım ölçümlerinde kısmi sonuç.

Testlerin başarılı olması uygulamanın tüm donanımlarda sorunsuz çalışacağını garanti etmez.

Ayrıntılı bilgiler:

- [Sensör Tanılama](SENSOR-DIAGNOSIS.md)
- [Güç Modu Doğrulaması](POWER-MODE-VALIDATION.md)

---

## Yerel Veriler ve Gizlilik

ThermalPilot, temel işlevleri için internet bağlantısı veya bulut altyapısı gerektirmeyen yerel bir uygulama olarak tasarlanmıştır.

Uygulama kayıtları şu dizinde saklanır:

`%LOCALAPPDATA%\ThermalPilot`

| Dosya / Klasör | Açıklama |
|---|---|
| `sensor-latest.json` | Son sensör ölçümleri ve tanılama bilgileri |
| `sensor-events.jsonl` | Sensör sağlayıcı olayları ve durum değişiklikleri |
| `sensors/` | Çıkarılan sensör bileşenleri |
| `changes.json` | Geri alınabilir sistem değişiklikleri |

Sensör olay kayıtları yaklaşık 1 MiB boyutuna ulaştığında döndürülür.

Uygulamanın temel özellikleri için:

- Kullanıcı hesabı gerekmez.
- Bulut veritabanı gerekmez.
- Ücretli API aboneliği gerekmez.
- Harici bir sunucu bağlantısı gerekmez.

Ölçümler ve tanılama raporları yerel olarak saklanır.

Geçici derleme dosyaları, test ekran görüntüleri ve yerel raporlar Git takibine dahil edilmez.

---

## Proje Dosya Yapısı

```text
ThermalPilot/
├── App.cs
├── MainWindow.xaml
├── Telemetry.cs
├── HardwareSampler.cs
├── Sensor*.cs
├── NativeGpuSensors.cs
├── NvidiaSmiSensors.cs
├── Optimization.cs
├── PowerProfiles.cs
├── ReaPeInspector.cs
├── SelfTests.cs
├── PowerProfileTests.cs
├── UiSmokeTest.cs
├── build.ps1
├── prepare-github.ps1
├── downloads/
│   └── ThermalPilot-1.3-Windows-x64.zip
└── vendor/
```

### GitHub Yayın Paketi Hazırlama

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\prepare-github.ps1
```

Bu komut iki çıktı oluşturur:

- `GITHUBA-YUKLE/`
- `ThermalPilot-1.3-GitHub.zip`

Yayın paketi, belirlenmiş dosya listesi kullanılarak hazırlanır.

Yerel raporlar, geçici dosyalar ve gereksiz geliştirme çıktıları pakete dahil edilmez.

Uygulama kodları değiştirildikten sonra GitHub yayın paketinin yeniden hazırlanması önerilir.

---

## Teknik Belgeler

- [Kullanım Kılavuzu](KULLANIM.txt)
- [Sensör Mimarisi ve Tanılama](SENSOR-DIAGNOSIS.md)
- [Güç Modu Doğrulaması](POWER-MODE-VALIDATION.md)
- [Sensör Bağımlılıkları ve SHA-256 Bilgileri](vendor/README.md)
- [Üçüncü Taraf Yazılım Bildirimleri](THIRD-PARTY-NOTICES.md)

---

## Üçüncü Taraf Lisansları

ThermalPilot, farklı açık kaynak bileşenleri kullanır veya bunlardan uyarlanan işlevler içerir.

| Bileşen | Lisans |
|---|---|
| LibreHardwareMonitorLib | MPL-2.0 |
| REA PE/COFF Bileşenleri | MIT |
| PawnIO Modülleri | İlgili lisans bildirimleri |

Lisans belgeleri:

- [Üçüncü Taraf Bildirimleri](THIRD-PARTY-NOTICES.md)
- [LibreHardwareMonitor Lisansı](LIBREHARDWAREMONITOR-LICENSE.txt)
- [PawnIO Lisans Bildirimleri](PAWNIO-MODULES-COPYING.txt)
- [REA Lisansı](REA-LICENSE.txt)

Bu lisanslar ThermalPilot projesinin tamamının otomatik olarak aynı lisanslarla dağıtıldığı anlamına gelmez.

---

## Geliştirme Yaklaşımı

ThermalPilot, geleneksel Windows yazılım geliştirme yöntemleri ile modern **yapay zekâ ajanlarının** birlikte kullanıldığı bir geliştirme süreciyle oluşturulmuştur.

Geliştirme sürecinde:

- Yapay zekâ destekli kod geliştirme
- Uygulama mimarisinin oluşturulması
- Donanım izleme bileşenlerinin entegrasyonu
- Sensör tanılama ve uyumluluk yönetimi
- Windows performans optimizasyonu
- Otomatik test ve doğrulama süreçleri
- REA tabanlı PE/COFF inceleme işlevleri

üzerinde çalışılmıştır.

Projenin amacı yalnızca bir sıcaklık izleme uygulaması geliştirmek değil, aynı zamanda **AI Agent'ların gerçek yazılım geliştirme süreçlerinde nasıl kullanılabileceğini** uygulamalı olarak deneyimlemektir.

ThermalPilot, yapay zekâ destekli yazılım mühendisliği ile pratik sistem araçlarının geliştirilmesine yönelik bağımsız bir projedir.

---

## Proje Bilgileri

**Proje Adı:** ThermalPilot  
**Sürüm:** 1.3  
**Platform:** Windows x64  
**Teknolojiler:** C#, WPF, PowerShell, .NET Framework  
**Geliştirme Yaklaşımı:** AI-Assisted Software Engineering, REA  
**Proje Türü:** Windows Donanım İzleme ve Performans Yönetimi

ThermalPilot bağımsız olarak geliştirilmiş bir Windows yardımcı uygulamasıdır.

Donanım sensörlerinin kullanılabilirliği ve performans ayarlarının etkisi, bilgisayarın donanımına, işletim sistemine ve yüklü sürücülere göre değişebilir.
