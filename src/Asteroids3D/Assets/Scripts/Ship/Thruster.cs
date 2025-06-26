using UnityEngine;

public class Thruster : MonoBehaviour
{
    public ParticleSystem[] thrustParticles;
    private Ship ship;

    void Start()    
    {
        ship = GetComponentInParent<Ship>();
    }

    void Update()
    {
        if (thrustParticles == null || thrustParticles.Length == 0 || ship == null) return;

        // Also check global VFX setting
        bool shouldPlay = ship.CurrentCommand.Thrust > 0.05f && GameSettings.VfxEnabled;
        
        foreach (var ps in thrustParticles)
        {
            if (!ps) continue;
            if (shouldPlay)
            {
                if (!ps.isPlaying) ps.Play(true);
            }
            else
            {
                if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}