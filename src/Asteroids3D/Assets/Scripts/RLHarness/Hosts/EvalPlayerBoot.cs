using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point of the player eval lane — the boot boundary that resolves ModelAssets out of the convert step's model bundle, then hands <see cref="HarnessSessionHost"/> the spec. A boot failure quits 1 here: a player has no -executeMethod boundary to fail for it.</summary>
    public sealed class EvalPlayerBoot : MonoBehaviour
    {
        [SerializeField] private HarnessAssets assets;

        private AssetBundle bundle;

        private void Awake()
        {
            try
            {
                if (!assets)
                    throw new InvalidOperationException(
                        "EvalPlayerBoot.assets is unset — assign the HarnessAssets catalog on the RLHarnessEval scene's [EvalPlayerBoot].");
                var spec = SessionSpec.ParsePlayerEval(Environment.GetEnvironmentVariable, LoadBundleAsset,
                    () => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null);
                var host = new GameObject("[HarnessSessionHost]").AddComponent<HarnessSessionHost>();
                host.spec = spec;
                host.assets = assets;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EvalPlayerBoot] fatal at boot: {e}");
                Application.Quit(1);
            }
        }

        private ModelAsset LoadBundleAsset(string bundlePath, string assetName)
        {
            if (!bundle)
            {
                bundle = AssetBundle.LoadFromFile(bundlePath);
                if (!bundle)
                    throw new InvalidOperationException($"Failed to load the model bundle at {bundlePath}.");
            }
            var model = bundle.LoadAsset<ModelAsset>(assetName);
            if (!model)
                throw new InvalidOperationException(
                    $"'{assetName}' is missing from the model bundle at {bundlePath} — the convert step (RLEvalModelConvert) writes it.");
            return model;
        }
    }
}
