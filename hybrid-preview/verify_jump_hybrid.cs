var source = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Travel.SectorJumpProfile>("Assets/Settings/Game/SectorJumpProfile.asset");
var profile = UnityEngine.Object.Instantiate(source);
var material = new UnityEngine.Material(UnityEngine.Shader.Find("Astronomical/Travel/SectorJump"));
var target = new UnityEngine.RenderTexture(960,540,0,UnityEngine.RenderTextureFormat.ARGB32);
var previous = UnityEngine.RenderTexture.active;
var output = System.IO.Path.GetFullPath("../../results/jump-hybrid");
System.IO.Directory.CreateDirectory(output);
using var outgoing = new Game.Travel.SectorJumpSkyPreview("Assets/Scenes/Locales/Locale_3.unity");
using var incoming = new Game.Travel.SectorJumpSkyPreview("Assets/Scenes/Locales/Locale_1.unity");
try
{
    var time = profile.turnDuration + profile.chargeDuration + profile.travelDuration * 0.4f;
    var direction = new UnityEngine.Vector2(1,-1).normalized;
    var distance = Game.Travel.SectorJumpPreview.TravelFraction(profile,time)*profile.skyTravelDistance;
    material.SetTexture("_FromSky",outgoing.Render(direction*distance));
    material.SetTexture("_ToSky",incoming.Render(direction*(distance-profile.skyTravelDistance)));
    material.SetTexture("_ShipImage",UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/Visuals/Travel/Ship.png"));
    UnityEngine.Color32[] Render(string name)
    {
        Game.Travel.SectorJumpPreview.Apply(material,profile,time,-135,16f/9f,540);
        UnityEngine.Graphics.Blit(null,target,material);
        UnityEngine.RenderTexture.active=target;
        var image=new UnityEngine.Texture2D(960,540,UnityEngine.TextureFormat.RGBA32,false);
        image.ReadPixels(new UnityEngine.Rect(0,0,960,540),0,0);image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),image.EncodeToPNG());
        var pixels=image.GetPixels32();
        UnityEngine.Object.DestroyImmediate(image);
        return pixels;
    }
    profile.rawBackgroundBlend=0;
    var softened=Render("background-soft");
    profile.rawBackgroundBlend=0.35f;
    var mixed=Render("background-mixed");
    profile.rawBackgroundBlend=1;
    var raw=Render("background-raw");
    profile.rawBackgroundBlend=0.35f;
    profile.shipBlurStrength=0;
    var withoutBlur=Render("ship-blur-off");
    var changedBackground=0;
    var changedShip=0;
    var distantDifference=0;
    for(var y=0;y<540;y++) for(var x=0;x<960;x++)
    {
        var i=y*960+x;
        int Delta(UnityEngine.Color32 a,UnityEngine.Color32 b) => System.Math.Abs(a.r-b.r)+System.Math.Abs(a.g-b.g)+System.Math.Abs(a.b-b.b);
        var far=x<250||x>710||y<70||y>470;
        if(far&&Delta(softened[i],raw[i])>3) changedBackground++;
        var blurDelta=Delta(mixed[i],withoutBlur[i]);
        if(blurDelta>3)changedShip++;
        if(far)distantDifference=System.Math.Max(distantDifference,blurDelta);
    }
    if(changedBackground<10000)throw new System.Exception("Raw blend did not alter the background");
    if(changedShip<50||distantDifference!=0)throw new System.Exception("Ship blur lost independence");
    var end=Game.Travel.SectorJumpPreview.TravelFraction(profile,profile.Duration);
    if(System.Math.Abs(end-1)>0.0001f)throw new System.Exception("Sky travel did not settle at destination");
    return new {changedBackgroundPixels=changedBackground,changedShipPixels=changedShip,distantBackgroundDifference=distantDifference,arrivalTravelFraction=end,sourceLocale="Locale_3",destinationLocale="Locale_1",additionalStreakBrightness=source.streakBrightness,output};
}
finally
{
    UnityEngine.RenderTexture.active=previous;
    target.Release();
    UnityEngine.Object.DestroyImmediate(target);
    UnityEngine.Object.DestroyImmediate(material);
    UnityEngine.Object.DestroyImmediate(profile);
}
