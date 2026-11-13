using System;
using NUnit.Framework;
using UI.Screens;

namespace Tests.EditMode
{
    [Category("UI")]
    public class SettingsRowEditModeTests
    {
        [Test]
        public void Factories_RejectANullCallback()
        {
            Assert.Throws<ArgumentException>(() => SettingsRow.Slider("MASTER", 1f, null));
            Assert.Throws<ArgumentException>(() => SettingsRow.Toggle("FULLSCREEN", true, null));
            Assert.Throws<ArgumentException>(() => SettingsRow.Choice("RESOLUTION", new[] { "A" }, 0, null));
        }

        [Test]
        public void Choice_RejectsEmptyOptions()
        {
            Assert.Throws<ArgumentException>(() => SettingsRow.Choice("RESOLUTION", null, 0, _ => { }));
            Assert.Throws<ArgumentException>(() =>
                SettingsRow.Choice("RESOLUTION", Array.Empty<string>(), 0, _ => { }));
        }

        [Test]
        public void Choice_RejectsAnIndexOutOfRange()
        {
            var options = new[] { "A", "B" };
            Assert.Throws<ArgumentOutOfRangeException>(() => SettingsRow.Choice("RESOLUTION", options, -1, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => SettingsRow.Choice("RESOLUTION", options, 2, _ => { }));
        }
    }
}
