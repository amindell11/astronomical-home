using System.Collections.Generic;
using System.Linq;
using Loot;
using NUnit.Framework;
using Substrate.Sectors.Elements.Stations;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>The armory's offer: the draw always holds the sector's answer when it is eligible, never twice, in place of one random card when the draw is full; an ineligible or missing answer leaves the plain draw.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class ArmoryOfferEditModeTests
    {
        private readonly List<ScriptableObject> items = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in items) Object.DestroyImmediate(item);
            items.Clear();
        }

        [Test]
        public void AShortDraw_GainsTheAnswer()
        {
            var pool = Pool(2);
            var answer = Item("answer");

            var offer = LootOffers.DrawWith(pool, answer, _ => true, 3, n => 0);

            CollectionAssert.AreEquivalent(pool.Append(answer), offer);
        }

        [Test]
        public void AFullDraw_PutsTheAnswerInPlaceOfOneRandomCard()
        {
            var pool = Pool(5);
            var answer = Item("answer");
            var calls = 0;

            var offer = LootOffers.DrawWith(pool, answer, _ => true, 3, n => calls++ < 3 ? 0 : 1);

            CollectionAssert.AreEqual(new[] { pool[0], answer, pool[2] }, offer,
                "The draw takes the pool's first three in turn, then the answer replaces card two.");
        }

        [Test]
        public void AnAnswerInThePool_IsNeverOfferedTwice()
        {
            var pool = Pool(2);
            var answer = Item("answer");
            pool.Insert(0, answer);

            for (var seed = 0; seed < 3; seed++)
            {
                var offer = LootOffers.DrawWith(pool, answer, _ => true, 3, n => seed % n);
                Assert.AreEqual(1, offer.Count(item => item == answer));
                Assert.AreEqual(3, offer.Distinct().Count());
            }
        }

        [Test]
        public void AnEquippedAnswer_LeavesThePlainDraw()
        {
            var pool = Pool(4);
            var answer = Item("answer");

            var offer = LootOffers.DrawWith(pool, answer, item => item != answer, 3, n => 0);

            CollectionAssert.AreEqual(pool.Take(3), offer);
        }

        [Test]
        public void NoAnswer_LeavesThePlainDraw()
        {
            var pool = Pool(4);

            CollectionAssert.AreEqual(pool.Take(3), LootOffers.DrawWith<ScriptableObject>(pool, null, _ => true, 3, n => 0));
        }

        private List<ScriptableObject> Pool(int count) =>
            Enumerable.Range(0, count).Select(i => Item($"weapon {i}")).ToList();

        private ScriptableObject Item(string name)
        {
            var item = ScriptableObject.CreateInstance<ArmoryFeelProfile>();
            item.name = name;
            items.Add(item);
            return item;
        }
    }
}
