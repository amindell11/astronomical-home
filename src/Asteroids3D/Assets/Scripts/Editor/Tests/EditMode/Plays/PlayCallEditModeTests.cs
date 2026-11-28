#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AI.Plays;
using Movement;
using NUnit.Framework;
using Ships;
using UnityEngine;

namespace Tests.EditMode.Plays
{
    [Category("AI")]
    public class PlayCallEditModeTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private Ship rival;
        private ScriptedPlay play;
        private PlayCall call;

        [SetUp]
        public void SetUp()
        {
            rival = Track(new GameObject("rival")).AddComponent<Ship>();
            play = Track(ScriptableObject.CreateInstance<ScriptedPlay>());
            play.readDelay = 1f;
            play.reformSeconds = 2f;
            play.answerSeconds = 3f;
            call = new PlayCall(rival, new WavePlay[] { play }, new System.Random(1));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void ReadA_Commits_ThenAnswersAfterTheDelay()
        {
            play.read = PlayRead.A;
            Assert.AreEqual(PlayStage.Imposing, call.Stage);

            call.Command(Pose(), Pose(), 5f);
            Assert.AreEqual(PlayStage.Imposing, call.Stage);

            call.Command(Pose(), Pose(), 5f);
            Assert.AreEqual(PlayStage.Committing, call.Stage);

            call.Command(Pose(), Pose(), 5f + play.readDelay - 0.5f);
            Assert.AreEqual(PlayStage.Committing, call.Stage);

            call.Command(Pose(), Pose(), 5f + play.readDelay);
            Assert.AreEqual(PlayStage.AnswerA, call.Stage);
        }

        [Test]
        public void AnswerB_Reforms_ThenImposesAgain()
        {
            play.read = PlayRead.B;
            call.Command(Pose(), Pose(), 0f);
            call.Command(Pose(), Pose(), 0f);
            var answerAt = play.readDelay;
            call.Command(Pose(), Pose(), answerAt);
            Assert.AreEqual(PlayStage.AnswerB, call.Stage);

            var reformAt = answerAt + play.answerSeconds;
            call.Command(Pose(), Pose(), reformAt - 0.5f);
            Assert.AreEqual(PlayStage.AnswerB, call.Stage);
            call.Command(Pose(), Pose(), reformAt);
            Assert.AreEqual(PlayStage.Reforming, call.Stage);

            var imposeAt = reformAt + play.reformSeconds;
            call.Command(Pose(), Pose(), imposeAt - 0.5f);
            Assert.AreEqual(PlayStage.Reforming, call.Stage);
            call.Command(Pose(), Pose(), imposeAt);
            Assert.AreEqual(PlayStage.Imposing, call.Stage);
        }

        [Test]
        public void LoseTarget_Searches_AndTheNextCommandImposesWithFreshClocks()
        {
            play.read = PlayRead.A;
            call.Command(Pose(), Pose(), 0f);
            call.Command(Pose(), Pose(), 0f);
            Assert.AreEqual(PlayStage.Committing, call.Stage);

            call.LoseTarget();
            Assert.AreEqual(PlayStage.Searching, call.Stage);
            call.LoseTarget();
            Assert.AreEqual(PlayStage.Searching, call.Stage);

            const float restart = 40f;
            call.Command(Pose(), Pose(), restart);
            Assert.AreEqual(PlayStage.Imposing, call.Stage);
            var view = call.View(Pose(), Pose(), restart);
            Assert.AreEqual(0f, view.stageSeconds, 1e-4f);
            Assert.AreEqual(0f, view.imposedSeconds, 1e-4f);

            call.Command(Pose(), Pose(), restart);
            Assert.AreEqual(PlayStage.Committing, call.Stage);
            call.Command(Pose(), Pose(), restart + play.readDelay - 0.5f);
            Assert.AreEqual(PlayStage.Committing, call.Stage);
            call.Command(Pose(), Pose(), restart + play.readDelay);
            Assert.AreEqual(PlayStage.AnswerA, call.Stage);
        }

        [Test]
        public void EmptyBook_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PlayCall(rival, Array.Empty<WavePlay>(), new System.Random(0)));
        }

        private T Track<T>(T item) where T : UnityEngine.Object
        {
            created.Add(item);
            return item;
        }

        private static Kinematics Pose() => new(Vector2.zero, Vector2.zero, 0f, 0f, 0f);

        private sealed class ScriptedPlay : WavePlay
        {
            internal PlayRead read;
            internal float answerSeconds = 3f;

            internal override float AnswerSeconds => answerSeconds;

            internal override PlayRead Read(in PlayView view) => read;

            internal override PlayOrder MinionOrder(in PlayView view, in Kinematics self, int rank, float speed) =>
                new PlayOrder(Vector2.zero, false);

            internal override PlayOrder? RivalOrder(in PlayView view, float speed) => null;
        }
    }
}
#endif
