# =============================================================================
# build.ps1
#
# Builds the Buridan.Bonsai package into bonsai\packages, where the Bonsai
# environment (.bonsai\NuGet.config) picks it up.
#
# Requirements: .NET SDK 8 or later (https://dotnet.microsoft.com/download).
#
# After changing the C# code:
#   1. increase <Version> in src\Buridan.Bonsai\Buridan.Bonsai.csproj,
#   2. run this script,
#   3. set the same version for Buridan.Bonsai in .bonsai\Bonsai.config
#      (or update it from the Bonsai package manager).
# =============================================================================
Push-Location $PSScriptRoot
dotnet pack "src\Buridan.Bonsai\Buridan.Bonsai.csproj" -c Release -o "packages"
Pop-Location
