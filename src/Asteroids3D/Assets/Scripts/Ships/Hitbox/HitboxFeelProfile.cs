using UnityEngine;

namespace Ships.Hitbox
{
    /// <summary>
    /// The hitbox knob every ship shares: how much of its width a shot can still hit at full bank.
    /// Each ship's <see cref="BankHitbox"/> scales it per ship, body and wings apart.
    /// </summary>
    [CreateAssetMenu(fileName = "HitboxFeel", menuName = "Ship/Hitbox Feel")]
    public sealed class HitboxFeelProfile : ScriptableObject
    {
        [Tooltip("Share of a ship's width still hittable at full bank (1 = rolling never narrows the target). " +
                 "Each ship multiplies it by its own scales.")]
        [SerializeField, Range(0f, 1f)] private float bankHittableShare = .6f;

        public float BankHittableShare => bankHittableShare;
    }
}
