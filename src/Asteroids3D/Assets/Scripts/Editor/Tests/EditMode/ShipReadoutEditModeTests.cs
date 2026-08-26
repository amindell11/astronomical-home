using System;
using Game.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the reservation table against the texts the native drawers actually emit. Rows sit at fixed offsets, so a drawer that outgrows its reservation would silently overlap the row above it instead of moving it.</summary>
    [Category("Ships")]
    public class ShipReadoutEditModeTests
    {
        [TestCase(ShipReadoutRow.Speed, 1)]
        [TestCase(ShipReadoutRow.Controls, 3)]
        [TestCase(ShipReadoutRow.Shield, 1)]
        [TestCase(ShipReadoutRow.Health, 1)]
        [TestCase(ShipReadoutRow.Heat, 1)]
        [TestCase(ShipReadoutRow.Missiles, 2)]
        [TestCase(ShipReadoutRow.LockOn, 3)]
        [TestCase(ShipReadoutRow.Policy, 4)]
        public void Row_RejectsTextPastItsReservation(ShipReadoutRow row, int reserved)
        {
            var onePastReservation = new string('\n', reserved);
            Assert.Throws<ArgumentException>(
                () => ShipReadout.Draw(Vector2.zero, row, onePastReservation, Color.white));
        }
    }
}
