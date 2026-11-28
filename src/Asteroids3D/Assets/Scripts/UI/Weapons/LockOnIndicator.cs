using Combat.Targeting;
using UI.PlayerState.Alerts;
using UI.Screens.Windows;
using UnityEngine;
using UnityEngine.UI;
using Substrate;

namespace UI.Weapons
{
    /// <summary>
    /// World-space UI element that displays lock-on progress over a target.
    /// It expects to live under a Canvas set to World Space and to have an Animator
    /// that exposes a float parameter named "lockProgress" (0–1) and a Trigger
    /// named "LockComplete" for the flash when locking finishes. HUD ownership keeps its
    /// visibility and appearance separate from the ship's world visuals.
    /// As the threat reticle (on the player's own ship) it bypasses the Animator and the sprite, and draws
    /// bold brackets in the alert ink that stay clear of the hull, pulse faster as the lock builds, and
    /// slam shut into a box on a hard flash when it completes.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LockOnIndicator : MonoBehaviour
    {
        public static readonly Color ThreatTint = WindowKit.Threat;

        private static readonly int LockProgress = Animator.StringToHash("lockProgress");
        private static readonly int LockComplete = Animator.StringToHash("LockComplete");

        // Scales of the prefab's 5-unit canvas, sized so the brackets never close onto the hull.
        private const float OpenScale = 1.1f;
        private const float LockedScale = .85f;
        private const float SlowPulseHz = 2f;
        private const float FastPulseHz = 9f;
        private const float PulseDepth = .12f;
        // The canopy adds ink as light, so full-strength alert ink washes out to white.
        private const float RestAlpha = .35f;
        private const float PeakAlpha = .7f;
        private const float FlashSeconds = .45f;
        private const float FlashScale = 1.6f;
        private const float FlashWhite = .75f;
        private const float BracketInset = .15f;
        private const float BracketArm = 1.5f;
        private const float BracketChamfer = .4f;
        private const float BracketStroke = .32f;
        private const float FlashStroke = .55f;

        [Tooltip("Animator driving reticle scale / flash. Will default to first child Animator if left unassigned.")]
        [SerializeField] private Animator animator;
        [SerializeField] private float verticalOffset = -5f;

        private CanvasGroup canvasGroup;
        private Graphic graphic;
        private AlertInk ink;
        private Color tint;
        private float stroke;
        private float flash;
        private LockChannel lockChannel;
        private bool subscribed;
        private Transform pose;
        private bool threat;
        private float progress;
        private bool locked;
        private float lockedAt;
        private float pulsePhase;

        public bool Visible => canvasGroup.alpha > 0f && pose && pose.gameObject.activeInHierarchy;

        public bool IsThreat => threat;

        internal Color Tint => tint;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            graphic = GetComponentInChildren<Graphic>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            ink = AlertInk.Create(transform, "Threat ink", DrawThreat);
            ink.enabled = false;
            Hide();
        }

        public void Initialize(LockChannel channel, Transform pose, bool threat)
        {
            Unsubscribe();
            lockChannel = channel;
            this.pose = pose;
            this.threat = threat;
            if (animator) animator.enabled = !threat;
            graphic.enabled = !threat;
            ink.enabled = threat;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (lockChannel != null) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Hide();
        }

