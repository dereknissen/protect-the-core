using UnityEngine;

public class HazardZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DeliveryObject"))
        {
            Debug.Log("Delivery Failed!");

            DeliveryObject delivery =
                 other.GetComponent<DeliveryObject>();

            if (delivery != null)
            {
                delivery.ResetDelivery();
            }
        }
    }
}