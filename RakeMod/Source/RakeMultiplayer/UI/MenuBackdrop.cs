using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using RakeMultiplayer.Patches;

namespace RakeMultiplayer.UI
{
    /// <summary>
    /// Keeps the vanilla main-menu scene visible behind the mod menu while hiding
    /// the vanilla buttons. The 3D desk room (posters, blinds, props) stays, the
    /// screen-space menu canvases are switched off and restored when the menu closes.
    /// </summary>
    public static class MenuBackdrop
    {
        private struct HiddenEntry
        {
            public GameObject Object;
            public bool WasActive;
        }

        private static readonly List<HiddenEntry> Hidden = new List<HiddenEntry>();
        private static bool _active;

        public static bool IsActive
        {
            get { return _active; }
        }

        public static void Enter(Canvas ownCanvas)
        {
            if (_active) return;
            if (Application.loadedLevel != 0) return; // only the vanilla main menu

            _active = true;
            Hidden.Clear();

            // Every vanilla Canvas in level0 is menu UI. Hide its root, not the 3D scene.
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null) continue;
                if (ownCanvas != null && canvas == ownCanvas) continue;
                if (canvas.gameObject.name == "MultiplayerUICanvas") continue;
                Hide(canvas.gameObject);
            }

            // Some menu groups are plain GameObjects referenced by MainMenu.
            var mainMenu = UnityEngine.Object.FindObjectOfType<MainMenu>();
            if (mainMenu != null)
            {
                Hide(mainMenu.GameMenu);
                Hide(mainMenu.newGame);
                Hide(mainMenu.setting);
                Hide(mainMenu.multiplayer);
                Hide(mainMenu.credits);
                Hide(mainMenu.help);
            }

            RakeMultiplayerPlugin.Log?.LogInfo("[Backdrop] vanilla menu UI hidden, scene kept (" + Hidden.Count + " objects)");
        }

        public static void Leave()
        {
            if (!_active) return;
            _active = false;

            for (int i = Hidden.Count - 1; i >= 0; i--)
            {
                var entry = Hidden[i];
                if (entry.Object != null) entry.Object.SetActive(entry.WasActive);
            }
            Hidden.Clear();

            RakeMultiplayerPlugin.Log?.LogInfo("[Backdrop] vanilla menu UI restored");
        }

        private static void Hide(GameObject go)
        {
            if (go == null) return;
            for (int i = 0; i < Hidden.Count; i++)
            {
                if (Hidden[i].Object == go) return;
            }
            Hidden.Add(new HiddenEntry { Object = go, WasActive = go.activeSelf });
            RakeMultiplayerPlugin.Log?.LogInfo("[Backdrop] hide '" + go.name + "'");
            go.SetActive(false);
        }

        /// <summary>Test hook: hide the vanilla buttons, save a screenshot, restore.</summary>
        public static void CaptureTest()
        {
            if (Application.loadedLevel != 0) return;

            Enter(null);
            CoroutineHelper.DelayedCall(0.5f, delegate
            {
                try
                {
                    string path = Path.Combine(Application.dataPath, "..\\menu_backdrop_test.png");
                    Application.CaptureScreenshot(path);
                    RakeMultiplayerPlugin.Log?.LogInfo("[Backdrop] test screenshot: " + path);
                }
                catch (Exception ex)
                {
                    RakeMultiplayerPlugin.Log?.LogError("[Backdrop] capture failed: " + ex.Message);
                }

                CoroutineHelper.DelayedCall(1.0f, Leave);
            });
        }
    }
}
