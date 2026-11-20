# ThermalPilot 1.3 sensör mimarisi

## Sağlayıcılar

1. **Windows WDDM:** `D3DKMTEnumAdapters2` ve `D3DKMTQueryAdapterInfo` aracılığıyla ekran sürücüsünün `KMTQAITYPE_ADAPTERPERFDATA` verisini okur. Sıcaklık 0.1 °C biriminden dönüştürülür; aygıt LUID'si korunur ve bağdaştırıcı handle'ları kapatılır. Destek ekran sürücüsüne bağlıdır.
2. **NVIDIA:** mevcut `nvidia-smi.exe` için System32, NVSMI ve PATH konumlarını kontrol eder. Yalnızca `--query-gpu=uuid,name,temperature.gpu --format=csv,noheader,nounits` okuma sorgusunu kullanır. UUID ve kart adı korunur; N/A, sıfır ve bozuk satırlar ölçüm sayılmaz.
3. **LibreHardwareMonitorLib:** CPU ve ek GPU sensörlerini ortak `HardwareSampler` üzerinden okur. Varsayılan .NET 10 paketi yerel `ThermalPilot.SensorBridge.dll` yardımcısında çalışır. İstek/sıra numarası/JSON yanıtları standart giriş/çıkış üzerinden taşınır. Yanıt süresi ve örnek yaşı denetlenir. Alternatif net472 arşivi ana işlemde çalışır.

GPU sağlayıcıları CPU paketinin hazır olmasından bağımsızdır. Bir sağlayıcının hatası diğerinin geçerli ölçümünü silmez. Uygulama manifesti yönetici erişimini zorunlu tutar; Windows açılışta gerektiğinde UAC onayı ister. CPU için ayrıca PawnIO sürücüsü ve desteklenen donanım gerekir. Sürücü kurulumu yalnızca kullanıcının seçimi ve onayıyla başlatılır.

## Sensör seçimi ve paket bütünlüğü

CPU Package, AMD Tdie/Tctl-Tdie, Tctl ve çekirdek ölçümleri öncelik sırasıyla değerlendirilir. GPU Core tercih edilir; yalnızca hotspot varsa yedek ölçüm olarak kendi etiketiyle gösterilir. GPU Memory ana sıcaklık yapılmaz. Boş, sıfır, NaN, sonsuz, kapsam dışı ve on saniyeden eski ölçümler güncel değer olarak sunulmaz. ACPI termal bölgesi CPU Package yerine kullanılmaz.

Sensör ZIP'i EXE'ye gömülür. Derlemede hash kontrolü yapılır; çalışma sırasında paket ve çıkarılmış dosyalar doğrulanır. Dosyalar `%LOCALAPPDATA%\ThermalPilot\sensors` altında hazırlanır. Başlangıçta paket indirilmez; ayrı LibreHardwareMonitor GUI uygulaması açılmaz. Tanılar ve ölçümler yerel dosyalarda tutulur.

## Doğrulamanın kapsamı

Önceki yerel donanım kontrollerinde AMD Radeon RX Vega 10 üzerinde WDDM sıcaklık okuması alınmıştı. Ryzen 7 3700U CPU ölçümü sürücü/erişim nedeniyle başarılı sayılmadı. NVIDIA ve Intel donanımlarıyla gerçek ölçüm doğrulanmadı. Bu sonuçlar tüm sistemler için destek garantisi değildir.

`build.ps1 -Test`, sensör önceliğini, NVIDIA CSV ayrıştırmasını, eski yanıtların reddini, WDDM ABI boyut/ofsetlerini ve ZIP bütünlüğünü kapsar. Bunlar gerçek donanım testinin yerine geçmez. Yeni donanım kontrolü için README'deki `--sensor-report` komutunu kullanın. Makineye ait ham raporlar ve ekran görüntüleri depoya dahil edilmez.

## Kodda kullanılan arayüzlerin kaynakları

- [Microsoft D3DKMT_ADAPTER_PERFDATA](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmthk/ns-d3dkmthk-_d3dkmt_adapter_perfdata)
- [Microsoft Windows SDK d3dkmthk.h](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/shared/d3dkmthk.h)
- [NVIDIA nvidia-smi](https://docs.nvidia.com/deploy/nvidia-smi/index.html)
- [LibreHardwareMonitor 0.9.6 kaynak kodu](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6)
- [LibreHardwareMonitor PawnIO okuyucusu](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/PawnIo/PawnIo.cs)
