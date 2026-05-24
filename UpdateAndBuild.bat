@echo off
echo ==============================================
echo        TinyWall Upstream Update ^& Rebuild
echo ==============================================
echo.

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

:: 1. Run git pull to fetch and merge upstream updates (master is default for pylorak/TinyWall)
echo [1/4] Pulling latest changes from pylorak/TinyWall (master)...
git pull upstream master
if %errorLevel% neq 0 (
    echo.
    echo WARNING: git pull failed or encountered conflicts.
    echo Please resolve any conflicts manually in Git.
    echo.
)

:: 2. Rebuild the solution using MSBuild
echo [2/4] Rebuilding TinyWall solution...
set "MSBuildEnableWorkloadResolver=false"
set "MSBuildSDKsPath=C:\Program Files\dotnet\sdk\9.0.203\Sdks"
set "MSBUILD_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

if exist "%MSBUILD_PATH%" (
    :: Run MSBuild directly on the TinyWall project
    "%MSBUILD_PATH%" TinyWall\TinyWall.csproj /t:Rebuild /p:Configuration=Release
) else (
    echo.
    echo ERROR: MSBuild.exe was not found at:
    echo "%MSBUILD_PATH%"
    echo Please open the solution in Visual Studio and rebuild it manually.
    pause
    exit /b
)

if %errorLevel% neq 0 (
    echo.
    echo ERROR: Build failed! Visual Studio's MSBuild encountered errors.
    pause
    exit /b
)

:: 3. Stage compiled files to C: drive temp folder
echo [3/4] Staging compiled files to C:\Users\basil\TinyWallTemp...
if not exist "C:\Users\basil\TinyWallTemp" mkdir "C:\Users\basil\TinyWallTemp"
xcopy /Y /S /E "TinyWall\bin\Release\*" "C:\Users\basil\TinyWallTemp\"
if %errorLevel% neq 0 (
    echo ERROR: Failed to stage files to C: drive!
    pause
    exit /b
)

:: 4. Trigger elevated deployment script
echo [4/4] Launching elevated installer...
powershell.exe -Command "Start-Process cmd.exe -ArgumentList '/c C:\Users\basil\TinyWallTemp\deploy.bat' -Verb RunAs"

echo.
echo ==============================================
echo SUCCESS: Build staged! Please accept the UAC
echo prompt to complete the installation.
echo ==============================================
echo.
pause
