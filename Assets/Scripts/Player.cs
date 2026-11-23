using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private Transform chassisVisual;

    [Header("Aiming")]
    [SerializeField] private Transform turretPivot;
    [SerializeField] private Camera aimCamera;

    [Header("Shooting")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private int maxActiveProjectiles = 5;

    private readonly List<GameObject> activeProjectiles = new List<GameObject>();

    private Rigidbody body;
    private Vector3 moveInput;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Keep the Player/Rigidbody upright and do not rotate it.
        body.constraints = RigidbodyConstraints.FreezeRotation;

        if (aimCamera == null)
            aimCamera = Camera.main;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            moveInput = Vector3.zero;
            return;
        }

        moveInput = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;

        AimTurretAtCursor();

        if (Input.GetMouseButtonDown(0))
            TryFire();
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            return;

        if (moveInput.sqrMagnitude < 0.001f)
            return;

        body.MovePosition(
            body.position + moveInput * moveSpeed * Time.fixedDeltaTime
        );

        if (chassisVisual != null)
        {
            Quaternion targetWorldRotation =
                Quaternion.LookRotation(moveInput, Vector3.up);

            Quaternion targetLocalRotation =
                Quaternion.Inverse(transform.rotation) * targetWorldRotation;

            chassisVisual.localRotation = Quaternion.RotateTowards(
                chassisVisual.localRotation,
                targetLocalRotation,
                turnSpeed * Time.fixedDeltaTime
            );
        }
    }

    private void AimTurretAtCursor()
    {
        if (turretPivot == null || aimCamera == null)
            return;

        Vector3 cursorScreen = Input.mousePosition;
        cursorScreen.z = aimCamera.WorldToScreenPoint(turretPivot.position).z;

        Vector3 cursorWorld = aimCamera.ScreenToWorldPoint(cursorScreen);
        Vector3 direction = cursorWorld - turretPivot.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            turretPivot.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void TryFire()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            return;

        activeProjectiles.RemoveAll(projectile => projectile == null);

        if (activeProjectiles.Count >= maxActiveProjectiles)
            return;

        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogError("Assign the projectile prefab and Fire Point.");
            return;
        }

        GameObject projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            firePoint.rotation
        );

        Projectile behavior = projectile.GetComponent<Projectile>();

        if (behavior == null)
        {
            Debug.LogError("Add Projectile.cs to the projectile prefab.");
            Destroy(projectile);
            return;
        }

        behavior.Fire(firePoint.forward, projectileSpeed);
        activeProjectiles.Add(projectile);
    }
}