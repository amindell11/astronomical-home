// SCRATCH canopy projection experiment arms for the perf pass. Never commit.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class PerfPassProbe
{
    private readonly struct Arm
    {
        public readonly string name;
        public readonly bool packed, half, lite, off;

        public Arm(string name, bool packed, bool half, bool lite, bool off)
        {
            this.name = name;
            this.packed = packed;
            this.half = half;
            this.lite = lite;
            this.off = off;
        }
    }

    private static readonly Arm A0 = new("A0_current", false, false, false, false);
    private static readonly Arm A1 = new("A1_packed", true, false, false, false);
    private static readonly Arm A2 = new("A2_half_afterglow", false, true, false, false);
    private static readonly Arm A3 = new("A3_ink_lite", false, false, true, false);
    private static readonly Arm A4 = new("A4_all", true, true, true, false);
    private static readonly Arm A5 = new("A5_feature_off", false, false, false, true);

    private FieldInfo expPacked, expHalf, expFreeze, expFormat;
    private UniversalAdditionalCameraData uiData;
    private bool uiShadows;
    private CameraOverrideOption uiDepth, uiColor;
    private List<ScriptableRendererFeature> canopyFeatures = new();
    private Texture2D shot;

    private IEnumerator Canopy()
    {
        var exp = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UI.Projection.CanopyExperiment"))
            .FirstOrDefault(t => t != null);
        if (exp == null)
        {
            Fail("CanopyExperiment type not found");
            yield break;
        }
        expPacked = exp.GetField("PackedScene");
        expHalf = exp.GetField("HalfAfterglow");
        expFreeze = exp.GetField("FreezeClock");
        expFormat = exp.GetField("SceneFormat");

        Time.timeScale = 0.001f;
        yield return new WaitForSecondsRealtime(1f);
        foreach (var res in resolutions)
        {
            yield return SetResolution(res);
            QualitySettings.vSyncCount = 0;
            var ctx = Snapshot("ObserverCam");
            var ui = FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.enabled && c.name.StartsWith("UI Cam"));
            uiData = ui ? ui.GetComponent<UniversalAdditionalCameraData>() : null;
            if (!uiData)
            {
                Fail("UI Cam camera data not found");
                yield break;
            }
            uiShadows = uiData.renderShadows;
            uiDepth = uiData.requiresDepthOption;
            uiColor = uiData.requiresColorOption;
            canopyFeatures = ctx.features.Select(f => f.feature).OfType<ScriptableRendererFeature>()
                .Where(f => f.GetType().Name == "CanopyProjectionFeature").ToList();
            Write(new Dictionary<string, object>
            {
                { "canopySetup", true },
                { "screen", Screen.width + "x" + Screen.height },
                { "quality", QualitySettings.names[QualitySettings.GetQualityLevel()] },
                { "uiCam", ui.name + " mask=" + ui.cullingMask + " renderer=" + uiData.scriptableRenderer?.GetType().Name },
                { "uiShadows", uiShadows },
                { "uiDepth", uiDepth.ToString() },
                { "uiColor", uiColor.ToString() },
                { "canopyFeatures", canopyFeatures.Count },
                { "cameras", string.Join(" | ", FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled)
                    .Select(c => c.name + " depth=" + c.depth + " hdr=" + c.allowHDR)) },
            });

            foreach (var arm in new[] { A0, A1, A2, A3, A4, A5 })
                yield return TimeArm(arm, ctx, arm.name);
            yield return TimeArm(A0, ctx, "A0_end");

            Apply(A0);
            expFreeze.SetValue(null, true);
            var grains = SetGrain(false);
            for (var i = 0; i < 40; i++) yield return null;
            yield return Capture();
            var reference = shot;
            Save(reference, "diffset_A0_current");
            foreach (var (arm, name) in new[] { (A1, A1.name), (A2, A2.name), (A3, A3.name), (A4, A4.name), (A0, "A0_repeat") })
            {
                Apply(arm);
                for (var i = 0; i < 40; i++) yield return null;
                yield return Capture();
                Diff(reference, shot, name);
                Destroy(shot);
            }
            Destroy(reference);
            RestoreGrain(grains);
            expFreeze.SetValue(null, false);

            foreach (var arm in new[] { A0, A4 })
            {
                Apply(arm);
                for (var i = 0; i < 40; i++) yield return null;
                yield return Capture();
                Save(shot, "still_" + arm.name);
                Destroy(shot);
            }
            Apply(A0);
        }

        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(1f);
        foreach (var res in resolutions)
        {
            yield return SetResolution(res);
            QualitySettings.vSyncCount = 0;
            foreach (var arm in new[] { A0, A4 })
            {
                Apply(arm);
                yield return Live("canopy_live_" + arm.name, 10f, null);
            }
        }
        Apply(A0);
    }

    private IEnumerator InkLite()
    {
        var exp = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UI.Projection.CanopyExperiment"))
            .FirstOrDefault(t => t != null);
        expPacked = exp.GetField("PackedScene");
        expHalf = exp.GetField("HalfAfterglow");
        expFreeze = exp.GetField("FreezeClock");
        expFormat = exp.GetField("SceneFormat");
        yield return SetResolution(resolutions[0]);
        QualitySettings.vSyncCount = 0;
        var ctx = Snapshot("ObserverCam");
        var ui = FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.enabled && c.name.StartsWith("UI Cam"));
        uiData = ui ? ui.GetComponent<UniversalAdditionalCameraData>() : null;
        if (!uiData)
        {
            Fail("UI Cam camera data not found");
            yield break;
        }
        uiShadows = uiData.renderShadows;
        uiDepth = uiData.requiresDepthOption;
        uiColor = uiData.requiresColorOption;
        canopyFeatures = ctx.features.Select(f => f.feature).OfType<ScriptableRendererFeature>()
            .Where(f => f.GetType().Name == "CanopyProjectionFeature").ToList();
        for (var block = 0; block < 6; block++)
            foreach (var arm in new[] { A0, A3 })
            {
                Apply(arm);
                yield return Live("ink_" + arm.name + "_b" + block, 10f, null);
            }
        foreach (var arm in new[] { A0, A3 })
        {
            Apply(arm);
            yield return Live("ink_prof_" + arm.name, 5f, Path.ChangeExtension(outPath, null) + "_prof_" + arm.name);
        }
        Apply(A0);
    }

    private IEnumerator TimeArm(Arm arm, Ctx ctx, string label)
    {
        Apply(arm);
        yield return Measure("canopy:" + label, ctx);
        yield return Passes("canopy:" + label);
        Write(new Dictionary<string, object>
        {
            { "armState", label },
            { "screen", Screen.width + "x" + Screen.height },
            { "sceneFormat", expFormat.GetValue(null) as string ?? "null" },
            { "packed", (bool)expPacked.GetValue(null) },
            { "half", (bool)expHalf.GetValue(null) },
            { "uiShadows", uiData.renderShadows },
            { "uiDepth", uiData.requiresDepthOption.ToString() },
            { "uiColor", uiData.requiresColorOption.ToString() },
            { "featureActive", canopyFeatures.All(f => f.isActive) },
        });
        expFormat.SetValue(null, null);
    }

    private void Apply(Arm arm)
    {
        expPacked.SetValue(null, arm.packed);
        expHalf.SetValue(null, arm.half);
        uiData.renderShadows = arm.lite ? false : uiShadows;
        uiData.requiresDepthOption = arm.lite ? CameraOverrideOption.Off : uiDepth;
        uiData.requiresColorOption = arm.lite ? CameraOverrideOption.Off : uiColor;
        foreach (var f in canopyFeatures) f.SetActive(!arm.off);
    }

    private static List<FilmGrain> SetGrain(bool on)
    {
        var changed = new List<FilmGrain>();
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            var profile = v.HasInstantiatedProfile() ? v.profile : v.sharedProfile;
            if (profile && profile.TryGet<FilmGrain>(out var grain) && grain.active != on)
            {
                grain.active = on;
                changed.Add(grain);
            }
        }
        return changed;
    }

    private static void RestoreGrain(List<FilmGrain> grains)
    {
        foreach (var g in grains) g.active = !g.active;
    }

    private IEnumerator Capture()
    {
        yield return new WaitForEndOfFrame();
        shot = ScreenCapture.CaptureScreenshotAsTexture();
    }

    private string ShotPath(string name) =>
        Path.ChangeExtension(outPath, null) + "_" + name + "_" + Screen.width + ".png";

    private void Save(Texture2D tex, string name) => File.WriteAllBytes(ShotPath(name), tex.EncodeToPNG());

    private void Diff(Texture2D reference, Texture2D candidate, string name)
    {
        var a = reference.GetPixels32();
        var b = candidate.GetPixels32();
        var image = new Color32[a.Length];
        var max = 0;
        long abs = 0, signed = 0;
        int over1 = 0, over2 = 0, over4 = 0, darker1 = 0;
        for (var i = 0; i < a.Length; i++)
        {
            int dr = b[i].r - a[i].r, dg = b[i].g - a[i].g, db = b[i].b - a[i].b;
            int ar = Math.Abs(dr), ag = Math.Abs(dg), ab = Math.Abs(db);
            var m = Math.Max(ar, Math.Max(ag, ab));
            max = Math.Max(max, m);
            abs += ar + ag + ab;
            signed += dr + dg + db;
            if (m > 1) over1++;
            if (m > 2) over2++;
            if (m > 4) over4++;
            if (dr < -1 || dg < -1 || db < -1) darker1++;
            image[i] = new Color32((byte)Math.Min(255, ar * 16), (byte)Math.Min(255, ag * 16), (byte)Math.Min(255, ab * 16), 255);
        }
        var channels = a.Length * 3.0;
        Write(new Dictionary<string, object>
        {
            { "diff", name },
            { "screen", Screen.width + "x" + Screen.height },
            { "pixels", a.Length },
            { "maxAbs", max },
            { "meanAbs", abs / channels },
            { "signedMean", signed / channels },
            { "over1", over1 },
            { "over2", over2 },
            { "over4", over4 },
            { "darkerOver1", darker1 },
        });
        Save(candidate, "diffset_" + name);
        var diff = new Texture2D(reference.width, reference.height, TextureFormat.RGBA32, false);
        diff.SetPixels32(image);
        diff.Apply();
        Save(diff, "diffimg_" + name);
        Destroy(diff);
    }
}
