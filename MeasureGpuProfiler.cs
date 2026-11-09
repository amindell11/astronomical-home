using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Capture.GameView;
using Capture.Recording;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Profiling;
using UnityEditor;

public static class MeasureGpuProfiler
{
    public static async Task<object> Main(string output, string label, string commit, bool hardwareEnabled, int width, int height)
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
        var console = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.Hardware.HangarConsole>(FindObjectsInactive.Include);
        var layer = screen.transform.Find("ConsoleHardware").gameObject;
        var sky = UnityEngine.Object.FindFirstObjectByType<Cameras.DriftCam>();
        var priorSky = sky.enabled; var priorPosition = sky.transform.position;
        var priorConsole = console.gameObject.activeSelf; var priorLayer = layer.activeSelf;
        var quality = QualitySettings.GetQualityLevel(); var vsync = QualitySettings.vSyncCount; var cap = Application.targetFrameRate;
        var module = ScriptableObject.CreateInstance<GameViewEpisodeCapture>(); var profilerEnabled = UnityEditorInternal.ProfilerDriver.enabled; var gpuProfiling=UnityEditorInternal.ProfilerDriver.profileGPU; UnityEditorInternal.ProfilerDriver.enabled=true; UnityEditorInternal.ProfilerDriver.profileGPU=true;
        Action<ScriptableRenderContext, List<Camera>> begin = null;
        Action<ScriptableRenderContext, List<Camera>> end = null;
        try
        {
            QualitySettings.SetQualityLevel(2, true); QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            sky.enabled = false; sky.transform.position = -Substrate.GamePlane.Normal * 50f;
            console.gameObject.SetActive(hardwareEnabled); layer.SetActive(hardwareEnabled);
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 512);
            using var ftRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "FrameTime.GPU", 512);
            using var recording = module.Prepare(new CaptureRequest(output, label, commit, width, height));
            var ready = recording.Ready; while (ready.MoveNext()) await Task.Delay(30);
            var samples = new Dictionary<ulong, FrameTiming>(); var seen = new HashSet<ulong>();
            var buffer = new FrameTiming[16]; var beginCount = 0; var endCount = 0;
            var started = Time.realtimeSinceStartupAsDouble; var firstFrame = Time.renderedFrameCount;
            Func<List<Camera>,bool> matches = cameras => cameras.Any(c => c.cameraType == CameraType.Game && !c.targetTexture && c.pixelWidth == width && c.pixelHeight == height);
            begin = (context,cameras) => { if(matches(cameras)) {beginCount++; FrameTimingManager.CaptureFrameTimings();} };
            end = (context,cameras) => {
                if (!matches(cameras)) return;
                endCount++;
                var n = FrameTimingManager.GetLatestTimings((uint)buffer.Length,buffer);
                for(var i=0;i<n;i++) {
                    var t=buffer[i];
                    if(!seen.Add(t.frameStartTimestamp)) continue;
                    if(Time.realtimeSinceStartupAsDouble-started >= 3) samples[t.frameStartTimestamp]=t;
                }
            };
            RenderPipelineManager.beginContextRendering += begin; RenderPipelineManager.endContextRendering += end;
            while(Time.realtimeSinceStartupAsDouble-started < 10) await Task.Delay(30);
            var rows=samples.OrderBy(p=>p.Key).Select(p=>p.Value).ToArray();
            var cpu=rows.Select(t=>t.cpuFrameTime).OrderBy(x=>x).ToArray();
            var gpu=rows.Where(t=>t.gpuFrameTime>0).Select(t=>t.gpuFrameTime).OrderBy(x=>x).ToArray();
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            return new {label,commit,hardwareEnabled,width=Screen.width,height=Screen.height,
                feature=FrameTimingManager.IsFeatureEnabled(), playerStats=PlayerSettings.enableFrameTimingStats,
                renderedFrames=Time.renderedFrameCount-firstFrame,beginCount,endCount,elapsed=Time.realtimeSinceStartupAsDouble-started,
                quality=QualitySettings.names[QualitySettings.GetQualityLevel()],pipeline=pipeline.name,pipeline.renderScale,
                graphics=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                sampleCount=rows.Length,validGpuCount=gpu.Length,
                cpuMedianMs=cpu.Length>0?cpu[cpu.Length/2]:0,cpuP95Ms=cpu.Length>0?cpu[(int)(cpu.Length*.95)]:0,
                gpuMedianMs=gpu.Length>0?gpu[gpu.Length/2]:0,gpuP95Ms=gpu.Length>0?gpu[(int)(gpu.Length*.95)]:0,
                recorderValid=recorder.Valid, recorderLast=recorder.Valid?recorder.LastValue:0, ftRecorderValid=ftRecorder.Valid,ftRecorderLast=ftRecorder.Valid?ftRecorder.LastValue:0,
                samples=rows,scope="Unity Editor Game View; fixed sky; 3s warmup; rendered-frame callbacks; no image acquisition"};
        }
        finally
        {
            if(begin!=null)RenderPipelineManager.beginContextRendering-=begin;
            if(end!=null)RenderPipelineManager.endContextRendering-=end;
            sky.enabled=priorSky;sky.transform.position=priorPosition;
            console.gameObject.SetActive(priorConsole);layer.SetActive(priorLayer);
            QualitySettings.SetQualityLevel(quality,true);QualitySettings.vSyncCount=vsync;Application.targetFrameRate=cap;
            UnityEngine.Object.DestroyImmediate(module); UnityEditorInternal.ProfilerDriver.enabled=profilerEnabled; UnityEditorInternal.ProfilerDriver.profileGPU=gpuProfiling;
        }
    }
}

