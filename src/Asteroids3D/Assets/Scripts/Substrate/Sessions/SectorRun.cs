using System;
using System.Collections.Generic;
using System.Linq;
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

        [Tooltip("Locales the Random rows draw from at each run's start, never repeating within a run. " +
                 "Locales named rows use are left out.")]
        [SerializeField] private LocaleSky[] localePool = Array.Empty<LocaleSky>();

        public IReadOnlyList<SectorEntry> Sectors => sectors;

        public IReadOnlyList<LocaleSky> LocalePool => localePool;

        public static SectorRun Create(params SectorEntry[] rows) => Create(Array.Empty<LocaleSky>(), rows);

        public static SectorRun Create(LocaleSky[] pool, params SectorEntry[] rows)
        {
            var run = CreateInstance<SectorRun>();
            run.sectors = rows ?? throw new ArgumentNullException(nameof(rows));
            run.localePool = pool ?? throw new ArgumentNullException(nameof(pool));
            return run;
        }

        /// <summary>One locale per row: named rows keep theirs; Random rows draw from the pool without repeats.</summary>
        public LocaleSky[] DrawLocales(System.Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var drawable = DrawableLocales();
            var drawn = new LocaleSky[sectors.Length];
            for (var i = 0; i < sectors.Length; i++)
            {
                if (!sectors[i].random)
                {
                    drawn[i] = sectors[i].locale;
                    continue;
                }
                var pick = random.Next(drawable.Count);
                drawn[i] = drawable[pick];
                drawable.RemoveAt(pick);
            }
            return drawn;
        }

        internal List<LocaleSky> DrawableLocales() =>
            localePool.Except(sectors.Where(row => !row.random).Select(row => row.locale)).ToList();
    }

    [Serializable]
    public class SectorEntry
    {
        public Sector prefab;

        [Tooltip("Locale prefab supplying this sector's sky layers, lighting and audio. Required for any " +
                 "sector a player sees unless the row is Random; only headless rows leave it unassigned.")]
        public LocaleSky locale;

        [Tooltip("Draw this sector's locale from the run's pool once per run instead of naming one.")]
        public bool random;
    }
}
