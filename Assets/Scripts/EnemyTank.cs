using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyTank : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Player player;
    [SerializeField] private float sightRange = 60f;
    [SerializeField] private float targetHeight = 0.6f;

    [Header("Turret")]
    [SerializeField] private Transform turretPivot;
    [SerializeField] private float turretTurnSpeed = 180f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float directionChangeInterval = 2f;
    [SerializeField] private float wallCheckDistance = 1.5f;

    [Header("Shooting")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float fireCooldown = 1.5f;
    [SerializeField] private float aimTolerance = 12f;
    [SerializeField, Min(1)] private int maxActiveProjectiles = 3;

    [Header("Scoring")]
    [SerializeField] private int scoreValue = 100;
    public int ScoreValue => scoreValue;

    private readonly List<GameObject> activeProjectiles = new List<GameObject>();

    private Rigidbody body;
    private Vector3 moveDirection;
    private Vector3 scanForward;
    private float nextDirectionChangeTime;
    private float nextFireTime;
    private float scanAngle;
    private float scanSign = 1f;
    private float nextPlayerSearchTime;
    private bool hasBeenDefeated;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.constraints |= RigidbodyConstraints.FreezeRotation;

        scanForward = turretPivot != null ? turretPivot.forward : transform.forward;
        scanForward.y = 0f;
        scanForward = scanForward.sqrMagnitude > 0.001f
            ? scanForward.normalized
            : Vector3.forward;
    }

    private void Start()
    {
        FindPlayer();
        ChooseRoamingDirection();

        if (turretPivot == null)
            Debug.LogError($"{name}: Assign Turret Pivot.", this);

        if (firePoint == null)
            Debug.LogError($"{name}: Assign Fire Point.", this);

        if (projectilePrefab == null)
            Debug.LogError($"{name}: Assign Projectile Prefab.", this);
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            return;

        if (player == null && Time.time >= nextPlayerSearchTime)
            FindPlayer();

        UpdateRoaming();

        if (turretPivot == null)
            return;

        if (player == null)
        {
            Scan();
            return;
        }

        Vector3 targetPoint = player.transform.position + Vector3.up * targetHeight;

        if (Vector3.Distance(turretPivot.position, targetPoint) > sightRange)
        {
            Scan();
            return;
        }

        AimAt(targetPoint);

        if (firePoint != null &&
            projectilePrefab != null &&
            !HasWallBetween(firePoint.position, targetPoint))
        {
            TryFire(targetPoint);
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            return;

        if (body != null && moveDirection.sqrMagnitude > 0.001f)
        {
            body.MovePosition(
                body.position + moveDirection * moveSpeed * Time.fixedDeltaTime
            );
        }
    }

    private void FindPlayer()
    {
        player = FindObjectOfType<Player>();
        nextPlayerSearchTime = Time.time + 1f;
    }

    public void Die()
    {
        if (hasBeenDefeated)
            return;

        hasBeenDefeated = true;

        if (GameManager.Instance != null)
            GameManager.Instance.EnemyDefeated(this);

        Destroy(gameObject);
    }

    private bool HasWallBetween(Vector3 origin, Vector3 target)
    {
        Vector3 delta = target - origin;
        float distance = delta.magnitude;

        if (distance <= 0.01f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            delta / distance,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            if (hit.collider.CompareTag("Wall"))
                return true;
        }

        return false;
    }

    private void AimAt(Vector3 targetPoint)
    {
        Vector3 direction = targetPoint - turretPivot.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion wantedRotation = Quaternion.LookRotation(direction.normalized);
        turretPivot.rotation = Quaternion.RotateTowards(
            turretPivot.rotation,
            wantedRotation,
            turretTurnSpeed * Time.deltaTime
        );
    }

    private void TryFire(Vector3 targetPoint)
    {
        activeProjectiles.RemoveAll(projectile => projectile == null);

        if (activeProjectiles.Count >= maxActiveProjectiles ||
            Time.time < nextFireTime)
        {
            return;
        }

        Vector3 direction = targetPoint - firePoint.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        if (Vector3.Angle(turretPivot.forward, direction) > aimTolerance)
            return;

        targetPoint = player.transform.position + Vector3.up * targetHeight;

        if (HasWallBetween(firePoint.position, targetPoint))
            return;

        direction = targetPoint - firePoint.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        Transform projectileParent = transform.root;

        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentLevelInstance != null)
        {
            projectileParent = GameManager.Instance.CurrentLevelInstance.transform;
        }

        GameObject projectile = Instantiate(
            projectilePrefab,
            firePoint.position,
            Quaternion.LookRotation(direction, Vector3.up),
            projectileParent
        );

        Projectile projectileScript = projectile.GetComponent<Projectile>();

        if (projectileScript == null)
        {
            Debug.LogError(
                $"{name}: Projectile prefab must have Projectile.cs on its root object.",
                projectile
            );
            Destroy(projectile);
            return;
        }

        IgnoreOwnCollisions(projectile);
        projectileScript.Fire(direction, projectileSpeed);

        activeProjectiles.Add(projectile);
        nextFireTime = Time.time + fireCooldown;
    }

    private void IgnoreOwnCollisions(GameObject projectile)
    {
        Collider[] tankColliders = GetComponentsInChildren<Collider>();
        Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>();

        foreach (Collider tankCollider in tankColliders)
        {
            foreach (Collider projectileCollider in projectileColliders)
            {
                if (tankCollider != null && projectileCollider != null)
                    Physics.IgnoreCollision(tankCollider, projectileCollider);
            }
        }
    }

    private void Scan()
    {
        scanAngle += scanSign * 90f * Time.deltaTime;

        if (scanAngle >= 120f)
        {
            scanAngle = 120f;
            scanSign = -1f;
        }
        else if (scanAngle <= -120f)
        {
            scanAngle = -120f;
            scanSign = 1f;
        }

        Vector3 direction = Quaternion.AngleAxis(scanAngle, Vector3.up) * scanForward;
        turretPivot.rotation = Quaternion.RotateTowards(
            turretPivot.rotation,
            Quaternion.LookRotation(direction, Vector3.up),
            90f * Time.deltaTime
        );
    }

    private void UpdateRoaming()
    {
        if (Time.time >= nextDirectionChangeTime)
            ChooseRoamingDirection();

        if (moveDirection.sqrMagnitude < 0.001f)
            return;

        Vector3 origin = body.position + Vector3.up * 0.5f;

        if (Physics.Raycast(
                origin,
                moveDirection,
                out RaycastHit hit,
                wallCheckDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)
            && hit.collider.CompareTag("Wall"))
        {
            Vector3 away = Vector3.ProjectOnPlane(hit.normal, Vector3.up);

            if (away.sqrMagnitude > 0.001f)
                moveDirection = away.normalized;
            else
                ChooseRoamingDirection();

            nextDirectionChangeTime = Time.time + directionChangeInterval;
        }
    }

    private void ChooseRoamingDirection()
    {
        moveDirection = Random.insideUnitSphere;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude < 0.01f)
            moveDirection = Vector3.forward;

        moveDirection.Normalize();
        nextDirectionChangeTime = Time.time + directionChangeInterval;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Wall"))
            ChooseRoamingDirection();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.collider.CompareTag("Wall"))
            ChooseRoamingDirection();
    }
}