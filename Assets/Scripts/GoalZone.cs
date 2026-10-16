using UnityEngine;

public class GoalZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DeliveryObject"))
        {
            Debug.Log("Successful Delivery!");

            DeliveryObject delivery =
                 other.GetComponent<DeliveryObject>();

            if (delivery != null)
            {
                delivery.ResetDelivery();
            }
        }
    }
}