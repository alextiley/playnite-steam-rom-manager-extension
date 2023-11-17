@echo off
:: cd %LocalAppData%\Playnite && %UserProfile%\Code\playnite-steam-rom-manager-extension\Source\Scripts\launch.cmd 12345
:: ---------------------------------------------------------------------------------
::   Playnite SteamRomManagerCompanion Game Session Launcher
:: ---------------------------------------------------------------------------------
::   The Playnite SteamRomManagerCompanion extension allows you to launch or install
::   all of your non-Steam games from Steam, desktop or big picture mode. Simply
::   install the extension, perform a library scan and then launch your games from
::   Steam!
::
::   This script acts as a launcher for Playnite game sessions.
::
::     How it works:
::       * It starts a Playnite process if one is not already running.
::       * It then triggers a Playnite game launch session, or installer.
::         - In the background, the extension writes a file to disk to indicate
::           that a game install or launch session is active.
::         - The extension removes the file when the game install or launch session
::           ends.
::       * It monitors for when the install or game session ends and exits the
::         process in order to notify Steam that the game session has ended.
::       * Steam will then update it's game play status. This means that your
::         friends will be able to see what you're playing, even if it's not a
::         Steam game!
::
::     How to use:
::       * Install the Playnite SteamRomManagerCompanion extension.
::       * Perform a library scan.
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

if "%~3" == "dry-run" (
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
set "GameStateFileToMonitor=%PlayniteDirectory%\ExtensionData\%ExtensionId%\state\%GameId%"

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
    set "PlayniteStartedByScript=0"
) else if "%PlayniteFullscreenProcessMissing%" equ "0" (
	echo %PlayniteFullscreenProcess% is already running.
    set "PlayniteStartedByScript=0"
) else (
    echo Playnite is not running. Starting process: %PlayniteLaunchPath% %PlayniteLaunchFlags%
    set "PlayniteStartedByScript=1"
)

:: ---------------------------------------------------------------------------------
::   Start the Playnite process if not already running and delay for 5 seconds
:: ---------------------------------------------------------------------------------

if "%PlayniteStartedByScript%" equ "1" (

    if "%IsDryRun%" equ "0" (
        echo Running powershell command...
        powershell -Command "Start-Process %PlayniteLaunchPath% -ArgumentList ""%PlayniteLaunchFlags%""" || (echo Failed to start Playnite process. && exit /b 3)
	)
    timeout /t 5 /nobreak >nul

    echo Playnite process started.
)

:: ---------------------------------------------------------------------------------
::   Start the game session and delay for 5 seconds
:: ---------------------------------------------------------------------------------

echo Starting game session via playnite://steam-launcher/%GameId%...

if "%IsDryRun%" equ "0" (
    echo Running powershell command...
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
if exist "%GameStateFileToMonitor%" (
    timeout /t 5 >nul
    goto :MonitorGameSession
)

echo Game session has ended.

:: ---------------------------------------------------------------------------------
::   Clean up and kill the Playnite process if it was started by this script
:: ---------------------------------------------------------------------------------
::
:: TODO consider if we really need to do this.
::
if "%PlayniteStartedByScript%" equ "1" if "%IsDryRun%" equ "0" (
    echo Exiting Playnite process.
	taskkill /f /im "%PlayniteDesktopProcess%" >nul
    echo Playnite successfully exited.
)

:: ---------------------------------------------------------------------------------
::   Tell Steam that the game session has ended by exiting this script
:: ---------------------------------------------------------------------------------

echo Exiting script. Goodbye.

exit /b