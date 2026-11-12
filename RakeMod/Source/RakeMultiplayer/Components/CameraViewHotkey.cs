using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Lets any hunter open the vanilla camera monitor from anywhere with Tab.
    ///
    /// Vanilla only exposes the feed through the safehouse computer: the player has to
    /// stand at it and press Use. <see cref="CameraComputer.Action"/> itself has no
    /// distance check, so pressing Tab simply runs the same sequence (fade, freeze the
    /// player, enable the monitor UI). Switching feeds and leaving with E are handled by
    /// the game, including the multiplayer patches that skip broken or picked-up cameras.
    ///
    /// Tab again (or E) closes the monitor.
    /// </summary>
    public class CameraViewHotkey : MonoBehaviour
    {
        private static float _lastWarn = -10f;

        private void Update()
        {
            if (!RakeMultiplayerPlugin.Enabled.Value) return;
            if (!Input.GetKeyDown(KeyCode.Tab)) return;

            // Never hijack the key while another screen owns the mouse or the body.
            if (DebugConsole.ConsoleOpen) return;
            if (UI.LoadingScreen.IsVisible) return;
            if (UI.MultiplayerMenu.IsOpen) return;
            if (PlayerRagdoll.IsActive) return;
            if (RakePlayerController.IsActive) return;

            var computer = CameraComputer.instance;
            if (computer == null || computer.processing) return;

            if (!computer.bWatching && Time.unscaledTime - _lastWarn > 2f)
            {
                var manager = CameraManager.instance;
                if (manager != null && manager.GetCameraCount() <= 0)
                {
                    _lastWarn = Time.unscaledTime;
                    RakeMultiplayerPlugin.Log?.LogInfo("[Cameras] Tab pressed, but no camera is set up");
                }
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Cameras] " + Toggle());
        }

        /// <summary>
        /// Enter or leave the camera monitor. Shared by the hotkey and the "cams" console
        /// command. Returns a short status for the log/console.
        /// </summary>
        public static string Toggle()
        {
            var computer = CameraComputer.instance;
            if (computer == null) return "cams: no camera computer in this scene";
            if (computer.processing) return "cams: busy";

            if (computer.bWatching)
            {
                computer.Exit();
                return "cams: leaving the monitor";
            }

            var manager = CameraManager.instance;
            if (manager == null) return "cams: no CameraManager";

            int count = manager.GetCameraCount();
            if (count <= 0)
            {
                // Vanilla draws the "You did not setup cameras" hint itself.
                computer.Action();
                return "cams: no cameras set up";
            }

            computer.Action();
            return "cams: opening the monitor (" + count + " feed(s))";
        }
    }
}
