using Levels.Audio;
using UnityEngine;

namespace Levels.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class LoadingScreen : MonoBehaviour
    {
        private double revealAt;
        public bool IsVisible => gameObject.activeInHierarchy;
        internal bool IsWaitingForAudio => Time.realtimeSinceStartupAsDouble < revealAt;

        public void Show()
        {
            revealAt = GameAudio.BeginLoading();
            GetComponent<Canvas>().enabled = !IsWaitingForAudio;
            gameObject.SetActive(true);
            if (!IsWaitingForAudio) GameAudio.RevealLoadingScreen();
        }

        internal void Reveal()
        {
            GameAudio.RevealLoadingScreen();
            GetComponent<Canvas>().enabled = true;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
