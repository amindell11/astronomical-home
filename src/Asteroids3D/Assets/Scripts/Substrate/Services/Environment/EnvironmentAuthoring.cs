using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Environment
{
    /// <summary>
    /// A locale scene's environment authoring root. Draws its flat background material behind the
    /// flight camera only while its scene is the active locale, mapped by absolute camera position so
    /// travel never accumulates drift. Preview a candidate by swapping the material in Play Mode (Unity
    /// reverts it on exit); apply by assigning it in Edit Mode. Design: arc #678.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentAuthoring : MonoBehaviour
    {
        private static readonly int MappingId = Shader.PropertyToID("_Mapping");
        private static readonly int RepeatDistanceId = Shader.PropertyToID("_RepeatDistance");
        private static readonly int ViewHeightInTilesId = Shader.PropertyToID("_ViewHeightInTiles");

        [SerializeField, Tooltip("Flat background material from Visuals/Environment/Flat/Generated. Swap it in Play Mode to preview a candidate.")]
        private Material background;
        private Mesh triangle;
        private MaterialPropertyBlock properties;

        private void Awake()
        {
            triangle = new Mesh { name = "Flat background triangle" };
            triangle.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 3, 0), new Vector3(3, -1, 0) };
            triangle.triangles = new[] { 0, 1, 2 };
            triangle.bounds = new Bounds(Vector3.zero, Vector3.one * 10);
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable() => FlatBackgroundCamera.Rendering += Draw;
        private void OnDisable() => FlatBackgroundCamera.Rendering -= Draw;
        private void OnDestroy() => Destroy(triangle);

        public static Vector2 Offset(Vector3 position, float distance)
        {
            if (!(distance > 0) || float.IsInfinity(distance))
                throw new ArgumentOutOfRangeException(nameof(distance));
            var x = (double)position.x / distance;
            var y = (double)position.y / distance;
            return new Vector2((float)(x - Math.Floor(x)), (float)(y - Math.Floor(y)));
        }

        private void Draw(Camera camera)
        {
            if (gameObject.scene != SceneManager.GetActiveScene())
                return;
            if (!background)
                throw new InvalidOperationException($"{name} has no flat background material assigned.");
            var offset = Offset(camera.transform.position, background.GetFloat(RepeatDistanceId));
            var tiles = background.GetFloat(ViewHeightInTilesId);
            properties.SetVector(MappingId, new Vector4(offset.x, offset.y, tiles * camera.aspect, tiles));
            Graphics.DrawMesh(triangle, Matrix4x4.Translate(camera.transform.position + camera.transform.forward),
                background, gameObject.layer, camera, 0, properties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }
    }
}
