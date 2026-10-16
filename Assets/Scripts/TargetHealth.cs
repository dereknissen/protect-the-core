
using UnityEngine;

public class TargetHealth : MonoBehaviour
{
    public float health = 100f;

    public void TakeDamage(float damage)
    {
        health -= damage;

        Debug.Log(gameObject.name + " health remaining: " + health);

        if (health <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
