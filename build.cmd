@echo off

call paket install -v
if errorlevel 1 (
  exit /b %errorlevel%
)

call fake build.fsx %*
