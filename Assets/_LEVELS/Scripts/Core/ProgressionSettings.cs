using System;
using UnityEngine;

namespace Levels.Core
{
    [CreateAssetMenu(menuName = "LEVEL's/Progression Settings")]
    public sealed class ProgressionSettings : ScriptableObject
    {
        [Header("EXP cost = first cost + (current level - 1) * increment")]
        [Min(1)] public int playerFirstCost = 20;
        [Min(0)] public int playerCostIncrement = 20;
        [Min(1)] public int enemyFirstCost = 20;
        [Min(0)] public int enemyCostIncrement = 20;
        [Header("Player: base value + bonus per completed level step")]
        [Min(0)] public int playerHealthPerLevel = 2;
        [Min(1)] public int playerDamageLevelStep = 2;
        [Min(0)] public int playerDamagePerStep = 1;
        [Header("Normal enemies")]
        [Min(0)] public int enemyHealthPerLevel = 1;
        [Min(1)] public int enemyDamageLevelStep = 2;
        [Min(0)] public int enemyDamagePerStep = 1;
        [Min(0)] public int enemyExperiencePerLevel = 5;
        [Min(0)] public int enemyGoldPerLevel = 2;
        [Min(1)] public int enemyGoldBonusLevelStep = 2;
        [Min(0)] public int enemyGoldBonusPerStep = 1;

        public int PlayerCost(int level) => Scale(playerFirstCost, level, playerCostIncrement);
        public int EnemyCost(int level) => Scale(enemyFirstCost, level, enemyCostIncrement);
        public int PlayerHealth(int basis, int level) => Scale(basis, level, playerHealthPerLevel);
        public int PlayerDamage(int basis, int level) => Step(basis, level, playerDamageLevelStep, playerDamagePerStep);
        public int EnemyHealth(int basis, int level) => Scale(basis, level, enemyHealthPerLevel);
        public int EnemyDamage(int basis, int level) => Step(basis, level, enemyDamageLevelStep, enemyDamagePerStep);
        public int EnemyExperience(int basis, int level) => Scale(basis, level, enemyExperiencePerLevel);
        public int EnemyGold(int basis, int level) => Step(Scale(basis, level, enemyGoldPerLevel), level,
            enemyGoldBonusLevelStep, enemyGoldBonusPerStep);

        private static int Scale(int basis, int level, int increment) =>
            (int)Math.Min(int.MaxValue, Math.Max(0, basis) + (long)Math.Max(0, level - 1) * Math.Max(0, increment));
        private static int Step(int basis, int level, int interval, int increment) =>
            Scale(basis, Math.Max(0, level - 1) / Math.Max(1, interval) + 1, increment);
    }
}
