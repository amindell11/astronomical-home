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
    /// A ship's engine: one sustained sound fed the live pilot command, plus a one-shot each time the
    /// boost fires, read as the boost-ready state falling. Nothing sounds until the rig is bound, so
    /// an unbound rig (the hangar preview) stays silent.
    /// </summary>
    public sealed class EngineAudio : MonoBehaviour, IShipVisual
    {
        private const string ThrustParameter = "Thrust";
        private const string StrafeParameter = "Strafe";

        [Tooltip("Looping FMOD event fed the command's signed thrust and its strafe magnitude.")]
        [SerializeField] private EventReference engineEvent;
        [Tooltip("FMOD one-shot played on the ship each time its boost fires.")]
        [SerializeField] private EventReference boostEvent;

        private Func<PilotCommand> command;
        private Func<bool> boostAvailable;
        private Rigidbody body;
        private bool wasBoostAvailable;
        private EventInstance engine;

        public void Bind(in ShipView view)
        {
            command = view.Command;
            boostAvailable = view.BoostAvailable;
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

            var available = boostAvailable();
            if (wasBoostAvailable && !available)
                RuntimeManager.PlayOneShotAttached(boostEvent, gameObject);
            wasBoostAvailable = available;
        }

        private void Begin()
        {
            wasBoostAvailable = boostAvailable();
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
