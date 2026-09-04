using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Game.Session
{
    /// <summary>
    /// Prefixes the editor main-window title with this checkout's identity —
    /// [PRIMARY] for the primary tree, [AGENT-N] for a pool worktree — so
    /// concurrent editors are distinguishable in the taskbar. Agents holding a
    /// live editor add a task label via the `set_window_title` CLI command
    /// (unity-access skill directs this on acquire); the label resets on domain
    /// reload, matching a task-scoped claim. Unity's title hook
    /// (EditorApplication.updateMainWindowTitle) hands an internal descriptor
    /// type, so the handler is built with expression trees at load time.
    /// </summary>
    [InitializeOnLoad]
    internal static class WorktreeWindowTitle
    {
        private static readonly string Prefix;
        private static readonly MethodInfo Refresh;
        private static string labelOverride = "";
        private static string lastComposed = "";

        static WorktreeWindowTitle()
        {
            Match slot = Regex.Match(Application.dataPath, "agent-(\\d+)");
            Prefix = "[" + (slot.Success ? "AGENT-" + slot.Groups[1].Value : "PRIMARY") + "] ";

            const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type editorApp = typeof(EditorApplication);
            EventInfo hook = editorApp.GetEvent("updateMainWindowTitle", anyStatic);
            Type descriptor = editorApp.Assembly.GetType("UnityEditor.ApplicationTitleDescriptor");
            FieldInfo title = descriptor?.GetField("title");
            Refresh = editorApp.GetMethod("UpdateMainWindowTitle", anyStatic);
            if (hook == null || title == null || Refresh == null)
                throw new InvalidOperationException("Unity's main-window-title internals moved; update WorktreeWindowTitle.");

            MethodInfo compose = typeof(WorktreeWindowTitle).GetMethod(nameof(Compose), anyStatic);
            ParameterExpression d = Expression.Parameter(descriptor, "d");
            LambdaExpression handler = Expression.Lambda(
                typeof(Action<>).MakeGenericType(descriptor),
                Expression.Assign(
                    Expression.Field(d, title),
                    Expression.Call(compose,
                        Expression.Property(d, "projectName"),
                        Expression.Property(d, "activeSceneName"))),
                d);

            hook.AddEventHandler(null, handler.Compile());
            EditorApplication.delayCall += () => Refresh.Invoke(null, null);
        }

        private static string Compose(string projectName, string sceneName)
        {
            string body = string.IsNullOrEmpty(labelOverride) ? projectName : labelOverride;
            return lastComposed = Prefix + body + " — " + sceneName;
        }

        [CliCommand("set_window_title",
            "Set a task label in this editor's main-window title ([SLOT] <label> — <scene>). Omit label to restore the default. Resets on domain reload.",
            Tags = new[] { "editor" })]
        public static string SetWindowTitle(
            [CliArg("label", "Task label shown in place of the project name; empty or omitted restores the default.")] string label = "")
        {
            labelOverride = label ?? "";
            Refresh.Invoke(null, null);
            return "title: " + lastComposed;
        }
    }
}
