using System;
using FMOD.Studio;
using FMODUnity;
using Ships.Command;
using Ships.Presentation;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Ships.Audio
{
    /// <summary>
    /// A ship's engine: one sustained sound fed the live pilot command. Nothing sounds until the rig
    /// is bound, so an unbound rig (the hangar preview) stays silent.
    /// </summary>
    public sealed class EngineAudio : MonoBehaviour, IShipVisual
    {
        private const string ThrustParameter = "Thrust";
        private const string StrafeParameter = "Strafe";

        [Tooltip("Looping FMOD event fed the command's signed thrust and its strafe magnitude.")]
        [SerializeField] private EventReference engineEvent;

        private Func<PilotCommand> command;
        private Rigidbody body;
        private EventInstance engine;

        public void Bind(in ShipView view)
        {
            command = view.Command;
            body = view.Body;
            if (isActiveAndEnabled) Begin();
        }

        private void OnEnable()
        {
            if (command != null) Begin();
        }

        private void OnDisable() => Stop();

        private void Update()
        {
            if (!engine.isValid()) return;

            var cmd = command();
            engine.setParameterByName(ThrustParameter, cmd.thrust);
            engine.setParameterByName(StrafeParameter, Mathf.Abs(cmd.strafe));
        }

        private void Begin()
        {
            engine = RuntimeManager.CreateInstance(engineEvent);
            RuntimeManager.AttachInstanceToGameObject(engine, gameObject, body);
            engine.start();
        }

        private void Stop()
        {
            if (!engine.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(engine);
            engine.stop(STOP_MODE.ALLOWFADEOUT);
            engine.release();
            engine = default;
        }
    }
}
