#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Tests.PlayMode.Common.SimulatedInput
{
    /// <summary>
    /// The InputTestFixture every PlayMode test simulates input through. The package fixture turns
    /// editor input updates off for a [UnityTest], then its own settings writes turn them back on.
    /// An editor update lets the parked real InputManager point the global device state at its own
    /// buffers until the next dynamic update, so a device read in the FixedUpdate phase sees the
    /// wrong manager's state, or null in batch mode. Setup turns editor updates off for every test
    /// once the base has finished writing settings. Root cause:
    /// https://github.com/amindell11/astronomical-home/issues/1196
    /// </summary>
    public class PlayModeInputTestFixture : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            StopEditorUpdates();
        }

        private static void StopEditorUpdates()
        {
            var manager = typeof(InputSystem).GetField("s_Manager", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
                ?? throw new MissingFieldException("InputSystem", "s_Manager");
            var updateMask = manager.GetType().GetField("m_UpdateMask", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException("InputManager", "m_UpdateMask");
            updateMask.SetValue(manager, (InputUpdateType)updateMask.GetValue(manager) & ~InputUpdateType.Editor);
        }
    }
}
#endif
