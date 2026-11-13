using System;
using System.Collections.Generic;
using NUnit.Framework;
using Substrate.Sectors.Elements.Stations;
using UnityEngine;

namespace Tests.EditMode.Sectors
{
    /// <summary>Each station kind has its own find colour.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class FindColoursEditModeTests
    {
        [Test]
        public void EachKind_HasItsOwnColour()
        {
            var colours = new List<Color>();
            foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
                colours.Add(FindColours.Of(kind));
            CollectionAssert.AllItemsAreUnique(colours);
        }

        [Test]
        public void TheRuling_ModIsGold_RepairIsTeal_ArmoryIsPeriwinkle()
        {
            var mod = FindColours.Of(StationKind.Mod);
            var repair = FindColours.Of(StationKind.Repair);
            var armory = FindColours.Of(StationKind.Armory);
            Assert.Greater(mod.r, mod.b);
            Assert.Greater(mod.g, mod.b);
            Assert.Greater(repair.g, repair.r);
            Assert.Greater(repair.g, repair.b);
            Assert.Greater(armory.b, armory.r);
            Assert.Greater(armory.b, armory.g);
        }
    }
}
