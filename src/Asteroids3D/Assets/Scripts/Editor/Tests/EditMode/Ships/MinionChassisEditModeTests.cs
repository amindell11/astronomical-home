using NUnit.Framework;
using Ships;
using Ships.Weapons;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Ships
{
    [Category("Ships")]
    public sealed class MinionChassisEditModeTests
    {
        [Test]
        public void ValisMinion_MatchesValisStats()
        {
            var valis = AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/Ships/Valis.prefab") as GameObject;
            var minion = AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/Ships/ValisMinion.prefab") as GameObject;
            Assert.That(valis, Is.Not.Null, "Assets/Prefabs/Ships/Valis.prefab");
            Assert.That(minion, Is.Not.Null, "Assets/Prefabs/Ships/ValisMinion.prefab");

            Same(valis.GetComponent<Ship>(), minion.GetComponent<Ship>(),
                ("mass", "mass"),
                ("maxHealth", "maxHealth"),
                ("engine", "engine"),
                ("shield", "shield"),
                ("keystone", "keystone"));
            Same(valis.GetComponent<Rigidbody>(), minion.GetComponent<Rigidbody>(),
                ("m_Mass", "mass"),
                ("m_ImplicitTensor", "automaticInertiaTensor"),
                ("m_InertiaTensor", "inertiaTensor"),
                ("m_InertiaRotation", "inertiaTensorRotation"));
            Assert.AreEqual(valis.transform.localScale, minion.transform.localScale, "localScale");
            Same(valis.GetComponent<WeaponsController>(), minion.GetComponent<WeaponsController>(),
                ("primaryMount", "primaryMount"),
                ("secondaryMount", "secondaryMount"));

            static void Same(Component left, Component right, params (string path, string name)[] properties)
            {
                using var a = new SerializedObject(left);
                using var b = new SerializedObject(right);
                foreach (var (path, name) in properties)
                {
                    var from = a.FindProperty(path);
                    Assert.That(from, Is.Not.Null, name);
                    Assert.That(SerializedProperty.DataEquals(from, b.FindProperty(path)), Is.True, name);
                }
            }
        }
    }
}
