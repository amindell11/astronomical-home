using System;
using System.Collections.Generic;
using Substrate.Sectors;
using Substrate.Services.Locales;
using UnityEngine;

namespace Substrate.Sessions
{
    [CreateAssetMenu(fileName = "SectorRun", menuName = "Game/Sector Run")]
    public sealed class SectorRun : ScriptableObject
    {
        [Tooltip("The sectors to load, in order; the host decides when to move to the next.")]
        [SerializeField] private SectorEntry[] sectors = Array.Empty<SectorEntry>();

        public IReadOnlyList<SectorEntry> Sectors => sectors;

        public static SectorRun Create(params SectorEntry[] rows)
        {
            var run = CreateInstance<SectorRun>();
            run.sectors = rows ?? throw new ArgumentNullException(nameof(rows));
            return run;
        }
    }

    [Serializable]
    public class SectorEntry
    {
        public Sector prefab;

        [Tooltip("Locale prefab supplying this sector's sky layers, lighting and audio. " +
                 "Required for any sector a player sees; only headless rows leave it unassigned.")]
        public LocaleSky locale;
    }
}
