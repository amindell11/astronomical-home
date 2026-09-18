using NUnit.Framework;
using UnityEngine;
using Substrate.Services.Units;
using Substrate.Sectors.Elements;

namespace Tests.EditMode
{
    /// <summary>Deterministic (radius 0) anchor math for <see cref="Respawn.Resolve"/> plus the <see cref="Respawn.Wire"/> guards; the full death→revive pipeline is covered in PlayMode.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class RespawnEditModeTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host) Object.DestroyImmediate(_host);
            _host = null;
        }

        private UnitService Units()
        {
            _host = new GameObject("Units");
            return _host.AddComponent<UnitService>();
        }

        [Test]
        public void Resolve_FixedPoint_ReturnsPoint()
        {
            var policy = new RespawnPolicy
            {
                origin = RespawnPolicy.Origin.FixedPoint,
                point = new Vector2(7, 3),
                radius = 0f,
            };

            Assert.AreEqual(new Vector2(7, 3), Respawn.Resolve(policy),
                "FixedPoint must revive at 'point' when radius is 0 and no producer base is given.");
        }

        [Test]
        public void Resolve_FixedPoint_IsProducerRelative()
        {
            var policy = new RespawnPolicy
            {
                origin = RespawnPolicy.Origin.FixedPoint,
                point = new Vector2(2, -3),
                radius = 0f,
            };

            var producerBase = new Vector2(10, 5);
            Assert.AreEqual(new Vector2(12, 2), Respawn.Resolve(policy, producerBase),
                "FixedPoint must resolve to the producer's base position plus 'point' (producer-relative offset).");
        }

        [Test]
        public void Wire_OriginNone_WiresNothing_ReturnsFalse()
        {
            var policy = new RespawnPolicy { origin = RespawnPolicy.Origin.None };
            Assert.IsFalse(Respawn.Wire(null, policy, Units()),
                "A None policy must wire nothing and report false (even with a null ship).");
        }

        [Test]
        public void Wire_MissingShipOrUnitService_ReturnsFalse()
        {
            var policy = new RespawnPolicy { origin = RespawnPolicy.Origin.FixedPoint };
            Assert.IsFalse(Respawn.Wire(null, policy, Units()), "Null ship must not wire.");
            Assert.IsFalse(Respawn.Wire(null, policy, null), "Null unit service must not wire.");
        }
    }
}
