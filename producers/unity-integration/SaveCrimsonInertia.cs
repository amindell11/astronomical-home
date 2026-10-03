using UnityEditor;
using UnityEngine;

public static class SaveCrimsonInertia
{
    public static string Save()
    {
        const string path = "Assets/Prefabs/Ships/Ship_2.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var body = root.GetComponent<Rigidbody>();
            body.automaticInertiaTensor = false;
            body.inertiaTensor = new Vector3(235.56543f, 212.813f, 331.0046f);
            body.inertiaTensorRotation = new Quaternion(.0416830853f, .000924919033f, .007938499f, .999099f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Saved Crimson inertia through PrefabUtility";
    }
}
