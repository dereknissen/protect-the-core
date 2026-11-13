
using System.Collections;
using UnityEngine;

public class MedPackSpawner : MonoBehaviour
{
    [Header("Med Pack")]
    public GameObject medPackPrefab;

    [Header("Spawn Points")]
    public Transform spawnPoints;

    [Header("Random Spawn Timing")]
    public float minimumSpawnTime = 15f;
    public float maximumSpawnTime = 35f;

    private GameObject currentMedPack;

    private void Start()
    {
        StartCoroutine(SpawnMedPacks());
    }

    IEnumerator SpawnMedPacks()
    {
        while (true)
        {
            // Wait a random amount of time
            float waitTime = Random.Range(
                minimumSpawnTime,
                maximumSpawnTime
            );

            yield return new WaitForSeconds(waitTime);

            // Only spawn if there isn't already a med pack
            if (currentMedPack == null)
            {
                SpawnMedPack();
            }
        }
    }

    void SpawnMedPack()
    {
        if (medPackPrefab == null)
        {
            Debug.LogError("Med Pack Prefab is missing!");
            return;
        }

        if (spawnPoints == null || spawnPoints.childCount == 0)
        {
            Debug.LogError("No med pack spawn points assigned!");
            return;
        }

        // Pick a random spawn point
        int randomIndex = Random.Range(
            0,
            spawnPoints.childCount
        );

        Transform selectedPoint = spawnPoints.GetChild(randomIndex);

        // Spawn the med pack
        currentMedPack = Instantiate(
            medPackPrefab,
            selectedPoint.position,
            selectedPoint.rotation
        );

        Debug.Log("Med pack spawned at: " + selectedPoint.name);
    }
}
