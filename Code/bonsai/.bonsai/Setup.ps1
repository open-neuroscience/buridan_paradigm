# =============================================================================
# Setup.ps1
#
# Creates the Bonsai environment for the Buridan arena in this folder:
#   1. downloads Bonsai (the version listed in Bonsai.config) if needed,
#   2. installs the packages listed in Bonsai.config.
# Run it by double-clicking Setup.cmd. Based on the official Bonsai
# environment template.
# =============================================================================
Push-Location $PSScriptRoot
if (!(Test-Path "./Bonsai.exe")) {
    # Use the Bonsai version pinned in Bonsai.config.
    $release = "https://github.com/bonsai-rx/bonsai/releases/latest/download/Bonsai.zip"
    $configPath = "./Bonsai.config"
    if (Test-Path $configPath) {
        [xml]$config = Get-Content $configPath
        $bootstrapper = $config.PackageConfiguration.Packages.Package.where{$_.id -eq 'Bonsai'}
        if ($bootstrapper) {
            $version = $bootstrapper.version
            $release = "https://github.com/bonsai-rx/bonsai/releases/download/$version/Bonsai.zip"
        }
    }
    Invoke-WebRequest $release -OutFile "temp.zip"
    # Keep our NuGet.config (with the local Buridan package source).
    Move-Item -Path "NuGet.config" "temp.config" -ErrorAction SilentlyContinue
    Expand-Archive "temp.zip" -DestinationPath "." -Force
    Move-Item -Path "temp.config" "NuGet.config" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "temp.zip"
}
# Installs the packages of Bonsai.config without opening the editor.
& .\Bonsai.exe --no-editor
Pop-Location
