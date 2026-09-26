#if UNITY_EDITOR
using System;
using Asteroids;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Rendering.AsteroidField
{
    public sealed class DrawnFieldStudyAssets : IDisposable
    {
        private const string Folder = "Assets/Visuals/Environment/Asteroids/";
        public readonly Mesh[] Rocks = new Mesh[10];
        private readonly Mesh[] drawings = new Mesh[10];
        private readonly Material[] paints = new Material[10];
        private readonly Material graphite;
        private readonly Material contour;

        public DrawnFieldStudyAssets()
        {
            graphite = Load<Material>(Folder + "DrawnStudy/AsteroidSurfaceDrawing.mat");
            contour = new Material(Shader.Find("Astronomical/Comparison/Drawn Contour"));
            contour.SetFloat("_ContourPixels", 5.5f);
            contour.SetFloat("_ContourMinimum", .6f);
            contour.SetColor("_ContourColor", new Color(.003f, .004f, .009f));
            for (var i = 0; i < 10; i++)
            {
                var path = Folder + $"DrawnField/Shape{i + 1:D2}/Asteroid{i + 1}";
                Rocks[i] = Load<GameObject>(path + ".fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
                drawings[i] = Load<GameObject>(path + "Drawing.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
                paints[i] = Load<Material>(path + "Paint.mat");
                Assert.That(paints[i].IsKeywordEnabled("_NORMALMAP"), Is.True);
                Assert.That(paints[i].GetTexture("_BumpMap"), Is.Not.Null);
                Assert.That(drawings[i].vertexCount, Is.GreaterThan(100));
            }
        }

        public GameObject Create(int index, Transform parent)
        {
            var rock = Layer("Painted asteroid " + (index + 1), parent, Rocks[index], paints[index]);
            AddDrawing(index, rock.transform);
            return rock;
        }

        public void Apply(AsteroidController rock)
        {
            rock.GetComponent<MeshFilter>().sharedMesh = Rocks[rock.MeshIndex];
            rock.Renderer.sharedMaterial = paints[rock.MeshIndex];
            AddDrawing(rock.MeshIndex, rock.transform);
        }

        private void AddDrawing(int index, Transform parent)
        {
            var ink = Layer("Crease drawing", parent, drawings[index], graphite).GetComponent<MeshRenderer>();
            ink.shadowCastingMode = ShadowCastingMode.Off;
            var outline = Layer("Outer contour", parent, Rocks[index], contour).GetComponent<MeshRenderer>();
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
        }

        private static GameObject Layer(string name, Transform parent, Mesh mesh, Material material)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.layer = parent.gameObject.layer;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            return item;
        }

        public static T Load<T>(string path) where T : Object
        {
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(result, Is.Not.Null, path);
            return result;
        }

        public void Dispose() => Object.DestroyImmediate(contour);
    }
}
#endif
