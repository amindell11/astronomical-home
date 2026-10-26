using System.Collections;
using Capture.Views;
using Game.Player;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Substrate;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class ValisInputScenario : CaptureScenario
{
    public static string Load() => "Valis input scenario loaded";
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    protected override DiagnosticViewConfig View => new() { minHalfHeight = 6f, padding = 2f };
    public override IEnumerator Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
        var pilot = AssetDatabase.LoadAssetAtPath<Commander>("Assets/Prefabs/Pilots/PlayerPilot.prefab");
        var ship = Session.Units.SpawnShip(prefab, pilot, 0, Vector3.zero, GamePlane.Rotation, null);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            yield return Film(ship);
            var step = new WaitForFixedUpdate();
            for (var i = 0; i < 25; i++) yield return step;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.LeftShift));
            yield return null;
            yield return null;
            yield return step;
            Assert.That(ship.Keystone.Active, Is.True, "Space must activate the player's Overdrive.");
            yield return null;
            var block = new MaterialPropertyBlock();
            ship.VisualRig.Silhouette[0].GetPropertyBlock(block);
            Assert.That(block.GetFloat("_DamageFlash"), Is.EqualTo(1f));
            Assert.That(block.GetColor("_BaseColor").g, Is.EqualTo(.9f).Within(.001f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            while (ship.Keystone.Active) yield return step;
            yield return null;
            yield return null;
            ship.VisualRig.Silhouette[0].GetPropertyBlock(block);
            Assert.That(block.GetFloat("_DamageFlash"), Is.Zero);
            for (var i = 0; i < 25; i++) yield return step;
        }
        finally { InputSystem.RemoveDevice(keyboard); }
    }
}
