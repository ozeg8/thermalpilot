# Third-party notices

ThermalPilot contains a C# adaptation of two record readers from REA:

- Project: https://github.com/morluto/rea
- Revision inspected: 4502bd44bb65741e3c62eb9c7d579df085d33a13
- Copyright (c) 2026 morluto
- License: MIT (full text in REA-LICENSE.txt and embedded in ThermalPilot.exe)
- Files: src/domain/peCoffHeader.ts and src/domain/peSectionHeader.ts
- Adaptation: ThermalPilot/ReaPeInspector.cs, ReadCoff and ReadSection
- Changes: language translation to C#, exceptions for truncated records, mutable field containers instead of Result records. COFF offsets, section-name decoding and raw/content size rules are retained.

ThermalPilot 1.3 integrates LibreHardwareMonitorLib through its public Computer/Hardware/Sensor APIs. The official sensor archive is embedded in the EXE; there are no runtime downloads. A net472 archive runs in-process. The net10 archive runs in a local product-owned worker using the installed .NET 10 Windows Desktop Runtime. The LibreHardwareMonitor GUI application is never launched. Its assembly resource is only read to extract the official PawnIO installer when the user explicitly requests driver installation.

- LibreHardwareMonitor source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6
- LibreHardwareMonitorLib license: MPL-2.0; original notices are preserved in the runtime archive.
- Embedded archive: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/download/v0.9.6/LibreHardwareMonitor.NET.10.zip
- SHA-256: 29739c4959b01b348fddad87664066634bcfd4f46e9bf41e4e916c318bcfdb99
- Optional net472 archive: LibreHardwareMonitor.zip, SHA-256 086d9f1b5a99e643edc2cfaaac16051685b551e4c5ac0b32a57c58c0e529c001
- SHA-256 provenance: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/expanded_assets/v0.9.6
- PawnIO source and licensing: https://github.com/namazso/PawnIO ; official distribution: https://pawnio.eu/

The application does not modify LibreHardwareMonitorLib. Full MPL-2.0 text is in LIBREHARDWAREMONITOR-LICENSE.txt and embedded in the EXE. The upstream PawnIO module COPYING notice is preserved in PAWNIO-MODULES-COPYING.txt and embedded in the EXE. Original sources are available from the tagged upstream tree linked above; native driver sources and licensing are linked separately. No driver installation is performed by a normal launch or test. All other functionality uses Windows/.NET Framework APIs and read-only queries to installed GPU driver interfaces.
