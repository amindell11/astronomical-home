using System;
using System.Collections;
using System.IO;
using Game.Bootstrap;
using Ships;
using Ships.Command;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Diagnostics
{
    public sealed class PerformanceProfileCapture : MonoBehaviour
    {
        private const string OutputArgument = "-astronomical-profile-output";
        private const string WarmupArgument = "-astronomical-profile-warmup-frames";
        private const string SampleArgument = "-astronomical-profile-sample-frames";
        private const string ExtraShipsArgument = "-astronomical-profile-extra-ships";

        [SerializeField] private Ship stressShipTemplate;
        [SerializeField] private Commander stressCommanderTemplate;

        private IEnumerator Start()
        {
            var arguments = Environment.GetCommandLineArgs();
            var outputPath = ArgumentValue(arguments, OutputArgument);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Destroy(this);
                yield break;
            }

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var driver = GetComponent<GameDriver>();
            while (driver && driver.CurrentState != GameState.InSector)
                yield return null;

            var extraShips = NonNegativeInt(ArgumentValue(arguments, ExtraShipsArgument), 0);
            SpawnStressShips(driver, extraShips);
            var warmupFrames = PositiveInt(ArgumentValue(arguments, WarmupArgument), 300);
            var sampleFrames = PositiveInt(ArgumentValue(arguments, SampleArgument), 1200);
            for (var frame = 0; frame < warmupFrames; frame++)
                yield return null;

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Application.persistentDataPath);
            EnableProfileAreas();
            Profiler.maxUsedMemory = 1024 * 1024 * 1024;
            Profiler.logFile = outputPath;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;

            for (var frame = 0; frame < sampleFrames; frame++)
                yield return null;

            Profiler.enableBinaryLog = false;
            Profiler.enabled = false;

            var metadata = new ProfileRunMetadata
            {
                unityVersion = Application.unityVersion,
                productVersion = Application.version,
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                graphicsMemoryMb = SystemInfo.graphicsMemorySize,
                width = Screen.width,
                height = Screen.height,
                qualityLevel = QualitySettings.GetQualityLevel(),
                warmupFrames = warmupFrames,
                requestedSampleFrames = sampleFrames,
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                extraShips = extraShips
            };
            File.WriteAllText(Path.ChangeExtension(outputPath, "metadata.json"), JsonUtility.ToJson(metadata, true));

            yield return null;
            Application.Quit(0);
        }

        private static void EnableProfileAreas()
        {
            var areas = new[]
            {
                ProfilerArea.CPU,
                ProfilerArea.GPU,
                ProfilerArea.Rendering,
                ProfilerArea.Memory,
                ProfilerArea.Physics,
                ProfilerArea.UI
            };
            foreach (var area in areas)
                Profiler.SetAreaEnabled(area, true);
        }

        private static string ArgumentValue(string[] arguments, string name)
        {
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return null;
        }

        private static int PositiveInt(string value, int fallback) =>
            int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

        private static int NonNegativeInt(string value, int fallback) =>
            int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : fallback;

        private void SpawnStressShips(GameDriver driver, int count)
        {
            if (!driver || count <= 0 || !stressShipTemplate || !stressCommanderTemplate)
                return;

            const float radius = 45f;
            for (var index = 0; index < count; index++)
            {
                var angle = Mathf.PI * 2f * index / count;
                var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                driver.Services.UnitService.SpawnShip(
                    stressShipTemplate,
                    stressCommanderTemplate,
                    1,
                    GamePlane.PlanePointToWorld(position),
                    GamePlane.Rotation);
            }
        }

        [Serializable]
        private sealed class ProfileRunMetadata
        {
            public string unityVersion;
            public string productVersion;
            public string operatingSystem;
            public string processor;
            public int processorCount;
            public string graphicsDevice;
            public string graphicsApi;
            public int graphicsMemoryMb;
            public int width;
            public int height;
            public int qualityLevel;
            public int warmupFrames;
            public int requestedSampleFrames;
            public int vSyncCount;
            public int targetFrameRate;
            public int extraShips;
        }
    }
}
