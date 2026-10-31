using System;
using System.Collections;
using System.Collections.Generic;
using Cameras;
using Substrate.Sessions;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>One stretch of a subject's timeline: a timed part, then for an input hold a wait the transport paces.</summary>
    public readonly struct PreviewSpan
    {
        public string Name { get; }
        public float Seconds { get; }
        public bool WaitsForInput { get; }

        public PreviewSpan(string name, float seconds, bool waitsForInput)
        {
            Name = name;
            Seconds = seconds;
            WaitsForInput = waitsForInput;
        }
    }

    public sealed class PreviewWorld
    {
        public Session Session { get; }
        public PlayerRig Rig { get; }
        public ObserverCam Observer { get; }
        public Transform Ui { get; }
        public Transform Root { get; }
        public Action<bool> FreezeSimulation { get; }

        public PreviewWorld(Session session, PlayerRig rig, ObserverCam observer, Transform ui, Transform root,
            Action<bool> freezeSimulation)
        {
            Session = session;
            Rig = rig;
            Observer = observer;
            Ui = ui;
            Root = root;
            FreezeSimulation = freezeSimulation;
        }
    }

    /// <summary>
    /// A presentation module with a chosen context, hosted by <see cref="PreviewHost"/>: it names its
    /// fixtures and spans, shows any instant of its timeline on the real renderers, and exposes the
    /// feel profile the control panel edits. It composes its own world setup from the host's
    /// <see cref="PreviewWorld"/> and releases it on Dispose. The transport decides which instant and
    /// how long an input hold lasts; the subject never drives time.
    /// </summary>
    public interface IPreviewSubject : IDisposable
    {
        string Name { get; }
        IReadOnlyList<string> Fixtures { get; }
        int Fixture { get; }
        void SelectFixture(int index);
        int SpanCount { get; }
        PreviewSpan Span(int index);
        IEnumerator Compose(PreviewWorld world);
        void Show(int span, float seconds, float holdSeconds);
        ScriptableObject Knobs { get; }
    }
}
