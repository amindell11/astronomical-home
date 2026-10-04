using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals;
using Ships.Visuals.Breakup;
using Substrate;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
    /// <summary>
    /// The anatomy law, enforced on every chassis the catalog lists except the legacy
    /// names: a depth-1 variant of ShipBase, the fixed slot set, overrides confined to declared
    /// slots, sockets placed by the hull, and hull meshes that come from the ship's own FBX.
    /// </summary>
    [Category("Ships")]
    public sealed class ShipAnatomyEditModeTests
    {
        private const string BasePath = "Assets/Prefabs/Ships/ShipBase.prefab";
        private const string BaseRigPath = "Assets/Visuals/Ships/_Shared/ShipBaseRig.prefab";
        private const string Rig = "ShipBaseRig";
        private const string HullSlot = Rig + "/Hull";
        private static readonly string[] Sockets = { "Hardpoints/Primary", "Hardpoints/Secondary", Rig + "/Thruster/EngineExhaust", Rig + "/Thruster/Reactor" };

        private static readonly (string path, System.Type type, string propertyPrefix)[] Slots =
        {
            ("", typeof(Transform), "m_LocalScale"),
            ("", typeof(Ship), ""),
            ("", typeof(Rigidbody), "m_Mass"),
            ("", typeof(Rigidbody), "m_InertiaTensor"),
            ("", typeof(Rigidbody), "m_InertiaRotation"),
            ("", typeof(Rigidbody), "m_ImplicitTensor"),
            ("Mesh", typeof(MeshCollider), "m_Mesh"),
            (Rig + "/MinimapMarker", typeof(MeshFilter), "m_Mesh"),
            (Rig, typeof(HullVisuals), "m_Enabled"),
            (Rig, typeof(ShipBreakupVisual), "hull"),
            (Rig, typeof(ShipBreakupVisual), "debrisPrefab"),
            (Rig, typeof(ShipBreakupVisual), "poseSources"),
        };

        public static IEnumerable<TestCaseData> MigratedChassis()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            foreach (var chassis in catalog.Chassis)
            {
                if (ShipLegacyList.Chassis.Contains(chassis.name)) continue;
                yield return new TestCaseData(AssetDatabase.GetAssetPath(chassis)).SetName($"{{m}}({chassis.name})");
            }
        }

        [Test]
        public void LegacyList_NamesOnlyCatalogChassis()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            var listed = catalog.Chassis.Select(c => c.name).ToList();
            foreach (var name in ShipLegacyList.Chassis)
                Assert.That(listed, Does.Contain(name), $"{name} left the catalog; remove it from the legacy list.");
            Assert.That(listed.Count, Is.GreaterThan(ShipLegacyList.Chassis.Length), "At least one migrated chassis must exercise the anatomy.");
        }

        [Test]
        public void ShipBase_IsNeutralAndNotAVariant()
        {
            var root = Load(BasePath);
            Assert.That(PrefabUtility.GetPrefabAssetType(root), Is.EqualTo(PrefabAssetType.Regular));
            Assert.That(root.transform.Find("Mesh").GetComponent<MeshCollider>().sharedMesh, Is.Null, "The base collider slot is empty.");
            Assert.That(root.transform.Find(HullSlot).GetComponentsInChildren<Renderer>(true), Is.Empty, "The base hull slot is empty.");
            Assert.That(root.transform.Find(Rig + "/MinimapMarker").GetComponent<MeshFilter>().sharedMesh, Is.Null);
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root.transform.Find(Rig).gameObject)), Is.EqualTo(BaseRigPath));
            foreach (var socket in Sockets)
                Assert.That(root.transform.Find(socket).localPosition, Is.EqualTo(Vector3.zero), socket);
        }

        [TestCaseSource(nameof(MigratedChassis))]
        public void Chassis_IsADepthOneVariantOfShipBase(string path)
        {
            var root = Load(path);
            Assert.That(PrefabUtility.GetPrefabAssetType(root), Is.EqualTo(PrefabAssetType.Variant));
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(BasePath), "Every chassis is a variant of ShipBase, never of another ship.");
            Assert.That(root.GetComponentsInChildren<ShipVisualRig>(true), Has.Length.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<HullVisuals>(true), Has.Length.EqualTo(1));
        }

        [TestCaseSource(nameof(MigratedChassis))]
        public void Chassis_FillsOnlyDeclaredSlots(string path)
        {
            var root = Load(path);
            Assert.That(PrefabUtility.GetRemovedComponents(root), Is.Empty, "A variant may not remove base components.");
            Assert.That(PrefabUtility.GetRemovedGameObjects(root), Is.Empty, "A variant may not remove base objects.");
            Assert.That(PrefabUtility.GetAddedComponents(root), Is.Empty, "A variant may not add components to the base.");
            var slot = root.transform.Find(HullSlot);
            foreach (var added in PrefabUtility.GetAddedGameObjects(root))
                Assert.That(added.instanceGameObject.transform.parent, Is.EqualTo(slot), $"{added.instanceGameObject.name}: additions go under the Hull slot only.");
            var undeclared = new List<string>();
            foreach (var mod in PrefabUtility.GetPropertyModifications(root))
            {
                if (PrefabUtility.IsDefaultOverride(mod)) continue;
                var instance = InstanceObjectFor(root, mod.target);
                Assert.That(instance, Is.Not.Null, $"stale override {mod.propertyPath} on {mod.target}");
                var instancePath = PathOf(instance, root.transform);
                var type = instance.GetType();
                var declared = Slots.Any(s => s.path == instancePath && s.type.IsAssignableFrom(type) && mod.propertyPath.StartsWith(s.propertyPrefix))
                    || Sockets.Contains(instancePath) && type == typeof(Transform) && mod.propertyPath.StartsWith("m_Local")
                    || IsFlameTuning(instance, mod.propertyPath);
                if (!declared) undeclared.Add($"{instancePath}#{type.Name}.{mod.propertyPath}");
            }
            Assert.That(undeclared, Is.Empty, "Overrides outside the declared slots; a new difference needs a new slot on the base.");
        }

        [TestCaseSource(nameof(MigratedChassis))]
        public void Chassis_PlacesEverySocket(string path)
        {
            var root = Load(path);
            var mods = PrefabUtility.GetPropertyModifications(root);
            foreach (var socket in Sockets)
            {
                var transform = root.transform.Find(socket);
                Assert.That(transform, Is.Not.Null, socket);
                var placed = mods.Any(m => m.propertyPath.StartsWith("m_LocalPosition") && InstanceObjectFor(root, m.target) == transform);
                Assert.That(placed, Is.True, $"{socket}: the hull must place every socket.");
            }
            var hull = HullBounds(root);
            var exhaust = root.transform.InverseTransformPoint(root.transform.Find(Rig + "/Thruster/EngineExhaust").position);
            Assert.That(exhaust.y, Is.LessThan(hull.center.y - hull.extents.y * .5f), "The engine exhaust sits aft of the hull; the prow faces +Y.");
            foreach (var hardpoint in new[] { "Hardpoints/Primary", "Hardpoints/Secondary" })
                Assert.That(root.transform.InverseTransformPoint(root.transform.Find(hardpoint).position).y, Is.GreaterThan(exhaust.y), hardpoint);
        }

        [TestCaseSource(nameof(MigratedChassis))]
        public void Chassis_HullSlotHoldsItsOwnRoleMeshes(string path)
        {
            var root = Load(path);
            var fbx = $"Assets/Visuals/Ships/{root.name}/{root.name}.fbx";
            var renderers = root.transform.Find(HullSlot).GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, "The hull slot carries the painted geometry.");
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.enabled && IsActiveInPrefab(renderer.transform), Is.True, renderer.name);
                Assert.That(renderer.gameObject.layer, Is.EqualTo(LayerIds.Ship), renderer.name);
                var skinned = renderer as SkinnedMeshRenderer;
                var mesh = skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null, renderer.name);
                Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo(fbx), $"{renderer.name}: hull meshes come from the ship's own role export.");
                Assert.That(renderer.sharedMaterials.All(AssetDatabase.Contains), Is.True, renderer.name);
            }
            var collider = root.transform.Find("Mesh").GetComponent<MeshCollider>();
            Assert.That(collider.sharedMesh, Is.Not.Null, "The collider slot is filled.");
            if (!ShipLegacyList.SavedColliderMeshes.Contains(root.name))
                Assert.That(AssetDatabase.GetAssetPath(collider.sharedMesh), Is.EqualTo(fbx), "The collider comes from the Collider role.");
            Assert.That(root.transform.Find(Rig + "/MinimapMarker").GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, "The minimap slot is filled.");
            var breakup = new SerializedObject(root.transform.Find(Rig).GetComponent<ShipBreakupVisual>());
            Assert.That(breakup.FindProperty("debrisPrefab").objectReferenceValue, Is.Not.Null, "The breakup slot is filled.");
            var breakupHull = (Transform)breakup.FindProperty("hull").objectReferenceValue;
            Assert.That(breakupHull && breakupHull.IsChildOf(root.transform.Find(HullSlot)), Is.True, "The breakup hull is the hull slot's content.");
        }

        [TestCaseSource(nameof(MigratedChassis))]
        public void Chassis_HullKeepsTheContourAsASeparateVertexRange(string path)
        {
            var root = Load(path);
            foreach (var filter in root.transform.Find(HullSlot).GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh.subMeshCount < 2) continue;
                Assert.That(mesh.GetIndexCount(1), Is.EqualTo(mesh.GetIndexCount(0)), $"{mesh.name}: the contour copies every surface triangle.");
                Assert.That(mesh.GetIndices(0).Intersect(mesh.GetIndices(1)), Is.Empty,
                    $"{mesh.name}: contour normals need their own vertex range; vertex welding on import merges them away.");
            }
        }

        private static Bounds HullBounds(GameObject root)
        {
            var renderers = root.transform.Find(HullSlot).GetComponentsInChildren<Renderer>(true);
            var matrix = root.transform.worldToLocalMatrix * renderers[0].transform.localToWorldMatrix;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(renderers[0].localBounds.center), Vector3.zero);
            foreach (var renderer in renderers)
            {
                matrix = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                var local = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(point));
                }
            }
            return bounds;
        }

        private static Object InstanceObjectFor(GameObject root, Object source)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == source) return t.gameObject;
                foreach (var component in t.GetComponents<Component>())
                    if (PrefabUtility.GetCorrespondingObjectFromSource(component) == source) return component;
            }
            return null;
        }

        private static string PathOf(Object instance, Transform root)
        {
            var component = instance as Component;
            var transform = component ? component.transform : ((GameObject)instance).transform;
            var names = new List<string>();
            for (var current = transform; current != root; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static bool IsFlameTuning(Object instance, string propertyPath)
        {
            var component = instance as Component;
            var transform = component ? component.transform : null;
            if (!transform || !transform.parent || !Sockets.Contains(PathOf(transform.parent, transform.root))) return false;
            return instance as ParticleSystemRenderer != null && propertyPath.StartsWith("m_Materials")
                || instance as ParticleSystem != null && propertyPath.StartsWith("InitialModule.startColor")
                || instance as Transform != null && propertyPath.StartsWith("m_LocalScale");
        }

        private static bool IsActiveInPrefab(Transform transform)
        {
            for (var current = transform; current; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }

        private static GameObject Load(string path)
        {
            var root = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            Assert.That(root, Is.Not.Null, path);
            return root;
        }
    }
}
