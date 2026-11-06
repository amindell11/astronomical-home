using System;
using System.Collections.Generic;
using AI;
using AI.Tactics;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class TacticsTarget : TuningTarget
    {
        public override string Name => "Tactics";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.CrowdedFight };

        // No shipped prefab reads the asset; the situation overlay finds it by path, so this does too.
        public override IEnumerable<ScriptableObject> Profiles(GameHost game)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TacticsSettings>(SituationGizmos.SettingsPath);
            if (!settings)
                throw new InvalidOperationException($"The tactics settings '{SituationGizmos.SettingsPath}' are missing.");
            return new ScriptableObject[] { settings };
        }
    }
}
