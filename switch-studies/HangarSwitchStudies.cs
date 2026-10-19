using System.Collections;
using System.IO;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals.Wings;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    [Category("UI"), Category("RequiresGraphics")]
    public class HangarSwitchStudies
    {
        private static readonly Quaternion Facing = Substrate.GamePlane.PlanePose(
            Vector3.down, new Vector3(-.32f, 0f, -1f));

        [UnityTest]
        public IEnumerator CaptureAlternatives()
        {
            var offer = AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset");
            var valis = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
            var nightshade = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Nightshade.prefab");
            var stages = new[] { HangarPreviewStage.Create(null, 512, offer.ships),
                HangarPreviewStage.Create(null, 512, offer.ships) };
            stages[0].Show(new ShipLoadout(valis, null, null, null, null));
            stages[1].Show(new ShipLoadout(nightshade, null, null, null, null));
            stages[1].transform.position += Vector3.right * 5000f;
            foreach (var light in stages[1].GetComponentsInChildren<Light>()) light.enabled = false;
            var hulls = new[] { stages[0].GetComponentInChildren<HangarPreviewFlight>(),
                stages[1].GetComponentInChildren<HangarPreviewFlight>() };
            var cameras = new[] { stages[0].GetComponentInChildren<Camera>(), stages[1].GetComponentInChildren<Camera>() };
            var radius = Vector3.Distance(cameras[0].transform.position, hulls[0].transform.parent.position)
                * Mathf.Sin(15f * Mathf.Deg2Rad) / 1.05f;
            var thrust = new float[2];
            for (var i = 0; i < 2; i++)
            {
                hulls[i].enabled = false;
                var index = i;
                foreach (var wings in hulls[i].GetComponentsInChildren<WingVisuals>())
                    wings.Bind(new ShipView(hulls[i].transform, null,
                        () => new PilotCommand { thrust = thrust[index] }, null, false));
            }
            var first = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var second = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var output = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var colors = new Color32[512 * 512];
            var savedDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            try
            {
                for (var style = 0; style < 3; style++)
                {
                    var names = new[] { "a-formation", "b-dissolve", "c-bank-cut" };
                    var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../../../results/hangar-flight/switch-studies", names[style]));
                    Directory.CreateDirectory(directory);
                    for (var frame = 0; frame < 210; frame++)
                    {
                        var time = frame / 30f;
                        var switching = time >= 1.5f;
                        var returning = time >= 4.5f;
                        var elapsed = time - (returning ? 4.5f : 1.5f);
                        var from = returning ? 1 : 0;
                        var to = 1 - from;
                        var duration = style == 0 ? .85f : style == 1 ? .35f : .48f;
                        var t = switching ? Mathf.Clamp01(elapsed / duration) : 0f;
                        var weights = new[] { 0f, 0f };
                        weights[from] = 1f - t;
                        weights[to] = t;
                        for (var i = 0; i < 2; i++)
                        {
                            Idle(hulls[i].transform, time, radius);
                            thrust[i] = switching && t < 1f ? 1f : 0f;
                        }
                        if (switching && style == 0)
                        {
                            var remaining = 1f - t;
                            hulls[from].transform.localPosition += radius * new Vector3(-4f * t * t, .35f * t * t, 2f * t * t);
                            hulls[from].transform.localRotation *= Quaternion.Euler(0f, -48f * t * t, 12f * t);
                            hulls[to].transform.localPosition += radius * new Vector3(3.4f * remaining * remaining * remaining,
                                -.2f * remaining * remaining, 1.4f * remaining * remaining * remaining);
                            hulls[to].transform.localRotation *= Quaternion.Euler(0f, 35f * remaining * remaining, -12f * remaining);
                            weights[from] = t < 1f ? 1f : 0f;
                            weights[to] = 1f;
                        }
                        else if (switching && style == 1)
                        {
                            var ease = t * t * (3f - 2f * t);
                            weights[from] = 1f - ease;
                            weights[to] = ease;
                        }
                        else if (switching && style == 2)
                        {
                            weights[from] = t < .5f ? 1f : 0f;
                            weights[to] = t < .5f ? 0f : 1f;
                            var departing = Mathf.Clamp01(t * 2f);
                            var arriving = Mathf.Clamp01((1f - t) * 2f);
                            hulls[from].transform.localRotation *= Quaternion.Euler(0f, 78f * departing * departing, 0f);
                            hulls[to].transform.localRotation *= Quaternion.Euler(0f, 78f * arriving * arriving, 0f);
                        }
                        yield return null;
                        Read(stages[0], first);
                        Read(stages[1], second);
                        var a = first.GetPixels32();
                        var b = second.GetPixels32();
                        var visible = 0;
                        for (var p = 0; p < colors.Length; p++)
                        {
                            var aa = a[p].a / 255f * weights[0];
                            var ba = b[p].a / 255f * weights[1];
                            var wa = style == 1 ? weights[0] : weights[0] * (1f - ba);
                            var bg = style == 1 ? 1f - aa - ba : (1f - aa) * (1f - ba);
                            colors[p] = new Color32(
                                (byte)Mathf.Clamp(a[p].r * wa + b[p].r * weights[1] + 8f * bg, 0, 255),
                                (byte)Mathf.Clamp(a[p].g * wa + b[p].g * weights[1] + 13f * bg, 0, 255),
                                (byte)Mathf.Clamp(a[p].b * wa + b[p].b * weights[1] + 23f * bg, 0, 255), 255);
                            if (aa + ba > .5f) visible++;
                        }
                        if (frame == 30 || frame == 100 || frame == 190)
                            Assert.That(visible, Is.GreaterThan(1000), "settled subject must be visible");
                        output.SetPixels32(colors);
                        output.Apply();
                        File.WriteAllBytes(Path.Combine(directory, $"frame-{frame:D4}.png"), output.EncodeToPNG());
                    }
                }
            }
            finally
            {
                Time.captureDeltaTime = savedDelta;
                foreach (var stage in stages) Object.Destroy(stage.gameObject);
                Object.Destroy(first);
                Object.Destroy(second);
                Object.Destroy(output);
            }
        }

        private static void Idle(Transform hull, float time, float radius)
        {
            var turn = Mathf.Sin(time * .85f);
            hull.localPosition = radius * new Vector3(.018f * Mathf.Sin(time * .53f), .022f * Mathf.Sin(time * 1.1f), 0f);
            hull.localRotation = Facing * Quaternion.Euler(-6f + 2f * Mathf.Sin(time * .67f), 14f + 8f * turn, 5f * turn);
        }

        private static void Read(HangarPreviewStage stage, Texture2D pixels)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = (RenderTexture)stage.Texture;
            pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
        }
    }
}
