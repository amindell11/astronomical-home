using NUnit.Framework;
using Ships;

namespace Tests.EditMode
{
    [Category("Ships")]
    public sealed class ShipRoleNamesEditModeTests
    {
        [TestCase("Assets/Visuals/Ships/Crimson/Crimson.fbx", "Crimson")]
        [TestCase("Assets/Visuals/Ships/Nightshade/Nightshade.fbx", "Nightshade")]
        public void TryGetShipName_AcceptsOnlyTheMirroredHullPath(string path, string expected)
        {
            Assert.That(ShipRoleNames.TryGetShipName(path, out var name), Is.True);
            Assert.That(name, Is.EqualTo(expected));
        }

        [TestCase("Assets/Visuals/Ships/Crimson/Other.fbx")]
        [TestCase("Assets/Visuals/Ships/Ship3/Study/Ship3.fbx")]
        [TestCase("Assets/Visuals/Studies/Crimson/Crimson.fbx")]
        [TestCase("Assets/Visuals/Ships/Crimson/Crimson.blend")]
        public void TryGetShipName_RejectsEverythingElse(string path)
        {
            Assert.That(ShipRoleNames.TryGetShipName(path, out _), Is.False);
        }

        [Test]
        public void Validate_PassesTheExporterGrammar()
        {
            Assert.That(ShipRoleNames.Validate(new[] { "Hull" }), Is.Empty);
            Assert.That(ShipRoleNames.Validate(new[] { "Hull", "Canopy", "Cores", "Ink", "Collider", "Sockets" }), Is.Empty);
        }

        [Test]
        public void Validate_ReportsMissingUnknownAndDuplicateRoles()
        {
            Assert.That(ShipRoleNames.Validate(new string[0]), Has.Count.EqualTo(1).And.Some.Contains("missing role node 'Hull'"));
            Assert.That(ShipRoleNames.Validate(new[] { "Hull", "Wing" }), Has.Count.EqualTo(1).And.Some.Contains("unknown role node 'Wing'"));
            Assert.That(ShipRoleNames.Validate(new[] { "Hull", "Hull" }), Has.Count.EqualTo(1).And.Some.Contains("duplicate role node 'Hull'"));
            Assert.That(ShipRoleNames.Validate(new[] { "Canopy", "Wing", "Wing" }), Has.Count.EqualTo(3));
        }
    }
}
