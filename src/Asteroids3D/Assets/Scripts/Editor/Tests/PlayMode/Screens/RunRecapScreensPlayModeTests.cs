#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UI.Screens;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    [Category("UI")]
    public class RunRecapScreensPlayModeTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root) Object.Destroy(root);
        }

        private static string TextAt(Component screen, string path) =>
            screen.transform.Find("Dim/Panel/" + path).GetComponent<Text>().text;

        [UnityTest]
        public IEnumerator DeathRecap_ShowsKillsAndZeroClearedSectors_AndContinues()
        {
            root = new GameObject("RecapRoot");
            var screen = DeathRecapScreen.Create(root.transform);
            var continued = false;

            screen.Show(7, 0, () => continued = true);
            yield return null;

            Assert.AreEqual("SHIP DESTROYED", TextAt(screen, "Title"));
            Assert.AreEqual("7", TextAt(screen, "Stats/Kills/Value"));
            Assert.AreEqual("0", TextAt(screen, "Stats/Sectors/Value"));
            Assert.AreEqual("SECTORS CLEARED", TextAt(screen, "Stats/Sectors/Label"));
            screen.transform.Find("Dim/Panel/Footer/Continue").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(continued);
        }

        [UnityTest]
        public IEnumerator Extraction_ShowsKillsAndAllClearedSectors_AndContinues()
        {
            root = new GameObject("WinRoot");
            var screen = RunWonScreen.Create(root.transform);
            var continued = false;

            screen.Show(12, 3, () => continued = true);
            yield return null;

            Assert.AreEqual("EXTRACTED", TextAt(screen, "Title"));
            Assert.AreEqual("12", TextAt(screen, "Stats/Kills/Value"));
            Assert.AreEqual("3", TextAt(screen, "Stats/Sectors/Value"));
            screen.transform.Find("Dim/Panel/Footer/Continue").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(continued);
        }
    }
}
#endif
