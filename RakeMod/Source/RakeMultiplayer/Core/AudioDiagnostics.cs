using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Guards and diagnostics for sound and look input.
    ///
    /// A fresh game install has no PlayerPrefs. The game reads "Sens:" and "Sound:"
    /// without a default, so the mouse sensitivity can end up 0 (the character never
    /// turns, while the debug camera still works because it uses its own constant).
    /// The same install can also start with a silent AudioListener.
    /// </summary>
    internal static class AudioDiagnostics
    {
        public static void EnsureListener()
        {
            if (AudioListener.volume <= 0.001f)
            {
                AudioListener.volume = 1f;
                RakeMultiplayerPlugin.Log?.LogWarning("[Audio] AudioListener.volume was 0, set to 1");
            }
        }

        public static string Describe()
        {
            string listener = "none";
            AudioListener[] listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] == null) continue;
                if (listener == "none") listener = string.Empty;
                listener += (listeners[i].enabled ? "on" : "off") + "@" + listeners[i].gameObject.name + " ";
            }

            string weapon = "no local player";
            string identity = "local=?";

            PlayerSpawner spawner = PlayerSpawner.Instance;
            NetworkedPlayer local = spawner != null ? spawner.LocalPlayer : null;

            if (local != null)
            {
                identity = "local=" + local.isLocalPlayer + " id=" + local.PlayerId;

                Rifle_weapon rifle = First<Rifle_weapon>(local.gameObject);
                SniperRifle_weapon sniper = First<SniperRifle_weapon>(local.gameObject);

                if (rifle != null)
                {
                    weapon = "rifle active=" + rifle.gameObject.activeInHierarchy +
                             " sfx=" + (rifle.SFX == null ? "null" : (rifle.SFX.enabled ? "on" : "off")) +
                             " clip=" + (rifle.fireSound == null ? "null" : "yes");
                }
                else if (sniper != null)
                {
                    weapon = "sniper active=" + sniper.gameObject.activeInHierarchy;
                }
                else
                {
                    weapon = "no weapon component";
                }
            }

            return "volume=" + AudioListener.volume.ToString("F2") +
                   ", listeners=[" + listener.Trim() + "]" +
                   ", " + identity + ", " + weapon;
        }

        private static T First<T>(GameObject root) where T : Component
        {
            T[] all = root.GetComponentsInChildren<T>(true);
            return all.Length > 0 ? all[0] : null;
        }

        public static void LogState()
        {
            RakeMultiplayerPlugin.Log?.LogInfo("[Audio] " + Describe());
        }
    }
}
