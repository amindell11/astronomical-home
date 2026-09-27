#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Damage;
using Game.Player;
using NUnit.Framework;
using Ships.Registry;
using UI.Screens;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    /// <summary>The death recap renders the run tally's kills and time survived as their own lines.</summary>
    [TestFixture]
    [Category("UI")]
    public class DeathRecapPlayModeTests
    {
        private sealed class FixedTally : IRunTally
        {
            public int Kills => 7;
            public float SecondsSurvived => 83.4f;
        }

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator Recap_RendersKillsAndTimeSurvived()
        {
            _root = new GameObject("RecapRoot");
            var screen = DeathRecapScreen.Create(_root.transform);
            var blow = new DamageInfo(10f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);

            screen.Show(blow, null, new FixedTally(), null);
            yield return null;

            var lines = screen.GetComponentsInChildren<Text>().SelectMany(t => t.text.Split('\n')).ToList();
            CollectionAssert.Contains(lines, "Kills: 7");
            CollectionAssert.Contains(lines, "Time survived: 1:23");
        }
    }
}
#endif
