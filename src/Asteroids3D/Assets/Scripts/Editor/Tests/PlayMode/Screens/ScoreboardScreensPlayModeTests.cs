#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Damage;
using Game.Player;
using Game.Runs;
using NUnit.Framework;
using Ships.Registry;
using UI.Screens;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Screens
{
    /// <summary>
    /// Both run-end screens show the scoreboard: this run's rank out of every run on the board, and
    /// the top five runs' sectors, kills, time and date, or NOT RANKED for a run that went unrecorded.
    /// </summary>
    [TestFixture]
    [Category("UI")]
    public class ScoreboardScreensPlayModeTests
    {
        private static readonly DateTime Ended = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        private sealed class FixedTally : IRunTally
        {
            public int Kills => 4;
            public float SecondsSurvived => 75f;
        }

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.Destroy(_root);
        }

        private static Scoreboard.Row[] SevenRuns() =>
            Enumerable.Range(1, 7).Select(kills => new Scoreboard.Row(1, kills, 60f, Ended)).ToArray();

        private static string TextAt(Component screen, string path) =>
            screen.transform.Find("Dim/Panel/" + path).GetComponent<Text>().text;

        [UnityTest]
        public IEnumerator DeathRecap_ShowsTheRank_AndTheTopFive()
        {
            _root = new GameObject("RecapRoot");
            var screen = DeathRecapScreen.Create(_root.transform);
            var blow = new DamageInfo(10f, DamageKind.Collision, ShipId.Invalid, 0f, Vector3.zero, Vector3.zero);
            var board = new Scoreboard(SevenRuns(), new Scoreboard.Row(1, 5, 90f, Ended));

            screen.Show(blow, new DamageLedger(), new FixedTally(), board, null);
            yield return null;

            Assert.AreEqual("RANK 3 OF 8", TextAt(screen, "Rank"));
            CollectionAssert.AreEqual(new[] { "KILLS", "7", "6", "5", "5", "4" }, TextAt(screen, "Board/Kills").Split('\n'));
            Assert.AreEqual(6, TextAt(screen, "Board/Date").Split('\n').Length, "a header and five dated rows");
        }

        [UnityTest]
        public IEnumerator WinScreen_ShowsTheBoard_AndAnUnrecordedRunAsNotRanked()
        {
            _root = new GameObject("WinRoot");
            var screen = RunWonScreen.Create(_root.transform);

            screen.Show(new FixedTally(), 3, new Scoreboard(SevenRuns(), null), null);
            yield return null;

            Assert.AreEqual("NOT RANKED", TextAt(screen, "Rank"));
            CollectionAssert.AreEqual(new[] { "TIME", "1:00", "1:00", "1:00", "1:00", "1:00" },
                TextAt(screen, "Board/Time").Split('\n'));
        }
    }
}
#endif
