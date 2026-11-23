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
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// The hangar: its console world, the composite image, and the choices on its screens.
    /// The left pair shows the ship; the right pair shows the weapon and the ordnance. The
    /// hologram projector is its part at the lens. An accepted Launch locks the keys and the
    /// pointer, presses the button and powers the screens down before the caller is handed the
    /// pending loadout, once. Each frame the console applies its light profile and hands its painted
    /// parts the light of its screens, hologram, Launch and overheads, which their shader sums per pixel.
    /// The left status lamps stand for the offered ships and the right ones for the three screens:
    /// white for the selected one, green for one the player can pick, dark for a locked ship.
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
        private static readonly int LightResponseId = Shader.PropertyToID("_LightResponse");
        private static readonly int EmissionTintId = Shader.PropertyToID("_EmissionTint");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int HangarLightPositionsId = Shader.PropertyToID("_HangarLightPositions");
        private static readonly int HangarLightColorsId = Shader.PropertyToID("_HangarLightColors");
        private static readonly int HangarLightCountId = Shader.PropertyToID("_HangarLightCount");
        private const int LightSlots = 8;
        private const float HologramLightRange = 12f;
        private const float LaunchLightRange = 6f;
        private const float OverheadLightRange = 14f;
        private const float LaunchDim = .1f;
        private const float LaunchBlinkPerSecond = 6f;
        private const int SlotCount = 3;

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
        [SerializeField] private Vector3 hologramLightOffset;
        [SerializeField] private Vector3 launchLightOffset;
        [SerializeField] private HangarGlow[] lampGlows;
        [SerializeField] private MeshRenderer[] launchLamps;
        [SerializeField] private HangarGlow[] launchLampGlows;
        [SerializeField] private MeshRenderer[] shipLamps;
        [SerializeField] private HangarGlow[] shipLampGlows;
        [SerializeField] private MeshRenderer[] slotLamps;
        [SerializeField] private HangarGlow[] slotLampGlows;
        [SerializeField, FormerlySerializedAs("downlightGlows")] private HangarGlow[] overheadGlows;
        [SerializeField] private HangarGlow lensGlow;
        [SerializeField] private Vector3 knobAxis;
        [SerializeField] private float knobDetentDegrees = 40f;

        private RenderTexture texture;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<InputAction> actions = new();
        private MaterialPropertyBlock legendBlock;
        private MaterialPropertyBlock lightBlock;
        private readonly Vector4[] hangarLightPositions = new Vector4[LightSlots];
        private readonly Vector4[] hangarLightColors = new Vector4[LightSlots];
        private Color legendLit;
        private Color launchArmed;
        private HangarConsoleRaycaster raycaster;
        private HangarSelection selection;
        private Action onLaunch;
        private bool launching;
        private float launchPressedAt;

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
            if (!Wired(shipLamps, shipLampGlows) || !Wired(slotLamps, slotLampGlows) || slotLamps.Length != SlotCount)
                throw new InvalidOperationException(
                    $"HangarConsole needs ship lamps with glows, and {SlotCount} slot lamps with glows.");
            if (!hardwareCamera || !mainScreenGlass || !detailScreenGlass || !primaryScreenGlass ||
                !ordnanceScreenGlass || !selectorKnob || !knob || !previousButton || !nextButton ||
                !launchButton || !primaryPreviousButton || !primaryNextButton || !ordnancePreviousButton ||
                !ordnanceNextButton || !launchLegend || !projectorLens || !compositeImage || !projector ||
                !mainDisplay || !detailDisplay || !primaryDisplay || !ordnanceDisplay || !mainView ||
                !detailView || !primaryView || !ordnanceView || !displayProfile || !lightProfile ||
                paintedParts == null || paintedParts.Length == 0 || !launchSurface ||
                lampGlows == null || !Wired(launchLamps, launchLampGlows) || overheadGlows == null || !lensGlow.card)
                throw new InvalidOperationException(
                    "HangarConsole needs its camera, four screen glasses, controls, weapon keys, knob, launch legend, displays, display profile, light profile, projector lens, composite image, projector, painted parts, launch surface, lamp glows, launch lamps, launch lamp glows, overhead glows and lens glow.");
            raycaster = GetComponent<HangarConsoleRaycaster>();
            if (!raycaster)
                throw new InvalidOperationException("HangarConsole needs its raycaster on the same object.");
            knob.Initialize(knobAxis);
            legendBlock = new MaterialPropertyBlock();
            lightBlock = new MaterialPropertyBlock();
            legendLit = launchLegend.sharedMaterial.GetColor(ConstantEmissionId);
            launchArmed = launchLamps[0].sharedMaterial.GetColor(ConstantEmissionId);
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
            if (selection.Ships.Count > shipLamps.Length)
                throw new InvalidOperationException(
                    $"The hangar offers {selection.Ships.Count} ships but the console has {shipLamps.Length} ship lamps.");
            projector.Open(offer ? offer.ships : Array.Empty<Ship>(), loadout.Ship);
            selection.ShipChanged += projector.Select;
            mainView.Bind(selection);
            detailView.Bind(selection);
            BindWeaponScreen(primaryView, HangarSlot.Weapon, primaryPreviousButton, primaryNextButton);
            BindWeaponScreen(ordnanceView, HangarSlot.Ordnance, ordnancePreviousButton, ordnanceNextButton);
            mainView.ArrowPressed += step => (step < 0 ? previousButton : nextButton).Press();
            previousButton.Pressed += () => selection.Step(HangarSlot.Ship, -1);
            nextButton.Pressed += () => selection.Step(HangarSlot.Ship, 1);
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
            launchPressedAt = Time.unscaledTime;
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
            var count = 0;
            count = AddLight(count, mainDisplay.Light);
            count = AddLight(count, detailDisplay.Light);
            count = AddLight(count, primaryDisplay.Light);
            count = AddLight(count, ordnanceDisplay.Light);
            count = AddLight(count, new HangarLight(
                projectorLens.transform.position + hologramLightOffset,
                lightProfile.HologramSpillColor.linear * (lightProfile.HologramSpill * projector.Level),
                HologramLightRange));
            var launchScale = LaunchLegendLit ? 1f : LaunchDim;
            count = AddLight(count, new HangarLight(
                launchButton.RestPosition + launchLightOffset,
                lightProfile.LaunchSpillColor.linear * (lightProfile.LaunchSpill * launchScale),
                LaunchLightRange));
            for (var i = 0; i < overheadGlows.Length; i++)
            {
                var glow = overheadGlows[i];
                count = AddLight(count, new HangarLight(glow.card.transform.position,
                    glow.color.linear * lightProfile.OverheadLight, OverheadLightRange));
            }
            for (var i = count; i < LightSlots; i++)
            {
                hangarLightPositions[i] = Vector4.zero;
                hangarLightColors[i] = Vector4.zero;
            }

            var emission = lightProfile.EmissionStrength;
            for (var i = 0; i < paintedParts.Length; i++)
            {
                paintedParts[i].GetPropertyBlock(lightBlock);
                lightBlock.SetColor(StarlightId, lightProfile.Ambient);
                lightBlock.SetFloat(EmissionStrengthId, emission);
                lightBlock.SetFloat(LightResponseId, lightProfile.LightResponse);
                lightBlock.SetVectorArray(HangarLightPositionsId, hangarLightPositions);
                lightBlock.SetVectorArray(HangarLightColorsId, hangarLightColors);
                lightBlock.SetFloat(HangarLightCountId, count);
                paintedParts[i].SetPropertyBlock(lightBlock);
            }

            projectorLens.GetPropertyBlock(lightBlock);
            lightBlock.SetColor(EmissionTintId, lightProfile.LensTint);
            projectorLens.SetPropertyBlock(lightBlock);
            launchSurface.GetPropertyBlock(lightBlock);
            lightBlock.SetColor(StarlightId, lightProfile.LaunchGlow);
            lightBlock.SetFloat(EmissionStrengthId, emission);
            launchSurface.SetPropertyBlock(lightBlock);

            for (var i = 0; i < lampGlows.Length; i++)
            {
                var glow = lampGlows[i];
                var span = glow.size + Vector2.one * lightProfile.LampGlowSize;
                ApplyGlow(glow, glow.color * lightProfile.LampGlow, new Vector3(span.x, span.y, 1f));
            }
            var launchLampColor = launchArmed;
            if (launching)
            {
                var cycle = (Time.unscaledTime - launchPressedAt) * LaunchBlinkPerSecond;
                launchLampColor = cycle - Mathf.Floor(cycle) < .5f ? lightProfile.LaunchPressedColor : Color.black;
            }
            for (var i = 0; i < launchLamps.Length; i++)
                ApplyLamp(launchLamps[i], launchLampGlows[i], launchLampColor);
            for (var i = 0; i < shipLamps.Length; i++)
            {
                var ship = selection != null && i < selection.Ships.Count ? selection.Ships[i] : null;
                ApplyLamp(shipLamps[i], shipLampGlows[i], ShipLampColor(ship));
            }
            for (var i = 0; i < slotLamps.Length; i++)
            {
                var color = selection == null ? Color.black
                    : (int)selection.Selected == i ? lightProfile.LampSelectedColor : lightProfile.LampAvailableColor;
                ApplyLamp(slotLamps[i], slotLampGlows[i], color);
            }
            for (var i = 0; i < overheadGlows.Length; i++)
            {
                var glow = overheadGlows[i];
                var span = glow.size + Vector2.one * lightProfile.OverheadGlowSize;
                ApplyGlow(glow, glow.color * lightProfile.OverheadGlow, new Vector3(span.x, span.y, 1f));
            }
            ApplyGlow(lensGlow, projector.BeamColor * lightProfile.LensGlow * projector.Level,
                new Vector3(lightProfile.LensGlowSize, lightProfile.LensGlowSize * .45f, 1f));
        }

        private int AddLight(int index, HangarLight light)
        {
            hangarLightPositions[index] = new Vector4(
                light.Position.x, light.Position.y, light.Position.z, light.Range);
            hangarLightColors[index] = new Vector4(light.Color.r, light.Color.g, light.Color.b, 0f);
            return index + 1;
        }

        private void ApplyLamp(MeshRenderer lamp, HangarGlow glow, Color color)
        {
            lamp.GetPropertyBlock(lightBlock);
            lightBlock.SetColor(ConstantEmissionId, color);
            lamp.SetPropertyBlock(lightBlock);
            var span = glow.size + Vector2.one * lightProfile.LampGlowSize;
            ApplyGlow(glow, color * lightProfile.LampGlow, new Vector3(span.x, span.y, 1f));
        }

        // A selected ship lights white even when locked; the screens already say it is locked.
        private Color ShipLampColor(Ship ship)
        {
            if (!ship) return Color.black;
            if (ship == selection.Ship) return lightProfile.LampSelectedColor;
            return selection.IsLocked(ship) ? Color.black : lightProfile.LampAvailableColor;
        }

        private static bool Wired(MeshRenderer[] lamps, HangarGlow[] glows)
        {
            if (lamps == null || glows == null || lamps.Length == 0 || lamps.Length != glows.Length) return false;
            for (var i = 0; i < lamps.Length; i++)
                if (!lamps[i] || !glows[i].card) return false;
            return true;
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
