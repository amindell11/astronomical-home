using System.Linq;
using NUnit.Framework;
using Substrate.Services.Locales;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    [TestFixture]
    [Category("Sectors")]
    public class SectorRunDrawEditModeTests
    {
        private const int Seeds = 200;
        private static readonly string[] PoolNames =
            { "IllustratedBlue", "CopperTurquoise", "CoralSunset", "CyanAurora", "VerdantGiant", "BlueCrescent" };

        private LocaleSky[] pool;
        private SectorRun run;

        [SetUp]
        public void SetUp() =>
            pool = PoolNames
                .Select(name => AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/{name}.prefab"))
                .ToArray();

        [TearDown]
        public void TearDown()
        {
            if (run) Object.DestroyImmediate(run);
        }

        private static SectorEntry NamedRow(LocaleSky locale) => new() { locale = locale };
        private static SectorEntry RandomRow() => new() { random = true };

        [Test]
        public void NamedRows_KeepTheirLocale_AndRandomRowsDrawPoolLocalesNoOtherRowShows()
        {
            run = SectorRun.Create(pool, RandomRow(), NamedRow(pool[0]), RandomRow(), RandomRow(), NamedRow(pool[0]));
            for (var seed = 0; seed < Seeds; seed++)
            {
                var drawn = run.DrawLocales(new System.Random(seed));
                Assert.That(drawn, Has.Length.EqualTo(5));
                Assert.That(drawn[1], Is.SameAs(pool[0]), "a named row keeps its locale");
                Assert.That(drawn[4], Is.SameAs(pool[0]), "named rows may share a locale");
                var randoms = new[] { drawn[0], drawn[2], drawn[3] };
                Assert.That(randoms, Is.SubsetOf(pool.Skip(1)),
                    "Random rows draw from the pool, leaving out named locales");
                Assert.That(randoms, Is.Unique, "no Random row repeats another's locale");
            }
        }

        [Test]
        public void RandomRows_DrawEveryPoolLocaleAcrossRuns()
        {
            run = SectorRun.Create(pool, RandomRow(), RandomRow(), RandomRow());
            var seen = Enumerable.Range(0, Seeds).SelectMany(seed => run.DrawLocales(new System.Random(seed))).Distinct();
            Assert.That(seen, Is.EquivalentTo(pool));
        }

        [Test]
        public void ADuplicatedPoolEntry_IsDrawnOnce()
        {
            run = SectorRun.Create(new[] { pool[1], pool[1], pool[2] }, RandomRow(), RandomRow());
            for (var seed = 0; seed < Seeds; seed++)
                Assert.That(run.DrawLocales(new System.Random(seed)), Is.EquivalentTo(new[] { pool[1], pool[2] }));
        }

        [Test]
        public void TheDraw_NeverAdvancesUnityRandom()
        {
            run = SectorRun.Create(pool, RandomRow(), RandomRow(), RandomRow());
            UnityEngine.Random.InitState(1234);
            var before = UnityEngine.Random.state;
            run.DrawLocales(new System.Random(7));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(before),
                "the sim reads UnityEngine.Random; the draw must not");
        }
    }
}
