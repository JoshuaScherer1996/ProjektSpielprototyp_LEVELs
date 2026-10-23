using UnityEngine;

namespace Levels.Enemies
{
    /// <summary>Stable category shared by placed prefab instances and dynamically spawned enemies.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemySpawnIdentity : MonoBehaviour
    {
        [SerializeField] private string category = "Goblin";
        public string Category => category;
        public bool IsAlive => GetComponent<EnemyHealth>().IsAlive;
    }
}
