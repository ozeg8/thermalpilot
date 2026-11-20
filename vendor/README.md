# Build dependency

`LibreHardwareMonitor.NET.10.zip` is the unmodified upstream LibreHardwareMonitor 0.9.6 archive. It is committed so a fresh checkout can build without downloading dependencies. `build.ps1` verifies its SHA-256 before embedding it in the executable.

- SHA-256: `29739c4959b01b348fddad87664066634bcfd4f46e9bf41e4e916c318bcfdb99`
- Upstream archive: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/download/v0.9.6/LibreHardwareMonitor.NET.10.zip
- Source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6
- License and source notices: [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md)

The default sensor worker requires the .NET 10 Windows Desktop Runtime x64 at runtime. Building the main application uses the Windows .NET Framework compiler.
