:: Usage: cmd.exe /q /C ""C:\Users\alext\AppData\Local\Playnite\ExtensionsData\5fe1d136-a9dc-44d7-80d2-43c02df6e546\script\launch.cmd" "C:\Users\alext\AppData\Local\Playnite\Playnite.DesktopApp.exe" "1af0de60-9842-46eb-b493-c5bcc2677bc0""

:: Target: "C:\Windows\System32\cmd.exe"
:: Start In: "C:\Users\alext\AppData\Local\Playnite\ExtensionsData\5fe1d136-a9dc-44d7-80d2-43c02df6e546"
:: Launch Options: /q /C "".\script\launch.cmd" "..\..\Playnite.DesktopApp.exe" "1af0de60-9842-46eb-b493-c5bcc2677bc0""

set PLAYNITE_EXE=%~1
set GUID=%~2

cd

:: Launch Playnite if not already running.
:: TODO Only do this if Playnite was not already running.
start /b "Launch Playnite" %PLAYNITE_EXE% --hidesplashscreen --startclosedtotray --nolibupdate
::powershell -command "Start-Process %PLAYNITE_EXE% -ArgumentList ""--hidesplashscreen --startclosedtotray"""

:: Wait for it to initialize before sending further commands.
:: TODO Only do this if Playnite was not already running.
timeout 5

:: Launch our custom playnite://install-or-start URI handler.
:: This URI will record the game state to a text file for us.
powershell -command "Start-Process playnite://install-or-start/%GUID%"

:: Wait for the tracking file to be deleted
:loop
timeout 10
echo "checking if game tracking file exists..."
if exist .\tracking\%GUID% echo "game in progress"
if exist .\tracking\%GUID% goto loop