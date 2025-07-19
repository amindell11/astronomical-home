using UnityEngine;

/// Plays audio cues driven by MissileLauncher‘s LockChannel events.
[RequireComponent(typeof(AudioSource))]
public class LockOnAudioFeedback : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Looping sound that plays while the launcher is locking on a target.")]
    [SerializeField] private AudioClip lockingLoopClip;
    [Tooltip("One-shot sound that plays when the launcher has fully locked on a target.")]
    [SerializeField] private AudioClip lockedClip;

    private AudioSource source;
    private MissileLauncher launcher;
    private MissileLauncher.LockState lastState = MissileLauncher.LockState.Idle;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop        = false;

        // Attempt to find the player's launcher at startup (may be null if player not spawned yet).
        TryAssignLauncher();
    }

    void OnDisable()
    {
        StopAudio();
    }

    void Update()
    {
        // Ensure we have a reference to the player's MissileLauncher.
        if (launcher == null)
        {
            TryAssignLauncher();
            if (launcher == null) return; // Still not found – wait until next frame.
        }

        var currentState = launcher.State;
        if (currentState != lastState)
        {
            HandleStateChange(currentState);
            lastState = currentState;
        }
    }

    /* ----------------- State-driven audio ----------------- */
    void HandleStateChange(MissileLauncher.LockState newState)
    {
        switch (newState)
        {
            case MissileLauncher.LockState.Locking:
                PlayLockingLoop();
                break;
            case MissileLauncher.LockState.Locked:
                PlayLockedClip();
                break;
            default: // Idle, Cooldown, etc.
                StopAudio();
                break;
        }
    }

    void PlayLockingLoop()
    {
        if (lockingLoopClip == null) return;

        source.loop = true;
        source.clip = lockingLoopClip;
        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    void PlayLockedClip()
    {
        // Ensure any looping clip is halted first
        StopAudio();
        if (lockedClip != null)
        {
            source.PlayOneShot(lockedClip);
        }
    }

    void StopAudio()
    {
        source.loop = false;
        source.Stop();
        source.clip = null;
    }

    /* ----------------- Helper ----------------- */
    void TryAssignLauncher()
    {
        var playerObj = GameObject.FindGameObjectWithTag(TagNames.Player);
        if (playerObj != null)
        {
            launcher = playerObj.GetComponentInChildren<MissileLauncher>();

            // Sync state immediately to avoid false triggers
            if (launcher != null)
            {
                lastState = launcher.State;
            }
        }
    }
}