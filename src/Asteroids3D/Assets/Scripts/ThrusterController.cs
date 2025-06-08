using UnityEngine;

public class ThrusterController : MonoBehaviour
{
    [Header("Thruster Settings")]
    [Tooltip("The minimum and maximum size of the main thruster particles.")]
    [SerializeField] private Vector2 mainThrusterSizeRange = new Vector2(1f, 5f);

    [Tooltip("How quickly the thruster grows and shrinks.")]
    [SerializeField] private float thrusterGrowthRate = 2f;

    [Header("Thruster Particle Systems")]
    [Tooltip("The main central thruster.")]
    [SerializeField] private ParticleSystem mainThruster;

    [Tooltip("The smaller reactor-side thrusters.")]
    [SerializeField] private ParticleSystem reactorThrusters;

    [Tooltip("The small directional thrusters.")]
    [SerializeField] private ParticleSystem smallThrusters;

    private ParticleSystem.MainModule mainThrusterModule;

    void Start()
    {
        if (mainThruster != null)
        {
            mainThrusterModule = mainThruster.main;
        }
    }

    void FixedUpdate()
    {
        HandleMainThruster();
    }

    private void HandleMainThruster()
    {
        if (mainThruster == null) return;

        // Assuming vertical input controls the main thruster
        bool isThrusting = Input.GetAxis("Vertical") > 0;

        float targetSize = isThrusting ? mainThrusterSizeRange.y : mainThrusterSizeRange.x;
        
        float currentSize = mainThrusterModule.startSize.constant;
        float newSize = Mathf.MoveTowards(currentSize, targetSize, thrusterGrowthRate * Time.fixedDeltaTime);

        mainThrusterModule.startSize = newSize;
    }
} 