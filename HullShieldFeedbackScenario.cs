#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Capture.Recording;
using Capture.Views;
using Damage;
using Ships;
using Ships.Registry;
using Ships.Visuals;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UnityEngine;

public sealed class HullShieldFeedbackScenario : CaptureScenario
{
    private readonly List<Ship> ships = new();
    private readonly List<HullVisuals> hulls = new();
    private int phase;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    protected override DiagnosticViewConfig View => new() { minHalfHeight = 11f, padding = 6f };
    protected override CaptureRequest Request => new("results/capture", "hull-shield-feedback", Commit, 1280, 720,
        measurements: new Dictionary<string, System.Func<double>>
        {
            ["phase"] = () => phase,
            ["shield"] = () => ships[0].Damage.Shield.CurrentValue,
            ["hull"] = () => ships[0].Damage.Health.CurrentValue
        });

    public override IEnumerator Run()
    {
        var names = new[] { "Vanguard", "Crimson", "Nightshade", "Valis", "Bulwark" };
        for (var i = 0; i < names.Length; i++)
        {
            var ship = Session.Units.SpawnShip(TestAssets.Load<Ship>("Assets/Prefabs/Ships/" + names[i] + ".prefab"),
                null, 0, new Vector3((i - 2) * 8f, 0f, 0f), Quaternion.identity, null);
            ship.Damage.Health.Configure(100f);
            ship.Damage.Shield.Configure(100f, 40f, 1.5f);
            ships.Add(ship);
            hulls.Add(ship.GetComponentInChildren<HullVisuals>());
        }
        yield return null;
        yield return Film(ships.ToArray());
        yield return new WaitForSeconds(0.6f);
        phase = 1;
        Hit(20f);
        yield return new WaitForSeconds(1f);
        phase = 2;
        Hit(10f, true);
        yield return new WaitForSeconds(1f);
        phase = 3;
        Hit(100f);
        yield return new WaitForSeconds(1f);
        phase = 4;
        foreach (var ship in ships) ship.Damage.TakeDamage(new DamageInfo(ship.Damage.Health.CurrentValue - 20f,
            DamageKind.Laser, ShipId.Invalid, 1f, Vector3.left, ship.transform.position + Vector3.right, true));
        yield return new WaitForSeconds(5.5f);
        phase = 5;
        foreach (var hull in hulls) hull.HoldFlash(Color.white);
        Hit(5f, true);
        yield return new WaitForSeconds(0.65f);
        foreach (var hull in hulls) hull.ReleaseFlash();
        yield return new WaitForSeconds(0.5f);
        phase = 6;
        foreach (var hull in hulls) hull.HoldFlash(Color.cyan);
        Hit(5f, true);
        yield return new WaitForSeconds(0.65f);
        foreach (var hull in hulls) hull.ReleaseFlash();
        phase = 7;
        foreach (var ship in ships) ship.ResetShip();
        yield return new WaitForSeconds(1f);
    }

    private void Hit(float amount, bool bypass = false)
    {
        foreach (var ship in ships) ship.Damage.TakeDamage(new DamageInfo(amount, DamageKind.Laser, ShipId.Invalid,
            1f, Vector3.left, ship.transform.position + Vector3.right, bypass));
    }
}
#endif
