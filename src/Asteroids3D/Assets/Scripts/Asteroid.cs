using UnityEngine;

public class Asteroid : MonoBehaviour
{

    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private float explosionVolume = 0.7f;
    private Rigidbody rb;
    private MeshFilter meshFilter;
    private Vector3 initialVelocity;
    private Vector3 initialAngularVelocity;

    public float CurrentMass => rb.mass;
    public Rigidbody Rb => rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        meshFilter = GetComponent<MeshFilter>();
        rb.useGravity = false;
    }

    public void Initialize(
        Mesh mesh, 
        float mass,
        float scale,
        Vector3 velocity,
        Vector3 angularVelocity
    )
    {
        this.meshFilter.mesh = mesh;
        
        this.rb.mass = mass;
        transform.localScale = Vector3.one * scale;

        rb.velocity = velocity;
        rb.angularVelocity = angularVelocity;
        
        UpdateMeshCollider();
    }

    public void ResetAsteroid()
    {
        rb.velocity = initialVelocity;
        rb.angularVelocity = initialAngularVelocity;
    }
    private void UpdateMeshCollider()
    {
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint)
    {       
        Fragnetics.Instance.CreateFragments(this, projectileMass, projectileVelocity, hitPoint);
        Explode();
        CleanupAsteroid();
    }

    private void Explode()
    {
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }
    }

    private void CleanupAsteroid()
    {
        if (AsteroidFieldManager.Instance != null)
        {
            AsteroidFieldManager.Instance.RemoveAsteroid(gameObject);   
        }
        Destroy(gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("OnTriggerExit" + other.name);
        if (other.CompareTag("AsteroidCullingBoundary"))
        {
            AsteroidFieldManager.Instance.CullableAsteroids.Add(gameObject);
            AsteroidFieldManager.Instance.ActiveAsteroids.Remove(gameObject);
            gameObject.SetActive(false);
        }
    }   
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("OnTriggerEnter" + other.name);
        if (other.CompareTag("AsteroidCullingBoundary"))
        {
            AsteroidFieldManager.Instance.CullableAsteroids.Remove(gameObject);
        }
    }

    private void LateUpdate()
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, 0);
    }

    private void OnDrawGizmos()
    {
        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 velocity = rb.velocity;
            Vector3 start = transform.position;
            Vector3 end = start + velocity.normalized * 2f;
            Gizmos.DrawLine(start, end);
            
            Vector3 right = Quaternion.Euler(0, 0, 30) * -velocity.normalized * (2f * 0.2f);
            Vector3 left = Quaternion.Euler(0, 0, -30) * -velocity.normalized * (2f * 0.2f);
            Gizmos.DrawLine(end, end + right);
            Gizmos.DrawLine(end, end + left);
        }
    }
}