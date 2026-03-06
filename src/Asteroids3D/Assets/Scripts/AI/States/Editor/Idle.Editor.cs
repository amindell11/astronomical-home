#if UNITY_EDITOR
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Idle
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);
            
            var position = ctx.ShipInfo.Pos3D;
            
            // Draw idle indicator - a pulsing circle
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f + 0.2f * Mathf.Sin(Time.time * 2f));
            Gizmos.DrawWireSphere(position, 2f);
        }
    }
}
#endif
