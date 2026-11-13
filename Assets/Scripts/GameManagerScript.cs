
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    public GameObject enemyPrefab;
    public float spawnDelay = 2f;

    [Header("UI References")]
    public GameObject hud;
    public GameObject winScreen;
    public GameObject loseScreen;

    [Header("Wave UI References")]
    public Text winText;
    public GameObject replayButton;

    [Header("Med Pack Spawning")]
    public GameObject medPackPrefab;
    public Transform medPackSpawnPoints;
    public float minMedPackSpawnTime = 15f;
    public float maxMedPackSpawnTime = 40f;

    private GameObject currentMedPack;

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

        // Check losing condition
        if (health <= 0)
        {
            isPlaying = false;
            playerLost();
            return;
        }

        // Check wave completion
        if (enemiesStoppedThisWave >= enemiesToStop &&
            !isTransitioningWave)
        {
            StartCoroutine(completeWave());
            return;
        }

        if (enemiesWrapper == null)
            return;

        int liveEnemies = enemiesWrapper.childCount;

        // Spawn another enemy when none remain
        if (liveEnemies <= 0 && !isSpawning)
        {
            StartCoroutine(spawnEnemyWithDelay());
        }
    }

    // Starts or restarts the game
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

        // Clear existing med pack
        if (currentMedPack != null)
        {
            Destroy(currentMedPack);
            currentMedPack = null;
        }

        enemiesStopped = 0;
        enemiesStoppedThisWave = 0;
        health = maxLives;
        currentWave = 1;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isPlaying = true;

        Debug.Log("Wave 1 started!");

        // Start random med pack spawning
        StartCoroutine(SpawnMedPacks());
    }

    // Enemy spawning coroutine
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

    // Random med pack spawning coroutine
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

    // Spawn med pack at a random location
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

        // Automatically assign GameManager
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

    // Restore Core health
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

    // Spawn an enemy
    void spawnEnemy()
    {
        if (enemySpawnPoints == null ||
            enemySpawnPoints.childCount == 0)
        {
            Debug.LogError("No enemy spawn points assigned!");
            return;
        }

        if (enemyPrefab == null)
        {
            Debug.LogError("No enemy prefab assigned!");
            return;
        }

        if (enemiesWrapper == null)
        {
            Debug.LogError("No enemies wrapper assigned!");
            return;
        }

        int randomIndex = Random.Range(
            0,
            enemySpawnPoints.childCount
        );

        Transform spawnPoint =
            enemySpawnPoints.GetChild(randomIndex);

        GameObject newEnemy = Instantiate(
            enemyPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            enemiesWrapper
        );

        // Calculate movement speed based on current wave
        float currentSpeed =
            startingEnemySpeed +
            ((currentWave - 1) * speedIncreasePerWave);

        // Assign GameManager and movement speed
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

        // Assign GameManager to enemy death effect
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
            $"Spawned enemy for Wave {currentWave} " +
            $"with movement speed {currentSpeed}"
        );
    }

    // Called when an enemy is defeated
    public void enemyDefeated()
    {
        if (!isPlaying || isTransitioningWave)
            return;

        enemiesStopped++;
        enemiesStoppedThisWave++;

        Debug.Log(
            $"Wave {currentWave}: " +
            $"{enemiesStoppedThisWave}/{enemiesToStop} defeated"
        );
    }

    // Complete a wave
    IEnumerator completeWave()
    {
        isTransitioningWave = true;
        isPlaying = false;

        isSpawning = false;

        Debug.Log($"Wave {currentWave} completed!");

        // Final wave completed
        if (currentWave >= totalWaves)
        {
            playerWon();
            yield break;
        }

        // Display wave completion message
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

        // Hide Replay button between waves
        if (replayButton != null)
            replayButton.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Pause gameplay
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(
            wavePauseDuration
        );

        // Resume gameplay
        Time.timeScale = 1f;

        clearEnemies();

        // Advance to next wave
        currentWave++;
        enemiesStoppedThisWave = 0;

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

    // Replay button
    public void replayButtonClicked()
    {
        Debug.Log("Replay button clicked!");
        startGame();
    }

    // Player wins
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

    // Player loses
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("GameManager unlocked cursor");
    }

    // Clear existing enemies
    void clearEnemies()
    {
        if (enemiesWrapper == null)
            return;

        for (int i = enemiesWrapper.childCount - 1;
             i >= 0; i--)
        {
            Transform enemy = enemiesWrapper.GetChild(i);

            enemy.SetParent(null);
            Destroy(enemy.gameObject);
        }
    }

    // Manage UI screens
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
    }
}
