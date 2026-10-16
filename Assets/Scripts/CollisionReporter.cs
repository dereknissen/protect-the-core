using UnityEngine;

public class CollisionReporter : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log(
            "Collision with: " +
            collision.gameObject.name
        );
    }
}