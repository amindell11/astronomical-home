using System.Runtime.InteropServices;
using Unity.Mathematics;
namespace Movement.MPC
{
    /// <summary>Which live referent frame a slot's free parameters live in: position (world axes), facing, or velocity direction.</summary>
    public enum ReferentFrame
    {
        Position = 0,
        Facing = 1,
        Velocity = 2,
    }

    /// <summary>AIM sentence slot: signed-weight facing offset around the referent's intercept anchor (sign pins: doc/Glossary.md → anchored intent).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AimSlot
    {
        public bool armed;
        public float offsetRad;    // CCW offset from the intercept yaw
        public float weight;       // Signed authority × the config's wFacing ceiling
        public int referent;
    }

    /// <summary>POS sentence slot: a point at polar offset (r, θ) in the referent's chosen frame; the setpoint makes it a hold-ring (0 = at the point).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PosSlot
    {
        public bool armed;
        public float offsetR;         // Meters from the referent along θ
        public float offsetThetaRad;  // CCW from the frame's forward
        public float setpoint;        // Ring radius around the resolved point
        public float weight;          // Signed authority × the config's wPos ceiling
        public int referent;
        public ReferentFrame frame;
    }

    /// <summary>VEL sentence slot: polar velocity relative to the referent's motion. vr &gt; 0 closes along +losHat, vt &gt; 0 orbits CCW.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VelSlot
    {
        public bool armed;
        public float radialSpeed;      // m/s
        public float tangentialSpeed;  // m/s
        public float weight;           // Signed authority × the config's wVelTrack ceiling
        public int referent;
    }

    /// <summary>FIELD sentence slot: hazard-repulsion authority over the turn-away branch only — the collision penalty is never sentence-weakened. Unarmed = ×1.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FieldSlot
    {
        public bool armed;
        public float weight;
    }

    /// <summary>LANE sentence slot: a ray-segment along the enemy's facing (referent pinned — rocks have no facing). Weight &gt; 0 holds the lane, &lt; 0 dodges it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LaneSlot
    {
        public bool armed;
        public float weight;   // Signed authority × the config's wLane ceiling
    }

    /// <summary>An intent sentence: the decision-varying slice of the MPC cost as typed sentence slots, each re-resolved against live referent state every rollout step (doc/Feature_Plans/Intent_Grammar.md). Referent 0 = the bound enemy (rolled prediction stream); 1–2 = <see cref="CostInput"/>'s synthetic snapshots. Default (nothing armed) = the legacy world-frame path, bit-unchanged.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct IntentSentence
    {
        public AimSlot aim;
        public PosSlot pos;
        public VelSlot vel;
        public FieldSlot field;
        public LaneSlot lane;

        /// <summary>An armed slot at weight 0 still counts: "nothing matters" is a sentence, absence of one is not.</summary>
        public bool AnyArmed => aim.armed || pos.armed || vel.armed || field.armed || lane.armed;
    }

    /// <summary>A synthetic referent: (pos, vel, yaw) snapshot extrapolated linearly in-rollout. Invalid = despawned — bound slots drop to weight 0 until the next decision.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ReferentSnapshot
    {
        public bool valid;
        public float2 pos;
        public float2 vel;
        public float yaw;    // Radians, MPC convention (fwd = (-sin, cos)); held constant in-rollout
    }
}
