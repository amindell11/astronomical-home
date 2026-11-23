using NUnit.Framework;
using Ships.Damage;

namespace Tests.EditMode.Ships
{
    [Category("Ships")]
    public sealed class ResourceEditModeTests
    {
        [Test]
        public void Restore_AddsItsPoints_AndStopsAtMax()
        {
            var resource = new Resource(100f);
            resource.ApplyDamage(50f);

            resource.Restore(10f);
            Assert.That(resource.CurrentValue, Is.EqualTo(60f), "A restore adds its points, not a share of max.");
            resource.Restore(70f);
            Assert.That(resource.CurrentValue, Is.EqualTo(100f), "A restore never passes max.");
        }
    }
}
