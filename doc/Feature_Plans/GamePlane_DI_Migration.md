# GamePlane DI Migration Notes

Date: 2026-03-02

## Decisions locked in

- Plane switching is **not** supported during a session.
- Missing reference plane is a **fatal setup error**.
- Long-term target is **strict DI** (remove static `GamePlane` access from gameplay systems).

## Changes implemented so far

1. `GamePlane` no longer auto-finds or auto-creates a reference plane.
   - Setup must call `GamePlane.SetReferencePlane(...)` explicitly.
   - Access before setup throws `InvalidOperationException`.
2. Added immutable `PlaneFrame`, DI contract `IGamePlane`, and runtime implementation `TransformGamePlane`.
3. Added shared `PlaneConstraints` utility to centralize planar constraint behavior.
4. Added explicit world-point projection API:
   - `ProjectWorldPointToPlaneWorld(...)`
5. Fixed point/vector conversion bugs:
   - `KinematicsPoller` uses vector conversion for velocity.
   - `Kinematics.WorldVel` returns `Vector3`.
6. Runtime DI slice completed for high-impact movement/combat flow:
   - `GameContext` now exposes `Plane` service.
   - `GameInitiator` creates and injects `TransformGamePlane`.
   - `Ship` stores injected plane and propagates to subsystems.
   - `MovementController`, `KinematicsPoller`, `PlayerCommander`, `ProjectileBase`, `Missile` consume injected plane provider.

## Current static usage inventory (`GamePlane.` references)

### Runtime gameplay
- `src/Asteroids3D/Assets/Scripts/AI/Gunner.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Scanning/ObstacleScanner.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/Navigator.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/Standard/PathPlanner.cs`
- `src/Asteroids3D/Assets/Scripts/Asteroids/Fields/AsteroidField.cs`
- `src/Asteroids3D/Assets/Scripts/Cameras/ObserverCam.cs`
- `src/Asteroids3D/Assets/Scripts/Cameras/SmoothCamBase.cs`
- `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs` (compatibility bridge only)
- `src/Asteroids3D/Assets/Scripts/Movement/FlightData.cs`
- `src/Asteroids3D/Assets/Scripts/Sensors/RayFanSensor.cs`
- `src/Asteroids3D/Assets/Scripts/Ships/Spawner.cs`
- `src/Asteroids3D/Assets/Scripts/UI/LockOnIndicator.cs`
- `src/Asteroids3D/Assets/Scripts/UI/MouseReticle.cs`

### Editor/debug only
- `src/Asteroids3D/Assets/Scripts/AI/Editor/AICommander.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Editor/StandardNavigator.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Scanning/Editor/ObstacleScanner.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/States/Editor/Attack.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/States/Editor/Evade.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/States/Editor/JinkEvade.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/States/Editor/Kite.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/States/Editor/Orbit.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/States/Editor/Patrol.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/MPC/Editor/MpcNavigator.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/Cameras/Editor/CameraFollow.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/Combat/Targeting/Editor/TargetingComputer.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/Player/Editor/PlayerCommander.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/Ships/Damage/Editor/DamageController.Editor.cs`
- `src/Asteroids3D/Assets/Scripts/Ships/Movement/Editor/MovementController.Editor.cs`

### Tests
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/CameraFollowPlayModeTests.cs`
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/Common/TestUtilities.cs`
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/GamePlanePlayModeTests.cs`
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/MpcNavigatorPlayModeTests.cs`
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/NavigatorPlayModeTests.cs`
- `src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/TestSceneBuilder.cs`

## Next DI slice recommendation

1. Migrate AI steering/scanning (`Navigator`, `PathPlanner`, `ObstacleScanner`, `Gunner`) to injected `IGamePlane`.
2. Migrate camera/UI runtime (`ObserverCam`, `SmoothCamBase`, `MouseReticle`, `LockOnIndicator`).
3. Remove compatibility fallback (`StaticGamePlaneAdapter`) once runtime static usage reaches zero.
4. Keep editor/test migration separate from runtime migration.
