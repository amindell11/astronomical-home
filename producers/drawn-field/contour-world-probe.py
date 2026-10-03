from pathlib import Path
p=Path('src/Asteroids3D/Assets/Visuals/Shaders/DrawnComparison/DrawnContour.shader')
s=p.read_text()
s=s.replace('positionCS.xy += direction * (2 * width / _ScaledScreenParams.xy) * positionCS.w;', 'float worldWidth = 2 * width * positionCS.w / (_ScaledScreenParams.y * UNITY_MATRIX_P._m11);\n                positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz) + normalWS * worldWidth);')
p.write_text(s)
