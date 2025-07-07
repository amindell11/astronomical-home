using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "AquireEnemies", story: "First [Enemy] within [Radius] is acquired", category: "Action", id: "bac221e895ed5a1a05bb060ab0e751d5")]
public partial class AquireEnemiesAction : Action
{
    [SerializeReference] public BlackboardVariable<Ship> Enemy;
    [SerializeReference, Tooltip("Detection radius (world units)")] public BlackboardVariable<float> Radius;

    // Cached reference to avoid GetComponent calls every frame
    private Ship selfShip;

    protected override Status OnStart()
    {        
        // Cache the selfShip reference
        GameObject self = this.GameObject;
        if (!self)
            return Status.Failure;
            
        selfShip = self.GetComponentInParent<Ship>();
        if (!selfShip)
            return Status.Failure;
        
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        GameObject self = this.GameObject;
        if (!self || !selfShip)
            return Status.Failure;

        // If we already have a valid enemy within range, succeed immediately
        if (Enemy != null && Enemy.Value != null && Enemy.Value.gameObject.activeInHierarchy)
        {
        return Status.Success;
        }

        // Scan for any Ship components in range (excluding self) using non-allocating overlap sphere
        int hitCount = Physics.OverlapSphereNonAlloc(self.transform.position, Radius.Value, PhysicsBuffers.GetColliderBuffer(32), LayerIds.Mask(LayerIds.Ship));
        for (int i = 0; i < hitCount; i++)
        {
            var col = PhysicsBuffers.GetColliderBuffer(32)[i];
            if (!col) continue;

            // More robust way to get the Ship component, assuming it's on the same GameObject as the Rigidbody
            Ship other = col.attachedRigidbody ? col.attachedRigidbody.GetComponent<Ship>() : col.GetComponentInParent<Ship>();
            
            if (other && other.gameObject != self)
            {
                // Check if the other ship is an enemy (not friendly)
                if (!selfShip.IsFriendly(other))
                {
                    Enemy.Value = other;
                    return Status.Success;
                }
            }
        }

        // No enemies found within radius
        Enemy.Value = null;
        return Status.Failure;
    }

    protected override void OnEnd()
    {
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        GameObject self = this.GameObject;
        if (self == null || Radius == null) return;

        Vector3 position = self.transform.position;
        float radius = Radius.Value;

        // Draw detection radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f); // Yellow, semi-transparent
        Gizmos.DrawWireSphere(position, radius);

        // Draw line to current target if we have one
        if (Enemy != null && Enemy.Value != null && Enemy.Value.gameObject.activeInHierarchy)
        {
            Gizmos.color = Color.red;
            Vector3 enemyPosition = Enemy.Value.transform.position;
            Gizmos.DrawLine(position, enemyPosition);
            
            // Draw target indicator
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(enemyPosition, 1f);
            
            // Show distance to target
            UnityEditor.Handles.color = Color.white;
            float distance = Vector3.Distance(position, enemyPosition);
            UnityEditor.Handles.Label(position + Vector3.up * 2f, $"Target: {Enemy.Value.name}\nDistance: {distance:F1}");
        }
    }

    void OnDrawGizmosSelected()
    {
        GameObject self = this.GameObject;
        if (self == null || Radius == null) return;

        // Use cached selfShip or get it if not cached (for editor-only gizmos)
        Ship currentSelfShip = selfShip ?? self.GetComponentInParent<Ship>();
        Vector3 position = self.transform.position;
        float radius = Radius.Value;

        // Draw more detailed gizmos when selected
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(position, radius);
        
        // Fill the sphere with a very transparent color
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawSphere(position, radius);

        // Show all potential targets in range (not just the acquired one)
        if (Application.isPlaying && currentSelfShip)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(position, radius, PhysicsBuffers.GetColliderBuffer(32), LayerMask.GetMask(TagNames.Ship));
            
            for (int i = 0; i < hitCount; i++)
            {
                var col = PhysicsBuffers.GetColliderBuffer(32)[i];
                if (!col) continue;
                
                Ship other = col.attachedRigidbody ? col.attachedRigidbody.GetComponent<Ship>() : col.GetComponentInParent<Ship>();

                if (other && other.gameObject != self)
                {
                    Vector3 otherPos = other.transform.position;
                    
                    // Color code based on team relationship
                    bool isEnemy = !currentSelfShip.IsFriendly(other);
                    Gizmos.color = isEnemy ? Color.red : Color.green;
                    
                    // Draw line to potential target
                    Gizmos.DrawLine(position, otherPos);
                    
                    // Draw sphere around potential target
                    Gizmos.DrawWireSphere(otherPos, 0.5f);
                }
            }
        }

        // Show configuration info
        UnityEditor.Handles.color = Color.white;
        string teamInfo = currentSelfShip ? $" (Team {currentSelfShip.teamNumber})" : "";
        string info = $"Acquire Enemies Action{teamInfo}\nRadius: {radius:F1}\nTargets: Enemy ships only";
        UnityEditor.Handles.Label(position + Vector3.up * (radius + 2f), info);
    }
#endif
}

