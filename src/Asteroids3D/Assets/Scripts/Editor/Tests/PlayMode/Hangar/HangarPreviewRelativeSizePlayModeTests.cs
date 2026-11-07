#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    // The stage camera renders into a RenderTexture: URP cannot create RTs under -nographics.
    [Category("UI")]
    [Category("RequiresGraphics")]
    public class HangarPreviewRelativeSizePlayModeTests
    {
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const float SettleSeconds = 1.2f;

        private HangarPreviewStage stage;
        private GameObject holder;

        [TearDown]
        public void TearDown()
        {
            if (stage) Object.Destroy(stage.gameObject);
            if (holder) Object.Destroy(holder);
        }

        [UnityTest]
        public IEnumerator DoublingAuthoredScale_KeepsOnScreenSize()
        {
            var valis = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            Assert.IsNotNull(valis, "Valis prefab loads");
            // Inactive parent: the enlarged copy's ship components never wake.
            holder = new GameObject("InactiveShips");
            holder.SetActive(false);
            var doubled = Object.Instantiate(valis, holder.transform);
            doubled.transform.localScale = valis.transform.localScale * 2f;

            var ships = new[] { valis, doubled };
            stage = HangarPreviewStage.Create(null, new Vector2Int(256, 256), new Rect(0f, 0f, 1f, 1f), ships);
            var camera = stage.GetComponentInChildren<Camera>();

            var onScreen = new float[ships.Length];
            for (var i = 0; i < ships.Length; i++)
            {
                stage.Show(new ShipLoadout(ships[i], null, null), backwards: false);
                yield return new WaitForSeconds(SettleSeconds);
                var hull = stage.transform.Find($"Anchor/{ships[i].name}_Preview");
                // Same hull mesh, so on-screen size goes as world scale over camera distance.
                onScreen[i] = hull.lossyScale.x / Vector3.Distance(camera.transform.position, hull.position);
            }

            Assert.That(onScreen[1] / onScreen[0], Is.EqualTo(1f).Within(0.01f),
                "every hull is fitted to the same share of the hangar preview, whatever its authored scale");
        }
    }
}
#endif
