# GamePlane DI Migration Notes

Date: 2026-03-02

## Decisions locked in

- Plane switching is **not** supported during a session.
- Missing reference plane is a **fatal setup error**.
- Long-term target is **strict DI** (remove static `GamePlane` access from gameplay systems).

## Changes in this refactor

1. `GamePlane` no longer auto-finds or auto-creates a reference plane.
   - Setup must call `GamePlane.SetReferencePlane(...)` explicitly.
   - Access before setup throws `InvalidOperationException`.
2. Added immutable `PlaneFrame` and `IGamePlane` contract in `GamePlane.cs`.
3. Added explicit world-point projection API:
   - `GamePlane.ProjectWorldPointToPlaneWorld(...)`
4. Removed ambiguous legacy projection callsites and updated usages.
5. Fixed point/vector conversion bugs:
   - `KinematicsPoller` now uses `WorldDirToPlane` for velocity.
   - `Kinematics.WorldVel` now returns `Vector3`.

## Current static usage inventory

The following files still reference `GamePlane.` and are migration candidates for strict DI.

### Runtime gameplay
- `src/Asteroids3D/Assets/Scripts/AI/Gunner.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Scanning/ObstacleScanner.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/Navigator.cs`
- `src/Asteroids3D/Assets/Scripts/AI/Steering/Standard/PathPlanner.cs`
- `src/Asteroids3D/Assets/Scripts/Asteroids/AsteroidController.cs`
- `src/Asteroids3D/Assets/Scripts/Asteroids/Fields/AsteroidField.cs`
- `src/Asteroids3D/Assets/Scripts/Cameras/ObserverCam.cs`
- `src/Asteroids3D/Assets/Scripts/Cameras/SmoothCamBase.cs`
- `src/Asteroids3D/Assets/Scripts/Combat/Projectile/Missile.cs`
- `src/Asteroids3D/Assets/Scripts/Combat/Projectile/ProjectileBase.cs`
- `src/Asteroids3D/Assets/Scripts/Game/GameInitiator.cs`
- `src/Asteroids3D/Assets/Scripts/Movement/FlightData.cs`
- `src/Asteroids3D/Assets/Scripts/Movement/KinematicsPoller.cs`
- `src/Asteroids3D/Assets/Scripts/Player/PlayerCommander.cs`
- `src/Asteroids3D/Assets/Scripts/Sensors/RayFanSensor.cs`
- `src/Asteroids3D/Assets/Scripts/Ships/Movement/MovementController.cs`
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

## Recommended next DI slice

1. Introduce `IPlaneFrameProvider` dependency on high-churn runtime systems first:
   - `MovementController`, `KinematicsPoller`, `PlayerCommander`, `Missile`, `Navigator`.
2. Pass provider through existing `Initialize(...)` seams where available.
3. Keep editor/test migration separate from runtime migration.
4. Remove static `GamePlane` usage package-by-package once consumers are migrated.
