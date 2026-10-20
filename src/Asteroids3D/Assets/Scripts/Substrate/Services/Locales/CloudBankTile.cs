using UnityEngine;
using UnityEngine.Rendering;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Bakes a cloud-bank sky layer — a <c>Custom/Nebula</c> material with <c>_CloudOpacity</c> above 0 — into a
    /// tile once, so the layer samples a texture instead of evaluating its clouds every frame. The layer is static
    /// and zoom-free, so a tile that wraps in both axes covers unbounded camera travel. Runs in Edit Mode too, so the
    /// Scene view keeps its clouds, and re-bakes whenever the editor sees the material change.
    /// Design: https://github.com/amindell11/astronomical-home/issues/1048
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public sealed class CloudBankTile : MonoBehaviour
    {
        // Two 16:9 screens at the reference zoom, so no cloud repeats within one view.
        private const float TileWorldUnits = 53f;
        private const int TileTexels = 4096;
        private static readonly int TileId = Shader.PropertyToID("_CloudBankTile");
        private static readonly int TileSizeId = Shader.PropertyToID("_CloudBankTileSize");

        private Renderer layer;
        private MaterialPropertyBlock properties;
        private RenderTexture tile;
#if UNITY_EDITOR
        private Hash128 bakedState;
#endif

        private void Awake()
        {
            layer = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            Bake();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += RebakeIfChanged;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= RebakeIfChanged;
#endif
            ReleaseTile();
        }

        private void Bake()
        {
            var material = layer.sharedMaterial;
            if (!tile)
            {
                tile = new RenderTexture(TileTexels, TileTexels, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = name + " cloud-bank tile",
                    useMipMap = true,
                    autoGenerateMips = true,
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    hideFlags = HideFlags.DontSave,
                };
            }

            properties.SetFloat(TileSizeId, TileWorldUnits);
            var commands = new CommandBuffer { name = "Cloud-bank tile bake" };
            commands.SetRenderTarget(tile);
            commands.DrawProcedural(Matrix4x4.identity, material, material.FindPass("CloudBankBake"),
                MeshTopology.Triangles, 3, 1, properties);
            Graphics.ExecuteCommandBuffer(commands);
            commands.Release();
            properties.SetTexture(TileId, tile);
            layer.SetPropertyBlock(properties);
#if UNITY_EDITOR
            bakedState = MaterialState(material);
#endif
        }

        private void ReleaseTile()
        {
            if (!tile) return;
            tile.Release();
            if (Application.isPlaying) Destroy(tile);
            else DestroyImmediate(tile);
            tile = null;
        }

#if UNITY_EDITOR
        private void RebakeIfChanged()
        {
            if (MaterialState(layer.sharedMaterial) != bakedState)
                Bake();
        }

        private static Hash128 MaterialState(Material material)
        {
            var state = new Hash128();
            state.Append(material.GetInstanceID());
            var shader = material.shader;
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                var id = shader.GetPropertyNameId(i);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        state.Append(material.GetFloat(id));
                        break;
                    case ShaderPropertyType.Color:
                    case ShaderPropertyType.Vector:
                        var value = material.GetVector(id);
                        state.Append(value.x);
                        state.Append(value.y);
                        state.Append(value.z);
                        state.Append(value.w);
                        break;
                }
            }
            return state;
        }
#endif
    }
}
