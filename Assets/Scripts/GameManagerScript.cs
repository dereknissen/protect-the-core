using System.Collections;
using UnityEngine;

public class GameManagerScript : MonoBehaviour
{
    [Header("Game Settings")]
    public int maxLives = 3;
    public int enemiesToStop = 10;

    [Header("Game State")]
    public bool isPlaying;
    public int enemiesStopped;
    public int health;

    [Header("Enemy Spawning")]
    public Transform enemiesWrapper;
    public Transform enemySpawnPoints;
    public GameObject enemyPrefab;
    public float spawnDelay = 2f;

    [Header("UI References")]
    public GameObject hud;
    public GameObject winScreen;
    public GameObject loseScreen;

    private bool isSpawning = false;

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

        // Check winning condition
        if (enemiesStopped >= enemiesToStop)
        {
            isPlaying = false;
            playerWon();
            return;
        }

        // Check number of living enemies
        if (enemiesWrapper == null)
            return;

        int liveEnemies = enemiesWrapper.childCount;

        // Start spawning if there are no enemies
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
        isSpawning = false;

        // Reset game state before clearing enemies
        isPlaying = false;

        clearUIs();
        clearEnemies();

        enemiesStopped = 0;
        health = maxLives;

        // Lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isPlaying = true;
    }

    IEnumerator spawnEnemyWithDelay()
    {
        isSpawning = true;

        // Wait before spawning the next enemy
        yield return new WaitForSeconds(spawnDelay);

        // Make sure the game is still running
        if (isPlaying &&
            enemiesWrapper != null &&
            enemiesWrapper.childCount == 0)
        {
            spawnEnemy();
        }

        isSpawning = false;
    }

    public void replayButtonClicked()
    {
        Debug.Log("Debug button clicked!");
        startGame();
    }

    void spawnEnemy()
    {
        // Make sure spawn points exist
        if (enemySpawnPoints == null ||
            enemySpawnPoints.childCount == 0)
        {
            Debug.LogError("No enemy spawn points assigned!");
            return;
        }

        // Make sure enemy prefab exists
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

        // Select random spawn point
        int randomIndex = Random.Range(
            0,
            enemySpawnPoints.childCount
        );

        Transform spawnPoint =
            enemySpawnPoints.GetChild(randomIndex);

        // Spawn enemy
        GameObject newEnemy = Instantiate(
            enemyPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            enemiesWrapper
        );

        // Assign GameManager to enemy follow script
        EnemyFollowCore enemyFollow =
            newEnemy.GetComponent<EnemyFollowCore>();

        if (enemyFollow != null)
        {
            enemyFollow.gameManagerScript = this;
        }

        // Assign GameManager to enemy death effect script
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
    }

    void playerWon()
    {
        Debug.Log("Player won the game!");

        // Stop enemy spawning
        StopAllCoroutines();
        isSpawning = false;

        // Hide HUD
        if (hud != null)
            hud.SetActive(false);

        // Show win screen
        if (winScreen != null)
            winScreen.SetActive(true);

        // Hide lose screen
        if (loseScreen != null)
            loseScreen.SetActive(false);

        // Unlock cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

   void playerLost()
{
    Debug.Log("Player lost the game!");

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

    void clearEnemies()
    {
        if (enemiesWrapper == null)
            return;

        foreach (Transform enemy in enemiesWrapper)
        {
            // Detach immediately so childCount is accurate
            // when the next spawning coroutine runs.
            enemy.SetParent(null);
            Destroy(enemy.gameObject);
        }
    }

    void clearUIs()
    {
        Debug.Log("Clearing UIs");

        // Show gameplay HUD
        if (hud != null)
            hud.SetActive(true);

        // Hide win screen
        if (winScreen != null)
            winScreen.SetActive(false);

        // Hide lose screen
        if (loseScreen != null)
            loseScreen.SetActive(false);
    }
}