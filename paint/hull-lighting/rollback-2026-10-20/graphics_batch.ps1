. 'D:/amind/git/agent-5/scripts/lib/unity_editor.ps1'
$rollbackProject = 'D:/amind/git/agent-5/src/Asteroids3D'
$rollbackEditor = Resolve-UnityEditorPath -ProjectPath $rollbackProject
& $rollbackEditor -batchmode -projectPath $rollbackProject -runTests -testPlatform EditMode -testFilter Tests.EditMode.Rendering.Illustrated.DrawnSurfaceEditModeTests -testResults 'D:/amind/git/agent-5/results/unity-tests-agent/hull-rollback.xml' -logFile 'D:/amind/git/agent-5/results/unity-tests-agent/hull-rollback.log'
exit $LASTEXITCODE
