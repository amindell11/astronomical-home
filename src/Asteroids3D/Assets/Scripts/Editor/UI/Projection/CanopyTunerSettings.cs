using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UI.Projection
{
    public sealed class CanopyTunerSettings
    {
        public bool PersonalityEnabled { get; }
        public CanopySettings Settings { get; }

        private CanopyTunerSettings(bool enabled, CanopySettings settings)
        {
            PersonalityEnabled = enabled;
            Settings = settings;
        }

        public static CanopyTunerSettings Parse(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException error) { throw new FormatException("Canopy settings must be a JSON object.", error); }
            if (root["schemaVersion"]?.Type != JTokenType.Integer || root["schemaVersion"].ToString() != "1")
                throw new FormatException("Canopy settings require schemaVersion 1.");
            if (root["personalityEnabled"]?.Type != JTokenType.Boolean)
                throw new FormatException("Canopy settings require a boolean personalityEnabled.");
            if (root["personality"] is not JObject values)
                throw new FormatException("Canopy settings require a personality object.");
            return new CanopyTunerSettings(root["personalityEnabled"].Value<bool>(), new CanopySettings
            {
                glow = Number(values, "glow", 2f),
                aberration = Number(values, "aberration", 12f),
                beamJitter = Number(values, "beamJitter", 4f),
                afterglow = Number(values, "afterglow", 1.5f),
                scanlines = Number(values, "scanlines", 1f),
                flicker = Number(values, "flicker", 1f),
                warmth = Number(values, "warmth", 1f),
                wear = Number(values, "wear", 1f),
                glassDistortion = Number(values, "glassDistortion", 1f),
                damageShudder = Number(values, "damageShudder", 18f),
                transitionGlitch = Number(values, "transitionGlitch", 1f)
            });
        }

        public void Apply(CanopyProjectionProfile profile, CanopyState target)
        {
            if (target == CanopyState.Base) profile.baseSettings = Settings;
            else
            {
                var block = profile.OverrideFor(target);
                block.settings = Settings;
                block.fields = CanopyFields.All;
            }
            profile.personalityEnabled = PersonalityEnabled;
        }

        private static float Number(JObject values, string key, float maximum)
        {
            var token = values[key];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new FormatException($"Canopy personality requires numeric '{key}'.");
            var value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > maximum)
                throw new FormatException($"Canopy personality '{key}' must be between 0 and {maximum}.");
            return (float)value;
        }
    }
}
