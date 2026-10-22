using UnityEngine;

namespace Levels.Scenes
{
    [DisallowMultipleComponent]
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;
    }
}
