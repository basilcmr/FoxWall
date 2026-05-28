@echo off
echo ==============================================
echo        TinyWall Custom Installer Builder
echo ==============================================
echo.

:: Sync version number from centralized version.json
echo Syncing version number from version.json...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0UpdateVersion.ps1"
if %errorLevel% neq 0 (
    echo ERROR: Version sync failed!
    pause
    exit /b
)

:: Read version dynamically from version.json
for /f "delims=" %%i in ('powershell -NoProfile -Command "(Get-Content '%~dp0version.json' -Raw | ConvertFrom-Json).Version"') do set "VERSION=%%i"
echo Centralized FoxWall Version: %VERSION%
echo.

:: Close any running installer instances to prevent file lock issues
taskkill /F /IM TinyWallJellyModeInstaller.exe >nul 2>&1
taskkill /F /IM FoxWallJellyModeInstaller.exe >nul 2>&1

:: Detect directory structure dynamically
if exist "%~dp0TinyWall\TinyWall.sln" (
    set "REPO_DIR=%~dp0TinyWall"
    set "WORKSPACE_DIR=%~dp0"
) else if exist "%~dp0TinyWall.sln" (
    set "REPO_DIR=%~dp0"
    set "WORKSPACE_DIR=%~dp0..\"
) else (
    echo ERROR: Could not find TinyWall repository!
    pause
    exit /b
)

cd /d "%REPO_DIR%"

:: Compile the TinyWall C# project first to ensure the staged binaries are fully up-to-date with version.json
echo [0/5] Compiling TinyWall binaries...

:: Find MSBuild path dynamically using vswhere
set "MSBUILD_PATH="
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "tokens=*" %%i in ('"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe') do (
        set "MSBUILD_PATH=%%i"
    )
)
if not defined MSBUILD_PATH (
    set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)

:: Find .NET SDK path dynamically
set "DOTNET_SDK_DIR="
if exist "%ProgramFiles%\dotnet\sdk" (
    for /f "tokens=*" %%i in ('dir "%ProgramFiles%\dotnet\sdk" /b /ad /o-n') do (
        if not defined DOTNET_SDK_DIR (
            set "DOTNET_SDK_DIR=%%i"
        )
    )
)
if defined DOTNET_SDK_DIR (
    set "MSBuildSDKsPath=%ProgramFiles%\dotnet\sdk\%DOTNET_SDK_DIR%\Sdks"
) else (
    set "MSBuildSDKsPath=C:\Program Files\dotnet\sdk\9.0.203\Sdks"
)
set "MSBuildEnableWorkloadResolver=false"

if exist "%MSBUILD_PATH%" (
    "%MSBUILD_PATH%" TinyWall\TinyWall.csproj /t:Rebuild /p:Configuration=Release
    if %errorLevel% neq 0 (
        echo ERROR: TinyWall compilation failed!
        pause
        exit /b
    )
) else (
    echo ERROR: MSBuild.exe was not found at: "%MSBUILD_PATH%"
    pause
    exit /b
)
echo.

:: 1. Copy latest Release builds to staging folder (%TEMP% directory)
echo [1/5] Staging latest compiled files...
if not exist "%TEMP%\TinyWallTemp" mkdir "%TEMP%\TinyWallTemp"
xcopy /Y /S /E "TinyWall\bin\Release\*" "%TEMP%\TinyWallTemp\"
copy /Y "version.json" "%TEMP%\TinyWallTemp\"
if %errorLevel% neq 0 (
    echo ERROR: Failed to stage Release files!
    pause
    exit /b
)

:: Remove any temp files from staging folder
del /F /Q "%TEMP%\TinyWallTemp\deploy.bat" >nul 2>&1
del /F /Q "%TEMP%\TinyWallTemp\deploy.log" >nul 2>&1
del /F /Q "%TEMP%\TinyWallTemp\process_icon.py" >nul 2>&1

:: 2. Compress files to ZIP archive
echo [2/5] Compressing custom assets into ZIP...
powershell.exe -Command "Compress-Archive -Path '%TEMP%\TinyWallTemp\*' -DestinationPath 'TinyWallFiles.zip' -Force"
if %errorLevel% neq 0 (
    echo ERROR: Failed to create ZIP archive!
    pause
    exit /b
)

:: 3. Restore NuGet packages for installer
echo [3/5] Restoring installer dependencies...
if exist "%MSBUILD_PATH%" (
    "%MSBUILD_PATH%" TinyWallJellyModeInstaller\TinyWallJellyModeInstaller.csproj /t:Restore
) else (
    echo ERROR: MSBuild.exe not found at: "%MSBUILD_PATH%"
    pause
    exit /b
)

:: 4. Build the C# Installer Project
echo [4/5] Compiling C# Installer Executable...
"%MSBUILD_PATH%" TinyWallJellyModeInstaller\TinyWallJellyModeInstaller.csproj /t:Rebuild /p:Configuration=Release
if %errorLevel% neq 0 (
    echo ERROR: Installer compilation failed!
    pause
    exit /b
)

:: 5. Copy output executable to workspace root
echo [5/5] Deploying final installer to root...
copy /Y "TinyWallJellyModeInstaller\bin\Release\net48\FoxWallJellyModeInstaller.exe" "FoxWallJellyModeInstaller %VERSION%.exe"

:: Clean up old unversioned installers in the project folder to keep it clean
del /F /Q "FoxWallJellyModeInstaller.exe" >nul 2>&1
del /F /Q "TinyWallJellyModeInstaller.exe" >nul 2>&1

:: Clean up temporary ZIP
del /F /Q "TinyWallFiles.zip" >nul 2>&1

echo.
echo ==============================================
echo SUCCESS: FoxWallJellyModeInstaller %VERSION%.exe is ready!
echo ==============================================
echo.
pause
