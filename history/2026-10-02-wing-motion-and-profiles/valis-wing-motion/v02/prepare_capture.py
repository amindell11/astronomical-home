from pathlib import Path
source = Path('D:/amind/git/astronomical-home/results/valis-wing-motion/ValisWingCapture.cs').read_text()
source = source.replace('ValisWingCapture', 'ValisUpdatedWingCapture').replace('Valis-wing-motion-unity', 'Valis-updated-wing-motion-unity').replace('results/valis-wing-motion/unity', 'results/valis-wing-motion/v02/unity')
source = source.replace('minHalfHeight = 1.55f', 'minHalfHeight = 1.9f')
path = Path('D:/amind/git/agent-5/src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/Scenarios/ValisUpdatedWingCapture.cs')
path.write_text(source)
