var locale = UnityEditor.AssetDatabase.LoadAssetAtPath<Substrate.Services.Locales.LocaleSky>("Assets/Prefabs/Locales/CopperTurquoise.prefab");
if (!locale) return "no locale";
var ok = Game.Preview.PreviewHostBootstrap.EnterPlayMode(Game.Preview.PreviewEntry.Combat, 0, locale, true);
return "entering play: " + ok;
