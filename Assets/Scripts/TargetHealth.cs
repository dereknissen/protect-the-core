
using UnityEngine;

public class TargetHealth : MonoBehaviour
{
    public float health = 100f;

    private bool isDead = false;

    public void TakeDamage(float damage)
    {
        // Prevent damage after the enemy dies
        if (isDead)
            return;

        health -= damage;

        Debug.Log(gameObject.name + " health remaining: " + health);

        if (health <= 0f)
        {
            isDead = true;

            // Find the death effect on this object or its parent
            EnemyDeathEffect deathEffect =
                GetComponentInParent<EnemyDeathEffect>();

            if (deathEffect != null)
            {
                deathEffect.Die();
            }
            else
            {
                // Destroy the enemy if no effect is attached
                Destroy(gameObject);
            }
        }
    }
}
