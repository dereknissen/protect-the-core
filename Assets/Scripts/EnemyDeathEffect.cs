
using System.Collections;
using UnityEngine;

public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Death Effect")]
    public float effectDuration = 0.5f;
    public float growMultiplier = 1.8f;
    public Color flashColor = Color.cyan;

    private bool isDying = false;
    private Vector3 originalScale;
    private Renderer[] enemyRenderers;
    private Color[] originalColors;
    private MaterialPropertyBlock propertyBlock;

    public GameManagerScript gameManagerScript;

    void Awake()
    {
        originalScale = transform.localScale;

        enemyRenderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[enemyRenderers.Length];

        propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < enemyRenderers.Length; i++)
        {
            originalColors[i] = Color.white;

            Material[] materials = enemyRenderers[i].sharedMaterials;

            if (materials.Length > 0 && materials[0] != null)
            {
                if (materials[0].HasProperty("_BaseColor"))
                    originalColors[i] = materials[0].GetColor("_BaseColor");
                else if (materials[0].HasProperty("_Color"))
                    originalColors[i] = materials[0].GetColor("_Color");
            }
        }
    }

    public void Die()
    {
        if (isDying)
            return;

        isDying = true;

        gameManagerScript.enemiesStopped++;

        // Stop enemy movement scripts during the effect.
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            if (script != this)
                script.enabled = false;
        }

        // Stop physics movement if the enemy has a Rigidbody.
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        StartCoroutine(PlayDeathEffect());
    }

    IEnumerator PlayDeathEffect()
    {
        float elapsed = 0f;

        while (elapsed < effectDuration)
        {
            float progress = elapsed / effectDuration;

            // Grow larger over time.
            transform.localScale = Vector3.Lerp(
                originalScale,
                originalScale * growMultiplier,
                progress
            );

            // Flash brightly, then fade back toward the original color.
            Color currentColor = Color.Lerp(
                flashColor,
                Color.white,
                progress
            );

            for (int i = 0; i < enemyRenderers.Length; i++)
            {
                Renderer rend = enemyRenderers[i];

                if (rend == null)
                    continue;

                rend.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", currentColor);
                propertyBlock.SetColor("_Color", currentColor);
                rend.SetPropertyBlock(propertyBlock);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
