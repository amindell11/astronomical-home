#if UNITY_EDITOR
namespace AI.States
{
    public abstract partial class State
    {
        /// <summary>
        /// Draw debug gizmos for this state. Override in derived states to provide custom visualization.
        /// </summary>
        public virtual void OnDrawGizmos(AI.Context.Info ctx)
        {
            // Base implementation is empty - override in derived states
        }
    }
}
#endif
