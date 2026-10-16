
using UnityEngine;

public class EnemyFollowCore : MonoBehaviour
{
    [Header("Target")]
    public Transform core;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float stoppingDistance = 1.5f;

    [Header("Rotation")]
    public bool faceCore = true;

    void Update()
    {
        // Find the Core automatically if one isn't assigned
        if (core == null)
        {
            GameObject coreObject = GameObject.Find("Core");

            if (coreObject != null)
                core = coreObject.transform;
            else
                return;
        }

        // Calculate direction toward the Core
        Vector3 direction = core.position - transform.position;
        direction.y = 0f;

        // Stop when close enough
        if (direction.magnitude <= stoppingDistance)
            return;

        // Move toward the Core
        transform.position +=
            direction.normalized * moveSpeed * Time.deltaTime;

        // Face the Core
        if (faceCore && direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                10f * Time.deltaTime
            );
        }
    }
}
