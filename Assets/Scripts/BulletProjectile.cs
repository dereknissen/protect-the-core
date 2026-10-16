
using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    public float speed = 40f;
    public float damage = 25f;
    public float lifeTime = 3f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Destroy(gameObject, lifeTime);
    }

    public void Launch(Vector3 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
    }

    void OnCollisionEnter(Collision collision)
    {
        TargetHealth target =
            collision.gameObject.GetComponentInParent<TargetHealth>();

        if (target != null)
        {
            target.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
