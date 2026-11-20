
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

    [Header("Burst Attack Settings")]
    public int burstBulletCount = 6;
    public float burstCooldown = 5f;
    public int burstAmmoCost = 3;
    public float burstSpawnDistance = 1f;

    [Header("References")]
    public Camera playerCamera;
    public ParticleSystem muzzleFlash;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public TMP_Text ammoText;

    [Header("Audio Settings")]
    public AudioSource gunAudioSource;

    private float nextFireTime;
    private float nextBurstTime;
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
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                StartReload();
            }

            // Press X for the six-direction burst
            if (Keyboard.current.xKey.wasPressedThisFrame)
            {
                TryBurst();
            }
        }

        if (Mouse.current == null || isReloading)
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

    void PlayGunSound()
    {
        if (gunAudioSource != null &&
            gunAudioSource.clip != null)
        {
            gunAudioSource.PlayOneShot(gunAudioSource.clip);
        }
    }

    void TryBurst()
    {
        if (isReloading)
            return;

        if (Time.time < nextBurstTime)
        {
            Debug.Log("Burst attack is on cooldown!");
            return;
        }

        if (currentAmmo < burstAmmoCost)
        {
            Debug.Log("Not enough ammo for burst!");
            return;
        }

        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning(
                "Assign Bullet Prefab and FirePoint!");
            return;
        }

        FireBurst();

        currentAmmo -= burstAmmoCost;
        UpdateAmmoUI();

        nextBurstTime = Time.time + burstCooldown;
    }

    void FireBurst()
    {
        // Same spawn position as normal shooting
        Vector3 center = firePoint.position;

        // Six different 3D directions
        Vector3[] directions =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right,
            Vector3.up,
            Vector3.down
        };

        foreach (Vector3 direction in directions)
        {
            SpawnBullet(center, direction);
        }

        if (muzzleFlash != null)
            muzzleFlash.Play();

        // Play sound once for the entire burst
        PlayGunSound();

        Debug.Log("6-direction burst fired!");
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
                "Assign Player Camera and Bullet Prefab!");
            return;
        }

        if (muzzleFlash != null)
            muzzleFlash.Play();

        // Play sound when shooting normally
        PlayGunSound();

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        Vector3 targetPoint =
            ray.origin + ray.direction * range;

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

        SpawnBullet(spawnPosition, direction);
    }

    // Shared bullet spawning for regular and burst attacks
    void SpawnBullet(Vector3 spawnPosition, Vector3 direction)
    {
        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.LookRotation(direction));

        // Prevent bullets from colliding with the player
        Collider[] bulletColliders =
            bullet.GetComponentsInChildren<Collider>();

        Collider[] playerColliders =
            transform.root.GetComponentsInChildren<Collider>();

        foreach (Collider bulletCollider in bulletColliders)
        {
            foreach (Collider playerCollider in playerColliders)
            {
                Physics.IgnoreCollision(
                    bulletCollider,
                    playerCollider);
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
