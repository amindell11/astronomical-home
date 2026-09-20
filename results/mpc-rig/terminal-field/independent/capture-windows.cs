return string.Join("\n", System.Linq.Enumerable.Select(UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>(), w => w.GetType().FullName));
