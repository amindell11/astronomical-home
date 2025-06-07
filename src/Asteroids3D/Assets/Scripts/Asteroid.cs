using UnityEngine;

public class Asteroid : MonoBehaviour
{

    [SerializeField] private GameObject explosionPrefab;

    private Rigidbody rb;
    private MeshFilter meshFilter;

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
        if (other.CompareTag("AsteroidCullingBoundary"))
        {
            CleanupAsteroid();
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