        private void Subscribe()
        {
            if (subscribed || lockChannel == null) return;
            lockChannel.Progress += HandleLockProgress;
            lockChannel.Acquired += HandleLockAcquired;
            lockChannel.Released += HandleLockReleased;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || lockChannel == null) return;
            lockChannel.Progress -= HandleLockProgress;
            lockChannel.Acquired -= HandleLockAcquired;
            lockChannel.Released -= HandleLockReleased;
            subscribed = false;
        }

        private void HandleLockProgress(float progress)
        {
            UpdateProgress(progress);
        }

        private void HandleLockAcquired()
        {
            OnLockComplete();
        }

        private void HandleLockReleased()
        {
            Hide();
        }

        private void UpdateProgress(float progress)
        {
            canvasGroup.alpha = 1f;
            this.progress = progress;
            if (!threat && animator) animator.SetFloat(LockProgress, progress);
        }

        private void OnLockComplete()
        {
            canvasGroup.alpha = 1f;
            locked = true;
            lockedAt = Time.time;
            if (!threat && animator) animator.SetTrigger(LockComplete);
        }

        private void Hide()
        {
            canvasGroup.alpha = 0f;
            progress = 0f;
            locked = false;
        }

        private void LateUpdate()
        {
            if (!pose || !pose.gameObject.activeInHierarchy) Hide();
            if (!Visible) return;
            transform.rotation = GamePlane.Rotation;
            transform.position = pose.position + GamePlane.Normal * verticalOffset;
            if (threat) PresentThreat();
        }

        private void PresentThreat()
        {
            var build = locked ? 1f : progress;
            pulsePhase += Time.deltaTime * Mathf.Lerp(SlowPulseHz, FastPulseHz, build) * 2f * Mathf.PI;
            var pulse = .5f + .5f * Mathf.Sin(pulsePhase);
            var scale = Mathf.Lerp(OpenScale, LockedScale, build) * (1f + PulseDepth * pulse);
            tint = ThreatTint;
            tint.a = Mathf.Lerp(RestAlpha, PeakAlpha, pulse);
            stroke = BracketStroke;
            flash = locked ? Mathf.Clamp01(1f - (Time.time - lockedAt) / FlashSeconds) : 0f;
            if (flash > 0f)
            {
                scale = Mathf.Lerp(LockedScale, LockedScale * FlashScale, flash);
                tint = Color.Lerp(ThreatTint, Color.white, flash * FlashWhite);
                stroke = Mathf.Lerp(BracketStroke, FlashStroke, flash);
            }
            transform.localScale = Vector3.one * scale;
            ink.SetVerticesDirty();
        }

        private void DrawThreat(VertexHelper mesh, Rect area)
        {
            Bracket(area.min, 1f, 1f);
            Bracket(new Vector2(area.xMax, area.yMin), -1f, 1f);
            Bracket(area.max, -1f, -1f);
            Bracket(new Vector2(area.xMin, area.yMax), 1f, -1f);
            if (flash <= 0f) return;
            // The flash closes the brackets into a box.
            var edge = new Color(tint.r, tint.g, tint.b, flash);
            var reach = BracketInset + BracketArm;
            var low = area.min + Vector2.one * BracketInset;
            var high = area.max - Vector2.one * BracketInset;
            AlertInk.Stroke(mesh, new Vector2(area.xMin + reach, low.y), new Vector2(area.xMax - reach, low.y), stroke, edge);
            AlertInk.Stroke(mesh, new Vector2(area.xMin + reach, high.y), new Vector2(area.xMax - reach, high.y), stroke, edge);
            AlertInk.Stroke(mesh, new Vector2(low.x, area.yMin + reach), new Vector2(low.x, area.yMax - reach), stroke, edge);
            AlertInk.Stroke(mesh, new Vector2(high.x, area.yMin + reach), new Vector2(high.x, area.yMax - reach), stroke, edge);

            void Bracket(Vector2 corner, float x, float y)
            {
                var arm = corner + new Vector2(x * BracketInset, y * (BracketInset + BracketArm));
                var bevelStart = corner + new Vector2(x * BracketInset, y * (BracketInset + BracketChamfer));
                var bevelEnd = corner + new Vector2(x * (BracketInset + BracketChamfer), y * BracketInset);
                var foot = corner + new Vector2(x * (BracketInset + BracketArm), y * BracketInset);
                AlertInk.Stroke(mesh, arm, bevelStart, stroke, tint);
                AlertInk.Stroke(mesh, bevelStart, bevelEnd, stroke, tint);
                AlertInk.Stroke(mesh, bevelEnd, foot, stroke, tint);
            }
        }
    }
}
