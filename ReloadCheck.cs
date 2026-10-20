// SCRATCH reload experiment for #1048. Never commit.
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ReloadCheck
{
    static ReloadCheck()
    {
        if (!SessionState.GetBool("ReloadCheck.pending", false)) return;
        SessionState.SetBool("ReloadCheck.pending", false);
        Debug.Log("RELOAD_CHECK after reload");
        EditorApplication.delayCall += Inspect;
    }

    public static void Start()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Locales/Locale_1.unity", OpenSceneMode.Single);
        Inspect(false);
        SessionState.SetBool("ReloadCheck.pending", true);
        Debug.Log("RELOAD_CHECK requesting reload");
        EditorUtility.RequestScriptReload();
    }

    private static void Inspect() => Inspect(true);

    private static void Inspect(bool exit)
    {
        var type = Type.GetType("Substrate.Services.Locales.CloudBankTile, Core");
        foreach (var component in UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None))
        {
            string State(string field)
            {
                var value = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
                return value == null || (value is UnityEngine.Object o && !o) ? "null" : "set";
            }
            Debug.Log($"RELOAD_CHECK {(exit ? "after" : "before")} {((Component)component).name} " +
                      $"layer={State("layer")} properties={State("properties")} tile={State("tile")}");
        }
        if (exit) EditorApplication.Exit(0);
    }
}
