@echo off
::
:: ---------------------------------------------------------------------------------
::   Playnite SteamRomManagerCompanion Game Session Launcher
:: ---------------------------------------------------------------------------------
::
::   This script acts as a launcher for Playnite game sessions.
::
::     How it works:
::       * It starts a Playnite process if one is not already running.
::       * It then triggers a Playnite game launch session, or installer.
::         - In the background, the extension writes a file to disk to indicate
::           that a game launch session is active.
::         - The extension removes the file when the game launch session ends.
::       * It monitors for when the game session ends and exits the process.
::         This helps Steam know about your in-game status.
::
::     How to use:
::       * Install the Playnite SteamRomManagerCompanion extension.
::       * Perform a library scan and let the extension do it's magic.
::       * Launch a game from Steam.
::
::     Notes:
::       * If you wish to run this script manually, do so at your own risk.
::       * This script must be run from the Playnite installation directory,
::         otherwise it will likely fail.
:: ---------------------------------------------------------------------------------
::   Arguments:
::     [GameId]                : The Playnite game ID to launch.
::     [DryRun]                : Optional. If set to "dry-run", the script will not
::                               actually launch the game session. This is useful
::                               for testing.
::
::     The script's working directory must be the Playnite installation directory.
:: ---------------------------------------------------------------------------------
::   Exit Codes:
::     0                       : Success.
::     1                       : Invalid arguments.
::     2                       : Playnite executable not found in current directory.
::     3                       : Playnite not running, user will start manually.
::     4                       : Failed to start Playnite process on user's behalf.
::     5                       : Failed to start game session.
:: ---------------------------------------------------------------------------------
::
::   Known issues:
::     * If Playnite wasn't open on launch, Steam shows as running until Playnite is terminated.
::
:: ---------------------------------------------------------------------------------
echo Starting Playnite SteamRomManagerCompanion game session...

:: ---------------------------------------------------------------------------------
::   Validate the provided arguments
:: ---------------------------------------------------------------------------------

echo Validating arguments...

if "%~1" == "" (
    echo Error: GameId argument not specified. Exiting.
    echo Usage: %~0 GameId [dry-run]
    exit /b 1
)

if "%~2" == "dry-run" (
    set "IsDryRun=1"
	echo Dry run mode enabled. No actions will be performed.
) else (
    set "IsDryRun=0"
)

echo Arguments appear to be valid.

set "GameId=%~1"
set "PlayniteDirectory=%cd%"
set "ExtensionId=5fe1d136-a9dc-44d7-80d2-43c02df6e546"
set "PlayniteDesktopProcess=Playnite.DesktopApp.exe"
set "PlayniteFullscreenProcess=Playnite.FullscreenApp.exe"
set "PlayniteLaunchPath=%PlayniteDirectory%\%PlayniteDesktopProcess%"
set "PlayniteLaunchFlags=--hidesplashscreen --startclosedtotray --nolibupdate"
set "PlayniteExtensionDir=%PlayniteDirectory%\ExtensionsData\%ExtensionId%"
set "GameStateFileToMonitor=%PlayniteExtensionDir%\state\track\%GameId%"

echo Validating working directory...

if not exist "%PlayniteDirectory%\%PlayniteDesktopProcess%" (
    echo Error: Playnite executable not found in %PlayniteDirectory%\%PlayniteDesktopProcess%. Exiting.
    exit /b 2
)

echo Working directory is valid: %PlayniteDirectory%.

:: ---------------------------------------------------------------------------------
::   Check if a Playnite process is already running
:: ---------------------------------------------------------------------------------

echo Checking if Playnite is already running...

tasklist /FI "IMAGENAME eq %PlayniteDesktopProcess%" 2>NUL | find /I /N "%PlayniteDesktopProcess%">NUL
set "PlayniteDesktopProcessMissing=%ERRORLEVEL%"

tasklist /FI "IMAGENAME eq %PlayniteFullscreenProcess%" 2>NUL | find /I /N "%PlayniteFullscreenProcess%">NUL
set "PlayniteFullscreenProcessMissing=%ERRORLEVEL%"

if "%PlayniteDesktopProcessMissing%" equ "0" (
	echo %PlayniteDesktopProcess% is already running.
    set "PlayniteNotRunning=0"
) else if "%PlayniteFullscreenProcessMissing%" equ "0" (
	echo %PlayniteFullscreenProcess% is already running.
    set "PlayniteNotRunning=0"
) else (
    echo Playnite is not running. Starting process: %PlayniteLaunchPath% %PlayniteLaunchFlags%
    set "PlayniteNotRunning=1"
)

:: ---------------------------------------------------------------------------------
::   Start the Playnite process if not already running and delay for 5 seconds
:: ---------------------------------------------------------------------------------
if "%PlayniteNotRunning%" equ "1" (
    powershell -Command "Add-Type -AssemblyName PresentationFramework; $Result = [System.Windows.MessageBox]::Show('It is recommended to run Playnite in the background before launching generated non-Steam games. Steam and Playnite will be unable to accurately detect play status. Press OK to proceed with incorrect tracking, or Cancel to abort game launch and start Playnite manually first.', 'Playnite not running', 1, 64); echo $Result; if ($Result -like '*cancel*') { Exit 1 }" ||  exit /b 3
    echo Starting Playnite, tracking will be skewed.
    powershell -Command "Start-Process %PlayniteLaunchPath% -ArgumentList ""%PlayniteLaunchFlags%""" || (echo Failed to start Playnite process. && exit /b 4)
    timeout /t 5 /nobreak >nul
)

:: ---------------------------------------------------------------------------------
::   Start the game session and delay for 5 seconds
:: ---------------------------------------------------------------------------------

echo Starting game session via playnite://steam-launcher/%GameId%...

if "%IsDryRun%" equ "0" (
    echo Running powershell command...
    powershell -Command "Start-Process playnite://steam-launcher/%GameId%" || (echo Failed to start game session && exit /b 5)
)

timeout /t 5 /nobreak >nul

echo Game session started.

:: ---------------------------------------------------------------------------------
::   Monitor the game session and wait for it to end
:: ---------------------------------------------------------------------------------

echo Game state file is %GameStateFileToMonitor%.
echo Monitoring game state file and waiting for it's removal...

:MonitorGameSession
timeout /t 5 >nul
if exist "%GameStateFileToMonitor%" (
    echo Game state file found, game is running.
    goto :MonitorGameSession
) else (
    echo Game state file not found, game is not running.
)

:: ---------------------------------------------------------------------------------
::   Tell Steam that the game session has ended by exiting this script
:: ---------------------------------------------------------------------------------

echo Exiting script.

exit /b