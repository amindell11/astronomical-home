using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Bootstrap")]
    public class EditorLaunchProfileEditModeTests
    {
        private string receiptPath;
        private int originalQuality;

        [SetUp]
        public void SetUp()
        {
            receiptPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            originalQuality = QualitySettings.GetQualityLevel();
        }

        [TearDown]
        public void TearDown()
        {
            QualitySettings.SetQualityLevel(originalQuality, true);
            if (File.Exists(receiptPath))
                File.Delete(receiptPath);
        }

        [TestCase("LowMemory", "Performant")]
        [TestCase("HighFidelity", "High Fidelity")]
        public void Apply_MapsProfileToExistingQualityTier(string profile, string expectedQuality)
        {
            EditorLaunchProfile.Receipt receipt = EditorLaunchProfile.Apply(profile, receiptPath);

            Assert.That(receipt.requestedProfile, Is.EqualTo(profile));
            Assert.That(receipt.observedQuality, Is.EqualTo(expectedQuality));
            Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()], Is.EqualTo(expectedQuality));

            var stored = JsonUtility.FromJson<EditorLaunchProfile.Receipt>(File.ReadAllText(receiptPath));
            Assert.That(stored.requestedProfile, Is.EqualTo(profile));
            Assert.That(stored.observedQuality, Is.EqualTo(expectedQuality));
        }

        [Test]
        public void QualityNameFor_RejectsUnknownProfile()
        {
            Assert.Throws<ArgumentException>(() => EditorLaunchProfile.QualityNameFor("Unknown"));
        }
    }
}
