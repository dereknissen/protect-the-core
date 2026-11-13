
using UnityEngine;

public class MedPackPickup : MonoBehaviour
{
    [Header("Healing Settings")]
    public int livesRestored = 1;

    [HideInInspector]
    public GameManagerScript gameManager;

    private bool collected = false;

    private void Awake()
    {
        // Automatically find the GameManager
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManagerScript>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        Debug.Log("Med pack touched by: " + other.name);

        // Search the collider and its parents for Player tag
        Transform playerTransform = other.transform;

        while (playerTransform != null)
        {
            if (playerTransform.CompareTag("Player"))
            {
                Debug.Log("PLAYER FOUND: " + playerTransform.name);

                CollectMedPack();
                return;
            }

            playerTransform = playerTransform.parent;
        }
    }

    void CollectMedPack()
    {
        // Try finding GameManager again if necessary
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManagerScript>();
        }

        if (gameManager == null)
        {
            Debug.LogError("Could not find GameManager in the scene!");
            return;
        }

        Debug.Log("GameManager successfully found!");

        bool healed = gameManager.RestoreCore(livesRestored);

        if (healed)
        {
            collected = true;

            Debug.Log("MED PACK COLLECTED! Core health restored!");

            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Med pack could not heal. Core may already be full.");
        }
    }
}
