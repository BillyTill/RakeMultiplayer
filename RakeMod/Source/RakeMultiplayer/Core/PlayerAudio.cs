using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Positional one-shot sounds for remote actors. The game plays its own audio on the
    /// object that owns it, but a remote player clone has every AudioSource disabled, so
    /// these helpers spawn a temporary 3D source at the right place instead.
    /// </summary>
    internal static class PlayerAudio
    {
        public static void PlayAt(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;

            // Explicit 3D source: PlayClipAtPoint leaves the spatial settings to the
            // engine defaults, and a 2D one-shot would be equally loud everywhere.
            var go = new GameObject("OneShotAudio");
            go.transform.position = position;

            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 35f;
            source.dopplerLevel = 0f;
            source.Play();

            UnityEngine.Object.Destroy(go, clip.length + 0.2f);
        }

        public static void PlayPain(Transform player)
        {
            if (player == null) return;

            var fps = player.GetComponent<FPSController>();
            if (fps == null || fps.painSounds == null || fps.painSounds.Length == 0) return;

            var clip = fps.painSounds[Random.Range(0, fps.painSounds.Length)];
            PlayAt(clip, player.position + Vector3.up * 1.5f, 1f);
        }

        public static void PlayFootstep(Transform player)
        {
            if (player == null) return;

            var movement = player.GetComponent<PlayerMovement>();
            if (movement == null)
            {
                var all = player.GetComponentsInChildren<PlayerMovement>(true);
                if (all.Length > 0) movement = all[0];
            }
            if (movement == null || movement.FootstepSound == null || movement.FootstepSound.Length == 0) return;

            int id = Mathf.Clamp(movement.stepType, 0, movement.FootstepSound.Length - 1);
            PlayAt(movement.FootstepSound[id], player.position + Vector3.up * 0.1f, 0.7f);
        }
    }
}