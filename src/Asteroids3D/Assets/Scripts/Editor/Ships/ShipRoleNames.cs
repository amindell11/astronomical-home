using System.Collections.Generic;
using System.Linq;

namespace Ships
{
    /// <summary>
    /// The role grammar of a ship hull model: the node names <c>ship_export</c> writes under the FBX
    /// root, mirrored here so the Unity import can refuse a file the exporter did not shape.
    /// </summary>
    public static class ShipRoleNames
    {
        public const string Hull = "Hull";
        public static readonly string[] Known = { Hull, "Canopy", "Cores", "Ink", "Collider", "Sockets" };

        public static bool TryGetShipName(string assetPath, out string shipName)
        {
            shipName = null;
            const string folder = "Assets/Visuals/Ships/";
            if (!assetPath.StartsWith(folder) || !assetPath.EndsWith(".fbx")) return false;
            var parts = assetPath.Substring(folder.Length).Split('/');
            if (parts.Length != 2 || parts[1] != parts[0] + ".fbx") return false;
            shipName = parts[0];
            return true;
        }

        public static List<string> Validate(IEnumerable<string> roleNodeNames)
        {
            var names = roleNodeNames.ToList();
            var findings = new List<string>();
            if (!names.Contains(Hull)) findings.Add($"missing role node '{Hull}'");
            foreach (var name in names.Where(n => !Known.Contains(n)).Distinct())
                findings.Add($"unknown role node '{name}' (known: {string.Join(", ", Known)})");
            foreach (var name in names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key))
                findings.Add($"duplicate role node '{name}'");
            return findings;
        }
    }
}
