"""PR-4b: apply named exact-match production mutations in the slot; each must match exactly once."""
import sys, pathlib

ROOT = pathlib.Path("D:/amind/git/agent-6/src/Asteroids3D/Assets/Scripts")

LAUNCH = "PlaneDirToWorld(aim * (initialSpeed + inheritedAlongAim))"

# name -> (file, old, new)
M = {
    # --- expected to turn nothing red (blind / equivalent) ---
    "kp26": ("Objectives/KeyPickup.cs",
             "            PlayerHasKey = false;\n            var offset2D",
             "            var offset2D"),
    "regen_phase": ("Ships/Damage/RegenResource.cs",
                    "            base.Reset();\n            clock = 0f;\n            lastDamageTime = -regenDelay;\n",
                    "            base.Reset();\n"),
    "md_terminal_row": ("Objectives/MissionDefinition.cs",
                        '                { "extraction", "extracted"  }\n',
                        '                { "extraction", "extracted"  },\n                { "extracted",  "explore"    }\n'),
    # --- missile launch ---
    "m_aimneg": ("Combat/Projectiles/Missile.cs", LAUNCH,
                 "PlaneDirToWorld(-aim * (initialSpeed + inheritedAlongAim))"),
    "m_across": ("Combat/Projectiles/Missile.cs", LAUNCH,
                 "PlaneDirToWorld(aim * (initialSpeed + inheritedAlongAim) + (shooterVelocity - aim * Vector2.Dot(shooterVelocity, aim)))"),
    "m_nomax": ("Combat/Projectiles/Missile.cs",
                "Mathf.Max(0f, Vector2.Dot(shooterVelocity, aim))",
                "Vector2.Dot(shooterVelocity, aim)"),
    # --- ship death / reset ---
    "ship250": ("Ships/Ship.cs",
                "            Lock.RaiseReleased();\n            gameObject.SetActive(false);\n",
                "            Lock.RaiseReleased();\n"),
    "ship259": ("Ships/Ship.cs",
                "            KinematicsPoller?.Poll();\n            gameObject.SetActive(true);\n",
                "            KinematicsPoller?.Poll();\n"),
    # --- lock-on wiring ---
    "ship68": ("Ships/Ship.cs",
               "public LockOnSensor Targeting => Weapons ? Weapons.Sensor : null;",
               "public LockOnSensor Targeting => null;"),
    "wc48": ("Ships/Weapons/WeaponsController.cs",
             "ResolveMount(WeaponSlot.Secondary));\n            Sensor = FindMountSensor();\n",
             "ResolveMount(WeaponSlot.Secondary));\n"),
    "wc146": ("Ships/Weapons/WeaponsController.cs",
              "WeaponSlot.Secondary => Secondary,", "WeaponSlot.Secondary => null,"),
    "wc145": ("Ships/Weapons/WeaponsController.cs",
              "WeaponSlot.Primary => Primary,", "WeaponSlot.Primary => null,"),
    # --- trigger volume / activation ---
    "tv60": ("Substrate/Sectors/Activation/TriggerVolume.cs",
             "private void Publish() => bus?.Set(signalToken, occupancy.Contains(playerBody));",
             "private void Publish() => bus?.Set(signalToken, false);"),
    "ar108": ("Substrate/Sectors/Activation/ActivationRule.cs",
              "            foreach (var token in publishOnFired)\n                bus?.Latch(token);\n", ""),
    "ar47": ("Substrate/Sectors/Activation/ActivationRule.cs",
             "            if (bus != null) bus.Changed += OnBusChanged;\n", ""),
    "bus33": ("Substrate/Sectors/Activation/SectorEventBus.cs",
              "            latched.Add(token);\n            Set(token, true);\n",
              "            latched.Add(token);\n"),
    # --- objective tracker ---
    "ot_fail": ("Objectives/ObjectiveTracker.cs",
                "            if (mission.FailCriteria != null && mission.FailCriteria())\n            {\n"
                "                TransitionTo(\"failed\");\n                return;\n            }\n\n", ""),
    "ot_failcall": ("Objectives/ObjectiveTracker.cs",
                    "            if (!IsTerminal(current.StateType))\n                TransitionTo(\"failed\");\n", ""),
    "ot_restart": ("Objectives/ObjectiveTracker.cs",
                   "            TransitionTo(mission.InitialStep);\n", ""),
    "ot49": ("Objectives/ObjectiveTracker.cs",
             "if (current.IsComplete && mission.TryGetNext(currentStep, out var next))",
             "if (mission.TryGetNext(currentStep, out var next))"),
    "md_initial": ("Objectives/MissionDefinition.cs",
                   '            "explore",\n            new Dictionary', '            "key",\n            new Dictionary'),
    "md_row1": ("Objectives/MissionDefinition.cs",
                '                { "explore",    "key"        },\n', ""),
    "md_row2": ("Objectives/MissionDefinition.cs",
                '                { "key",        "extraction" },\n', ""),
    "md_row3": ("Objectives/MissionDefinition.cs",
                '                { "key",        "extraction" },\n                { "extraction", "extracted"  }\n',
                '                { "key",        "extraction" }\n'),
    "kas": ("Objectives/States/KeyAcquiredState.cs",
            "public override bool IsComplete => true;", "public override bool IsComplete => false;"),
    "ecs": ("Objectives/States/ExtractionChallengeState.cs",
            "public override bool IsComplete => zone.IsPlayerInZone;", "public override bool IsComplete => false;"),
    # --- damage reset ---
    "regen29": ("Ships/Damage/RegenResource.cs",
                "            base.Reset();\n            clock = 0f;", "            clock = 0f;"),
    "dc82": ("Ships/Damage/DamageController.cs", "            Health.Reset();\n", ""),
    "dc83": ("Ships/Damage/DamageController.cs", "            Shield.Reset();\n", ""),
    # --- merge sanity: the merged tests still catch what the originals caught ---
    "rt88": ("Game/Player/RunTally.cs", "            Kills++;\n", ""),
    "rt89": ("Game/Player/RunTally.cs", "            Killed?.Invoke();\n", ""),
    "seed_w0": ("RL/Hosts/TrainingHost.cs", "workerIndex == 0\n", "workerIndex == -1\n"),
    "seed_a0": ("RL/Hosts/TrainingHost.cs", "arenaIndex == 0\n", "arenaIndex == -1\n"),
    "mpc_seat2": ("AI/Navigation/MPC/Cost/Cost.cs",
                  "case 2: return Extrapolate(input.referent2,", "case 2: return Extrapolate(input.referent1,"),
    "mpc_seat3": ("AI/Navigation/MPC/Cost/Cost.cs",
                  "case 3: return Extrapolate(input.referent3,", "case 3: return Extrapolate(input.referent1,"),
}

for name in sys.argv[1:]:
    rel, old, new = M[name]
    p = ROOT / rel
    raw = p.read_bytes()
    crlf = b"\r\n" in raw
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig") if bom else raw.decode("utf-8")
    text = text.replace("\r\n", "\n")
    n = text.count(old)
    if n != 1:
        sys.exit(f"{name}: expected exactly 1 match in {rel}, found {n}")
    text = text.replace(old, new)
    if crlf:
        text = text.replace("\n", "\r\n")
    p.write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
    print(f"mutated {name}: {rel}")
