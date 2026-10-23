using System.Globalization;
using TMPro;
using UnityEngine;

namespace Levels.UI
{
    [DisallowMultipleComponent]
    public sealed class FloatingDamageNumber : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup opacity;
        [SerializeField, Min(.01f)] private float duration = .8f;
        [SerializeField, Min(0f)] private float riseDistance = .65f;
        [SerializeField, Range(0f, .99f)] private float fadeStart = .65f;

        private Vector3 origin;
        private float elapsed;
        public float Duration => duration;

        private void OnEnable()
        {
            origin = transform.position;
            elapsed = 0f;
            if (opacity != null) opacity.alpha = 1f;
        }

        public void Show(int amount, Color color)
        {
            if (label == null || amount <= 0) { Destroy(gameObject); return; }
            label.text = amount.ToString(CultureInfo.InvariantCulture);
            label.color = color;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
            transform.position = origin + Vector3.up * (riseDistance * progress);
            if (opacity != null) opacity.alpha = 1f - Mathf.InverseLerp(fadeStart, 1f, progress);
            if (progress >= 1f) Destroy(gameObject);
        }
    }
}
