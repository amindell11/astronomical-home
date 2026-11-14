#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Capture.Recording;
using Capture.Views;
using Game.Preview;
using NUnit.Framework;
using Ships;
using Substrate.Sectors;
using Substrate.Sectors.Factions;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;

// Scratch capture for #1339: every faction skin (and the player's own paint) as a pair of the same
// ship side by side; the right-hand twin dies mid-clip, so one frame shows a hull beside its wreck.
public sealed class FactionDebrisSheet : CaptureScenario
{
    private const string SectorPath = "Assets/Prefabs/Sectors/TuningSector.prefab";
    private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
    private const float PairGap = 6f;
    private static readonly float[] Columns = { -19.5f, -6.5f, 6.5f, 19.5f };
    private static readonly float[] Rows = { 10.6f, 3.5f, -3.5f, -10.6f };

    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    protected override DiagnosticViewConfig View => new() { minHalfHeight = 15f, padding = 2f };
    protected override CaptureRequest Request => new("results/capture", GetType().Name, Commit, 1920, 1080);

    public override SectorEntry SectorEntry => new()
    {
        prefab = Load<Sector>(SectorPath),
        locale = Load<LocaleSky>(LocalePath),
    };

    public override IEnumerator Run()
    {
        var all = new List<Ship>();
        var victims = new List<Ship>();
        var factions = new[] { "Shoal", "Veil", "Bastion" };
        for (var row = 0; row < factions.Length; row++)
        {
            var faction = Load<Faction>($"Assets/Settings/Factions/{factions[row]}.asset");
            Pair(faction, faction.common.ship, false, 0, row, all, victims);
            Pair(faction, faction.signature.ship, false, 1, row, all, victims);
            Pair(faction, faction.signature.ship, true, 2, row, all, victims);
        }
        var players = new[] { "Crimson", "Valis", "Nightshade", "Bulwark" };
        for (var column = 0; column < players.Length; column++)
            Pair(null, Load<Ship>($"Assets/Prefabs/Ships/{players[column]}.prefab"), false, column, 3, all, victims);

        HideAudioOverlay();
        yield return Film(all.ToArray());
        HideAudioOverlay();
        for (var i = 0; i < 4; i++) yield return null;
        Debug.Log($"FACTION_DEBRIS_KILL_FRAME={Time.frameCount}");
        foreach (var victim in victims) StagedMoments.Kill(victim);
        var end = Time.time + 1.1f;
        while (Time.time < end) yield return null;
    }

    private void Pair(Faction faction, Ship chassis, bool elite, int column, int row, List<Ship> all, List<Ship> victims)
    {
        for (var side = 0; side < 2; side++)
        {
            var plane = new Vector2(Columns[column] + (side == 0 ? -PairGap / 2f : PairGap / 2f), Rows[row]);
            var ship = Session.Units.SpawnShip(chassis, null, 1, Session.Frame.Place(plane), Substrate.GamePlane.Rotation, null);
            Assert.IsNotNull(ship, $"no ship from {chassis.name}");
            if (faction) faction.Dress(chassis, ship, elite);
            all.Add(ship);
            if (side == 1) victims.Add(ship);
        }
    }

    // The FMOD debug window draws over the top-left pair.
    private static void HideAudioOverlay()
    {
        foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            if (behaviour.GetType().Name == "FMODRuntimeManagerOnGUIHelper") behaviour.enabled = false;
    }

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.IsNotNull(asset, $"missing {path}");
        return asset;
    }
}
#endif
