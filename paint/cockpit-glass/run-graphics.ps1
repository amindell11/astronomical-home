param([string]$Stage = 'before', [string]$Filter = 'Bulwark')
$ErrorActionPreference = 'Stop'
$unityArgs = @('-batchmode', '-projectPath', 'D:/amind/git/agent-8/src/Asteroids3D', '-runTests', '-testPlatform', 'EditMode', '-testFilter', $Filter, '-testResults', "D:/amind/git/agent-8/results/unity-tests-agent/bulwark-$Stage.xml", '-logFile', "D:/amind/git/agent-8/results/unity-tests-agent/bulwark-$Stage.log")
. 'D:/amind/git/agent-8/scripts/lib/unity_editor.ps1'
& (Resolve-UnityEditorPath -ProjectPath 'D:/amind/git/agent-8/src/Asteroids3D') @unityArgs
exit $LASTEXITCODE

