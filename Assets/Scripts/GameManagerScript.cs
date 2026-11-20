
using System.Collections;
using UnityEngine;
using TMPro;

public class GameManagerScript : MonoBehaviour
{
    [Header("Game Settings")]
    public int maxLives = 3;
    public int enemiesToStop = 3;
    public int totalWaves = 3;

    [Header("Game State")]
    public bool isPlaying;
    public int enemiesStopped;
    public int health;
    public int currentWave = 1;

    [Header("Wave Settings")]
    public float startingEnemySpeed = 0.5f;
    public float speedIncreasePerWave = 0.25f;
    public float wavePauseDuration = 3f;

    [Header("Enemy Spawning")]
    public Transform enemiesWrapper;
    public Transform enemySpawnPoints;

    [Header("Zombie Prefabs")]
    public GameObject greenZombiePrefab;
    public GameObject redZombiePrefab;
    public GameObject blueZombiePrefab;

    public float spawnDelay = 2f;

    [Header("UI References")]
    public GameObject hud;
    public GameObject winScreen;
    public GameObject loseScreen;

    [Header("Wave UI References")]
    public TMP_Text winText;
    public TMP_Text loseText;
    public GameObject replayButton;

    [Header("Med Pack Spawning")]
    public GameObject medPackPrefab;
    public Transform medPackSpawnPoints;
    public float minMedPackSpawnTime = 15f;
    public float maxMedPackSpawnTime = 40f;

    private GameObject currentMedPack;

    [Header("HUD Wave Counter")]
    public TMP_Text currentWaveText;
    public TMP_Text enemiesRemainingText;

    private bool isSpawning = false;
    private bool isTransitioningWave = false;
    private int enemiesStoppedThisWave = 0;

    void Start()
    {
        startGame();
    }

    void Update()
    {
        if (!isPlaying)
            return;

        if (health <= 0)
        {
            isPlaying = false;
            playerLost();
            return;
        }

        if (enemiesStoppedThisWave >= enemiesToStop &&
            !isTransitioningWave)
        {
            StartCoroutine(completeWave());
            return;
        }

        if (enemiesWrapper == null)
            return;

        int liveEnemies = enemiesWrapper.childCount;

        if (liveEnemies <= 0 && !isSpawning)
        {
            StartCoroutine(spawnEnemyWithDelay());
        }
    }

    public void startGame()
    {
        Debug.Log("Starting game");

        StopAllCoroutines();

        Time.timeScale = 1f;

        isSpawning = false;
        isTransitioningWave = false;
        isPlaying = false;

        clearUIs();
        clearEnemies();

        if (currentMedPack != null)
        {
            Destroy(currentMedPack);
            currentMedPack = null;
        }

        enemiesStopped = 0;
        enemiesStoppedThisWave = 0;
        health = maxLives;
        currentWave = 1;

        updateWaveUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isPlaying = true;

        Debug.Log("Wave 1 started!");

        StartCoroutine(SpawnMedPacks());
    }

    IEnumerator spawnEnemyWithDelay()
    {
        isSpawning = true;

        yield return new WaitForSeconds(spawnDelay);

        if (isPlaying &&
            !isTransitioningWave &&
            enemiesWrapper != null &&
            enemiesWrapper.childCount == 0)
        {
            spawnEnemy();
        }

        isSpawning = false;
    }

    IEnumerator SpawnMedPacks()
    {
        Debug.Log("MED PACK SPAWNER STARTED!");

        while (true)
        {
            float waitTime = Random.Range(
                minMedPackSpawnTime,
                maxMedPackSpawnTime
            );

            Debug.Log(
                "Next med pack spawn in " + waitTime + " seconds."
            );

            yield return new WaitForSeconds(waitTime);

            if (isPlaying && currentMedPack == null)
            {
                SpawnMedPack();
            }
        }
    }

