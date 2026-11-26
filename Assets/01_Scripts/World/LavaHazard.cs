using UnityEngine;

namespace Nexoterra.World
{
    /// <summary>Applies periodic burn damage while the player is standing on the volcanic lava mesh.</summary>
    public sealed class LavaHazard : MonoBehaviour
    {
        public MeshCollider surface;
        public float tickInterval = .85f;
        public float contactDamage = 2.5f;
        public float burnDamagePerSecond = 3.5f;
        public float burnDuration = 1f;

        float nextDamageAt;
        PlayerHealth player;

        void Awake()
        {
            if (!surface) surface = GetComponent<MeshCollider>();
        }

        void Update()
        {
            if (GameSession.Blocked || !surface || !surface.enabled) return;
            if (!player) player = FindAnyObjectByType<PlayerHealth>();
            if (!player || player.IsDead) return;

            var controller = player.GetComponent<CharacterController>();
            float feet = controller
                ? controller.bounds.min.y
                : player.transform.position.y;
            var ray = new Ray(new Vector3(player.transform.position.x, feet + .3f, player.transform.position.z), Vector3.down);
            if (!surface.Raycast(ray, out _, .65f) || Time.time < nextDamageAt) return;

            nextDamageAt = Time.time + Mathf.Max(.2f, tickInterval);
            player.ReceiveDamage(contactDamage);
            player.ApplyBurn(burnDamagePerSecond, burnDuration);
        }
    }
}
