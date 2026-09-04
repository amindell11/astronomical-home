using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Session
{
    /// <summary>
    /// Applies the coordinator's launch-scoped editor profile and records the
    /// observed quality tier before the coordinator gives the editor to its caller.
    /// The coordinator supplies both values through process environment variables;
    /// launches without a profile leave the editor unchanged.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorLaunchProfile
    {
        internal const string ProfileEnvironmentVariable = "ASTRONOMICAL_EDITOR_PROFILE";
        internal const string ReceiptEnvironmentVariable = "ASTRONOMICAL_EDITOR_PROFILE_RECEIPT";

        [Serializable]
        internal struct Receipt
        {
            public string requestedProfile;
            public string observedQuality;
            public string error;
        }

        static EditorLaunchProfile()
        {
            string profile = Environment.GetEnvironmentVariable(ProfileEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(profile))
                return;

            string receiptPath = Environment.GetEnvironmentVariable(ReceiptEnvironmentVariable);
            Environment.SetEnvironmentVariable(ProfileEnvironmentVariable, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ReceiptEnvironmentVariable, null, EnvironmentVariableTarget.Process);
            Apply(profile, receiptPath);
        }

        internal static string QualityNameFor(string profile)
        {
            switch (profile)
            {
                case "LowMemory":
                    return "Performant";
                case "HighFidelity":
                    return "High Fidelity";
                default:
                    throw new ArgumentException($"Unknown editor profile: {profile}", nameof(profile));
            }
        }

        internal static Receipt Apply(string profile, string receiptPath)
        {
            if (string.IsNullOrWhiteSpace(receiptPath))
                throw new ArgumentException("Editor profile launch omitted its receipt path.", nameof(receiptPath));

            try
            {
                string expectedQuality = QualityNameFor(profile);
                int qualityIndex = Array.IndexOf(QualitySettings.names, expectedQuality);
                if (qualityIndex < 0)
                    throw new InvalidOperationException($"Editor profile {profile} requires missing quality tier {expectedQuality}.");

                QualitySettings.SetQualityLevel(qualityIndex, true);
                string observedQuality = QualitySettings.names[QualitySettings.GetQualityLevel()];
                if (!string.Equals(observedQuality, expectedQuality, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Editor profile {profile} observed {observedQuality}, expected {expectedQuality}.");

                var receipt = new Receipt { requestedProfile = profile, observedQuality = observedQuality };
                WriteReceipt(receiptPath, receipt);
                return receipt;
            }
            catch (Exception error)
            {
                WriteReceipt(receiptPath, new Receipt { requestedProfile = profile, error = error.Message });
                throw;
            }
        }

        private static void WriteReceipt(string receiptPath, Receipt receipt)
        {
            string fullPath = Path.GetFullPath(receiptPath);
            string parent = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(parent))
                throw new InvalidOperationException($"Editor profile receipt has no parent directory: {receiptPath}");

            Directory.CreateDirectory(parent);
            string tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(receipt));
            File.Move(tempPath, fullPath);
        }
    }
}
