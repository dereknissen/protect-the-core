
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class GunController : MonoBehaviour
{
    [Header("Gun Settings")]
    public float damage = 25f;
    public float range = 100f;
    public float fireRate = 0.2f;
    public float bulletSpeed = 40f;

    [Header("Ammo Settings")]
    public int maxAmmo = 10;
    public int currentAmmo = 10;
    public float reloadTime = 2f;

    [Header("References")]
    public Camera playerCamera;
    public ParticleSystem muzzleFlash;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public TMP_Text ammoText;

    private float nextFireTime;
    private bool isReloading = false;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        currentAmmo = maxAmmo;
        UpdateAmmoUI();
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartReload();
        }

        if (Mouse.current == null)
            return;

        if (isReloading)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState == CursorLockMode.Locked &&
            Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();

                currentAmmo--;

                UpdateAmmoUI();

                nextFireTime = Time.time + fireRate;
            }
            else
            {
                Debug.Log("Out of energy! Press R to reload.");
            }
        }
    }

    void StartReload()
    {
        if (isReloading || currentAmmo == maxAmmo)
            return;

        StartCoroutine(Reload());
    }

    IEnumerator Reload()
    {
        isReloading = true;

        if (ammoText != null)
        {
            ammoText.text = "RELOADING...";
            ammoText.color = new Color(1f, 0.75f, 0.1f);
        }

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;

        UpdateAmmoUI();
    }

    void UpdateAmmoUI()
    {
        if (ammoText == null)
            return;

        ammoText.text = currentAmmo + " / " + maxAmmo;

        if (currentAmmo <= 3)
            ammoText.color = Color.red;
        else
            ammoText.color = new Color(0.2f, 0.85f, 1f);
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

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        Vector3 targetPoint = ray.origin + ray.direction * range;

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            if (hit.transform.root != transform.root)
                targetPoint = hit.point;
        }

        Vector3 spawnPosition = firePoint != null
            ? firePoint.position
            : transform.position;

        Vector3 direction =
            (targetPoint - spawnPosition).normalized;

        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.LookRotation(direction));

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
