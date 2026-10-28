#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Tests.PlayMode.Common.SimulatedInput
{
    /// <summary>
    /// The InputTestFixture every PlayMode test simulates input through. Once the base Setup has
    /// finished writing settings, Setup corrects two package hazards. The base turns editor input
    /// updates off for a [UnityTest], then its own settings writes turn them back on, and an editor
    /// update lets the parked real InputManager point the global device state at its own buffers, so a
    /// FixedUpdate-phase device read sees the wrong state, or null in batch mode. The manager also
    /// drops events stamped inside the editor's edit-to-play window, recorded on the editor's clock,
    /// while simulated events run on the test runtime's clock from zero and can land inside it.
    /// Root causes: https://github.com/amindell11/astronomical-home/issues/1196,
    /// https://github.com/amindell11/astronomical-home/issues/1205
    /// </summary>
    public class PlayModeInputTestFixture : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            StopEditorUpdates();
            ClearEditToPlayWindow();
        }

        // The base TearDown restores the real window from the snapshot its Setup took.
        private static void ClearEditToPlayWindow()
        {
            var systemObject = typeof(InputSystem).GetField("s_SystemObject", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
                ?? throw new MissingFieldException("InputSystem", "s_SystemObject");
            var exitEditModeTime = systemObject.GetType().GetField("exitEditModeTime")
                ?? throw new MissingFieldException("InputSystemObject", "exitEditModeTime");
            exitEditModeTime.SetValue(systemObject, 0d);
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
