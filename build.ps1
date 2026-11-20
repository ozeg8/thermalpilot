param([switch]$Test, [string]$SensorArchive, [switch]$RequireEmbeddedSensors, [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkDirectory 'csc.exe'
$distributionDirectory = Join-Path $projectDirectory 'dist'
$artifactDirectory = Join-Path $projectDirectory 'build'
New-Item -ItemType Directory -Path $distributionDirectory,$artifactDirectory -Force | Out-Null
if (-not $SensorArchive) {
    foreach ($candidate in @('vendor\LibreHardwareMonitor.zip','vendor\LibreHardwareMonitor.NET.10.zip')) {
        $candidatePath = Join-Path $projectDirectory $candidate
        if (Test-Path -LiteralPath $candidatePath) { $SensorArchive = $candidatePath; break }
    }
}
if (-not $SensorArchive) { throw 'Official sensor ZIP required. This build never downloads runtime components.' }
$sensorArchivePath = (Resolve-Path -LiteralPath $SensorArchive).Path
$sensorHash = (Get-FileHash -LiteralPath $sensorArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$formats = @{'086d9f1b5a99e643edc2cfaaac16051685b551e4c5ac0b32a57c58c0e529c001'='net472';'29739c4959b01b348fddad87664066634bcfd4f46e9bf41e4e916c318bcfdb99'='net10'}
if (-not $formats.ContainsKey($sensorHash)) { throw 'Sensor ZIP does not match either official LibreHardwareMonitor 0.9.6 archive hash.' }
$format = $formats[$sensorHash]
$descriptorPath = Join-Path $artifactDirectory 'SensorDescriptor.json'
[IO.File]::WriteAllText($descriptorPath, (@{format=$format;sha256=$sensorHash} | ConvertTo-Json -Compress), (New-Object Text.UTF8Encoding($false)))
$bridgePath = Join-Path $artifactDirectory 'ThermalPilot.SensorBridge.dll'
$bridgeArgs = @('/nologo','/target:exe','/platform:x64','/optimize+','/codepage:65001',"/out:$bridgePath",'/reference:System.Core.dll')
$bridgeArgs += @('SensorBridge.cs','SensorData.cs','HardwareSampler.cs') | ForEach-Object { Join-Path $projectDirectory $_ }
& $compilerPath @bridgeArgs
if ($LASTEXITCODE -ne 0) { throw 'Sensor bridge build failed.' }
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/codepage:65001','/warn:4',"/out:$distributionDirectory\ThermalPilot.exe", "/win32manifest:$projectDirectory\app.manifest", "/resource:$projectDirectory\MainWindow.xaml,MainWindow.xaml", "/resource:$projectDirectory\REA-LICENSE.txt,ReaLicense", "/resource:$sensorArchivePath,IntegratedSensors.zip", "/resource:$descriptorPath,SensorDescriptor.json", "/resource:$bridgePath,SensorBridge.dll")
$compilerArgs += "/resource:$projectDirectory\LIBREHARDWAREMONITOR-LICENSE.txt,LhmLicense"
$compilerArgs += "/resource:$projectDirectory\PAWNIO-MODULES-COPYING.txt,PawnIoModulesLicense"
foreach ($assemblyName in @('System.dll','System.Core.dll','System.Management.dll','System.Web.Extensions.dll','System.Xaml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')) { $compilerArgs += '/reference:' + (Join-Path $frameworkDirectory $assemblyName) }
foreach ($assemblyName in @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')) { $compilerArgs += '/reference:' + (Join-Path $frameworkDirectory "WPF\$assemblyName") }
$compilerArgs += @('App.cs','Telemetry.cs','SensorData.cs','HardwareSampler.cs','SensorRuntime.cs','SensorArchive.cs','NativeGpuSensors.cs','NvidiaSmiSensors.cs','SensorReport.cs','Optimization.cs','PowerProfiles.cs','PowerProfileTests.cs','ReaPeInspector.cs','SelfTests.cs','UiSmokeTest.cs') | ForEach-Object { Join-Path $projectDirectory $_ }
& $compilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Write-Output "Built: $distributionDirectory\ThermalPilot.exe | embedded sensors: $format"
if ($Test) {
    $testOutput = Join-Path $projectDirectory 'self-test-results.json'
    $testProcess = Start-Process -FilePath (Join-Path $distributionDirectory 'ThermalPilot.exe') -ArgumentList @('--self-test', ('"' + $testOutput + '"')) -Wait -PassThru -WindowStyle Hidden
    Get-Content -LiteralPath $testOutput -Encoding UTF8
    if ($testProcess.ExitCode -ne 0) { throw 'Self tests failed.' }
}
if ($Package) {
    $executablePath = Join-Path $distributionDirectory 'ThermalPilot.exe'
    $versionInfo = (Get-Item -LiteralPath $executablePath).VersionInfo
    $releaseVersion = '{0}.{1}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart
    $packageFiles = @($executablePath)
    foreach ($document in @('KULLANIM.txt','README.md','SENSOR-DIAGNOSIS.md','POWER-MODE-VALIDATION.md','THIRD-PARTY-NOTICES.md','REA-LICENSE.txt','LIBREHARDWAREMONITOR-LICENSE.txt','PAWNIO-MODULES-COPYING.txt')) {
        $destination = Join-Path $distributionDirectory $document
        Copy-Item -LiteralPath (Join-Path $projectDirectory $document) -Destination $destination -Force
        $packageFiles += $destination
    }
    $packagePath = Join-Path $projectDirectory "ThermalPilot-$releaseVersion-Windows-x64.zip"
    Compress-Archive -LiteralPath $packageFiles -DestinationPath $packagePath -Force
    Write-Output "Packaged: $packagePath"
    $downloadDirectory = Join-Path $projectDirectory 'downloads'
    New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
    Copy-Item -LiteralPath $packagePath -Destination (Join-Path $downloadDirectory "ThermalPilot-$releaseVersion-Windows-x64.zip") -Force
    Write-Output "GitHub download updated: $downloadDirectory"
}
