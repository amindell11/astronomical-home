using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.Sensors;

/// <summary>
/// Simple on-screen overlay that visualises the observation vector being fed
/// to an <see cref="RLCommanderAgent"/>.  When the agent's <c>ShowObservationUI</c>
/// toggle is enabled this component draws each float in a scrollable box so
/// you can debug live while piloting via the heuristic.
/// </summary>
[RequireComponent(typeof(RLCommanderAgent))]
public class RLObservationDebugUI : MonoBehaviour
{
    [Tooltip("How many Academy steps to wait between UI refreshes (0=every frame)")]
    [SerializeField] private int refreshInterval = 0;

    private RLCommanderAgent _agent;
    private readonly List<float> _obsBuffer = new List<float>(64);
    private int _lastRenderedStep = -1;

    // Pre-allocated rects for IMGUI; tweak width/height as needed.
    private readonly Rect _panelRect = new Rect(10, 10, 300, 400);
    private Vector2 _scrollPos;

    // Wrapper that captures observations to our buffer
    private class DebugSensorWrapper : VectorSensor
    {
        private readonly List<float> _buffer;
        private readonly VectorSensor _wrappedSensor;

        public DebugSensorWrapper(List<float> buffer) : base(64)
        {
            _buffer = buffer;
            _wrappedSensor = new VectorSensor(64);
        }

        public new void AddObservation(float observation)
        {
            _buffer.Add(observation);
            _wrappedSensor.AddObservation(observation);
        }

        public new void AddObservation(int observation)
        {
            _buffer.Add(observation);
            _wrappedSensor.AddObservation(observation);
        }

        public new void AddObservation(bool observation)
        {
            _buffer.Add(observation ? 1f : 0f);
            _wrappedSensor.AddObservation(observation);
        }

        public new void AddObservation(Vector2 observation)
        {
            _buffer.Add(observation.x);
            _buffer.Add(observation.y);
            _wrappedSensor.AddObservation(observation);
        }

        public new void AddObservation(Vector3 observation)
        {
            _buffer.Add(observation.x);
            _buffer.Add(observation.y);
            _buffer.Add(observation.z);
            _wrappedSensor.AddObservation(observation);
        }

        public new void AddObservation(Quaternion observation)
        {
            _buffer.Add(observation.x);
            _buffer.Add(observation.y);
            _buffer.Add(observation.z);
            _buffer.Add(observation.w);
            _wrappedSensor.AddObservation(observation);
        }
    }

    private DebugSensorWrapper _debugSensor;

    void Awake()
    {
        _agent = GetComponent<RLCommanderAgent>();
        _debugSensor = new DebugSensorWrapper(_obsBuffer);
    }

    void Update()
    {
        if (_agent == null || !_agent.ShowObservationUI) return;

        if (refreshInterval > 0 && (_agent.StepCount - _lastRenderedStep) < refreshInterval)
            return;

        _lastRenderedStep = _agent.StepCount;

        // Clear buffer and collect fresh observations
        _obsBuffer.Clear();
        _agent.Observer.CollectObservations(_debugSensor);
    }

    void OnGUI()
    {
        if (_agent == null || !_agent.ShowObservationUI) return;

        GUI.Box(_panelRect, "Observations");
        var contentRect = new Rect(_panelRect.x + 5, _panelRect.y + 20, _panelRect.width - 10, _panelRect.height - 25);
        _scrollPos = GUI.BeginScrollView(_panelRect, _scrollPos, new Rect(0, 0, contentRect.width - 20, (_obsBuffer.Count + 1) * 18));

        for (int i = 0; i < _obsBuffer.Count; i++)
        {
            string label = (i < RLObserver.ObservationLabels.Length) ? RLObserver.ObservationLabels[i] : $"obs_{i:00}";
            GUI.Label(new Rect(5, i * 18, contentRect.width, 18), $"{i:00} {label}: {_obsBuffer[i]:0.00}");
        }

        GUI.EndScrollView();
    }
}   
