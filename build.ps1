[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$Package,
    [switch]$DebugSymbols,
    [string]$KspRoot =
        'C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program',
    [string]$Compiler =
        'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
)

$ErrorActionPreference = 'Stop'
$modVersion = '2.6.0'
$projectRoot = $PSScriptRoot
$sourceModRoot = Join-Path $projectRoot 'GameData\KerbalProportions'
$sourceSettings = Join-Path $sourceModRoot 'PluginData\settings.cfg'
$sourceVersion = Join-Path $sourceModRoot 'KerbalProportions.version'
$managed = Join-Path $KspRoot 'KSP_x64_Data\Managed'
$distGameDataRoot = Join-Path $projectRoot 'dist\GameData'
$outputRoot = Join-Path $distGameDataRoot 'KerbalProportions'
$legacyOutputRoot = Join-Path $distGameDataRoot 'KerbalProportionsV2'
$pluginRoot = Join-Path $outputRoot 'Plugins'
$pluginDataRoot = Join-Path $outputRoot 'PluginData'
$outputDll = Join-Path $pluginRoot 'KerbalProportions.dll'
$artifactRoot = Join-Path $projectRoot 'artifacts'
$packageStageRoot = Join-Path $artifactRoot "package-$modVersion"
$packagePath = Join-Path $artifactRoot "KerbalProportions-v$modVersion.zip"

function Assert-ChildPath([string]$Path, [string]$Parent) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullParent = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($fullParent,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing operation outside expected parent: $fullPath"
    }
    return $fullPath
}

function Assert-File([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }
}

function New-ReleaseArchive([string]$SourceRoot, [string]$DestinationPath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $fullSourceRoot = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
    $stream = [IO.File]::Open($DestinationPath, [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::None)
    $archive = $null
    try {
        $archive = New-Object IO.Compression.ZipArchive -ArgumentList $stream, ([IO.Compression.ZipArchiveMode]::Create), $false
        $files = @(Get-ChildItem -LiteralPath $fullSourceRoot -Recurse -File |
            Sort-Object FullName)
        foreach ($file in $files) {
            $entryName = $file.FullName.Substring($fullSourceRoot.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $file.FullName, $entryName,
                [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $stream.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $KspRoot -PathType Container)) {
    throw "KSP root was not found. Pass -KspRoot explicitly: $KspRoot"
}
Assert-File $Compiler 'C# compiler'
Assert-File $sourceSettings 'Default settings file'
Assert-File $sourceVersion 'KSP-AVC version file'

$references = @(
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.IMGUIModule.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $managed 'UnityEngine.AnimationModule.dll'),
    (Join-Path $managed 'UnityEngine.PhysicsModule.dll'),
    (Join-Path $managed 'UnityEngine.InputLegacyModule.dll'),
    (Join-Path $managed 'UnityEngine.UI.dll')
)
foreach ($reference in $references) {
    Assert-File $reference 'KSP/Unity reference assembly'
}

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File |
    Select-Object -ExpandProperty FullName)
if ($sourceFiles.Count -eq 0) { throw 'No C# source files were found.' }

if (Test-Path -LiteralPath $outputRoot) {
    $verifiedOutputRoot = Assert-ChildPath $outputRoot $distGameDataRoot
    Remove-Item -LiteralPath $verifiedOutputRoot -Recurse -Force
}
if (Test-Path -LiteralPath $legacyOutputRoot) {
    $verifiedLegacyOutput = Assert-ChildPath $legacyOutputRoot $distGameDataRoot
    Remove-Item -LiteralPath $verifiedLegacyOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
New-Item -ItemType Directory -Path $pluginDataRoot -Force | Out-Null

if ($Package -and $DebugSymbols) {
    Write-Warning 'Ignoring -DebugSymbols for a public package.'
    $DebugSymbols = $false
}
$arguments = @('/nologo', '/target:library', '/optimize+', '/langversion:5',
    "/out:$outputDll")
$arguments += if ($DebugSymbols) { '/debug:pdbonly' } else { '/debug-' }
foreach ($reference in $references) { $arguments += "/reference:$reference" }
$arguments += $sourceFiles
& $Compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "Compiler exited with code $LASTEXITCODE" }

Copy-Item -LiteralPath $sourceSettings -Destination $pluginDataRoot
Copy-Item -LiteralPath $sourceVersion -Destination $outputRoot

if ($Install) {
    if (Get-Process -Name KSP_x64 -ErrorAction SilentlyContinue) {
        throw 'KSP is running; installation aborted.'
    }
    $gameDataRoot = Join-Path $KspRoot 'GameData'
    $installRoot = Join-Path $gameDataRoot 'KerbalProportions'
    $legacyInstallRoot = Join-Path $gameDataRoot 'KerbalProportionsV2'
    $verifiedInstallRoot = Assert-ChildPath $installRoot $gameDataRoot
    $verifiedLegacyInstallRoot = Assert-ChildPath $legacyInstallRoot $gameDataRoot
    $installedPluginRoot = Join-Path $installRoot 'Plugins'
    $installedPluginDataRoot = Join-Path $installRoot 'PluginData'
    New-Item -ItemType Directory -Path $installedPluginRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $installedPluginDataRoot -Force |
        Out-Null

    $legacySettings = Join-Path $legacyInstallRoot 'PluginData\settings.cfg'
    $legacyProfiles = Join-Path $legacyInstallRoot 'PluginData\profiles.cfg'
    $installedSettings = Join-Path $installedPluginDataRoot 'settings.cfg'
    $installedProfiles = Join-Path $installedPluginDataRoot 'profiles.cfg'
    if (Test-Path -LiteralPath $legacySettings) {
        Copy-Item -LiteralPath $legacySettings -Destination $installedSettings -Force
        $legacySettingsHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $legacySettings).Hash
        $installedSettingsHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installedSettings).Hash
        if ($legacySettingsHash -ne $installedSettingsHash) {
            throw 'Settings migration verification failed; legacy install retained.'
        }
    } elseif (-not (Test-Path -LiteralPath $installedSettings)) {
        Copy-Item -LiteralPath $sourceSettings -Destination $installedSettings
    }
    if (Test-Path -LiteralPath $legacyProfiles) {
        Copy-Item -LiteralPath $legacyProfiles -Destination $installedProfiles -Force
        $legacyProfilesHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $legacyProfiles).Hash
        $installedProfilesHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installedProfiles).Hash
        if ($legacyProfilesHash -ne $installedProfilesHash) {
            throw 'Profile migration verification failed; legacy install retained.'
        }
    }

    $installedDll = Join-Path $installedPluginRoot 'KerbalProportions.dll'
    $installedPdb = Join-Path $installedPluginRoot 'KerbalProportions.pdb'
    Copy-Item -LiteralPath $outputDll -Destination $installedDll -Force
    Copy-Item -LiteralPath $sourceVersion -Destination $installRoot -Force
    $outputPdb = [IO.Path]::ChangeExtension($outputDll, '.pdb')
    if (Test-Path -LiteralPath $outputPdb) {
        Copy-Item -LiteralPath $outputPdb -Destination $installedPdb -Force
    } elseif (Test-Path -LiteralPath $installedPdb) {
        Remove-Item -LiteralPath $installedPdb -Force
    }
    if (Test-Path -LiteralPath $legacyInstallRoot) {
        Remove-Item -LiteralPath $verifiedLegacyInstallRoot -Recurse -Force
    }
    Write-Output "Installed canonical mod to $verifiedInstallRoot"
}

