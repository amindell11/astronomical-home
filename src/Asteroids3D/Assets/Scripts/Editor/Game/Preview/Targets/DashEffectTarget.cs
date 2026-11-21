using System;
using System.Collections.Generic;
using System.Linq;
using Ships;
using Ships.Keystones.Tells;
using UnityEditor;
using UnityEngine;

namespace Game.Preview.Targets
{
    internal sealed class DashEffectTarget : TuningTarget
    {
        public override string Name => "Dash effect";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game)
        {
            var feels = game.hangarOffer.ships.Select(Feel).Where(feel => feel).Distinct().ToArray();
            if (feels.Length == 0)
                throw new InvalidOperationException("No hangar ship's keystone kit carries a flipbook feel profile.");
            return feels;
        }

        public override void DrawControls(PreviewHost host)
        {
            EditorGUILayout.HelpBox("Pick Vanguard in the hangar and dash. Knobs apply to the next frame drawn.",
                MessageType.None);
            if (!Application.isPlaying) return;
            // Write timeScale only when the slider moves, so a pause stays paused.
            EditorGUI.BeginChangeCheck();
            var speed = EditorGUILayout.Slider("Game speed", Time.timeScale, 0.05f, 1f);
            if (EditorGUI.EndChangeCheck()) Time.timeScale = speed;
        }

        // Read through serialization, so the kit grows no member for the preview.
        private static KeystoneFlipbookFeel Feel(Ship ship)
        {
            var kit = ship.KeystonePrefab;
            if (!kit) return null;
            var flipbook = kit.GetComponent<KeystoneFlipbook>();
            if (!flipbook) return null;
            using var serialized = new SerializedObject(flipbook);
            return (KeystoneFlipbookFeel)serialized.FindProperty("feel").objectReferenceValue;
        }
    }
}
