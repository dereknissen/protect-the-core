using System;
using Unity.VectorGraphics;
using UnityEngine;

public class GameManagerScript : MonoBehaviour
{
    // Game settings
    public int maxLives;
    public int enemiesToStop;

    // Initialize vars
    public bool isPlaying;
    public int enemiesStopped;
    public int health;
    public Transform enemiesWrapper;

    // This runs when the sccene is started
    void Start()
    {
        startGame(); // automatically start the game for the player
    }

    // Update is called once per frame
    void Update()
    {
       // check for losing condition
       if (health <= 0)
        {
            isPlaying = false; // stop the game
            playerLost(); // start the lose sequence
        }

       // check for winning condition
       if (enemiesStopped >= enemiesToStop)
        {
            isPlaying = false; // stop the game
            playerWon(); // start the win sequence
        }
    }

    public void startGame()
    {
        clearUIs();
        clearEnemies();
        isPlaying = true;
        enemiesStopped = 0;
        health = maxLives; // 3 lives (player loses when 3 enemies reach the columns)
    }

    private void playerWon()
    {
        Console.WriteLine("Player won the game!");
        // display the win UI
    }

    private void playerLost()
    {
        Console.WriteLine("Player lost the game!");
        // display the lost UI
    }

    private void clearEnemies()
    {
        foreach (Transform enemy in enemiesWrapper)
        {
            Destroy(enemy);
        }
    }
    // This method is supposed to destroy all of the UIs like "You won the game" or "You lost the game" when the game is restarted
    private void clearUIs()
    {
        Console.WriteLine("Clearing UIs");
    }
}
