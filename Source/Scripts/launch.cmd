:: Name: Some Game
:: Target: "C:\Windows\System32\cmd.exe"
:: Start In: "C:\Users\alext\AppData\Local\Playnite"
:: Launch Options: /q /C "".\ExtensionsData\5fe1d136-a9dc-44d7-80d2-43c02df6e546\script\launch.cmd" ".\Playnite.DesktopApp.exe" "1af0de60-9842-46eb-b493-c5bcc2677bc0""

set PLAYNITE_EXE=%~1
set GUID=%~2

cd

:: Launch Playnite if not already running.
:: TODO Only do this if Playnite was not already running.
start /b "launch_playnite" %PLAYNITE_EXE% --hidesplashscreen --startclosedtotray --nolibupdate
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
if exist .\ExtensionsData\5fe1d136-a9dc-44d7-80d2-43c02df6e546\tracking\%GUID% echo "game in progress"
if exist .\ExtensionsData\5fe1d136-a9dc-44d7-80d2-43c02df6e546\tracking\%GUID% goto loop

echo "closing Playnite process..."
:: TODO Kill the Playnite process we spawned