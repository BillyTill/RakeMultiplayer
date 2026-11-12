using UnityEngine;

namespace RakeMultiplayer
{
    /// <summary>
    /// Spawns the Rake's corpse on every peer.
    ///
    /// The vanilla death path instantiates <see cref="Enemy_AI.deadPrefab"/> on the host
    /// only. Clients use the same prefab from their Rake clone; if a level ships without
    /// one, a static copy of the Rake model is dropped on the ground instead, so a kill
    /// never leaves an empty spot.
    /// </summary>
    public static class RakeCorpse
    {
        public static GameObject Spawn(GameObject rakeObject, GameObject deadPrefab,
                                       Vector3 position, Quaternion rotation)
        {
            if (deadPrefab != null)
            {
                return Object.Instantiate(deadPrefab, position, rotation) as GameObject;
            }

            if (rakeObject == null) return null;

            RakeMultiplayerPlugin.Log?.LogWarning("[Rake] deadPrefab is missing - spawning a fallback corpse");

            var corpse = Object.Instantiate(rakeObject, position, rotation) as GameObject;
            if (corpse == null) return null;

            corpse.name = "RakeCorpse";

            // Remove/disable everything that would turn the corpse back into a live Rake.
            var ai = corpse.GetComponent<Enemy_AI>();
            if (ai != null) ai.enabled = false;

            var net = corpse.GetComponent<NetworkedRakeAI>();
            if (net != null) Object.Destroy(net);

            var agent = corpse.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            var controller = corpse.GetComponent<CharacterController>();
            if (controller != null) Object.Destroy(controller);

            var animator = corpse.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            var audio = corpse.GetComponent<AudioSource>();
            if (audio != null) audio.enabled = false;

            // Lay it down and let it settle on the ground.
            corpse.transform.rotation = Quaternion.Euler(90f, rotation.eulerAngles.y, 0f);

            if (corpse.GetComponent<Collider>() == null)
            {
                var capsule = corpse.AddComponent<CapsuleCollider>();
                capsule.height = 1.8f;
                capsule.radius = 0.4f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
            }

            var body = corpse.GetComponent<Rigidbody>();
            if (body == null) body = corpse.AddComponent<Rigidbody>();
            body.mass = 80f;
            body.drag = 0.4f;
            body.angularDrag = 2f;

            return corpse;
        }
    }
}
