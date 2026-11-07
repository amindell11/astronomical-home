namespace Capture.Recording
{
    public interface IEpisodeCapture
    {
        CaptureRecording Prepare(CaptureRequest request);
    }
}
