using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelEnemySpawner : MonoBehaviour
{
    [Serializable]
    public class EnemySpawnOption
    {
        public GameObject enemyPrefab;

        [Min(0f)]
        [Tooltip("Relative chance for this enemy type to be selected.")]
        public float spawnWeight = 1f;
    }

    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Enemy Types")]
    [SerializeField] private List<EnemySpawnOption> enemyTypes =
        new List<EnemySpawnOption>();

    private void Start()
    {
        SpawnEnemies();
    }

    public void SpawnEnemies()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning($"{name}: No spawn points assigned.", this);
            return;
        }

        if (!HasValidEnemyOptions())
        {
            Debug.LogWarning(
                $"{name}: Assign at least one enemy prefab with a positive spawn weight.",
                this
            );
            return;
        }

        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning($"{name}: A spawn point is not assigned.", this);
                continue;
            }

            EnemySpawnOption option = GetRandomEnemyType();

            if (option == null || option.enemyPrefab == null)
                continue;

            Instantiate(
                option.enemyPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                transform
            );
        }
    }

    private bool HasValidEnemyOptions()
    {
        foreach (EnemySpawnOption option in enemyTypes)
        {
            if (option != null &&
                option.enemyPrefab != null &&
                option.spawnWeight > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private EnemySpawnOption GetRandomEnemyType()
    {
        float totalWeight = 0f;

        foreach (EnemySpawnOption option in enemyTypes)
        {
            if (option != null && option.enemyPrefab != null)
                totalWeight += Mathf.Max(0f, option.spawnWeight);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.value * totalWeight;

        foreach (EnemySpawnOption option in enemyTypes)
        {
            if (option == null || option.enemyPrefab == null)
                continue;

            float weight = Mathf.Max(0f, option.spawnWeight);

            if (roll < weight)
                return option;

            roll -= weight;
        }

        return null;
    }
}