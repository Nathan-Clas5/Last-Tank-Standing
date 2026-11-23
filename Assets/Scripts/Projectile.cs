using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private int wallBounces = 1;
    [SerializeField] private float skinWidth = 0.15f;

    private Rigidbody body;
    private int bouncesLeft;
    private float speed;
    private Vector3 moveDirection;
    private bool isFired;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;

        Collider projectileCollider = GetComponent<Collider>();
        projectileCollider.isTrigger = true;

        bouncesLeft = wallBounces;
    }

    public void Fire(Vector3 direction, float projectileSpeed)
    {
        speed = projectileSpeed;
        direction.y = 0f;
        moveDirection = direction.normalized;

        FaceDirection(moveDirection);
        isFired = true;
    }

    private void FixedUpdate()
    {
        if (!isFired)
            return;

        float moveDistance = speed * Time.fixedDeltaTime;

        if (Physics.Raycast(
                body.position,
                moveDirection,
                out RaycastHit hit,
                moveDistance + skinWidth,
                ~0,
                QueryTriggerInteraction.Collide))
        {
            EnemyTank enemy = hit.collider.GetComponentInParent<EnemyTank>();

            if (enemy != null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.AddScore(enemy.ScoreValue);

                enemy.Die();
                Destroy(gameObject);
                return;
            }

            Player player = hit.collider.GetComponentInParent<Player>();

            if (player != null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.GameOver();

                Destroy(player.gameObject);
                Destroy(gameObject);
                return;
            }

            if (hit.collider.CompareTag("Wall"))
            {
                if (bouncesLeft <= 0)
                {
                    Destroy(gameObject);
                    return;
                }

                bouncesLeft--;

                Vector3 normal = hit.normal;
                normal.y = 0f;

                if (normal.sqrMagnitude > 0.001f)
                {
                    normal.Normalize();
                    moveDirection = Vector3.Reflect(moveDirection, normal).normalized;
                    FaceDirection(moveDirection);

                    Vector3 bouncePoint = hit.point + normal * skinWidth;
                    bouncePoint.y = body.position.y;
                    body.MovePosition(bouncePoint);
                }

                return;
            }
        }

        body.MovePosition(body.position + moveDirection * moveDistance);
    }

    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}