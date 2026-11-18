using System;
using System.Collections;
using System.Collections.Generic;
using Ships;
using Ships.Loadout;
using UI.Hangar.Displays;
using UI.Hangar.Hologram;
using UI.Hangar.Selection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// The hangar: its console world, the composite image, and the choices on its screens.
    /// The left pair shows the ship; the right pair shows the weapon and the ordnance. The
    /// hologram projector is its part at the lens. An accepted Launch locks the keys and the
    /// pointer, presses the button and powers the screens down before the caller is handed the
    /// pending loadout, once. The console applies its light profile each frame.
    /// </summary>
    public sealed class HangarConsole : MonoBehaviour
    {
        public const float ReferenceAspect = 16f / 9f;
        public const int CompositeSortingOrder = 50;
        private const float TallerViewVerticalBias = .36f;
        private const float TallerViewOverscan = 1.01f;
        private static readonly int ConstantEmissionId = Shader.PropertyToID("_ConstantEmission");
        private static readonly int StarlightId = Shader.PropertyToID("_Starlight");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        private static readonly int EmissionTintId = Shader.PropertyToID("_EmissionTint");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int TopShareId = Shader.PropertyToID("_TopShare");
        private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
        private const float LaunchDim = .1f;

        [SerializeField] private Camera hardwareCamera;
        [SerializeField] private float referenceVerticalFov = 34.11393f;
        [SerializeField] private MeshRenderer mainScreenGlass;
        [SerializeField] private MeshRenderer detailScreenGlass;
        [SerializeField] private MeshRenderer primaryScreenGlass;
        [SerializeField] private MeshRenderer ordnanceScreenGlass;
        [SerializeField] private HangarControl selectorKnob;
        [SerializeField] private HangarKnob knob;
        [SerializeField] private HangarControl previousButton;
        [SerializeField] private HangarControl nextButton;
        [SerializeField] private HangarControl launchButton;
        [SerializeField] private HangarControl primaryPreviousButton;
        [SerializeField] private HangarControl primaryNextButton;
        [SerializeField] private HangarControl ordnancePreviousButton;
        [SerializeField] private HangarControl ordnanceNextButton;
        [SerializeField] private MeshRenderer launchLegend;
        [SerializeField] private MeshRenderer projectorLens;
        [SerializeField] private RawImage compositeImage;
        [SerializeField] private HangarHologramProjector projector;
        [SerializeField] private HangarDisplay mainDisplay;
        [SerializeField] private HangarDisplay detailDisplay;
        [SerializeField] private HangarDisplay primaryDisplay;
        [SerializeField] private HangarDisplay ordnanceDisplay;
        [SerializeField] private HangarMainDisplayView mainView;
        [SerializeField] private HangarDetailDisplayView detailView;
        [SerializeField] private HangarWeaponDisplayView primaryView;
        [SerializeField] private HangarWeaponDisplayView ordnanceView;
        [SerializeField] private HangarDisplayProfile displayProfile;
        [SerializeField] private HangarLightProfile lightProfile;
        [SerializeField] private MeshRenderer[] paintedParts;
        [SerializeField] private MeshRenderer launchSurface;
        [SerializeField] private Light projectorSpill;
        [SerializeField] private Light launchSpill;
        [SerializeField] private HangarGlow[] lampGlows;
        [SerializeField] private HangarGlow[] downlightGlows;
        [SerializeField] private HangarGlow[] downlightCones;
        [SerializeField] private Vector3 knobAxis;
        [SerializeField] private float knobDetentDegrees = 40f;

        private RenderTexture texture;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<InputAction> actions = new();
        private MaterialPropertyBlock legendBlock;
        private MaterialPropertyBlock lightBlock;
        private Color legendLit;
        private HangarConsoleRaycaster raycaster;
        private HangarSelection selection;
        private Action onLaunch;
        private bool launching;

        public Camera HardwareCamera => hardwareCamera;
        public MeshRenderer MainScreenGlass => mainScreenGlass;
        public MeshRenderer DetailScreenGlass => detailScreenGlass;
        public MeshRenderer PrimaryScreenGlass => primaryScreenGlass;
        public MeshRenderer OrdnanceScreenGlass => ordnanceScreenGlass;
        public Transform SelectorKnob => selectorKnob.transform;
        public Transform PreviousButton => previousButton.transform;
        public Transform NextButton => nextButton.transform;
        public Transform LaunchButton => launchButton.transform;
        public Transform PrimaryPreviousButton => primaryPreviousButton.transform;
        public Transform PrimaryNextButton => primaryNextButton.transform;
        public Transform OrdnancePreviousButton => ordnancePreviousButton.transform;
        public Transform OrdnanceNextButton => ordnanceNextButton.transform;
        public MeshRenderer ProjectorLens => projectorLens;
        public HangarHologramProjector Projector => projector;
        public Texture Texture => texture;
        internal RawImage CompositeImage => compositeImage;
        internal HangarDisplay MainDisplay => mainDisplay;
        internal HangarDisplay DetailDisplay => detailDisplay;
        internal HangarDisplay PrimaryDisplay => primaryDisplay;
        internal HangarDisplay OrdnanceDisplay => ordnanceDisplay;
        internal HangarSelection Selection => selection;
        internal Vector3 KnobAxis => knobAxis;
        internal float KnobDetentDegrees => knobDetentDegrees;
        internal bool KnobSettled => knob.Settled;
        internal bool InputLocked => launching;
        internal bool LaunchLegendLit => legendBlock.GetColor(ConstantEmissionId) == legendLit;

        private void Awake()
        {
            if (!hardwareCamera || !mainScreenGlass || !detailScreenGlass || !primaryScreenGlass ||
                !ordnanceScreenGlass || !selectorKnob || !knob || !previousButton || !nextButton ||
                !launchButton || !primaryPreviousButton || !primaryNextButton || !ordnancePreviousButton ||
                !ordnanceNextButton || !launchLegend || !projectorLens || !compositeImage || !projector ||
                !mainDisplay || !detailDisplay || !primaryDisplay || !ordnanceDisplay || !mainView ||
                !detailView || !primaryView || !ordnanceView || !displayProfile || !lightProfile ||
                paintedParts == null || paintedParts.Length == 0 || !launchSurface || !projectorSpill ||
                !launchSpill || lampGlows == null || downlightGlows == null || downlightCones == null)
                throw new InvalidOperationException(
                    "HangarConsole needs its camera, four screen glasses, controls, weapon keys, knob, launch legend, displays, display profile, light profile, projector lens, composite image, projector, painted parts, launch surface, projector spill, launch spill, lamp glows, downlight glows and downlight cones.");
            raycaster = GetComponent<HangarConsoleRaycaster>();
            if (!raycaster)
                throw new InvalidOperationException("HangarConsole needs its raycaster on the same object.");
            knob.Initialize(knobAxis);
            legendBlock = new MaterialPropertyBlock();
            lightBlock = new MaterialPropertyBlock();
            legendLit = launchLegend.sharedMaterial.GetColor(ConstantEmissionId);
            LightLegend(true);
            mainDisplay.Initialize(hardwareCamera, mainScreenGlass, () => displayProfile.MainDisplay,
                () => lightProfile.MainScreenSpill, () => lightProfile.DisplayBacking);
            detailDisplay.Initialize(hardwareCamera, detailScreenGlass, () => displayProfile.DetailDisplay,
                () => lightProfile.DetailScreenSpill, () => lightProfile.DisplayBacking);
            primaryDisplay.Initialize(hardwareCamera, primaryScreenGlass, () => displayProfile.WeaponDisplay,
                () => lightProfile.WeaponScreenSpill, () => lightProfile.DisplayBacking);
            ordnanceDisplay.Initialize(hardwareCamera, ordnanceScreenGlass, () => displayProfile.WeaponDisplay,
                () => lightProfile.WeaponScreenSpill, () => lightProfile.DisplayBacking);
        }

        public void Show(ItemSubset offer, ShipLoadout loadout, Func<Ship, bool> earned, Action launch)
        {
            if (selection != null)
                throw new InvalidOperationException("HangarConsole.Show was called twice.");
            onLaunch = launch;
            selection = new HangarSelection(offer, loadout, earned);
            projector.Open(offer ? offer.ships : Array.Empty<Ship>(), loadout.Ship);
            selection.ShipChanged += projector.Select;
            mainView.Bind(selection);
            detailView.Bind(selection);
            BindWeaponScreen(primaryView, HangarSlot.Weapon, primaryPreviousButton, primaryNextButton);
            BindWeaponScreen(ordnanceView, HangarSlot.Ordnance, ordnancePreviousButton, ordnanceNextButton);
            mainView.ArrowPressed += step => (step < 0 ? previousButton : nextButton).Press();
            previousButton.Pressed += () => selection.Step(-1);
            nextButton.Pressed += () => selection.Step(1);
            selectorKnob.Pressed += () => selection.Turn(1);
            launchButton.Pressed += Launch;
            selection.Changed += ApplyKnob;
            selection.Changed += ApplyLaunch;
            selection.OptionChanged += mainDisplay.Glitch;
            selection.OptionChanged += detailDisplay.Glitch;
            selection.OptionChanged += primaryDisplay.Glitch;
            selection.OptionChanged += ordnanceDisplay.Glitch;
            EnableKeys();
            knob.Snap(Detent());
            ApplyLaunch();
        }

        public void Launch()
        {
            if (selection == null)
                throw new InvalidOperationException("HangarConsole.Launch was called before Show.");
            if (!selection.CanLaunch || launching) return;
            launching = true;
            for (var i = 0; i < actions.Count; i++) actions[i].Disable();
            raycaster.enabled = false;
            launchButton.Move();
            mainDisplay.PowerDown();
            detailDisplay.PowerDown();
            primaryDisplay.PowerDown();
            ordnanceDisplay.PowerDown();
            StartCoroutine(HandOff());
        }

        private void BindWeaponScreen(HangarWeaponDisplayView view, HangarSlot slot, HangarControl previousKey,
            HangarControl nextKey)
        {
            view.Bind(selection);
            view.ArrowPressed += step => (step < 0 ? previousKey : nextKey).Press();
            previousKey.Pressed += () => selection.Step(slot, -1);
            nextKey.Pressed += () => selection.Step(slot, 1);
        }

        private IEnumerator HandOff()
        {
            var started = Time.unscaledTime;
            while (Time.unscaledTime - started < HangarDisplay.PowerDownSeconds) yield return null;
            onLaunch?.Invoke();
        }

        private void LateUpdate()
        {
            ApplyLight();
            compositeImage.rectTransform.GetWorldCorners(corners);
            Resize(Vector2Int.CeilToInt(corners[2] - corners[0]));
            compositeImage.texture = texture;
        }

        public void Resize(Vector2Int pixels)
        {
            var aspect = (float)pixels.x / pixels.y;
            var halfHeight = Mathf.Tan(referenceVerticalFov * Mathf.Deg2Rad * .5f);
            hardwareCamera.fieldOfView = 2f * Mathf.Atan(halfHeight *
                Mathf.Max(1f, ReferenceAspect / aspect)) * Mathf.Rad2Deg;
            var projection = Matrix4x4.Perspective(hardwareCamera.fieldOfView, aspect,
                hardwareCamera.nearClipPlane, hardwareCamera.farClipPlane);
            if (aspect < ReferenceAspect)
            {
                // Keep both finite silhouette edges beyond the frame in taller views.
                projection.m12 = -(1f - aspect / ReferenceAspect) * TallerViewVerticalBias;
                projection.m00 *= TallerViewOverscan;
                projection.m11 *= TallerViewOverscan;
            }
            hardwareCamera.projectionMatrix = projection;
            projector.Fit(hardwareCamera);
            if (texture && texture.width == pixels.x && texture.height == pixels.y) return;

            ReleaseTexture();
            texture = new RenderTexture(pixels.x, pixels.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "HangarConsoleRT",
                antiAliasing = 2
            };
            hardwareCamera.targetTexture = texture;
        }

        private void ApplyLight()
        {
            var emission = lightProfile.EmissionStrength;
            for (var i = 0; i < paintedParts.Length; i++)
            {
                paintedParts[i].GetPropertyBlock(lightBlock);
                lightBlock.SetColor(StarlightId, lightProfile.Ambient);
                lightBlock.SetFloat(EmissionStrengthId, emission);
                paintedParts[i].SetPropertyBlock(lightBlock);
            }

            projectorLens.GetPropertyBlock(lightBlock);
            lightBlock.SetColor(EmissionTintId, lightProfile.LensTint);
            projectorLens.SetPropertyBlock(lightBlock);
            launchSurface.GetPropertyBlock(lightBlock);
            lightBlock.SetColor(StarlightId, lightProfile.LaunchGlow);
            lightBlock.SetFloat(EmissionStrengthId, emission);
            launchSurface.SetPropertyBlock(lightBlock);
            projectorSpill.color = lightProfile.HologramSpillColor;
            projectorSpill.intensity = lightProfile.HologramSpill;
            launchSpill.color = lightProfile.LaunchSpillColor;
            launchSpill.intensity = lightProfile.LaunchSpill;

            var lampSize = Vector3.one * lightProfile.LampGlowSize;
            for (var i = 0; i < lampGlows.Length; i++)
                ApplyGlow(lampGlows[i], lampGlows[i].color * lightProfile.LampGlow, lampSize);
            var downlightSize = Vector3.one * lightProfile.DownlightGlowSize;
            for (var i = 0; i < downlightGlows.Length; i++)
                ApplyGlow(downlightGlows[i], downlightGlows[i].color * lightProfile.DownlightGlow, downlightSize);
            var coneScale = new Vector3(lightProfile.ConeWidth, lightProfile.ConeLength, 1f);
            for (var i = 0; i < downlightCones.Length; i++)
            {
                var glow = downlightCones[i];
                glow.card.GetPropertyBlock(lightBlock);
                lightBlock.SetColor(GlowColorId, glow.color * lightProfile.ConeStrength);
                lightBlock.SetFloat(TopShareId, lightProfile.ConeTopShare);
                lightBlock.SetFloat(FalloffId, lightProfile.ConeFalloff);
                glow.card.SetPropertyBlock(lightBlock);
                glow.card.transform.localScale = coneScale;
            }
        }

        private void ApplyGlow(HangarGlow glow, Color color, Vector3 scale)
        {
            glow.card.GetPropertyBlock(lightBlock);
            lightBlock.SetColor(GlowColorId, color);
            glow.card.SetPropertyBlock(lightBlock);
            glow.card.transform.localScale = scale;
        }

        private void ApplyKnob() => knob.Turn(Detent());

        private float Detent() => ((int)selection.Selected - 1) * knobDetentDegrees;

        private void ApplyLaunch() => LightLegend(selection.CanLaunch);

        private void LightLegend(bool lit)
        {
            legendBlock.SetColor(ConstantEmissionId, lit ? legendLit : legendLit * LaunchDim);
            launchLegend.SetPropertyBlock(legendBlock);
        }

        private void EnableKeys()
        {
            Key("StepBack", "<Keyboard>/leftArrow", "<Keyboard>/a", previousButton.Press);
            Key("StepForward", "<Keyboard>/rightArrow", "<Keyboard>/d", nextButton.Press);
            Key("TurnBack", "<Keyboard>/upArrow", "<Keyboard>/w", () => selection.Turn(-1));
            Key("TurnForward", "<Keyboard>/downArrow", "<Keyboard>/s", selectorKnob.Press);
            // Scroll is a signed axis; a button binding ignores the negative half.
            var wheel = new InputAction("TurnWheel", InputActionType.Value, "<Mouse>/scroll/y");
            wheel.performed += context =>
            {
                var y = context.ReadValue<float>();
                if (y > 0f) selection.Turn(-1);
                else if (y < 0f) selection.Turn(1);
            };
            wheel.Enable();
            actions.Add(wheel);
            var launch = new InputAction("Launch", InputActionType.Button, "<Keyboard>/enter");
            launch.AddBinding("<Keyboard>/numpadEnter");
            launch.AddBinding("<Keyboard>/space");
            launch.performed += _ => launchButton.Press();
            launch.Enable();
            actions.Add(launch);
        }

        private void Key(string name, string binding, string extra, Action act)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            action.AddBinding(extra);
            action.performed += _ => act();
            action.Enable();
            actions.Add(action);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < actions.Count; i++) actions[i].Dispose();
            actions.Clear();
            ReleaseTexture();
        }

        private void ReleaseTexture()
        {
            hardwareCamera.targetTexture = null;
            if (!texture) return;
            texture.Release();
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
            texture = null;
        }
    }
}
