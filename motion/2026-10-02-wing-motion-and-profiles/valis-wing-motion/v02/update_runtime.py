from pathlib import Path

root = Path('D:/amind/git/agent-5/src/Asteroids3D/Assets/Scripts')
runtime = root / 'Ships/Visuals/Wings/WingVisuals.cs'
text = runtime.read_text()
text = text.replace('            public Vector3 pivot;\n            public Vector3 translation;\n            public Quaternion forwardRotation;\n            [Range(0f, 1f)] public float idleFraction;', '            public Quaternion restRotation;')
text = text.replace('var next = Mathf.Abs(thrust) > .05f ? Mathf.Sign(thrust) : 0f;', 'var next = thrust > .05f ? 1f : 0f;')
text = text.replace('''            {
                var fraction = joint.idleFraction + demand *
                    (demand >= 0f ? 1f - joint.idleFraction : joint.idleFraction);
                joint.bone.localPosition = joint.pivot + joint.translation * fraction;
                joint.bone.localRotation = Quaternion.Slerp(Quaternion.identity, joint.forwardRotation, fraction);
            }''', '                joint.bone.localRotation = Quaternion.Slerp(joint.restRotation, Quaternion.identity, demand);')
runtime.write_text(text)
tests = root / 'Editor/Tests/PlayMode/Presentation/Wings/WingVisualsPlayModeTests.cs'
text = tests.read_text().replace('private Transform fin;', 'private Transform lower;')
text = text.replace('"10 upper swept wings right"', '"Upper wing right"').replace('fin = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "30 forward lower fins right");', 'lower = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Lower wing right");')
text = text.replace('AssertAngle(main, 1.547583f);\n            AssertAngle(fin, 61.86656f);', 'AssertAngle(main, 20f);\n            AssertAngle(lower, 20f);')
text = text.replace('ThrustCommands_ReachApprovedPoses_AndReverseSmoothly', 'ThrustCommands_SweepCompleteWings_AndBrakeAtIdlePose')
text = text.replace('AssertAngle(main, 19.34479f);\n                AssertAngle(fin, 123.73312f);', 'AssertAngle(main, 0f);\n                AssertAngle(lower, 0f);')
text = text.replace('Assert.That(Vector3.Distance(idle[4034], forward[4034]), Is.GreaterThan(.01f));\n                var weights = renderer.sharedMesh.boneWeights;', 'var weights = renderer.sharedMesh.boneWeights;\n                var moving = Enumerable.Range(0, weights.Length).First(i => weights[i].boneIndex0 != 0);\n                Assert.That(Vector3.Distance(idle[moving], forward[moving]), Is.GreaterThan(.01f));\n                Assert.That(renderer.bones, Has.Length.EqualTo(5));')
text = text.replace('AssertAngle(main, 0f);\n                AssertAngle(fin, 0f);', 'AssertAngle(main, 20f);\n                AssertAngle(lower, 20f);')
text = text.replace('Vector3.Distance(rest[i], reverse[i])', 'Vector3.Distance(idle[i], reverse[i])')
text = text.replace('AssertAngle(main, 1.547583f);\n                AssertAngle(fin, 61.86656f);', 'AssertAngle(main, 20f);\n                AssertAngle(lower, 20f);')
tests.write_text(text)
