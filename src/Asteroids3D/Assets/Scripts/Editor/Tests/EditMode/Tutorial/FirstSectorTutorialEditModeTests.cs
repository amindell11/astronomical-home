using Game.Tutorial;
using NUnit.Framework;
using Substrate.Sectors.Elements.Stations;

namespace Tests.EditMode.Tutorial
{
    [Category("UI")]
    public class FirstSectorTutorialEditModeTests
    {
        [Test]
        public void StationsText_NamesOnlyTheKindsTheSectorHas()
        {
            var text = FirstSectorTutorial.StationsText(new[] { StationKind.Mod, StationKind.Repair });
            StringAssert.Contains("MOD ADDS AN UPGRADE AND REPAIR FIXES YOUR HULL.", text);
            StringAssert.DoesNotContain("ARMORY", text);
        }

        [Test]
        public void StationsText_ListsThreeKindsWithCommas()
        {
            var text = FirstSectorTutorial.StationsText(new[] { StationKind.Repair, StationKind.Mod, StationKind.Armory });
            StringAssert.Contains("REPAIR FIXES YOUR HULL, MOD ADDS AN UPGRADE AND ARMORY SWAPS YOUR WEAPON.", text);
        }

        [Test]
        public void StationsText_OneKind_StandsAlone()
        {
            var text = FirstSectorTutorial.StationsText(new[] { StationKind.Armory });
            StringAssert.Contains("MAP. ARMORY SWAPS YOUR WEAPON. FLY", text);
        }
    }
}
