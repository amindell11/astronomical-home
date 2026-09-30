using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ships;
using Ships.Loadout;
using Substrate.Sectors;
using Object = UnityEngine.Object;

namespace Balance
{
    /// <summary>
    /// Hashes the balance numbers a ship, or a whole run, is built from. The walk starts at an object,
    /// reads its <see cref="StatAttribute"/> fields and follows the marked references, so it knows no
    /// weapon's or module's fields and works on a prefab asset, where Awake never ran. Every number
    /// becomes one <c>asset/Type.field=value</c> line and every reference one line naming what it
    /// points at; the sorted, de-duplicated lines are the text both hashes are taken over, so neither
    /// depends on list order. Nothing about values is cached: an inspector edit gives a new hash.
    /// Why marks, and what each hash covers: https://github.com/amindell11/astronomical-home/issues/772#issuecomment-5905846586
    /// </summary>
    public static class StatHash
    {
        private const int HashBytes = 8;
        private const string CloneSuffix = "(Clone)";
        private const BindingFlags DeclaredInstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, FieldInfo[]> statFieldsByType = new();

        /// <summary>The loadout stat hash; the same on a spawned instance and on its prefab asset.</summary>
        public static string OfLoadout(Ship ship) => Digest(LoadoutLines(ship));

        /// <summary>The stat fingerprint: the hangar's offer, the sector's spawners and the kill refill.</summary>
        public static string OfSetting(LoadoutConfig hangarOffer, Sector sectorPrefab, float killHullRestore) =>
            Digest(SettingLines(hangarOffer, sectorPrefab, killHullRestore));

        internal static SortedSet<string> LoadoutLines(Ship ship)
        {
            var lines = NewLines();
            Visit(ship, lines, new HashSet<Object>());
            return lines;
        }

        internal static SortedSet<string> SettingLines(LoadoutConfig hangarOffer, Sector sectorPrefab,
            float killHullRestore)
        {
            var lines = NewLines();
            var visited = new HashSet<Object>();
            Visit(hangarOffer, lines, visited);
            Visit(sectorPrefab, lines, visited);
            lines.Add($"setting/killHullRestore={Format(killHullRestore)}");
            return lines;
        }

        // Ordinal, so the sort never depends on the machine's culture.
        private static SortedSet<string> NewLines() => new(StringComparer.Ordinal);

        private static void Visit(Object source, ISet<string> lines, ISet<Object> visited)
        {
            if (!source || !visited.Add(source)) return;
            ReadStatFields(source, AssetName(source), lines, visited);
        }

        private static void ReadStatFields(object owner, string asset, ISet<string> lines, ISet<Object> visited)
        {
            var type = owner.GetType();
            foreach (var field in StatFields(type))
                Read($"{asset}/{type.Name}.{field.Name}", field.GetValue(owner), asset, lines, visited);
        }

        private static void Read(string key, object value, string asset, ISet<string> lines, ISet<Object> visited)
        {
            switch (value)
            {
                case null:
                    return;
                case Object reference:
                    if (!reference) return;
                    lines.Add($"{key}={AssetName(reference)}");
                    Visit(reference, lines, visited);
                    return;
                case IEnumerable items:
                    foreach (var item in items)
                        Read(key, item, asset, lines, visited);
                    return;
            }

            var number = Format(value);
            if (number != null)
            {
                lines.Add($"{key}={number}");
                return;
            }

            if (StatFields(value.GetType()).Length == 0)
                throw new NotSupportedException(
                    $"[Stat] on {key}: {value.GetType().Name} is not a number, a reference, a list, or a type with [Stat] fields.");
            ReadStatFields(value, asset, lines, visited);
        }

        private static string Format(object value) => value switch
        {
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            Enum choice => choice.ToString(),
            _ => null,
        };

        // Instantiate appends the clone suffix; dropping it lets an instance and its prefab hash alike.
        private static string AssetName(Object source) => source.name.Replace(CloneSuffix, string.Empty);

        private static FieldInfo[] StatFields(Type type)
        {
            if (statFieldsByType.TryGetValue(type, out var cached)) return cached;

            var found = new List<FieldInfo>();
            for (var declaring = type; declaring != null; declaring = declaring.BaseType)
                foreach (var field in declaring.GetFields(DeclaredInstanceFields))
                    if (field.IsDefined(typeof(StatAttribute), false))
                        found.Add(field);
            return statFieldsByType[type] = found.ToArray();
        }

        private static string Digest(IEnumerable<string> lines)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
            var hex = new StringBuilder(HashBytes * 2);
            for (var i = 0; i < HashBytes; i++)
                hex.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }
}
