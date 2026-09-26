using NUnit.Framework;
using Substrate.Services.Environment;
using UnityEngine;

namespace Tests.EditMode.Environments
{
    [Category("Sectors")]
    public sealed class EnvironmentAuthoringEditModeTests
    {
        [Test]
        public void Offset_LongTravelInBothDirectionsAndRepeatedReturnsDoNotAccumulateDrift()
        {
            const float distance = 65536;
            var origin = new Vector3(128, 384, 0);
            var expected = EnvironmentAuthoring.Offset(origin, distance);
            for (int cycle = -5000; cycle <= 5000; cycle++)
            {
                var position = origin + new Vector3(cycle * distance, -cycle * distance, cycle);
                Assert.That(EnvironmentAuthoring.Offset(position, distance), Is.EqualTo(expected));
                Assert.That(EnvironmentAuthoring.Offset(origin, distance), Is.EqualTo(expected));
            }
            Assert.That(EnvironmentAuthoring.Offset(new Vector3(-128, -384, 0), distance),
                Is.EqualTo(Vector2.one - expected));
        }
    }
}