    void SpawnMedPack()
    {
        if (medPackPrefab == null)
        {
            Debug.LogError("Med Pack Prefab is not assigned!");
            return;
        }

        if (medPackSpawnPoints == null ||
            medPackSpawnPoints.childCount == 0)
        {
            Debug.LogError("Med Pack Spawn Points are missing!");
            return;
        }

        int randomIndex = Random.Range(
            0,
            medPackSpawnPoints.childCount
        );

        Transform selectedPoint =
            medPackSpawnPoints.GetChild(randomIndex);

        currentMedPack = Instantiate(
            medPackPrefab,
            selectedPoint.position,
            selectedPoint.rotation
        );

        MedPackPickup pickup =
            currentMedPack.GetComponentInChildren<MedPackPickup>(true);

        if (pickup != null)
        {
            pickup.gameManager = this;
            Debug.Log("GameManager successfully assigned!");
        }
        else
        {
            Debug.LogError(
                "MedPackPickup script not found on med pack!"
            );
        }

        Debug.Log(
            "Med pack spawned at: " + selectedPoint.name
        );
    }

    public bool RestoreCore(int amount)
    {
        if (!isPlaying)
            return false;

        if (health >= maxLives)
        {
            Debug.Log("Core health is already full!");
            return false;
        }

        health = Mathf.Min(health + amount, maxLives);

        Debug.Log(
            "Core repaired! Health: " +
            health + "/" + maxLives
        );

        return true;
    }

    // Determine which zombie should spawn
    GameObject GetZombiePrefab()
    {
        int zombieNumber = enemiesStoppedThisWave;

        // WAVE 1: All green zombies
        if (currentWave == 1)
        {
            return greenZombiePrefab;
        }

        // WAVE 2: One red zombie
        if (currentWave == 2)
        {
            if (zombieNumber == 1)
            {
                return redZombiePrefab;
            }

            return greenZombiePrefab;
        }

        // WAVE 3: One red and one blue zombie
        if (currentWave >= 3)
        {
            if (zombieNumber == 1)
            {
                return redZombiePrefab;
            }

            if (zombieNumber == 2)
            {
                return blueZombiePrefab;
            }

            return greenZombiePrefab;
        }

        return greenZombiePrefab;
    }

    void spawnEnemy()
    {
        if (enemySpawnPoints == null ||
            enemySpawnPoints.childCount == 0)
        {
            Debug.LogError("No enemy spawn points assigned!");
            return;
        }

        if (enemiesWrapper == null)
        {
            Debug.LogError("No enemies wrapper assigned!");
            return;
        }

        // Select zombie based on wave and progress
        GameObject selectedPrefab = GetZombiePrefab();

        if (selectedPrefab == null)
        {
            Debug.LogError(
                "Zombie prefab missing for Wave " + currentWave
            );
            return;
        }

        int randomIndex = Random.Range(
            0,
            enemySpawnPoints.childCount
        );

        Transform spawnPoint =
            enemySpawnPoints.GetChild(randomIndex);

        GameObject newEnemy = Instantiate(
            selectedPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            enemiesWrapper
        );

        float currentSpeed =
            startingEnemySpeed +
            ((currentWave - 1) * speedIncreasePerWave);

        EnemyFollowCore enemyFollow =
            newEnemy.GetComponent<EnemyFollowCore>();

        if (enemyFollow != null)
        {
            enemyFollow.gameManagerScript = this;
            enemyFollow.moveSpeed = currentSpeed;
        }
        else
        {
            Debug.LogWarning(
                "Spawned enemy is missing EnemyFollowCore!"
            );
        }

        EnemyDeathEffect enemyDeath =
            newEnemy.GetComponent<EnemyDeathEffect>();

        if (enemyDeath != null)
        {
            enemyDeath.gameManagerScript = this;
        }
        else
        {
            Debug.LogWarning(
                "Spawned enemy is missing EnemyDeathEffect!"
            );
        }

        Debug.Log(
            $"Wave {currentWave}: Spawned {selectedPrefab.name} " +
            $"with movement speed {currentSpeed}"
        );
    }

