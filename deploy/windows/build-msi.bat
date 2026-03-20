@echo off
REM Windows MSI Build Script for HASS Agent
REM Requires: WiX Toolset v3.x installed and in PATH
REM           Visual Studio Build Tools or MSBuild

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set PROJECT_ROOT=%SCRIPT_DIR%..\..
set BUILD_DIR=%PROJECT_ROOT%\build\windows
set OUTPUT_DIR=%PROJECT_ROOT%\dist\windows
set VERSION=2.0.0

echo ================================================
echo Building HASS Agent Windows MSI v%VERSION%
echo ================================================

REM Check for WiX
where candle >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo Error: WiX Toolset not found in PATH
    echo Please install WiX Toolset from: https://wixtoolset.org/
    exit /b 1
)

REM Clean previous build
if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%"
mkdir "%BUILD_DIR%\publish"
mkdir "%BUILD_DIR%\publish\Service"
mkdir "%OUTPUT_DIR%"

REM Build applications
echo Building GUI application...
cd /d "%PROJECT_ROOT%\src"

REM Try Desktop first, fall back to Avalonia
if exist "HASS.Agent.Desktop" (
    set GUI_PROJECT=HASS.Agent.Desktop\HASS.Agent.Desktop.csproj
    set GUI_EXE=HASS.Agent.Desktop.exe
) else if exist "HASS.Agent.Avalonia" (
    set GUI_PROJECT=HASS.Agent.Avalonia\HASS.Agent.Avalonia.csproj
    set GUI_EXE=HASS.Agent.Avalonia.exe
) else (
    echo Error: No GUI project found!
    exit /b 1
)

dotnet publish %GUI_PROJECT% ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishTrimmed=false ^
    -o "%BUILD_DIR%\publish"

if %ERRORLEVEL% neq 0 (
    echo Error: GUI build failed
    exit /b 1
)

echo Building Headless service...
dotnet publish HASS.Agent.Headless\HASS.Agent.Headless.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishTrimmed=false ^
    -o "%BUILD_DIR%\publish\Service"

if %ERRORLEVEL% neq 0 (
    echo Error: Service build failed
    exit /b 1
)

REM Copy configuration files
copy "%PROJECT_ROOT%\config\appsettings.json" "%BUILD_DIR%\publish\" >nul 2>&1

REM Compile WiX sources
echo Compiling WiX sources...
cd /d "%SCRIPT_DIR%"

candle -nologo ^
    -dPublishDir="%BUILD_DIR%\publish" ^
    -dSourceDir="%PROJECT_ROOT%" ^
    -dVersion=%VERSION% ^
    -out "%BUILD_DIR%\Product.wixobj" ^
    Product.wxs

if %ERRORLEVEL% neq 0 (
    echo Error: WiX candle failed
    exit /b 1
)

REM Link MSI
echo Linking MSI...
light -nologo ^
    -ext WixUIExtension ^
    -ext WixUtilExtension ^
    -cultures:en-us ^
    -out "%OUTPUT_DIR%\HASSAgent-%VERSION%-x64.msi" ^
    "%BUILD_DIR%\Product.wixobj"

if %ERRORLEVEL% neq 0 (
    echo Error: WiX light failed
    exit /b 1
)

REM Sign the MSI if certificate is available
if defined WINDOWS_SIGNING_CERT (
    echo Signing MSI...
    signtool sign /f "%WINDOWS_SIGNING_CERT%" ^
        /p "%WINDOWS_SIGNING_PASSWORD%" ^
        /t http://timestamp.digicert.com ^
        /fd SHA256 ^
        "%OUTPUT_DIR%\HASSAgent-%VERSION%-x64.msi"
)

echo.
echo ================================================
echo Build Complete!
echo ================================================
echo MSI: %OUTPUT_DIR%\HASSAgent-%VERSION%-x64.msi
echo.

REM Calculate checksum
certutil -hashfile "%OUTPUT_DIR%\HASSAgent-%VERSION%-x64.msi" SHA256 > "%OUTPUT_DIR%\HASSAgent-%VERSION%-x64.msi.sha256"
echo SHA256 checksum saved to: %OUTPUT_DIR%\HASSAgent-%VERSION%-x64.msi.sha256

endlocal
