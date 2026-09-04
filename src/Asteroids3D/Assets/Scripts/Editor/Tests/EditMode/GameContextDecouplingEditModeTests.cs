using System.Reflection;
using AI;
using AI.Scanning;
using NUnit.Framework;
using Player;
using Ships;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Core")]
    public class GameContextDecouplingEditModeTests
    {
        [Test]
        public void PlayerInputReader_ScreenProjectorCanBeReconfigured()
        {
            var reader = new PlayerInputReader(_ => new Vector3(1f, 2f, 3f));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), reader.GetMouseWorldPosition());

            reader.SetScreenToGamePlane(_ => new Vector3(9f, 8f, 7f));
            Assert.AreEqual(new Vector3(9f, 8f, 7f), reader.GetMouseWorldPosition());
        }
        
        [Test]
        public void AiCommander_ExposesSensingInjectionApi()
        {
            var method = typeof(AICommander).GetMethod("SetSensing", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method);
            var parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(IShipRegistry)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(IObstacleField)));
        }

    }
}
