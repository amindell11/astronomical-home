var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Travel.SectorJumpProfile>("Assets/Settings/Game/SectorJumpProfile.asset");
var clone = UnityEngine.Object.Instantiate(profile);
var material = new UnityEngine.Material(UnityEngine.Shader.Find("Astronomical/Travel/SectorJump"));
material.SetTexture("_FromSky", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Visuals/Travel/LocaleFrom.png"));
material.SetTexture("_ToSky", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Visuals/Travel/LocaleTo.png"));
material.SetTexture("_ShipImage", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Visuals/Travel/Ship.png"));
var target = new UnityEngine.RenderTexture(960, 540, 0, UnityEngine.RenderTextureFormat.ARGB32);
var prior = UnityEngine.RenderTexture.active;
var output = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../results/jump-controls"));
System.IO.Directory.CreateDirectory(output);
UnityEngine.Color32[] Render(string name)
{
    Game.Travel.SectorJumpPreview.Apply(material, clone, clone.turnDuration + clone.chargeDuration + clone.travelDuration * 0.4f, -135, 16f / 9f, 540);
    UnityEngine.Graphics.Blit(null, target, material);
    UnityEngine.RenderTexture.active = target;
    var image = new UnityEngine.Texture2D(960, 540, UnityEngine.TextureFormat.RGBA32, false);
    image.ReadPixels(new UnityEngine.Rect(0, 0, 960, 540), 0, 0);
    image.Apply();
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, name + ".png"), image.EncodeToPNG());
    var pixels = image.GetPixels32();
    UnityEngine.Object.DestroyImmediate(image);
    return pixels;
}
var enabled = Render("ship-blur-on");
clone.shipBlurStrength = 0;
var disabled = Render("ship-blur-off");
var changed = 0;
var farDifference = 0;
for (var y = 0; y < 540; y++)
for (var x = 0; x < 960; x++)
{
    var a = enabled[y * 960 + x];
    var b = disabled[y * 960 + x];
    var delta = System.Math.Abs(a.r - b.r) + System.Math.Abs(a.g - b.g) + System.Math.Abs(a.b - b.b);
    if (delta > 3) changed++;
    if (x < 250 || x > 710 || y < 70 || y > 470) farDifference = System.Math.Max(farDifference, delta);
}
UnityEngine.RenderTexture.active = prior;
target.Release();
UnityEngine.Object.DestroyImmediate(target);
UnityEngine.Object.DestroyImmediate(material);
UnityEngine.Object.DestroyImmediate(clone);
if (changed < 50) throw new System.Exception("Ship blur control did not change the rendered ship trail: " + changed);
if (farDifference > 0) throw new System.Exception("Ship blur control changed distant background pixels: " + farDifference);
return new { changedShipPixels = changed, distantBackgroundDifference = farDifference, output };
