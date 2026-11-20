
# ThermalPilot

**ThermalPilot 1.3 — Windows Hardware Monitoring & Performance Control**

ThermalPilot is a lightweight Windows desktop application designed to monitor CPU and RAM usage, CPU/GPU temperatures, running processes, and system performance in real time.

Built with **C#, WPF, and AI-assisted development**, ThermalPilot combines hardware monitoring, sensor diagnostics, reversible performance optimization, and local system inspection in a single interface.

**Key Highlights**
- Real-time CPU, GPU, and RAM monitoring
- Hardware temperature tracking and sensor diagnostics
- Windows performance and power management
- Running process monitoring and prioritization
- Local operation without cloud services or paid APIs
- AI-assisted development and REA-based PE/COFF inspection

### [Download ThermalPilot 1.3 — Windows x64](downloads/ThermalPilot-1.3-Windows-x64.zip?raw=true)

[Installation](#installation) · [Features](#features) · [Performance Modes](#performance-modes) · [Build from Source](#build-from-source) · [Technical Documentation](#technical-documentation)

---

## Screenshots

### System Overview

Monitor hardware performance, CPU and GPU temperatures, resource utilization, and system activity through a desktop dashboard.

![ThermalPilot System Overview](https://plain-eeur-prod-public.komododecks.com/202611/20/Dt0IuTRkblNAXyOENXlX/image.png)

### Process Monitoring

Identify resource-intensive applications and monitor running processes, CPU utilization, and memory consumption.

![ThermalPilot Process Monitoring](https://plain-eeur-prod-public.komododecks.com/202611/20/vV4gnxFxs2Myx9T5V9iR/image.png)

### Performance Optimization

Switch between Quiet / Cool, Balanced, and Performance modes while maintaining the ability to restore previous Windows power settings.

![ThermalPilot Performance Optimization](https://plain-eeur-prod-public.komododecks.com/202611/20/DjxeQfFtSt0bYdYyJR5J/image.png)

### Hardware Sensors & Diagnostics

Inspect CPU and GPU sensor information, temperature readings, hardware compatibility, and diagnostic reports.

![ThermalPilot Hardware Sensors](https://plain-eeur-prod-public.komododecks.com/202611/20/rlK9lpw9bbF6vSIuyVx0/image.png)

> Screenshots are currently hosted by Kommodo. For permanent availability, they can be stored directly in the repository.

---

## Installation

1. Download the [ThermalPilot 1.3 Windows x64 ZIP](downloads/ThermalPilot-1.3-Windows-x64.zip?raw=true).
2. Extract the archive into a folder.
3. Launch `ThermalPilot.exe`.
4. Approve the Windows administrator permission prompt.

**Important:** GitHub's **Code → Download ZIP** downloads the source code. Use the application download link above to obtain the prebuilt Windows package.

### System Requirements

| Requirement | Details |
|---|---|
| Operating System | Windows x64 |
| Main Application | .NET Framework 4.7.2 or later |
| Sensor Helper | .NET 10 Windows Desktop Runtime x64 |
| Permissions | Administrator access |
| CPU Temperature | Supported hardware and PawnIO driver |
| GPU Monitoring | Compatible GPU and drivers |

ThermalPilot does not automatically install runtime components.

On compatible systems, GPU temperature information can be retrieved through Windows WDDM. NVIDIA hardware may also use the existing `nvidia-smi` utility.

The sensor package is embedded in the executable and does not require a separate LibreHardwareMonitor interface.

Optional driver installation requires explicit user approval.

---

## Features

### Real-Time Hardware Monitoring

- CPU utilization monitoring
- RAM consumption tracking
- CPU and GPU temperature readings
- Temperature and usage history charts
- Automatic updates approximately every three seconds
- Running process information
- Sensor availability and diagnostic reporting

### Intelligent Sensor Selection

ThermalPilot prioritizes appropriate temperature sensors, including:

- CPU Package
- CPU Tdie
- CPU Tctl
- GPU Core

Invalid, stale, empty, or zero-valued temperature readings are rejected rather than displayed as legitimate measurements.

Sensor availability depends on hardware and driver compatibility.

### Windows Performance Management

ThermalPilot provides three configurable performance profiles:

- **Quiet / Cool:** Reduce CPU power limits and prioritize lower temperatures.
- **Balanced:** Maintain a balance between performance and energy consumption.
- **Performance:** Prioritize processing performance through supported Windows power settings.

All profiles support restoration of previous settings.

### Process Management

- Monitor running processes
- View CPU and memory utilization
- Reduce selected process priority
- Restore previous priority settings

### Offline PE/COFF Inspection

ThermalPilot includes PE/COFF inspection functionality adapted from REA components.

It allows supported executable file headers to be examined without executing the selected file.

---

## Performance Modes

ThermalPilot applies temporary copies of the existing Windows power plan after user confirmation.

| Mode | Maximum CPU State (AC / Battery) | Boost (AC / Battery) | EPP (AC / Battery) |
|---|---|---|---|
| Quiet / Cool | 70% / 50% | Disabled / Disabled | 80 / 90 |
| Balanced | 100% / 100% | Enabled / Enabled | 50 / 75 |
| Performance | 100% / 100% | Aggressive Preference / Enabled | 0 / 25 |

The minimum CPU state is set to 5%.

**Important Notes:**

- These profiles do not guarantee higher FPS or fixed CPU frequencies.
- Performance mode may increase temperatures, fan noise, and battery consumption.
- Unsupported power settings are reported.
- If required settings cannot be applied, the selected profile is not activated.
- Fan curves, voltages, and BIOS settings are not modified.

The **Restore Previous Settings** feature restores the earlier Windows power plan.

Automatic restoration on exit is enabled by default. If the user independently selects a different Windows power plan, that selection is preserved.

---

## Build from Source

ThermalPilot can be built using Windows PowerShell and the Windows .NET Framework C# compiler.

The build process does not automatically download or install an SDK.

The required sensor archive is included at:

`vendor/LibreHardwareMonitor.NET.10.zip`

Its SHA-256 hash is checked before building.

### Build Application

Open PowerShell in the repository directory:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

### Run Regression Tests

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

Generated application:

`dist/ThermalPilot.exe`

Test results:

`self-test-results.json`

### Build Distribution Package

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package
```

This command generates:

`ThermalPilot-1.3-Windows-x64.zip`

The distribution package is also updated in the `downloads/` directory.

An alternative `-SensorArchive` option supports a hash-verified official LibreHardwareMonitor 0.9.6 .NET Framework archive. This removes the .NET 10 helper requirement by running supported sensor processing in the main process.

This alternative has not been validated on real hardware.

---

## Testing & Verification

ThermalPilot includes regression tests for application functionality and system behavior.

### UI Smoke Test

```powershell
Start-Process .\dist\ThermalPilot.exe -ArgumentList '--ui-smoke-test', ('"' + "$PWD\verification-1.3" + '"') -Wait -WindowStyle Hidden
```

### Hardware Sensor Report

```powershell
Start-Process .\dist\ThermalPilot.exe -ArgumentList '--sensor-report', ('"' + "$PWD\sensor-results.json" + '"') -Wait -WindowStyle Hidden
```

These commands may request administrator privileges through Windows UAC.

Regression tests do not modify the active Windows power plan. Process-priority testing operates on a dedicated child process.

Sensor report exit codes:

- `0` — CPU and GPU sensor readings succeeded.
- `2` — Partial hardware monitoring result.

Successful regression testing does not guarantee functionality on every hardware configuration.

See [Sensor Diagnostics](SENSOR-DIAGNOSIS.md) and [Power Mode Validation](POWER-MODE-VALIDATION.md) for additional information.

---

## Local Data & Privacy

ThermalPilot is designed for local execution.

Application records are stored under:

`%LOCALAPPDATA%\ThermalPilot`

| File / Directory | Description |
|---|---|
| `sensor-latest.json` | Latest sensor readings and diagnostics |
| `sensor-events.jsonl` | Sensor provider events and status changes |
| `sensors/` | Extracted sensor components |
| `changes.json` | Reversible system modifications |

Sensor event logs rotate after approximately 1 MiB.

The application does not require a cloud account, cloud database, or paid API subscription for its core functionality.

Measurements and diagnostic reports remain stored locally.

Intermediate build files, temporary reports, and test screenshots are excluded from Git tracking.

---

## Repository Structure

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

### Prepare GitHub Distribution

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\prepare-github.ps1
```

This command prepares:

- `GITHUBA-YUKLE/`
- `ThermalPilot-1.3-GitHub.zip`

The publication package uses an explicit file list to exclude local reports, temporary outputs, and unnecessary development files.

After application changes, rebuild the distribution before preparing a new GitHub package.

---

## Technical Documentation

- [User Guide — Turkish](KULLANIM.txt)
- [Sensor Architecture & Diagnostics](SENSOR-DIAGNOSIS.md)
- [Power Mode Verification](POWER-MODE-VALIDATION.md)
- [Sensor Dependencies & SHA-256 Information](vendor/README.md)
- [Third-Party Notices](THIRD-PARTY-NOTICES.md)

---

## Third-Party Licenses

ThermalPilot uses or incorporates components distributed under their respective licenses:

| Component | License |
|---|---|
| LibreHardwareMonitorLib | MPL-2.0 |
| REA PE/COFF Components | MIT |
| PawnIO Modules | See included license notices |

Relevant documentation:

- [Third-Party Notices](THIRD-PARTY-NOTICES.md)
- [LibreHardwareMonitor License](LIBREHARDWAREMONITOR-LICENSE.txt)
- [PawnIO Module Notices](PAWNIO-MODULES-COPYING.txt)
- [REA License](REA-LICENSE.txt)

These third-party licenses do not automatically establish licensing terms for ThermalPilot as a whole.

---

## Development Approach

ThermalPilot was developed using an AI-assisted engineering workflow, combining traditional Windows development with modern AI coding agents.

The development process included:

- Application architecture and feature implementation
- Hardware monitoring integration
- Sensor diagnostics and compatibility handling
- Windows performance control
- Automated verification and regression testing
- REA-derived PE/COFF inspection functionality

The goal is to explore practical applications of AI-assisted software engineering while building a useful Windows system utility.

---

## Project Information

**Project:** ThermalPilot  
**Version:** 1.3  
**Platform:** Windows x64  
**Technologies:** C#, WPF, PowerShell, .NET Framework  
**Development:** AI-Assisted Software Engineering

ThermalPilot is an independently developed Windows utility.

Hardware sensor compatibility and performance behavior vary depending on the device, operating system, and installed drivers.
