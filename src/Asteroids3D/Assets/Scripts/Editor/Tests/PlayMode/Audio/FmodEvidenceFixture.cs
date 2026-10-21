#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using FMOD.Studio;
using FMODUnity;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode.Audio
{
    /// <summary>
    /// Local FMOD evidence, compiled only against the real package: instance state read back from
    /// Studio after a flushed frame.
    /// </summary>
    public abstract class FmodEvidenceFixture : PlayModeWorldFixture
    {
        /// <summary>The event's description with every instance earlier tests left released.</summary>
        protected static EventDescription Fresh(string eventPath)
        {
            var description = RuntimeManager.GetEventDescription(eventPath);
            description.releaseAllInstances();
            RuntimeManager.StudioSystem.flushCommands();
            return description;
        }

        /// <summary>Flushes Studio's command queue and yields a frame, so instance state reflects everything sent.</summary>
        protected static IEnumerator Flush()
        {
            RuntimeManager.StudioSystem.flushCommands();
            yield return null;
        }

        protected static EventInstance[] Instances(EventDescription description)
        {
            description.getInstanceList(out var instances);
            return instances;
        }

        protected static PLAYBACK_STATE State(EventInstance instance)
        {
            instance.getPlaybackState(out var state);
            return state;
        }

        protected static float Parameter(EventInstance instance, string name)
        {
            instance.getParameterByName(name, out var value);
            return value;
        }

        protected static Vector3 Position(EventInstance instance)
        {
            instance.get3DAttributes(out var attributes);
            return new Vector3(attributes.position.x, attributes.position.y, attributes.position.z);
        }

        protected static void AssertPlaying(EventInstance instance, string why) =>
            Assert.That(State(instance), Is.EqualTo(PLAYBACK_STATE.PLAYING).Or.EqualTo(PLAYBACK_STATE.STARTING), why);
    }
}
#endif
