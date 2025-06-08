using UnityEngine;

[RequireComponent(typeof(ReflectionProbe))]
public class DynamicReflectionProbe : MonoBehaviour
{
    private ReflectionProbe probe;
    public float updateInterval = 0.5f;
    private float lastUpdateTime;

    void Start()
    {
        probe = GetComponent<ReflectionProbe>();
        // Set initial settings
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        
        // Do initial render
        probe.RenderProbe();
    }

    void Update()
    {
        // Update the probe periodically
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            probe.RenderProbe();
            lastUpdateTime = Time.time;
        }
    }
} 