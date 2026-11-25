using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ships;
using UI.Hangar;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Hangar
{
    [Category("UI")]
    public class HangarCarouselEditModeTests
    {
        private const float FrameSeconds = 1f / 60f;

        private static readonly string[] ShipPaths =
        {
            "Assets/Prefabs/Ships/Valis.prefab", "Assets/Prefabs/Ships/Nightshade.prefab",
            "Assets/Prefabs/Ships/Crimson.prefab", "Assets/Prefabs/Ships/Vanguard.prefab",
        };

        [TestCase(1)]
        [TestCase(-1)]
        public void OneWayBurst_WalksTheListInOrder_WithoutSwappingAShipOnScreen(int direction)
        {
            var ships = ShipPaths.Select(AssetDatabase.LoadAssetAtPath<Ship>).ToArray();
            var carousel = new HangarCarousel(ships.Length);
            carousel.Start(ships[0]);
            var pick = 0;
            var entered = 0;
            var onScreen = new List<(float offset, Ship ship)> { (0f, ships[0]) };

            for (var frame = 0; frame < 6 * 60; frame++)
            {
                if (frame < 3 * 60 && frame % 6 == 0)
                {
                    pick = (pick + direction + ships.Length) % ships.Length;
                    carousel.Step(ships[pick], direction);
                }
                carousel.Advance(FrameSeconds);

                var now = new List<(float offset, Ship ship)>();
                var nearest = Mathf.FloorToInt(carousel.Position);
                for (var index = nearest; index <= nearest + 1; index++)
                    if (Mathf.Abs(index - carousel.Position) < HangarCarousel.PathEnd)
                        now.Add((index - carousel.Position, carousel.At(index)));

                foreach (var (offset, ship) in now)
                {
                    var same = onScreen.Where(s => Math.Abs(s.offset - offset) < .3f).ToList();
                    if (same.Count > 0)
                    {
                        Assert.That(ship, Is.SameAs(same[0].ship), $"a ship changed on screen at frame {frame}");
                        continue;
                    }
                    entered = (entered + direction + ships.Length) % ships.Length;
                    Assert.That(ship, Is.SameAs(ships[entered]), $"the ship entering at frame {frame} is next in the list");
                }
                onScreen = now;
            }

            Assert.That(carousel.Position, Is.EqualTo(carousel.Target), "the carousel settles");
            Assert.That(carousel.At(carousel.Target), Is.SameAs(ships[pick]), "it settles on the last pick");
        }
    }
}
