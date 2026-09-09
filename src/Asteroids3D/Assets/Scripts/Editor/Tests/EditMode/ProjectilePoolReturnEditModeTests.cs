using System.Collections.Generic;
using System.Text.RegularExpressions;
using Combat.Projectiles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.EditMode
{
    /// <summary>Pool-size deltas instead of Get round-trips: Get's instantiate path calls DontDestroyOnLoad, which EditMode forbids.</summary>
    [Category("Weapons")]
    public class ProjectilePoolReturnEditModeTests
    {
        private readonly List<GameObject> tempObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in tempObjects)
                if (go != null)
                    Object.DestroyImmediate(go);
            tempObjects.Clear();
        }

        private Laser CreateLaser()
        {
            var go = new GameObject("laser");
            tempObjects.Add(go);
            return go.AddComponent<Laser>();
        }

        [Test]
        public void ReturnToPool_SecondCallSameActivation_PushesAndRaisesOnce()
        {
            var laser = CreateLaser();
            var sizeBefore = SimplePool<Laser>.PoolSize;
            var returnedEvents = 0;
            laser.ReturnedToPool += () => returnedEvents++;

            laser.ReturnToPoolImmediate();
            laser.ReturnToPoolImmediate();

            Assert.IsFalse(laser.gameObject.activeSelf);
            Assert.AreEqual(1, returnedEvents);
            Assert.AreEqual(sizeBefore + 1, SimplePool<Laser>.PoolSize);
        }

        [Test]
        public void Release_AlreadyInactive_LogsErrorAndRefusesDuplicatePush()
        {
            var laser = CreateLaser();
            var sizeBefore = SimplePool<Laser>.PoolSize;

            SimplePool<Laser>.Release(laser);
            LogAssert.Expect(LogType.Error, new Regex("released while already inactive"));
            SimplePool<Laser>.Release(laser);

            Assert.AreEqual(sizeBefore + 1, SimplePool<Laser>.PoolSize);
        }
    }
}
