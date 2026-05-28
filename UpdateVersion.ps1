# UpdateVersion.ps1
# Reads version.json and updates version strings in SettingsForm.Import.cs and InstallerForm.cs

$jsonFile = Join-Path $PSScriptRoot "version.json"
if (-not (Test-Path $jsonFile)) {
    Write-Error "version.json not found in root!"
    Exit 1
}

$versionData = Get-Content $jsonFile -Raw | ConvertFrom-Json
$version = $versionData.Version

if (-not $version) {
    Write-Error "Version property not found in version.json!"
    Exit 1
}

Write-Host "Syncing FoxWall Version: $version"

# 1. Update SettingsForm.Import.cs
$settingsFile = Join-Path $PSScriptRoot "TinyWall\SettingsForm.Import.cs"
if (Test-Path $settingsFile) {
    $content = Get-Content $settingsFile -Raw
    # Match: \nFoxWall \d+\.\d+\.\d+
    $updatedContent = $content -replace '\\nFoxWall \d+\.\d+\.\d+', "\nFoxWall $version"
    [System.IO.File]::WriteAllText($settingsFile, $updatedContent, [System.Text.Encoding]::UTF8)
    Write-Host "Updated SettingsForm.Import.cs"
} else {
    Write-Warning "SettingsForm.Import.cs not found!"
}

# 2. Update InstallerForm.cs
$installerFile = Join-Path $PSScriptRoot "TinyWallJellyModeInstaller\InstallerForm.cs"
if (Test-Path $installerFile) {
    $content = Get-Content $installerFile -Raw
    # Update Text strings
    $updatedContent = $content -replace 'FoxWall Jelly Mode Setup \(v\d+\.\d+\.\d+\)', "FoxWall Jelly Mode Setup (v$version)"
    $updatedContent = $updatedContent -replace 'FoxWall Custom Setup \(v\d+\.\d+\.\d+\)', "FoxWall Custom Setup (v$version)"
    $updatedContent = $updatedContent -replace 'FoxWall Jelly Mode config \(v\d+\.\d+\.\d+\)', "FoxWall Jelly Mode config (v$version)"
    $updatedContent = $updatedContent -replace 'Starting custom FoxWall \d+\.\d+\.\d+ installation\.\.\.', "Starting custom FoxWall $version installation..."
    $updatedContent = $updatedContent -replace 'FoxWall version \d+\.\d+\.\d+ \(Custom Jelly Mode\)', "FoxWall version $version (Custom Jelly Mode)"
    [System.IO.File]::WriteAllText($installerFile, $updatedContent, [System.Text.Encoding]::UTF8)
    Write-Host "Updated InstallerForm.cs"
} else {
    Write-Warning "InstallerForm.cs not found!"
}

Write-Host "FoxWall version synced successfully!"
