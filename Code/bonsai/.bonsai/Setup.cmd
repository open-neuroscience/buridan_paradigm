@echo off
rem Creates the Bonsai environment (see Setup.ps1).
pushd %~dp0
powershell -ExecutionPolicy Bypass -File ./Setup.ps1
popd
