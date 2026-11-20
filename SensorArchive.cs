using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
namespace ThermalPilot { internal static partial class SensorPackage {
        internal static void InstallArchive(string archivePath, string target, string expected, CancellationToken cancellation)
        {
            using (var source = File.OpenRead(archivePath))
                if (Hash(source) != expected) throw new InvalidDataException("Sensör arşivi beklenen SHA-256 özetiyle eşleşmiyor.");
            // A failed extraction is never published as a ready runtime. Keep existing runtimes intact.
            string staging = target + ".stage-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            try
            {
                var manifest = new List<string>();
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    long total = 0;
                    if (archive.Entries.Count > 2048) throw new InvalidDataException("Sensör arşivinde beklenenden fazla dosya var.");
                    foreach (var entry in archive.Entries)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string destination = Path.GetFullPath(Path.Combine(staging, relative));
                        string prefix = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || relative.Contains(":")) throw new InvalidDataException("Arşiv yolu hedef klasörün dışına çıkıyor.");
                        if (entry.Name.Length == 0) { Directory.CreateDirectory(destination); continue; }
                        if (entry.Length > 33554432 || total + entry.Length > 134217728) throw new InvalidDataException("Açılmış sensör paketi boyut sınırını aşıyor.");
                        total += entry.Length;
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (var input = entry.Open())
                        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                        {
                            byte[] buffer = new byte[65536]; int count; long written = 0;
                            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellation.ThrowIfCancellationRequested(); written += count;
                                if (written > entry.Length) throw new InvalidDataException("ZIP dosya uzunluğu geçersiz.");
                                output.Write(buffer, 0, count);
                            }
                            if (written != entry.Length) throw new InvalidDataException("ZIP dosyası eksik.");
                        }
                        using (var stream = File.OpenRead(destination)) manifest.Add(Hash(stream) + " " + relative);
                    }
                }
                if (!File.Exists(Path.Combine(staging, "LibreHardwareMonitorLib.dll"))) throw new InvalidDataException("Pakette .NET Framework sensör kütüphanesi bulunamadı.");
                File.WriteAllLines(Path.Combine(staging, "files.sha256"), manifest, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(staging, "verified.sha256"), expected, new UTF8Encoding(false));
                cancellation.ThrowIfCancellationRequested();
                if (Directory.Exists(target)) throw new IOException("Sensör hedef klasörü zaten var; otomatik üzerine yazılmadı: " + target);
                Directory.Move(staging, target);
            }
            finally
            {
                // Only this invocation's newly created staging directory is removable.
                string parent = Path.GetFullPath(Path.GetDirectoryName(target)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string resolved = Path.GetFullPath(staging);
                if (resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
} }
