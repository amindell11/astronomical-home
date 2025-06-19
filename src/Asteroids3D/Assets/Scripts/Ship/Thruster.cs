using UnityEngine;

public class Thruster : MonoBehaviour
{
    public ParticleSystem[] thrustParticles;
    public  ShipMovement.ShipMovement2D Controller;

    void Start()    
    {
        Controller = GetComponentInParent<ShipMovement>().Controller;
    }

    void Update()
    {
        if (thrustParticles == null || thrustParticles.Length == 0) return;
        bool shouldPlay = Controller.ThrustInput > 0.05f;
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