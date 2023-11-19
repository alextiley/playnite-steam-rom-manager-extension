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
::     3                       : Failed to start Playnite process.
::     4                       : Failed to start game session.
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
set "PlayniteLaunchPath=%PlayniteDirectory%\%PlayniteDesktopProcess%"
set "PlayniteLaunchFlags=--hidesplashscreen --startclosedtotray --nolibupdate"
set "GameStateFileToMonitor=%PlayniteDirectory%\ExtensionsData\%ExtensionId%\state\active_game\%GameId%"

echo Validating working directory...

if not exist "%PlayniteDirectory%\%PlayniteDesktopProcess%" (
    echo Error: Playnite executable not found in %PlayniteDirectory%\%PlayniteDesktopProcess%. Exiting.
    exit /b 2
)

echo Working directory is valid: %PlayniteDirectory%.

:: ---------------------------------------------------------------------------------
::   Start the game session and delay for 5 seconds
:: ---------------------------------------------------------------------------------

echo Starting game session via playnite://steam-launcher/%GameId%...

if "%IsDryRun%" equ "0" (
    echo Running powershell command...
    :: TODO see if possible to launch %PlayniteLaunchPath% with URI handler and arguments
    :: This will show the splash screen on startup, which would be better hidden.
	powershell -Command "Start-Process playnite://steam-launcher/%GameId%" || (echo Failed to start game session && exit /b 4)
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