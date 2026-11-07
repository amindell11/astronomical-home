using System;
using System.Collections.Generic;
using System.Linq;
using Ships.Visuals.Breakup;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class ShipDeathTarget : TuningTarget
    {
        public override string Name => "Ship death";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[]
        {
            PreviewEntry.EnemyDeath, PreviewEntry.RunRecap, PreviewEntry.Combat
        };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) =>
            new ScriptableObject[] { DeathFeel(game), game.recapFeel };

        // Read through serialization, so the rig grows no member for the preview.
        private static DeathFeelProfile DeathFeel(GameHost game)
        {
            var feels = game.hangarOffer.ships.Select(ship =>
            {
                using var breakup = new SerializedObject(ship.GetComponentInChildren<ShipBreakupVisual>(true));
                return (DeathFeelProfile)breakup.FindProperty("feel").objectReferenceValue;
            }).Distinct().ToArray();
            if (feels.Length != 1 || !feels[0])
                throw new InvalidOperationException("Every hangar ship's breakup must reference one death feel profile.");
            return feels[0];
        }
    }
}
