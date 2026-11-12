using System;
using System.Collections.Generic;
using System.Linq;
using Game.Preview.Fixtures;
using Objectives;
using Ships;
using Ships.Command;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Preview.Sectors
{
    /// <summary>
    /// The Sectors window's pages, checks, shared values and wave-group lines,
    /// over a sector run, its preview fixtures and the sector layouts. It writes
    /// nothing. <see cref="Sector.CheckRow"/> supplies a page's failure message,
    /// and a shared value's users are the run sectors, fixtures and other layouts
    /// that reference the asset.
    /// </summary>
    internal sealed class SectorPages
    {
        private const int NamesShown = 3;

        private readonly SectorRun run;
        private readonly IReadOnlyList<PreviewFixture> fixtures;
        private readonly IReadOnlyList<Sector> layouts;
        private readonly List<Page> pages = new();
        private readonly Dictionary<int, HashSet<int>> componentRefs = new();
        private readonly Dictionary<int, ScriptableObject[]> feelAssets = new();
        private readonly Dictionary<string, HashSet<string>> dependencyPaths = new();

        public SectorPages(SectorRun run, IReadOnlyList<PreviewFixture> fixtures, IReadOnlyList<Sector> layouts)
        {
            if (!run) throw new ArgumentNullException(nameof(run));
            this.run = run;
            this.fixtures = fixtures;
            this.layouts = layouts;
            for (var i = 0; i < run.Sectors.Count; i++)
                pages.Add(new Page($"Sector {i + 1}", false, i));
            for (var i = 0; i < this.fixtures.Count; i++)
                pages.Add(new Page(FixtureName(this.fixtures[i]), true, i));
        }

        public IReadOnlyList<Page> List => pages;

        public static SectorPages Load()
        {
            var host = AssetDatabase.LoadAssetAtPath<GameHost>(PreviewHostBootstrap.GameHostPath);
            if (!host || host.sessionProfile == null || !host.sessionProfile.run) return null;
            var asset = AssetDatabase.LoadAssetAtPath<PreviewFixtures>(PreviewHostBootstrap.FixturesPath);
            var entries = asset ? asset.Entries : Array.Empty<PreviewFixture>();
            return new SectorPages(host.sessionProfile.run, entries, ProjectLayouts());
        }

        public IReadOnlyList<string> Checks(Page page)
        {
            if (!TryResolve(page, out var layout, out var row, out var failure)) return new[] { failure };
            if (!layout || row == null) return Array.Empty<string>();
            try
            {
                layout.CheckRow(row);
                return Array.Empty<string>();
            }
            catch (InvalidOperationException ex)
            {
                return new[] { ex.Message };
            }
        }

        public IReadOnlyList<SharedValue> Uses(Page page)
        {
            TryResolve(page, out var layout, out var row, out _);
            var list = new List<SharedValue>();
            var seen = new HashSet<int>();
            if (row != null)
            {
                AddAsset(list, seen, row.faction);
                AddAsset(list, seen, row.elite);
                AddAsset(list, seen, row.field);
            }
            if (layout)
            {
                foreach (var feel in Feels(layout))
                    AddAsset(list, seen, feel);
                AddNumbers(list, layout);
            }
            return list;
        }

        internal static string GroupSummary(WaveGroup group)
        {
            var ships = group.ships;
            if (ships == null || ships.Length == 0) return "empty group";
            var templates = new List<Ship>();
            var pilots = new List<List<string>>();
            foreach (var ship in ships)
            {
                var found = -1;
                for (var i = 0; i < templates.Count; i++)
                {
                    if (templates[i] != ship.template) continue;
                    found = i;
                    break;
                }
                if (found < 0)
                {
                    found = templates.Count;
                    templates.Add(ship.template);
                    pilots.Add(new List<string>());
                }
                pilots[found].Add(PilotName(ship.pilot));
            }
            var parts = new string[templates.Count];
            for (var i = 0; i < templates.Count; i++)
                parts[i] = FormatShips(TemplateName(templates[i]), pilots[i]);
            return string.Join(" + ", parts);
        }

        private static string FormatShips(string template, List<string> pilots) =>
            pilots.Count == 1
                ? $"{template} ({pilots[0]})"
                : $"{pilots.Count}\u00d7 {template}: {string.Join(", ", pilots)}";

        private static string TemplateName(Ship template) => template ? template.name : "none";

        private static string PilotName(Commander pilot)
        {
            if (!pilot) return "none";
            var name = pilot.name;
            const string suffix = "Pilot";
            return name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length
                ? name.Substring(0, name.Length - suffix.Length)
                : name;
        }

        private void AddAsset(List<SharedValue> list, HashSet<int> seen, Object asset)
        {
            if (!asset || !seen.Add(asset.GetInstanceID())) return;
            var value = new SharedValue(ObjectName(asset), Label(asset));
            value.AddWhole(asset);
            list.Add(value);
        }

        private void AddNumbers(List<SharedValue> list, Sector layout)
        {
            var usedBy = Label(layout);
            var director = layout.GetComponentInChildren<WaveDirector>(true);
            var spine = layout.GetComponentInChildren<PhaseSpineModule>(true);
            var lulls = new SharedValue("Lulls", usedBy);
            AddFields(lulls, director, "lullSeconds", "openingLullSeconds");
            AddFields(lulls, spine, "beaconDistance");
            AddZone(lulls, SpineZone(spine, "beaconZone"));
            if (lulls.Parts.Count > 0) list.Add(lulls);

            AddGroup(list, "Beacon lead and grace", usedBy, spine, "beaconLeadSeconds", "beaconGraceSeconds");

            var extraction = new SharedValue("Extraction", usedBy);
            AddFields(extraction, spine, "zoneDistance", "zoneClearance");
            AddZone(extraction, SpineZone(spine, "extractionZone"));
            if (extraction.Parts.Count > 0) list.Add(extraction);

            AddGroup(list, "Spawn rings", usedBy, director, "spawnRadius", "groupSpread");
        }

        private static ExtractionZone SpineZone(PhaseSpineModule spine, string field)
        {
            if (!spine) return null;
            using var serialized = new SerializedObject(spine);
            return serialized.FindProperty(field)?.objectReferenceValue as ExtractionZone;
        }

        private static void AddZone(SharedValue value, ExtractionZone zone)
        {
            if (!zone) return;
            AddFields(value, zone, "blockDistance", "departureHeading");
            AddFields(value, zone.GetComponent<SphereCollider>(), "m_Radius");
        }

        private static void AddGroup(List<SharedValue> list, string title, string usedBy, Object target,
            params string[] names)
        {
            var value = new SharedValue(title, usedBy);
            AddFields(value, target, names);
            if (value.Parts.Count > 0) list.Add(value);
        }

        private static void AddFields(SharedValue value, Object target, params string[] names)
        {
            var present = Present(target, names);
            if (present.Length > 0) value.Add(target, present);
        }

        private static string[] Present(Object target, string[] names)
        {
            if (!target) return Array.Empty<string>();
            using var so = new SerializedObject(target);
            var found = new List<string>(names.Length);
            foreach (var name in names)
                if (so.FindProperty(name) != null) found.Add(name);
            return found.ToArray();
        }

        private bool TryResolve(Page page, out Sector layout, out SectorEntry row, out string failure)
        {
            failure = null;
            layout = null;
            row = null;
            if (!page.Fixture)
            {
                row = run.Sectors[page.Index];
                layout = row == null ? null : row.prefab;
                return true;
            }
            var fixture = fixtures[page.Index];
            if (fixture == null) return true;
            layout = fixture.layout;
            try
            {
                row = fixture.Resolve(run);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                failure = ex.Message;
                return false;
            }
        }

        private string Label(Object asset)
        {
            var names = new List<string>();
            for (var i = 0; i < run.Sectors.Count; i++)
                if (SectorUses(run.Sectors[i], asset)) names.Add($"Sector {i + 1}");
            if (run.Sectors.Count > 0 && names.Count == run.Sectors.Count) return "all sectors";
            foreach (var fixture in fixtures)
                if (FixtureUses(fixture, asset)) AddName(names, FixtureName(fixture));
            foreach (var layout in layouts)
                if (LayoutUses(layout, asset)) AddName(names, ObjectName(layout));
            if (names.Count == 0) return "";
            if (names.Count <= NamesShown) return string.Join(", ", names);
            return string.Join(", ", names.GetRange(0, NamesShown)) + ", …";
        }

        private bool SectorUses(SectorEntry row, Object asset)
        {
            if (row == null || !asset) return false;
            if (row.prefab == asset || row.faction == asset || row.elite == asset || row.field == asset)
                return true;
            return LayoutUses(row.prefab, asset);
        }

        private bool FixtureUses(PreviewFixture fixture, Object asset)
        {
            if (fixture == null || !asset) return false;
            if (fixture.layout == asset) return true;
            SectorEntry row;
            try
            {
                row = fixture.Resolve(run);
            }
            catch (InvalidOperationException)
            {
                row = null;
            }
            if (row != null && (row.faction == asset || row.elite == asset || row.field == asset))
                return true;
            return LayoutUses(fixture.layout, asset);
        }

        private bool LayoutUses(Sector layout, Object asset)
        {
            if (!layout || !asset) return false;
            if (layout == asset) return true;
            return ComponentReferences(layout, asset) || DependsOn(layout, asset);
        }

        private bool ComponentReferences(Sector layout, Object asset)
        {
            Ensure(layout);
            return componentRefs[layout.GetInstanceID()].Contains(asset.GetInstanceID());
        }

        private ScriptableObject[] Feels(Sector layout)
        {
            Ensure(layout);
            return feelAssets[layout.GetInstanceID()];
        }

        private void Ensure(Sector layout)
        {
            var id = layout.GetInstanceID();
            if (componentRefs.ContainsKey(id)) return;
            var refs = new HashSet<int>();
            var feels = new List<ScriptableObject>();
            var seen = new HashSet<int>();
            foreach (var behaviour in layout.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour) continue;
                using var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                if (!property.Next(true)) continue;
                do
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var reference = property.objectReferenceValue;
                    if (!reference) continue;
                    refs.Add(reference.GetInstanceID());
                    var asset = reference as ScriptableObject;
                    if (!asset || !IsFeel(property.name, asset) || !seen.Add(asset.GetInstanceID())) continue;
                    feels.Add(asset);
                }
                while (property.Next(true));
            }
            componentRefs[id] = refs;
            feelAssets[id] = feels.ToArray();
        }

        private static bool IsFeel(string propertyName, ScriptableObject asset) =>
            propertyName == "feel" || asset.GetType().Name.Contains("Feel");

        private bool DependsOn(Sector layout, Object asset)
        {
            var layoutPath = AssetDatabase.GetAssetPath(layout);
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(layoutPath) || string.IsNullOrEmpty(assetPath)) return false;
            if (!dependencyPaths.TryGetValue(layoutPath, out var paths))
            {
                paths = new HashSet<string>(AssetDatabase.GetDependencies(layoutPath, true));
                dependencyPaths[layoutPath] = paths;
            }
            return paths.Contains(assetPath);
        }

        private static void AddName(List<string> names, string name)
        {
            if (!names.Contains(name)) names.Add(name);
        }

        private static string FixtureName(PreviewFixture fixture)
        {
            if (fixture == null || !fixture.layout || string.IsNullOrEmpty(fixture.layout.name)) return "unnamed";
            return fixture.layout.name;
        }

        private static string ObjectName(Object asset) =>
            asset && !string.IsNullOrEmpty(asset.name) ? asset.name : "unnamed";

        private static Sector[] ProjectLayouts() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sector>(path))
                .Where(sector => sector)
                .OrderBy(sector => sector.name, StringComparer.Ordinal)
                .ToArray();

        internal readonly struct Page
        {
            public Page(string name, bool fixture, int index)
            {
                Name = name;
                Fixture = fixture;
                Index = index;
            }

            public string Name { get; }
            public bool Fixture { get; }
            public int Index { get; }
        }

        internal sealed class SharedValue
        {
            private readonly List<Part> parts = new();

            public SharedValue(string title, string usedBy)
            {
                Title = title;
                UsedBy = usedBy;
            }

            public string Title { get; }
            public string UsedBy { get; }
            public Object Target => parts.Count == 0 ? null : parts[0].Target;
            public IReadOnlyList<Part> Parts => parts;

            public void AddWhole(Object target) => parts.Add(new Part(target, null));

            public void Add(Object target, string[] properties) => parts.Add(new Part(target, properties));
        }

        internal readonly struct Part
        {
            public Part(Object target, string[] properties)
            {
                Target = target;
                Properties = properties;
            }

            public Object Target { get; }
            public string[] Properties { get; }
        }
    }
}
