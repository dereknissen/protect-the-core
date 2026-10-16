using UnityEngine;

public class DeliveryObject : MonoBehaviour
{
    public Transform resetPoint;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void ResetDelivery()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = resetPoint.position;
        rb.rotation = resetPoint.rotation;
    }
}