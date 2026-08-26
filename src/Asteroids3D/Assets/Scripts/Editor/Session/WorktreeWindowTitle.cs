using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prefixes the editor main-window title with this checkout's identity —
/// [PRIMARY] for the primary tree, [AGENT-N] for a pool worktree — so
/// concurrent editors are distinguishable in the taskbar. Unity's title hook
/// (EditorApplication.updateMainWindowTitle) hands an internal descriptor
/// type, so the handler is built with expression trees at load time.
/// </summary>
[InitializeOnLoad]
internal static class WorktreeWindowTitle
{
    static WorktreeWindowTitle()
    {
        Match slot = Regex.Match(Application.dataPath, "agent-(\\d+)");
        string prefix = "[" + (slot.Success ? "AGENT-" + slot.Groups[1].Value : "PRIMARY") + "] ";

        const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type editorApp = typeof(EditorApplication);
        EventInfo hook = editorApp.GetEvent("updateMainWindowTitle", anyStatic);
        Type descriptor = editorApp.Assembly.GetType("UnityEditor.ApplicationTitleDescriptor");
        FieldInfo title = descriptor?.GetField("title");
        MethodInfo refresh = editorApp.GetMethod("UpdateMainWindowTitle", anyStatic);
        if (hook == null || title == null || refresh == null)
            throw new InvalidOperationException("Unity's main-window-title internals moved; update WorktreeWindowTitle.");

        ParameterExpression d = Expression.Parameter(descriptor, "d");
        MethodInfo concat = typeof(string).GetMethod(nameof(string.Concat),
            new[] { typeof(string), typeof(string), typeof(string), typeof(string) });
        Expression newTitle = Expression.Call(concat,
            Expression.Constant(prefix),
            Expression.Property(d, "projectName"),
            Expression.Constant(" — "),
            Expression.Property(d, "activeSceneName"));
        LambdaExpression handler = Expression.Lambda(
            typeof(Action<>).MakeGenericType(descriptor),
            Expression.Assign(Expression.Field(d, title), newTitle),
            d);

        hook.AddEventHandler(null, handler.Compile());
        EditorApplication.delayCall += () => refresh.Invoke(null, null);
    }
}
