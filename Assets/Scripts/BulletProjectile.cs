using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    public float speed = 40f;
    public float damage = 25f;
    public float lifeTime = 2f;

    private Rigidbody rb;
    private Vector3 previousPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Destroy(gameObject, lifeTime);
    }

    void Start()
    {
        previousPosition = transform.position;
    }

    public void Launch(Vector3 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
    }

    void FixedUpdate()
    {
        Vector3 currentPosition = rb.position;
        Vector3 movement = currentPosition - previousPosition;

        if (movement.sqrMagnitude > 0.000001f)
        {
            float distance = movement.magnitude;

            RaycastHit[] hits = Physics.SphereCastAll(
                previousPosition,
                0.075f,
                movement.normalized,
                distance,
                ~0,
                QueryTriggerInteraction.Collide
            );

            foreach (RaycastHit hit in hits)
            {
                TargetHealth target =
                    hit.collider.GetComponentInParent<TargetHealth>();

                if (target != null)
                {
                    Debug.Log("Enemy detected by SphereCast!");

                    target.TakeDamage(damage);
                    Destroy(gameObject);
                    return;
                }
            }
        }

        previousPosition = currentPosition;
    }

    void OnCollisionEnter(Collision collision)
    {
        CheckForEnemy(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        CheckForEnemy(other.gameObject);
    }

    void CheckForEnemy(GameObject hitObject)
    {
        TargetHealth target =
            hitObject.GetComponentInParent<TargetHealth>();

        if (target != null)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}