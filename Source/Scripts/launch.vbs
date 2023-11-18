' Runs the launch.cmd batch file in the same directory, but without a console window.
Set objArgs = Wscript.Arguments
strGameId = objArgs(0)
strCmdPath = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\") - 1) & "\" & "launch.cmd"
CreateObject("Wscript.Shell").Run """" & strCmdPath & """ """ & strGameId & """", 0, True