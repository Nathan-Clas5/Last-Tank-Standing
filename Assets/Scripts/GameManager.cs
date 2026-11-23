using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsPlaying { get; private set; }
    public bool IsChangingLevel { get; private set; }
    public GameObject CurrentLevelInstance => currentLevelInstance;

    [Header("Level Prefabs — in play order")]
    [SerializeField] private GameObject[] levelPrefabs;
    [SerializeField, Min(0f)] private float levelTransitionDelay = 2f;
    [SerializeField] private Transform levelParent;

    [Header("UI")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject youWinPanel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private TMP_Text youWinText;

    private GameObject currentLevelInstance;
    private int currentLevelIndex = -1;
    private int score;
    private bool gameEnded;
    private Coroutine transitionCoroutine;
    private Coroutine enemyCheckCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Time.timeScale = 0f;
        IsPlaying = false;
        IsChangingLevel = false;
        gameEnded = false;

        SetActive(startPanel, true);
        SetActive(hudPanel, false);
        SetActive(gameOverPanel, false);
        SetActive(youWinPanel, false);
        UpdateScoreUI();

        if (levelPrefabs == null || levelPrefabs.Length == 0)
            Debug.LogError("GameManager: Assign at least one level prefab.", this);
    }

    public void StartGame()
    {
        if (levelPrefabs == null || levelPrefabs.Length == 0)
        {
            Debug.LogError("Cannot start: no level prefabs are assigned.", this);
            return;
        }

        StopRunningCoroutines();
        ClearAllProjectiles();

        if (currentLevelInstance != null)
            Destroy(currentLevelInstance);

        score = 0;
        gameEnded = false;
        IsChangingLevel = false;
        IsPlaying = true;
        currentLevelIndex = 0;

        Time.timeScale = 1f;

        SetActive(startPanel, false);
        SetActive(hudPanel, true);
        SetActive(gameOverPanel, false);
        SetActive(youWinPanel, false);
        UpdateScoreUI();

        SpawnLevel(currentLevelIndex);
    }

    public void AddScore(int points)
    {
        if (gameEnded)
            return;

        score += points;
        UpdateScoreUI();
    }

    public void EnemyDefeated(EnemyTank defeatedEnemy)
    {
        if (!IsPlaying || IsChangingLevel || gameEnded ||
            defeatedEnemy == null || currentLevelInstance == null)
        {
            return;
        }

        if (!defeatedEnemy.transform.IsChildOf(currentLevelInstance.transform))
            return;

        if (enemyCheckCoroutine == null)
            enemyCheckCoroutine = StartCoroutine(CheckForRemainingEnemies());
    }

    private IEnumerator CheckForRemainingEnemies()
    {
        // Let Unity finish Destroy() before counting remaining enemies.
        yield return null;
        enemyCheckCoroutine = null;

        if (!IsPlaying || IsChangingLevel || gameEnded || currentLevelInstance == null)
            yield break;

        EnemyTank[] remaining =
            currentLevelInstance.GetComponentsInChildren<EnemyTank>(true);

        if (remaining.Length == 0)
            AllEnemiesDefeated();
    }

    public void AllEnemiesDefeated()
    {
        if (!IsPlaying || gameEnded || IsChangingLevel || transitionCoroutine != null)
            return;

        transitionCoroutine = StartCoroutine(AdvanceLevelAfterDelay());
    }

    private IEnumerator AdvanceLevelAfterDelay()
    {
        IsChangingLevel = true;
        IsPlaying = false;
        Time.timeScale = 0f;

        // Clear projectiles as soon as the level is completed.
        ClearAllProjectiles();

        yield return new WaitForSecondsRealtime(levelTransitionDelay);

        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;
        }

        currentLevelIndex++;

        if (currentLevelIndex >= levelPrefabs.Length)
        {
            ShowYouWin();
            transitionCoroutine = null;
            yield break;
        }

        // Clear again before the new level starts, in case another
        // script created a projectile during the transition.
        ClearAllProjectiles();

        if (!SpawnLevel(currentLevelIndex))
        {
            transitionCoroutine = null;
            yield break;
        }

        Time.timeScale = 1f;
        IsChangingLevel = false;
        IsPlaying = true;
        transitionCoroutine = null;
    }

    private void ClearAllProjectiles()
    {
        Projectile[] projectiles = FindObjectsOfType<Projectile>(true);

        foreach (Projectile projectile in projectiles)
        {
            if (projectile != null)
                Destroy(projectile.gameObject);
        }
    }

    private bool SpawnLevel(int index)
    {
        if (index < 0 || index >= levelPrefabs.Length)
        {
            Debug.LogError($"Invalid level prefab index: {index}.", this);
            return false;
        }

        GameObject prefab = levelPrefabs[index];

        if (prefab == null)
        {
            Debug.LogError($"Level prefab at index {index} is not assigned.", this);
            return false;
        }

        currentLevelInstance = Instantiate(prefab, levelParent);
        currentLevelInstance.name = prefab.name;
        return true;
    }

    private void ShowYouWin()
    {
        gameEnded = true;
        IsPlaying = false;
        IsChangingLevel = false;
        Time.timeScale = 0f;

        SetActive(hudPanel, false);
        SetActive(youWinPanel, true);

        if (youWinText != null)
            youWinText.text = $"You Win!\nScore: {score}";
    }

    public void GameOver()
    {
        if (gameEnded || IsChangingLevel)
            return;

        ClearAllProjectiles();

        gameEnded = true;
        IsPlaying = false;
        Time.timeScale = 0f;

        SetActive(hudPanel, false);
        SetActive(gameOverPanel, true);

        if (gameOverText != null)
            gameOverText.text = $"Game Over\nScore: {score}";
    }

    public void RestartGame()
    {
        StopRunningCoroutines();

        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;
        }

        ClearAllProjectiles();
        Time.timeScale = 1f;
        StartGame();
    }

    private void StopRunningCoroutines()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        if (enemyCheckCoroutine != null)
        {
            StopCoroutine(enemyCheckCoroutine);
            enemyCheckCoroutine = null;
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}