    public void enemyDefeated()
    {
        if (!isPlaying || isTransitioningWave)
            return;

        enemiesStopped++;
        enemiesStoppedThisWave++;

        updateWaveUI();

        Debug.Log(
            $"Wave {currentWave}: " +
            $"{enemiesStoppedThisWave}/{enemiesToStop} defeated"
        );
    }

    void updateWaveUI()
    {
        int enemiesRemaining = Mathf.Max(
            0,
            enemiesToStop - enemiesStoppedThisWave
        );

        if (currentWaveText != null)
        {
            currentWaveText.text =
                $"WAVE {currentWave}/{totalWaves}";
        }

        if (enemiesRemainingText != null)
        {
            enemiesRemainingText.text =
                $"{enemiesRemaining} {(enemiesRemaining == 1 ? "Enemy" : "Enemies")} Remaining";
        }
    }

    IEnumerator completeWave()
    {
        isTransitioningWave = true;
        isPlaying = false;

        isSpawning = false;

        Debug.Log($"Wave {currentWave} completed!");

        if (currentWave >= totalWaves)
        {
            playerWon();
            yield break;
        }

        if (winText != null)
        {
            winText.text = $"WAVE {currentWave} COMPLETE!";
        }

        if (hud != null)
            hud.SetActive(false);

        if (winScreen != null)
            winScreen.SetActive(true);

        if (loseScreen != null)
            loseScreen.SetActive(false);

        if (replayButton != null)
            replayButton.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(
            wavePauseDuration
        );

        Time.timeScale = 1f;

        clearEnemies();

        currentWave++;
        enemiesStoppedThisWave = 0;

        updateWaveUI();

        if (winScreen != null)
            winScreen.SetActive(false);

        if (hud != null)
            hud.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isTransitioningWave = false;
        isPlaying = true;

        Debug.Log($"Wave {currentWave} started!");
    }

    public void replayButtonClicked()
    {
        Debug.Log("Replay button clicked!");
        startGame();
    }

    void playerWon()
    {
        Debug.Log("Player won the game!");

        isPlaying = false;

        StopAllCoroutines();
        isSpawning = false;

        Time.timeScale = 1f;

        if (winText != null)
        {
            winText.text = "YOU WIN!";
        }

        if (hud != null)
            hud.SetActive(false);

        if (winScreen != null)
            winScreen.SetActive(true);

        if (loseScreen != null)
            loseScreen.SetActive(false);

        if (replayButton != null)
            replayButton.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void playerLost()
    {
        Debug.Log("Player lost the game!");

        isPlaying = false;

        StopAllCoroutines();
        isSpawning = false;

        Time.timeScale = 1f;

        if (hud != null)
            hud.SetActive(false);

        if (winScreen != null)
            winScreen.SetActive(false);

        if (loseScreen != null)
            loseScreen.SetActive(true);

        if (loseText != null)
            loseText.text = "YOU LOST!";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("GameManager unlocked cursor");
    }

    void clearEnemies()
    {
        if (enemiesWrapper == null)
            return;

        for (int i = enemiesWrapper.childCount - 1;
             i >= 0; i--)
        {
            Transform enemy =
                enemiesWrapper.GetChild(i);

            enemy.SetParent(null);
            Destroy(enemy.gameObject);
        }
    }

    void clearUIs()
    {
        Debug.Log("Clearing UIs");

        if (hud != null)
            hud.SetActive(true);

        if (winScreen != null)
            winScreen.SetActive(false);

        if (loseScreen != null)
            loseScreen.SetActive(false);

        if (replayButton != null)
            replayButton.SetActive(true);

        if (winText != null)
            winText.text = "YOU WIN!";

        if (loseText != null)
            loseText.text = "YOU LOST!";
    }
}
