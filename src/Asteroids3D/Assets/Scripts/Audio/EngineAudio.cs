using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Two-loop engine audio system that cross-fades between an IDLE and a FULL-THROTTLE clip.
/// A child AudioSource is spawned for each loop so both can play simultaneously while
/// volumes blend according to the ship's absolute thrust input (0-1).
/// </summary>
public sealed class EngineAudio : MonoBehaviour
{
    [Header("Loop Clips")]
    [SerializeField] private AudioClip idleLoop;
    [SerializeField] private AudioClip fullLoop;

    [Header("Cross-fade Mapping")]
    [Tooltip("Curve mapping |thrust| (0–1) → weight of FULL loop (0-1). The IDLE loop weight is 1-fullWeight.")]
    [SerializeField] private AnimationCurve thrustToFullWeight = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("Optional pitch modulation shared by both loops (0–1 → pitch)")]
    [SerializeField] private AnimationCurve thrustToPitch = AnimationCurve.Linear(0, 1, 1, 1.3f);

    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    // Spawned sources
    private AudioSource idleSource;
    private AudioSource fullSource;

    private Ship ship;

    void Awake()
    {
        ship = GetComponentInParent<Ship>();
        idleSource = CreateChildSource("IdleLoop", idleLoop);
        fullSource = CreateChildSource("FullLoop", fullLoop);
    }

    AudioSource CreateChildSource(string name, AudioClip clip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f; // 3-D positional
        if (clip)
            src.Play();
        return src;
    }

    void Update()
    {
        if (ship == null || idleSource == null || fullSource == null) return;

        float thrust01 = Mathf.Abs(ship.CurrentCommand.Thrust); // 0-1 regardless of direction
        float fullW = Mathf.Clamp01(thrustToFullWeight.Evaluate(thrust01));
        float idleW = 1f - fullW;

        // Volume cross-fade
        idleSource.volume = idleW * masterVolume;
        fullSource.volume = fullW * masterVolume;

        // Optional pitch modulation (applied to BOTH loops so timbre stays consistent)
        float pitch = Mathf.Clamp(thrustToPitch.Evaluate(thrust01), 0.1f, 3f);
        idleSource.pitch = pitch;
        fullSource.pitch = pitch;
    }
} 