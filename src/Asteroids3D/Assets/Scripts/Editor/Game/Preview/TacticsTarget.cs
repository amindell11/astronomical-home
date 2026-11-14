using System;
using System.Collections.Generic;
using System.Linq;
using AI.Tactics;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class TacticsTarget : TuningTarget
    {
        public override string Name => "Tactics";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.CrowdedFight };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game) =>
            new[] { "Aggressor", "Orbiter", "Kiter", "Lurker", "Elite", "Escort" }.Select(Temper);

        private static ScriptableObject Temper(string kind)
        {
            var path = $"Assets/Settings/AI/Tactics/{kind}Temper.asset";
            var temper = AssetDatabase.LoadAssetAtPath<TacticsSettings>(path);
            if (!temper) throw new InvalidOperationException($"The {kind.ToLowerInvariant()} temper '{path}' is missing.");
            return temper;
        }
    }
}
