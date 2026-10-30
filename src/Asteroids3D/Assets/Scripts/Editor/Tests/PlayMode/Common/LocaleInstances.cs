using System.Linq;
using Substrate.Services.Locales;
using UnityEngine;

namespace Tests.PlayMode.Common
{
    /// <summary>The live locale instances, each named for its prefab, and the one the locale service shows.</summary>
    public static class LocaleInstances
    {
        public static LocaleSky[] All() =>
            Object.FindObjectsByType<LocaleSky>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        public static string Shown() => All().Where(locale => locale.Shown).Select(locale => locale.name).SingleOrDefault();

        public static bool Loaded(string name) => All().Any(locale => locale.name == name);
    }
}
