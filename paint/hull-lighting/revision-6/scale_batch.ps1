# Coordinator child: graphics batch Unity for CLI renders; exits when the CLI requests editor exit.
. 'D:/amind/git/agent-4/scripts/lib/unity_editor.ps1'
$brushProject = 'D:/amind/git/agent-4/src/Asteroids3D'
$brushEditor = Resolve-UnityEditorPath -ProjectPath $brushProject
& $brushEditor -batchmode -projectPath $brushProject -logFile 'D:/amind/git/agent-4/scratch/hull-lighting/scale-batch-editor.log'
exit $LASTEXITCODE

