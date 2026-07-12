using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class TrackedUnityMcpSession
{
    private const string EndpointVariable = "ASTRONOMICAL_UNITY_MCP_ENDPOINT";

    static TrackedUnityMcpSession()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EndpointVariable)))
            return;

        EditorApplication.delayCall += Connect;
    }

    private static async void Connect()
    {
        string endpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        Assembly assembly = FindAssembly("MCPForUnity.Editor");
        if (assembly == null)
        {
            Debug.LogError("Tracked Unity editor could not load the MCP for Unity assembly.");
            return;
        }

        Type configurationType = assembly.GetType("MCPForUnity.Editor.Services.EditorConfigurationCache");
        object configuration = configurationType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        configurationType?.GetMethod("SetUseHttpTransport")?.Invoke(configuration, new object[] { true });
        configurationType?.GetMethod("SetHttpBaseUrl")?.Invoke(configuration, new object[] { endpoint });

        Type locatorType = assembly.GetType("MCPForUnity.Editor.Services.MCPServiceLocator");
        object bridge = locatorType?.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (bridge == null)
        {
            Debug.LogError("Tracked Unity editor could not resolve the MCP bridge.");
            return;
        }

        Type bridgeType = bridge.GetType();
        bool running = (bool)(bridgeType.GetProperty("IsRunning")?.GetValue(bridge) ?? false);
        if (running)
            return;

        Task<bool> start = bridgeType.GetMethod("StartAsync")?.Invoke(bridge, null) as Task<bool>;
        if (start == null || !await start)
            Debug.LogError($"Tracked Unity editor could not connect to MCP at {endpoint}.");
    }

    private static Assembly FindAssembly(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == name)
                return assembly;
        }

        return null;
    }
}
