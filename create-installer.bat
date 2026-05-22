@echo off
chcp 65001 > nul
setlocal EnableDelayedExpansion

echo ================================================
echo   GruderPOS - Criacao de Instalador
echo ================================================
echo.

:: Paths
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "PROJECT_FILE=%ROOT%\GruderPOS\GruderPOS.csproj"
set "PUBLISH_DIR=%ROOT%\GruderPOS\bin\Release\net8.0-windows\win-x64\publish"
set "INSTALLER_DIR=%ROOT%\installers"
set "ISS_FILE=%ROOT%\setup\GruderPOS.iss"
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

:: Verificar InnoSetup
if not exist "%ISCC%" (
    echo [ERRO] Inno Setup 6 nao encontrado em:
    echo        %ISCC%
    echo.
    echo Instala Inno Setup 6: https://jrsoftware.org/isinfo.php
    pause
    exit /b 1
)

:: Ler versao atual do .csproj
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "([xml](Get-Content -LiteralPath '%PROJECT_FILE%')).Project.PropertyGroup.Version"`) do set "CURRENT_VERSION=%%v"

if "%CURRENT_VERSION%"=="" (
    echo [AVISO] Versao nao encontrada. A usar 1.0.0.
    set "CURRENT_VERSION=1.0.0"
)

:: Dividir versao em partes (Major.Minor.Patch)
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set "MAJOR=%%a"
    set "MINOR=%%b"
    set /a "PATCH=%%c+1"
)
set "NEW_VERSION=%MAJOR%.%MINOR%.%PATCH%"

echo Versao atual:  %CURRENT_VERSION%
echo Nova versao:   %NEW_VERSION%
echo.

:: Atualizar versao no .csproj
powershell -NoProfile -Command ^
  "(Get-Content -LiteralPath '%PROJECT_FILE%') -replace '<Version>%CURRENT_VERSION%</Version>', '<Version>%NEW_VERSION%</Version>' | Set-Content -LiteralPath '%PROJECT_FILE%' -Encoding UTF8"

if %ERRORLEVEL% NEQ 0 (
    echo [ERRO] Falha ao actualizar o .csproj.
    pause
    exit /b 1
)
echo [1/3] Versao actualizada no projeto.

:: Compilar em Release
echo.
echo [2/3] A compilar em Release (dotnet publish)...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained false -o "%PUBLISH_DIR%"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRO] Falha na compilacao.
    pause
    exit /b 1
)

:: Criar pasta de instaladores
if not exist "%INSTALLER_DIR%" mkdir "%INSTALLER_DIR%"

:: Criar instalador com InnoSetup
echo.
echo [3/3] A criar instalador com InnoSetup...
"%ISCC%" /DAppVersion=%NEW_VERSION% "%ISS_FILE%"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERRO] Falha ao criar o instalador.
    pause
    exit /b 1
)

echo.
echo ================================================
echo   Concluido com sucesso!
echo   Ficheiro: %INSTALLER_DIR%\GruderPOS-Setup-%NEW_VERSION%.exe
echo ================================================
echo.
pause
