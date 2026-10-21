using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ships
{
    public sealed class ShipRoleImportPostprocessor : AssetPostprocessor
    {
        private void OnPostprocessModel(GameObject root)
        {
            if (!ShipRoleNames.TryGetShipName(assetPath, out var shipName)) return;
            if (ShipLegacyList.HullModels.Contains(shipName)) return;
            foreach (var finding in ShipRoleNames.Validate(root.transform.Cast<Transform>().Select(t => t.name)))
                context.LogImportError($"{assetPath}: {finding}", root);
        }
    }
}
