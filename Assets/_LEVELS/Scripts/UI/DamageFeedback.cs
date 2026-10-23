using Levels.Enemies;
using Levels.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Levels.UI
{
    /// <summary>Listens only to applied damage, never to input, hitboxes or session changes.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageFeedback : MonoBehaviour
    {
        [SerializeField] private FloatingDamageNumber numberPrefab;
        [SerializeField] private Vector3 offset = new Vector3(0f, .95f, 0f);
        [SerializeField] private Color enemyDamageColor = new Color(1f, .79f, .16f);
        [SerializeField] private Color playerDamageColor = new Color(1f, .28f, .12f);
        [SerializeField, Min(0f)] private float horizontalSpread = .28f;

        private EnemyHealth enemy;
        private PlayerHealth player;
        private int sequence;

        private void OnEnable()
        {
            enemy = GetComponent<EnemyHealth>();
            player = GetComponent<PlayerHealth>();
            if (enemy != null) enemy.DamageApplied += Show;
            else if (player != null) player.DamageApplied += Show;
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.DamageApplied -= Show;
            if (player != null) player.DamageApplied -= Show;
            enemy = null;
            player = null;
        }

        private void Show(int appliedDamage)
        {
            if (appliedDamage <= 0 || numberPrefab == null) return;
            // Cycle locally so repeated hits remain readable without shared target state.
            int slot = sequence;
            sequence = (sequence + 1) % 5;
            float horizontal = slot == 0 ? 0f : ((slot + 1) / 2) * (slot % 2 == 1 ? -1f : 1f) * horizontalSpread;
            Vector3 position = transform.position + offset + Vector3.right * horizontal;
            FloatingDamageNumber number = Instantiate(numberPrefab, position, Quaternion.identity);
            // Survive the target's death, but belong to its scene and unload with it.
            SceneManager.MoveGameObjectToScene(number.gameObject, gameObject.scene);
            number.Show(appliedDamage, enemy != null ? enemyDamageColor : playerDamageColor);
        }
    }
}
