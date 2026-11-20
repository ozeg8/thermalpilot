# Creates an upload folder and ZIP from an explicit list of repository files.
# Run build.ps1 -Package first when application sources or release documents change.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$projectDirectory = $PSScriptRoot
$versionText = [IO.File]::ReadAllText((Join-Path $projectDirectory 'App.cs'))
$versionMatch = [regex]::Match($versionText, 'AssemblyVersion\("(\d+\.\d+)\.\d+\.\d+"\)')
if (-not $versionMatch.Success) { throw 'Cannot read the application version from App.cs.' }
$releaseVersion = $versionMatch.Groups[1].Value
$downloadFile = "downloads/ThermalPilot-$releaseVersion-Windows-x64.zip"
$repositoryFiles = @(
    '.gitattributes', '.gitignore', 'README.md', 'build.ps1', 'prepare-github.ps1',
    'App.cs', 'app.manifest', 'MainWindow.xaml', 'Telemetry.cs',
    'SensorData.cs', 'HardwareSampler.cs', 'SensorRuntime.cs', 'SensorArchive.cs',
    'SensorBridge.cs', 'NativeGpuSensors.cs', 'NvidiaSmiSensors.cs', 'SensorReport.cs',
    'Optimization.cs', 'PowerProfiles.cs', 'PowerProfileTests.cs', 'ReaPeInspector.cs',
    'SelfTests.cs', 'UiSmokeTest.cs', 'verification-gpu-probe.cs',
    'verification-sensor-probe.cs', 'verify-rea.mjs',
    'KULLANIM.txt', 'SENSOR-DIAGNOSIS.md', 'POWER-MODE-VALIDATION.md',
    'THIRD-PARTY-NOTICES.md', 'REA-LICENSE.txt',
    'LIBREHARDWAREMONITOR-LICENSE.txt', 'PAWNIO-MODULES-COPYING.txt',
    'vendor/README.md', 'vendor/LibreHardwareMonitor.NET.10.zip', $downloadFile
)

foreach ($relativePath in $repositoryFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectDirectory $relativePath) -PathType Leaf)) {
        throw "Required repository file is missing: $relativePath"
    }
}
$dependencyHash = (Get-FileHash -LiteralPath (Join-Path $projectDirectory 'vendor/LibreHardwareMonitor.NET.10.zip') -Algorithm SHA256).Hash
if ($dependencyHash -ne '29739c4959b01b348fddad87664066634bcfd4f46e9bf41e4e916c318bcfdb99') {
    throw 'The sensor dependency failed SHA-256 verification.'
}
$applicationZip = [IO.Compression.ZipFile]::OpenRead((Join-Path $projectDirectory $downloadFile))
try {
    foreach ($requiredEntry in @('ThermalPilot.exe', 'KULLANIM.txt', 'README.md', 'THIRD-PARTY-NOTICES.md', 'REA-LICENSE.txt', 'LIBREHARDWAREMONITOR-LICENSE.txt', 'PAWNIO-MODULES-COPYING.txt')) {
        $entry = $applicationZip.GetEntry($requiredEntry)
        if (-not $entry -or $entry.Length -eq 0) { throw "Application ZIP is missing: $requiredEntry" }
    }
} finally { $applicationZip.Dispose() }

$uploadDirectory = Join-Path $projectDirectory 'GITHUBA-YUKLE'
New-Item -ItemType Directory -Path $uploadDirectory -Force | Out-Null
# Refuse unexpected files instead of deleting anything or silently including it.
foreach ($existingFile in (Get-ChildItem -LiteralPath $uploadDirectory -File -Recurse -Force)) {
    $relativePath = $existingFile.FullName.Substring($uploadDirectory.Length + 1).Replace('\', '/')
    if ($repositoryFiles -notcontains $relativePath) {
        throw "Unexpected file in upload folder: $relativePath. Move it out and rerun."
    }
}
foreach ($relativePath in $repositoryFiles) {
    $destinationPath = Join-Path $uploadDirectory $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destinationPath) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectDirectory $relativePath) -Destination $destinationPath -Force
}

$artifactDirectory = Join-Path $projectDirectory 'build'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$temporaryZip = Join-Path $artifactDirectory ('github-' + [guid]::NewGuid().ToString('N') + '.zip')
$archivePath = Join-Path $projectDirectory "ThermalPilot-$releaseVersion-GitHub.zip"
try {
    # ZipFile also includes dotfiles such as .gitignore and .gitattributes.
    [IO.Compression.ZipFile]::CreateFromDirectory($uploadDirectory, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $false)
    $archive = [IO.Compression.ZipFile]::OpenRead($temporaryZip)
    try {
        $entries = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\', '/') })
        $difference = Compare-Object -ReferenceObject $repositoryFiles -DifferenceObject $entries
        if ($difference) { throw 'The GitHub ZIP contents do not match the repository file list.' }
    } finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporaryZip -Destination $archivePath -Force
} finally {
    if (Test-Path -LiteralPath $temporaryZip) { Remove-Item -LiteralPath $temporaryZip -Force }
}

Write-Output "Upload folder: $uploadDirectory"
Write-Output "GitHub ZIP: $archivePath"
Write-Output "Repository files: $($repositoryFiles.Count)"
Write-Output "SHA-256: $((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant())"
