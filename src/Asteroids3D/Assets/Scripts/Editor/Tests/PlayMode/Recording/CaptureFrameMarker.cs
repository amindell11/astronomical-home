using UnityEngine;
using UnityEngine.UI;

namespace Tests.PlayMode.Recording
{
    public sealed class CaptureFrameMarker : MonoBehaviour
    {
        private Image[] bits;
        public int FrameId { get; private set; }

        public void Initialize(Image[] images) => bits = images;

        private void Update()
        {
            FrameId = (FrameId + 1) % 256;
            for (var bit = 0; bit < bits.Length; bit++)
                bits[bit].color = (FrameId & (1 << bit)) == 0 ? Color.black : Color.white;
        }
    }
}
