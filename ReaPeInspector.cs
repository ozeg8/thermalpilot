// PE/COFF record readers adapted from morluto/rea (MIT), copyright (c) 2026 morluto.
// Original owners: src/domain/peCoffHeader.ts and src/domain/peSectionHeader.ts.
// This is a bounded C# adaptation, not the complete REA CLI/MCP runtime.
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ThermalPilot
{
    internal sealed class CoffFacts
    {
        public ushort Machine, SectionCount, OptionalSize, Characteristics;
    }
    internal sealed class SectionFacts
    {
        public string Name;
        public uint VirtualSize, VirtualAddress, RawSize, Offset, Characteristics, Size;
    }
    internal static class ReaPeInspector
    {
        internal static CoffFacts ReadCoff(byte[] bytes)
        {
            if (bytes.Length < 20) throw new InvalidDataException("PE COFF header is truncated");
            return new CoffFacts { Machine = BitConverter.ToUInt16(bytes, 0), SectionCount = BitConverter.ToUInt16(bytes, 2), OptionalSize = BitConverter.ToUInt16(bytes, 16), Characteristics = BitConverter.ToUInt16(bytes, 18) };
        }
        internal static SectionFacts ReadSection(byte[] bytes)
        {
            if (bytes.Length < 40) throw new InvalidDataException("PE section header is truncated");
            int nul = Array.IndexOf(bytes, (byte)0, 0, 8);
            uint vs = BitConverter.ToUInt32(bytes, 8), raw = BitConverter.ToUInt32(bytes, 16);
            return new SectionFacts { Name = Encoding.GetEncoding(28591).GetString(bytes, 0, nul < 0 ? 8 : nul), VirtualSize = vs, VirtualAddress = BitConverter.ToUInt32(bytes, 12), RawSize = raw, Offset = BitConverter.ToUInt32(bytes, 20), Characteristics = BitConverter.ToUInt32(bytes, 36), Size = vs == 0 ? raw : Math.Min(vs, raw) };
        }
        private static byte[] Read(BinaryReader r, long offset, int length)
        {
            if (offset < 0 || offset > r.BaseStream.Length || length < 0 || length > r.BaseStream.Length - offset) throw new InvalidDataException("Dosya sınırları dışında / eksik PE verisi.");
            r.BaseStream.Position = offset;
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return bytes;
        }
        public static string Inspect(string path)
        {
            var output = new StringBuilder();
            output.AppendLine("THERMALPILOT · REA TABANLI STATİK DOSYA İNCELEMESİ");
            output.AppendLine("Kaynak: https://github.com/morluto/rea (MIT)");
            output.AppendLine("İnceleme zamanı: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            output.AppendLine("Dosya: " + path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var r = new BinaryReader(stream, Encoding.UTF8, true))
            {
                output.AppendLine("Boyut: " + stream.Length.ToString("N0") + " bayt");
                byte[] dos = Read(r, 0, 64);
                if (dos[0] != 'M' || dos[1] != 'Z') throw new InvalidDataException("MZ yürütülebilir dosya başlığı yok.");
                uint pe = BitConverter.ToUInt32(dos, 60);
                if (pe < 64) throw new InvalidDataException("PE başlığı DOS başlığıyla çakışıyor.");
                byte[] sig = Read(r, pe, 4);
                if (sig[0] != 'P' || sig[1] != 'E' || sig[2] != 0 || sig[3] != 0) throw new InvalidDataException("Geçerli bir PE başlığı bulunamadı.");
                CoffFacts coff = ReadCoff(Read(r, pe + 4L, 20));
                byte[] optional = Read(r, pe + 24L, coff.OptionalSize);
                if (optional.Length < 2) throw new InvalidDataException("PE optional header is truncated");
                ushort magic = BitConverter.ToUInt16(optional, 0);
                if (magic != 0x10b && magic != 0x20b) throw new InvalidDataException("Desteklenmeyen PE isteğe bağlı başlık biçimi.");
                string architecture = coff.Machine == 0x8664 && magic == 0x20b ? "x64" : coff.Machine == 0x14c && magic == 0x10b ? "x86" : coff.Machine == 0xaa64 && magic == 0x20b ? "ARM64" : "Bilinmiyor / kapsam dışında (0x" + coff.Machine.ToString("X4") + ")";
                output.AppendLine("COFF mimarisi: " + architecture + " · " + (magic == 0x20b ? "PE32+" : "PE32"));
                output.AppendLine("Not: .NET AnyCPU dosyalarında COFF mimarisi, çalışan işlemin bit genişliğini tek başına kanıtlamaz.");
                if (coff.SectionCount > 96) throw new InvalidDataException("96 bölüm üzerindeki PE dosyaları bu okuyucunun kapsamı dışındadır.");
                output.AppendLine("\nBÖLÜMLER (REA bölüm kaydı okuyucusundan uyarlama)");
                for (int i = 0; i < coff.SectionCount; i++)
                {
                    SectionFacts section = ReadSection(Read(r, pe + 24L + coff.OptionalSize + i * 40L, 40));
                    if ((ulong)section.Offset + section.RawSize > (ulong)stream.Length) throw new InvalidDataException("Bölüm dosya aralığı dosya sınırını aşıyor: " + section.Name);
                    output.AppendLine(section.Name + " · dosya " + section.RawSize.ToString("N0") + " B · içerik " + section.Size.ToString("N0") + " B · RVA 0x" + section.VirtualAddress.ToString("X") + ((section.Characteristics & 0x20000000) != 0 ? " · yürütülebilir" : "") + ((section.Characteristics & 0x80000000) != 0 ? " · yazılabilir" : ""));
                }
                if (stream.Length <= 536870912L)
                {
                    stream.Position = 0;
                    using (SHA256 sha = SHA256.Create()) output.AppendLine("\nSHA-256: " + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant());
                }
                else output.AppendLine("\nSHA-256: 512 MiB üzerindeki dosyada hesaplanmadı.");
            }
            try
            {
                FileVersionInfo v = FileVersionInfo.GetVersionInfo(path);
                output.AppendLine("\nSürüm: " + v.FileVersion + "\nÜrün: " + v.ProductName + "\nŞirket (dosyanın beyanı): " + v.CompanyName);
            }
            catch (Exception e) { output.AppendLine("Sürüm bilgisi okunamadı: " + e.Message); }
            try
            {
                using (var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)))
                    output.AppendLine("İmza sertifikası konusu: " + cert.Subject + "\nSertifika konusu okunmuştur; imzanın geçerliliği ve güven zinciri doğrulanmamıştır.");
            }
            catch { output.AppendLine("İmza sertifikası okunamadı / imza bulunamadı. Bu bir güvenlik hükmü değildir."); }
            output.AppendLine("\nBu rapor dosyayı çalıştırmadan başlıklarını okur. Sıcaklık, çalışma anındaki davranış veya zararlı yazılım tespiti yapmaz. Tam REA motoru bu uygulamaya gömülü değildir.");
            return output.ToString();
        }
    }
}
