using UnityEditor;
using UnityEngine;

public static class RevisePlanetLocales
{
    public static void Run()
    {
        for (var id = 6; id <= 7; id++)
        {
            var folder = "Assets/Visuals/Locales/Sky/Locales/Locale_" + id;
            var planet = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Planet.mat");
            planet.SetVector("_PlanetCenter", id == 6 ? new Vector4(3500, 0, 6156.25f, 0) :
                new Vector4(4687.5f, 375, 6156.25f, 0));
            planet.SetFloat("_PlanetRadius", id == 6 ? 3750 : 3562.5f);
            planet.SetFloat("_SurfaceLongitude", id == 6 ? -66 : 100);
            planet.SetFloat("_AxialTilt", id == 6 ? 45 : 13);
            planet.SetColor("_AtmosphereColor", id == 6 ? new Color(.14f, .38f, .32f) : new Color(.12f, .34f, .55f));
            planet.SetVector("_LightDirection", id == 6 ? new Vector4(-.9f, .25f, -.1f, 0) : new Vector4(-.8f, .15f, .35f, 0));
            EditorUtility.SetDirty(planet);
            Set(folder, "Background", "_RepeatDistance", 60000);
            Set(folder, "DistantStars", "_ParallaxScale", .0003f);
            Set(folder, "StarField", "_ParallaxScale", .00065f);
            Set(folder, "StarField", "_ShootingParallax", .00065f);
            Set(folder, "FarNebula", "_NebulaParallax", .0014f);
            Set(folder, "CloseNebula", "_NebulaParallax", .008f);
            Debug.Log("REVISED_PLANET=Locale_" + id);
        }
        AssetDatabase.SaveAssets();
    }

    private static void Set(string folder, string layer, string property, float value)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + layer + ".mat");
        material.SetFloat(property, value);
        EditorUtility.SetDirty(material);
    }
}
