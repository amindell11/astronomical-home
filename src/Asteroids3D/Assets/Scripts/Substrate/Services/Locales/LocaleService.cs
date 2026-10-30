using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Locale switching for one session, held privately by it rather than exposed beside its services: it
    /// instantiates a sector's locale prefab under the session's locale root before the sector's content builds,
    /// shows the idle locale (the one shown while no sector is loaded) whenever the sector unloads, and restores the
    /// boot scene's lighting, captured at construction, when it shows none. A sector naming the shown locale keeps its
    /// instance. A travel backdrop holds both locales across the transfer and destroys the one no longer shown when
    /// released. The session skips these steps headless.
    /// </summary>
    public class LocaleService
    {
        private readonly Transform root;
        private readonly LocaleSky idleLocale;
        private readonly LocaleLighting bootLighting = LocaleLighting.Capture();
        private readonly SphericalHarmonicsL2 bootProbe = RenderSettings.ambientProbe;
        private LocaleSky shownLocale;
        private LocaleSky shown;
        private LocaleSky retained;
        private LocaleSky backdropDestination;
        private LocaleBackdropPair backdrop;

        public LocaleService(Transform root, LocaleSky idleLocale = null)
        {
            this.root = root ? root : throw new ArgumentNullException(nameof(root));
            this.idleLocale = idleLocale;
        }

        public LocaleBackdropPair PrepareBackdrop(LocaleSky destination, Transform parent)
        {
            if (!destination) throw new ArgumentNullException(nameof(destination));
            if (backdrop) throw new InvalidOperationException("A locale backdrop is already held.");
            if (!shown) throw new InvalidOperationException("A backdrop requires a shown departure locale.");
            var to = shown;
            if (destination != shownLocale)
            {
                to = retained = Instantiate(destination);
                to.SetVisible(false);
            }
            var pairRoot = new GameObject("Locale backdrop pair");
            pairRoot.transform.SetParent(parent, false);
            backdrop = pairRoot.AddComponent<LocaleBackdropPair>();
            backdrop.Initialize(shown, to, Release);
            backdropDestination = destination;
            return backdrop;
        }

        /// <summary>An unassigned locale keeps the shown one.</summary>
        public void Show(LocaleSky locale)
        {
            if (!locale || locale == shownLocale) return;
            if (backdrop && locale != backdropDestination)
                throw new InvalidOperationException("A held travel backdrop can only transfer to its destination.");

            if (backdrop)
            {
                (shown, retained) = (retained, shown);
                retained.SetVisible(false);
            }
            else
            {
                if (shown) Discard(shown);
                shown = Instantiate(locale);
            }
            shownLocale = locale;
            shown.ApplyLighting();
            shown.SetVisible(true);
        }

        /// <summary>With no idle locale configured, shows none.</summary>
        public void ShowIdle()
        {
            if (idleLocale) Show(idleLocale);
            else Clear();
        }

        /// <summary>Destroy every locale instance and restore the boot scene's lighting.</summary>
        public void Clear()
        {
            if (shown) Discard(shown);
            if (retained) Object.Destroy(retained.gameObject);
            shown = retained = shownLocale = null;
            bootLighting.Apply();
            RenderSettings.ambientProbe = bootProbe;
        }

        private LocaleSky Instantiate(LocaleSky locale)
        {
            var instance = Object.Instantiate(locale, root);
            instance.name = locale.name;
            return instance;
        }

        // Destroy waits for the frame's end; hidden first, the instance lights nothing meanwhile.
        private static void Discard(LocaleSky instance)
        {
            instance.SetVisible(false);
            Object.Destroy(instance.gameObject);
        }

        private void Release()
        {
            if (retained) Object.Destroy(retained.gameObject);
            retained = backdropDestination = null;
            backdrop = null;
        }
    }
}
