using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tests.EditMode
{
    [Category("Core")]
    public class PlayerInputReaderEditModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private PlayerInputReader reader;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            reader = new PlayerInputReader(screen => screen);
            reader.Enable();
        }

        public override void TearDown()
        {
            // Not Dispose: the generated wrapper's Dispose calls Object.Destroy, an error in edit mode.
            reader.Disable();
            base.TearDown();
        }

        [Test]
        public void W_DrivesFullThrust()
        {
            Press(keyboard.wKey);

            Assert.AreEqual(1f, reader.Thrust);
        }

        [Test]
        public void LeftShift_HoldsHeading()
        {
            Assert.IsTrue(reader.WantsToRotate, "ship faces the cursor by default");

            Press(keyboard.leftShiftKey);

            Assert.IsFalse(reader.WantsToRotate);
        }

        [Test]
        public void LeftMouse_FiresWithoutTouchingAim()
        {
            Set(mouse.position, new Vector2(40f, 60f));
            var aimBefore = reader.GetMouseWorldPosition();

            Press(mouse.leftButton);

            Assert.IsTrue(reader.PrimaryFire);
            Assert.IsTrue(reader.WantsToRotate, "firing does not hold heading");
            Assert.AreEqual(aimBefore, reader.GetMouseWorldPosition());
        }

        // Unpark has no input gate of its own; a trigger held through Launch must not fire.
        [Test]
        public void FireHeldBeforeEnable_ReadsUnpressedUntilPressedAfresh()
        {
            reader.Disable();
            Press(mouse.leftButton);
            Press(mouse.rightButton);
            reader.Enable();
            InputSystem.Update();

            Assert.IsFalse(reader.PrimaryFire, "a primary trigger held across enable does not fire");
            Assert.IsFalse(reader.SecondaryFire, "a secondary trigger held across enable does not fire");

            Release(mouse.leftButton);
            Press(mouse.leftButton);

            Assert.IsTrue(reader.PrimaryFire, "a fresh press after enable fires");
        }

        [Test]
        public void MousePosition_ReachesProjector()
        {
            reader.SetScreenToGamePlane(screen => screen * 2f);

            Set(mouse.position, new Vector2(120f, 45f));

            Assert.AreEqual(new Vector3(240f, 90f, 0f), reader.GetMouseWorldPosition());
        }
    }
}
