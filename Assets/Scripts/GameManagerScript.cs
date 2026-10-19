
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
        int liveEnemies = enemiesWrapper.childCount;

        // Start spawning if there are no enemies
        if (liveEnemies <= 0 && !isSpawning)
        {
            StartCoroutine(spawnEnemyWithDelay());
        }
    }

    void startGame()
    {
        StopAllCoroutines();
        isSpawning = false;

        clearUIs();
        clearEnemies();

        enemiesStopped = 0;
        health = maxLives;
        isPlaying = true;
    }

    IEnumerator spawnEnemyWithDelay()
    {
        isSpawning = true;

        // Wait before spawning next enemy
        yield return new WaitForSeconds(spawnDelay);

        // Make sure game is still running
        if (isPlaying && enemiesWrapper.childCount == 0)
        {
            spawnEnemy();
        }

        isSpawning = false;
    }


    void spawnEnemy()
    {
        // Make sure spawn points exist
        if (enemySpawnPoints == null || enemySpawnPoints.childCount == 0)
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

        // Select random spawn point
        int randomIndex = Random.Range(0, enemySpawnPoints.childCount);
        Transform spawnPoint = enemySpawnPoints.GetChild(randomIndex);

        // Spawn enemy
        GameObject newEnemy = Instantiate(
            enemyPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            enemiesWrapper
        );

        // Assign GameManager to the enemy's follow script
        EnemyFollowCore enemyFollow = newEnemy.GetComponent<EnemyFollowCore>();

        if (enemyFollow != null)
        {
            enemyFollow.gameManagerScript = this;
        }

        // Initialize enemy health using EnemyDeath
        EnemyDeath enemyDeath = newEnemy.GetComponent<EnemyDeath>();

        if (enemyDeath != null)
        {
            enemyDeath.health = 1;
        }
        else
        {
            Debug.LogWarning("Spawned enemy is missing EnemyDeath!");
        }
    }


    void playerWon()
    {
        Debug.Log("Player won the game!");

        // Display win UI here
    }

    void playerLost()
    {
        Debug.Log("Player lost the game!");

        // Display lose UI here
    }

    void clearEnemies()
    {
        foreach (Transform enemy in enemiesWrapper)
        {
            Destroy(enemy.gameObject);
        }
    }

    void clearUIs()
    {
        Debug.Log("Clearing UIs");

        // Hide win and lose screens here
    }
}
