@echo off
rem Opens the Buridan arena workflow in the Bonsai editor of this environment.
rem Run .bonsai\Setup.cmd once before the first use.
pushd %~dp0
start "" ".bonsai\Bonsai.exe" "BuridanArena.bonsai"
popd
