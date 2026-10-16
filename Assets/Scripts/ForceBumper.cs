using UnityEngine;

public class ForceBumper : MonoBehaviour
{
    public float launchForce = 10f;

    void OnTriggerEnter(Collider other)
    {
        Rigidbody rb =
            other.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(
                transform.forward * launchForce,
                ForceMode.Impulse
            );
        }
    }
}