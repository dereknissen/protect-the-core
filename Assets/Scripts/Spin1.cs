using UnityEngine;

public class Spin1 : MonoBehaviour
{
    [Header("Rotation Setting")]
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 90f;
    public bool isSpinning = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!isSpinning) return;

        transform.Rotate(
            rotationAxis.normalized,
            rotationSpeed * Time.deltaTime,
            Space.Self
        );
    }
}