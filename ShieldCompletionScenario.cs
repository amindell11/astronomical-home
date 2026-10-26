#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Capture.Recording;
using Capture.Views;
using Damage;
using Ships;
using Ships.Registry;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShieldCompletionScenario : CaptureScenario
{
    private Ship ship;
    private Image ring;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    protected override DiagnosticViewConfig View => new() { minHalfHeight = 4f, padding = 2f };
    protected override CaptureRequest Request => new("results/capture", "shield-completion", Commit, 960, 640,
        measurements: new Dictionary<string, System.Func<double>>
        {
            ["shield"] = () => ship.Damage.Shield.CurrentValue,
            ["arc"] = () => ring.enabled ? ring.fillAmount : 0f,
            ["highlight"] = () => ring.material.GetFloat("_Highlight")
        });

    public override IEnumerator Run()
    {
        ship = Session.Units.SpawnShip(TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"),
            null, 0, Vector3.zero, Quaternion.identity, null);
        ship.Damage.Shield.Configure(100f, 30f, 0.6f);
        ring = ship.GetComponentInChildren<ShieldUI>().GetComponent<Image>();
        yield return null;
        yield return Film(ship);
        yield return new WaitForSeconds(0.4f);
        ship.Damage.TakeDamage(new DamageInfo(90f, DamageKind.Laser, ShipId.Invalid,
            1f, Vector3.left, ship.transform.position + Vector3.right));
        yield return new WaitForSeconds(6.2f);
    }
}
#endif
