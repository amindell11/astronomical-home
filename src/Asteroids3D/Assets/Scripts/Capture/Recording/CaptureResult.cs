namespace Capture.Recording
{
    public sealed class CaptureResult
    {
        public string OutputDirectory { get; }
        public string FrameDirectory { get; }
        public string FrameMetadataPath { get; }
        public string ProvenancePath { get; }
        public int FrameCount { get; }

        public CaptureResult(string outputDirectory, string frameDirectory, string frameMetadataPath,
            string provenancePath, int frameCount)
        {
            OutputDirectory = outputDirectory;
            FrameDirectory = frameDirectory;
            FrameMetadataPath = frameMetadataPath;
            ProvenancePath = provenancePath;
            FrameCount = frameCount;
        }
    }
}
