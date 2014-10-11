@echo off

if not exist .paket\paket.exe (
    .paket\paket.bootstrapper.exe prerelease
    if errorlevel 1 (
        exit /b %errorlevel%
    )
)

".paket\paket.exe" %*
