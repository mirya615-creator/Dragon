@echo off
setlocal

REM ============================================================
REM  Get Android debug.keystore SHA-1 fingerprint
REM  Usage: double-click this file. No arguments needed.
REM ============================================================

set "KT=D:\Unity\2022.3.62t14\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"
set "KS=%USERPROFILE%\.android\debug.keystore"

if not exist "%KT%" (
    echo [ERROR] keytool.exe not found:
    echo   %KT%
    echo.
    echo Fix: edit this .bat and point KT= to your Unity OpenJDK path.
    echo      e.g. D:\Unity\2022.3.62t9\...\OpenJDK\bin\keytool.exe
    echo.
    pause
    exit /b 1
)

if not exist "%KS%" (
    echo [ERROR] debug.keystore not found:
    echo   %KS%
    echo.
    echo Fix: in Unity, build an Android APK once; Android SDK generates it.
    echo.
    pause
    exit /b 1
)

echo ============================================================
echo  Keystore : %KS%
echo  Alias    : androiddebugkey
echo ============================================================
echo.

"%KT%" -list -v -alias androiddebugkey -keystore "%KS%" -storepass android -keypass android

echo.
echo ============================================================
echo  Copy the SHA1 / SHA256 value above into Firebase Console:
echo  Project settings ^> Your apps ^> Android app ^> Add fingerprint
echo ============================================================
echo.
pause
