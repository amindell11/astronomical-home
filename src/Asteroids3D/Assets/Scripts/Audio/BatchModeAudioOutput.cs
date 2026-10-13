using System;
using FMODUnity;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Picks FMOD's output per run. Output Mode is a per-platform FMOD setting and the
    /// Play-In-Editor platform has none, so every editor run, batch included, would open a real
    /// device. Batch runs mix to NOSOUND instead; ASTRO_FMOD_OUTPUT names any
    /// <see cref="FMOD.OUTPUTTYPE"/> (WAVWRITER for audio evidence). Assigned as the Default
    /// platform's callback handler in FMOD settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Audio/Batch Mode Audio Output")]
    public sealed class BatchModeAudioOutput : PlatformCallbackHandler
    {
        private const string OverrideVariable = "ASTRO_FMOD_OUTPUT";

        public override void PreInitialize(FMOD.Studio.System system, Action<FMOD.RESULT, string> reportResult)
        {
            var requested = Environment.GetEnvironmentVariable(OverrideVariable);
            FMOD.OUTPUTTYPE output;
            if (!string.IsNullOrEmpty(requested))
                output = Enum.Parse<FMOD.OUTPUTTYPE>(requested, ignoreCase: true);
            else if (Application.isBatchMode)
                output = FMOD.OUTPUTTYPE.NOSOUND;
            else
                return;

            reportResult(system.getCoreSystem(out var core), "FMOD.Studio.System.getCoreSystem");
            reportResult(core.setOutput(output), "FMOD.System.setOutput");
        }
    }
}
