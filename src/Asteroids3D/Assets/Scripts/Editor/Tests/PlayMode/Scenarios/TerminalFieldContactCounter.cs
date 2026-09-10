#if UNITY_EDITOR
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    public sealed class TerminalFieldContactCounter : MonoBehaviour
    {
        private double lastContact = double.NaN;
        public int Steps { get; private set; }

        private void OnCollisionEnter(Collision collision) => Record();
        private void OnCollisionStay(Collision collision) => Record();

        private void Record()
        {
            if (lastContact == Time.fixedTimeAsDouble) return;
            lastContact = Time.fixedTimeAsDouble;
            Steps++;
        }
    }
}
#endif
