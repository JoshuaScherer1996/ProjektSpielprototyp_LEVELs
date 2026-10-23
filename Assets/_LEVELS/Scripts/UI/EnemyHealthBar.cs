using Levels.Enemies;
using UnityEngine;

namespace Levels.UI
{
    /// <summary>A nested prefab owned by one enemy; EnemyHealth is the only HP source.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private Canvas canvas;
        private EnemyHealth health;

        public EnemyHealth Health => health;

        private void OnEnable()
        {
            health = GetComponentInParent<EnemyHealth>();
            if (health != null) health.HealthChanged += Refresh;
            RefreshFromSource();
        }

        // Also covers any parent/child Awake ordering before progression's Start event.
        private void Start() => RefreshFromSource();

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= Refresh;
            health = null;
        }

        private void RefreshFromSource()
        {
            if (health == null) { if (canvas != null) canvas.enabled = false; return; }
            Refresh(health.CurrentHealth, health.MaxHealth);
        }

        private void Refresh(int current, int maximum)
        {
            if (canvas != null) canvas.enabled = current > 0 && maximum > 0;
            if (fill != null)
                fill.anchorMax = new Vector2(maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f, 1f);
        }
    }
}
