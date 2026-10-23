using UnityEngine;
using UnityEngine.InputSystem;

public class GunController : MonoBehaviour
{
    [Header("Gun Settings")]
    public float damage = 25f;
    public float range = 100f;
    public float fireRate = 0.2f;
    public float bulletSpeed = 40f;

    [Header("References")]
    public Camera playerCamera;
    public ParticleSystem muzzleFlash;
    public Transform firePoint;
    public GameObject bulletPrefab;

    private float nextFireTime;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState == CursorLockMode.Locked &&
            Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    void Shoot()
    {
        if (playerCamera == null || bulletPrefab == null)
        {
            Debug.LogWarning(
                "Assign Player Camera and Bullet Prefab in the Inspector!");
            return;
        }

        if (muzzleFlash != null)
            muzzleFlash.Play();

        // Aim toward the center of the crosshair
        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        Vector3 targetPoint = ray.origin + ray.direction * range;

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            if (hit.transform.root != transform.root)
                targetPoint = hit.point;
        }

        // Spawn the bullet at the gun barrel
        Vector3 spawnPosition = firePoint != null
            ? firePoint.position
            : transform.position;

        Vector3 direction =
            (targetPoint - spawnPosition).normalized;

        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.LookRotation(direction));

        // Prevent the bullet from hitting the player
        Collider bulletCollider = bullet.GetComponent<Collider>();

        Collider[] playerColliders =
            transform.root.GetComponentsInChildren<Collider>();

        if (bulletCollider != null)
        {
            foreach (Collider playerCollider in playerColliders)
            {
                Physics.IgnoreCollision(
                    bulletCollider, playerCollider);
            }
        }

        // Launch the projectile
        BulletProjectile projectile =
            bullet.GetComponent<BulletProjectile>();

        if (projectile != null)
        {
            projectile.damage = damage;
            projectile.Launch(direction);
        }
        else
        {
            Debug.LogWarning(
                "Your Bullet prefab needs a BulletProjectile script!");
        }
    }
}