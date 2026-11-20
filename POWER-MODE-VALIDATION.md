# ThermalPilot 1.3 mod doğrulaması

`build.ps1 -Test` toplam 23 regresyon testini çalıştırır. Profil yazma/etkinleştirme hatasında önceki ayarlar,
mod değişiminde geçici plan temizliği, yeniden açılışta geri alma ve kullanıcının
Windows'tan seçtiği yeni plana saygı kontrollü güç arayüzüyle test edildi.

Native Windows testinde yalnızca etkin olmayan bir plan kopyası oluşturulup
ayarların yazılması planlandı. Bu otomasyon oturumunda PowerDuplicateScheme,
Windows hata kodu 5 (erişim reddedildi) döndürdü. Gerçek Windows mod geçişi
başarılı olarak raporlanmıyor; aktif güç planı değiştirilmedi.

Regresyon raporu: self-test-results.json
İsteğe bağlı arayüz testi: verification-1.3/ui-smoke-results.json
Bu yerel çıktılar Git takibine alınmaz.
Hız, FPS, fan sesi ve sıcaklık farkı ölçülmedi.

Mod seçimi uygulamada açık onaydan sonra yapılır. Windows izin vermiyorsa
hata gösterilir ve mevcut ayarlar korunur; otomatik yetki yükseltme yapılmaz.
