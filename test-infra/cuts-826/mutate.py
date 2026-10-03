"""Apply named single-line production mutations in a slot; each must match exactly once."""
import sys, pathlib

ROOT = pathlib.Path("D:/amind/git/agent-3/src/Asteroids3D/Assets/Scripts")

# name -> (file, old, new, occurrence index among matches or None for must-be-unique)
M = {
    "aic115": ("AI/AICommander.cs",
               "            engageSecondary = false;\n            boost = false;\n            context = new AIContext",
               "            engageSecondary = false;\n            context = new AIContext"),
    "aic145": ("AI/AICommander.cs",
               "                engageSecondary = true;\n                boost = false;\n                return;",
               "                engageSecondary = true;\n                return;"),
    "session99": ("Substrate/Sessions/Session.cs",
                  "            holder.SetActive(false);\n", ""),
    "marker34": ("UI/MinimapObjectiveMarker.cs",
                 "            objectives.OnSpineTargetChanged += OnSpineTargetChanged;\n", ""),
    "statusbar": ("UI/PlayerState/StatusBarUI.cs",
                  "        private void OnEnable()\n        {\n",
                  "        private void OnEnable()\n        {\n            if (fill) fill.fillAmount = 0f;\n"),
    "statusbar2": ("UI/PlayerState/StatusBarUI.cs",
                   "            if (source == null) return;\n            Subscribe();\n            Refresh();",
                   "            if (source == null) { if (fill) fill.fillAmount = 0f; return; }\n            Subscribe();\n            Refresh();"),
    # --- signature mutations: each should fail to compile at a real call site ---
    "sig_isector_event": ("Substrate/Sectors/ISector.cs",
                          "event Action<SectorResult> OnSectorComplete;",
                          "event Action<SectorResult> OnSectorCompleted;"),
    "sig_isector_init": ("Substrate/Sectors/ISector.cs",
                         "void Initialize(IUnitService units, IObjectiveService objectives, bool presentationEnabled,",
                         "void Initialize(IObjectiveService objectives, IUnitService units, bool presentationEnabled,"),
    "sig_isector_setup": ("Substrate/Sectors/ISector.cs",
                          "IEnumerator Setup();", "IEnumerable Setup();"),
    "sig_isector_teardown": ("Substrate/Sectors/ISector.cs",
                             "IEnumerator Teardown();", "IEnumerable Teardown();"),
    "sig_settings_locale": ("Substrate/Sectors/SectorSettings.cs",
                            "public SceneReference Locale => locale;",
                            "public SceneReference LocaleX => locale;"),
    "sig_session_compose": ("Substrate/Sessions/Session.cs",
                            "public IEnumerator Compose()", "public IEnumerator Compose(int step)"),
    "sig_session_load_swap": ("Substrate/Sessions/Session.cs",
                              "public IEnumerator LoadSector(Ship hero = null, Action<SectorResult> onSectorComplete = null)",
                              "public IEnumerator LoadSector(Action<SectorResult> onSectorComplete = null, Ship hero = null)"),
    "sig_session_unload": ("Substrate/Sessions/Session.cs",
                           "public IEnumerator UnloadSector()", "public IEnumerator UnloadSector(int step)"),
    "sig_session_units": ("Substrate/Sessions/Session.cs",
                          "public IUnitService Units { get; private set; }",
                          "public IUnitService UnitsX { get; private set; }"),
    "sig_session_projectiles": ("Substrate/Sessions/Session.cs",
                                "public IProjectileService Projectiles { get; private set; }",
                                "public IProjectileService ProjectilesX { get; private set; }"),
    "sig_session_objectives": ("Substrate/Sessions/Session.cs",
                               "public IObjectiveService Objectives { get; private set; }",
                               "public IObjectiveService ObjectivesX { get; private set; }"),
    "sig_session_active": ("Substrate/Sessions/Session.cs",
                           "public Sector ActiveSector { get; private set; }",
                           "public Sector ActiveSectorX { get; private set; }"),
    "sig_session_frame": ("Substrate/Sessions/Session.cs",
                          "public SessionFrame Frame { get; }", "public SessionFrame FrameX { get; }"),
    "sig_session_ctor": ("Substrate/Sessions/Session.cs",
                         "public Session(SessionProfile profile, Transform root, UnitService units, ObjectiveService objectives)",
                         "public Session(SessionProfile profile, Transform root, ObjectiveService objectives, UnitService units)"),
    "sig_rig_build": ("Game/PlayerRig.cs",
                      "SessionFrame frame, Action<ShipId, DamageInfo> onPlayerDeath)",
                      "SessionFrame frame, Action<ShipId, DamageInfo> onPlayerDeath, int extra)"),
    # --- enforced only at test call sites ---
    "sig_session_load_required": ("Substrate/Sessions/Session.cs",
                                  "public IEnumerator LoadSector(Ship hero = null, Action<SectorResult> onSectorComplete = null)",
                                  "public IEnumerator LoadSector(Ship hero, Action<SectorResult> onSectorComplete)"),
    "sig_session_teardown": ("Substrate/Sessions/Session.cs",
                             "public IEnumerator Teardown()", "public IEnumerator Teardown(int step)"),
    # --- compiles; caught behaviourally ---
    "compose_ienumerable": ("Substrate/Sessions/Session.cs",
                            "public IEnumerator Compose()", "public IEnumerable Compose()"),
    # --- GamePlane facade forwards ---
    "f_rot": ("Substrate/GamePlane.cs",
              "public static Quaternion Rotation => Canonical.Rotation;",
              "public static Quaternion Rotation => Canonical.Rotation * Quaternion.Euler(0f, 0f, 90f);"),
    "f_proj": ("Substrate/GamePlane.cs",
               "ProjectOntoPlane(Vector3 world) => Canonical.ProjectOntoPlane(world);",
               "ProjectOntoPlane(Vector3 world) => world;"),
    "f_w2p": ("Substrate/GamePlane.cs",
              "WorldPointToPlane(Vector3 worldPt) => Canonical.WorldPointToPlane(worldPt);",
              "WorldPointToPlane(Vector3 worldPt) => -Canonical.WorldPointToPlane(worldPt);"),
    "f_wd2p": ("Substrate/GamePlane.cs",
               "WorldDirToPlane(Vector3 worldDir) => Canonical.WorldDirToPlane(worldDir);",
               "WorldDirToPlane(Vector3 worldDir) => -Canonical.WorldDirToPlane(worldDir);"),
    "f_p2w": ("Substrate/GamePlane.cs",
              "PlanePointToWorld(Vector2 planePt) => Canonical.PlanePointToWorld(planePt);",
              "PlanePointToWorld(Vector2 planePt) => Canonical.PlanePointToWorld(-planePt);"),
    "f_pd2w": ("Substrate/GamePlane.cs",
               "PlaneDirToWorld(Vector2 planeDir) => Canonical.PlaneDirToWorld(planeDir);",
               "PlaneDirToWorld(Vector2 planeDir) => Canonical.PlaneDirToWorld(-planeDir);"),
    "sector_abstract": ("Substrate/Sectors/Sector.cs",
                        "    public class Sector : MonoBehaviour, ISector",
                        "    public abstract class Sector : MonoBehaviour, ISector"),
    "pir28": ("Game/Player/PlayerInputReader.cs",
              "screenToGamePlane = projector ?? throw",
              "screenToGamePlane ??= projector ?? throw"),
    "sector115": ("Substrate/Sectors/Sector.cs",
                  "            OnSectorComplete?.Invoke(result);\n", ""),
    "gp51": ("Substrate/GamePlane.cs",
             "Rotation = PlanePose(normal, forward);",
             "Rotation = PlanePose(forward, normal);"),
    "bd_secondary": ("AI/Strategy/BrainDecision.cs",
                     "bool engageSecondary = false, bool boost = false)",
                     "bool engageSecondary = true, bool boost = false)"),
}

for name in sys.argv[1:]:
    rel, old, new = M[name]
    p = ROOT / rel
    raw = p.read_bytes()
    crlf = b"\r\n" in raw
    text = raw.decode("utf-8-sig") if raw.startswith(b"\xef\xbb\xbf") else raw.decode("utf-8")
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = text.replace("\r\n", "\n")
    n = text.count(old)
    if n != 1:
        sys.exit(f"{name}: expected exactly 1 match in {rel}, found {n}")
    text = text.replace(old, new)
    if crlf:
        text = text.replace("\n", "\r\n")
    p.write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
    print(f"mutated {name}: {rel}")