if ($Package) {
    if (Test-Path -LiteralPath $packageStageRoot) {
        $verifiedStage = Assert-ChildPath $packageStageRoot $artifactRoot
        Remove-Item -LiteralPath $verifiedStage -Recurse -Force
    }
    if (-not (Test-Path -LiteralPath $artifactRoot)) {
        New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    }
    if (Test-Path -LiteralPath $packagePath) {
        $verifiedPackage = Assert-ChildPath $packagePath $artifactRoot
        Remove-Item -LiteralPath $verifiedPackage -Force
    }

    $stageModRoot = Join-Path $packageStageRoot 'GameData\KerbalProportions'
    $stagePluginRoot = Join-Path $stageModRoot 'Plugins'
    New-Item -ItemType Directory -Path $stagePluginRoot -Force | Out-Null
    Copy-Item -LiteralPath $outputDll -Destination $stagePluginRoot
    Copy-Item -LiteralPath $sourceVersion -Destination $stageModRoot
    foreach ($document in @('README.md', 'LICENSE', 'CHANGELOG.md')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $stageModRoot
    }

    try {
        New-ReleaseArchive $packageStageRoot $packagePath
    }
    finally {
        if (Test-Path -LiteralPath $packageStageRoot) {
            $verifiedStage = Assert-ChildPath $packageStageRoot $artifactRoot
            Remove-Item -LiteralPath $verifiedStage -Recurse -Force
        }
    }
    $packageHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagePath).Hash
    Write-Output "Package: $packagePath"
    Write-Output "SHA256: $packageHash"
}

Get-Item -LiteralPath $outputDll |
    Select-Object FullName, Length, LastWriteTime
