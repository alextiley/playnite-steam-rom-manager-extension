@echo off

REM ---------------------------------------------------------------------------------
REM   Playnite SteamRomManagerCompanion Game Session Launcher
REM ---------------------------------------------------------------------------------
REM   The Playnite SteamRomManagerCompanion extension allows you to launch or install
REM   all of your non-Steam games from Steam, desktop or big picture mode. Simply
REM   install the extension, perform a library scan and then launch your games from
REM   Steam!
REM
REM   This script acts as a launcher for Playnite game sessions.
REM
REM     How it works:
REM       * It starts a Playnite process if one is not already running.
REM       * It then triggers a Playnite game launch session, or installer.
REM         - In the background, the extension writes a file to disk to indicate
REM           that a game install or launch session is active.
REM         - The extension removes the file when the game install or launch session
REM           ends.
REM       * It monitors for when the install or game session ends and exits the
REM         process in order to notify Steam that the game session has ended.
REM       * Steam will then update it's game play status. This means that your
REM         friends will be able to see what you're playing, even if it's not a
REM         Steam game!
REM
REM     How to use:
REM       * Install the Playnite SteamRomManagerCompanion extension.
REM       * Perform a library scan.
REM       * Launch a game from Steam.
REM
REM     Notes:
REM       * If you wish to run this script manually, do so at your own risk.
REM       * This script must be run from the Playnite installation directory,
REM         otherwise it will likely fail.
REM ---------------------------------------------------------------------------------
REM   Arguments:
REM     [ExtensionId]           : The Playnite SteamRomManagerCompanion extension ID.
REM     [GameId]                : The Playnite game ID to launch.
REM     [DryRun]                : Optional. If set to "dry-run", the script will not
REM                               actually launch the game session. This is useful
REM                               for testing.
REM
REM     The script's working directory must be the Playnite installation directory.
REM ---------------------------------------------------------------------------------
REM   Exit Codes:
REM     0                       : Success.
REM     1                       : Invalid arguments.
REM     2                       : Playnite executable not found in current directory.
REM     3                       : Failed to start Playnite process.
REM     4                       : Failed to start game session.
REM ---------------------------------------------------------------------------------

REM Function to display usage instructions
:ShowUsage
echo Usage: %0 [ExtensionId] [GameId]

REM Function to display an error message and exit
:ShowErrorAndExit
echo Error: %1
if "%3" == "usage" call :ShowUsage
exit /b %2

echo Starting Playnite SteamRomManagerCompanion game session...

REM ---------------------------------------------------------------------------------
REM   Validate the provided arguments
REM ---------------------------------------------------------------------------------

echo Validating arguments...

if "%~1" == "" (
    call :ShowErrorAndExit "ExtensionId argument not specified. Exiting." 1 "usage"
) else if "%~2" == "" (
    call :ShowErrorAndExit "GameId argument not specified. Exiting." 1 "usage"
)

if "%~3" == "dry-run" (
    @echo on
    set "IsDryRun=1"
	echo Dry run mode enabled. No actions will be performed.
)

set "PlayniteDirectory=%cd%"
set "ExtensionId=%~1"
set "GameId=%~2"
set "PlayniteDesktopProcess=Playnite.DesktopApp.exe"
set "PlayniteFullscreenProcess=Playnite.FullscreenApp.exe"
set "PlayniteLaunchPath=%PlayniteDirectory%\%PlayniteDesktopProcess%"
set "PlayniteLaunchFlags=--hidesplashscreen --startclosedtotray --nolibupdate"
set "GameStateFileToMonitor=%PlayniteDirectory%\ExtensionData\%ExtensionId%\state\%GameId%"

echo Validating working directory...

if not exist "%PlayniteDirectory%\%PlayniteDesktopProcess%" (
    call :ShowErrorAndExit "Playnite executable not found in the current directory. Exiting." 2 "usage"
)

REM ---------------------------------------------------------------------------------
REM   Check if a Playnite process is already running
REM ---------------------------------------------------------------------------------

echo Checking if Playnite is already running...

tasklist /FI "IMAGENAME eq %PlayniteDesktopProcess%" 2>NUL | find /I /N "%PlayniteDesktopProcess%">NUL
set "PlayniteDesktopProcessMissing=%ERRORLEVEL%"

tasklist /FI "IMAGENAME eq %PlayniteFullscreenProcess%" 2>NUL | find /I /N "%PlayniteFullscreenProcess%">NUL
set "PlayniteFullscreenProcessMissing=%ERRORLEVEL%"

REM ---------------------------------------------------------------------------------
REM   Start the Playnite process if not already running and delay for 5 seconds
REM ---------------------------------------------------------------------------------

echo Starting Playnite process if not already running...

if "%PlayniteDesktopProcessMissing%" equ "0" (
    echo %PlayniteDesktopProcess% is already running.
    set "PlayniteStartedByScript=0"
) else if "%PlayniteFullscreenProcessMissing%" equ "0" (
    echo %PlayniteFullscreenProcess% is already running.
    set "PlayniteStartedByScript=0"
) else (
    echo Playnite is not running. Starting process.
    if "%DryRun%" equ "0" (
        powershell -Command "Start-Process %PlayniteLaunchPath% -ArgumentList %PlayniteLaunchFlags%" || call :ShowErrorAndExit "Failed to start Playnite process." 3
	)
    set "PlayniteStartedByScript=1"
    timeout /t 5 /nobreak >nul
)

REM ---------------------------------------------------------------------------------
REM   Start the game session and delay for 5 seconds
REM ---------------------------------------------------------------------------------

echo Starting game session...

if "%DryRun%" equ "0" (
	powershell -Command "Start-Process 'playnite://steam-launcher/%GameId%'" || call :ShowErrorAndExit "Failed to start game session." 4
)

timeout /t 5 /nobreak >nul

REM ---------------------------------------------------------------------------------
REM   Monitor the game session and wait for it to end
REM ---------------------------------------------------------------------------------

echo Monitoring game session and waiting for it to end...

:MonitorGameSession
if exist "%GameStateFileToMonitor%" (
    timeout /t 5 >nul
    goto :MonitorGameSession
)

REM ---------------------------------------------------------------------------------
REM   Clean up and kill the Playnite process if it was started by this script
REM ---------------------------------------------------------------------------------

if "%PlayniteStartedByScript%" equ "1" if "%DryRun%" equ "0" (
	taskkill /f /im "%PlayniteDesktopProcess%" >nul
)

REM ---------------------------------------------------------------------------------
REM   Tell Steam that the game session has ended by exiting this script
REM ---------------------------------------------------------------------------------

echo Game session ended. Exiting.

exit /b