using UnityEngine;

public class RallyCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Position")]
    public Vector3 offset =
        new Vector3(0f, 3.5f, -6.5f);

    [Header("Camera Movement")]
    public float positionSmoothness = 5f;
    public float rotationSmoothness = 7f;

    [Header("Camera Collision")]
    [Tooltip("Thickness of the camera collision check.")]
    public float collisionRadius = 0.35f;

    [Tooltip("Small gap kept between camera and obstacle.")]
    public float collisionPadding = 0.15f;

    [Tooltip("Camera will not be pulled closer than this.")]
    public float minimumDistanceFromTarget = 1.0f;

    [Tooltip("Height from the car used as the collision-check origin.")]
    public float collisionOriginHeight = 1.2f;

    [Tooltip("Which layers can block the camera.")]
    public LayerMask collisionMask = ~0;

    private readonly RaycastHit[] collisionHits =
        new RaycastHit[32];

    private void LateUpdate()
    {
        if (target == null)
            return;

        // Where the camera normally wants to be.
        Vector3 desiredPosition =
            target.TransformPoint(offset);

        // Start the collision check a little above
        // the centre of the car.
        Vector3 collisionOrigin =
            target.position +
            Vector3.up * collisionOriginHeight;

        Vector3 toCamera =
            desiredPosition - collisionOrigin;

        float desiredDistance =
            toCamera.magnitude;

        bool blocked = false;

        if (desiredDistance > 0.001f)
        {
            Vector3 direction =
                toCamera / desiredDistance;

            int hitCount =
                Physics.SphereCastNonAlloc(
                    collisionOrigin,
                    collisionRadius,
                    direction,
                    collisionHits,
                    desiredDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore
                );

            float closestDistance =
                desiredDistance;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit =
                    collisionHits[i];

                if (hit.collider == null)
                    continue;

                // Ignore the car itself.
                if (IsTargetCollider(hit.collider))
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance =
                        hit.distance;

                    blocked = true;
                }
            }

            if (blocked)
            {
                float safeDistance =
                    closestDistance -
                    collisionPadding;

                safeDistance =
                    Mathf.Clamp(
                        safeDistance,
                        minimumDistanceFromTarget,
                        desiredDistance
                    );

                desiredPosition =
                    collisionOrigin +
                    direction * safeDistance;
            }
        }

        if (blocked)
        {
            // Move immediately in front of an obstacle.
            // This prevents the camera from passing through
            // bridges, walls, buildings, etc.
            transform.position =
                desiredPosition;
        }
        else
        {
            // When the obstacle disappears, smoothly move
            // back to the normal follow position.
            float positionT =
                1f -
                Mathf.Exp(
                    -positionSmoothness *
                    Time.deltaTime
                );

            transform.position =
                Vector3.Lerp(
                    transform.position,
                    desiredPosition,
                    positionT
                );
        }

        Vector3 lookDirection =
            target.position -
            transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation =
                Quaternion.LookRotation(
                    lookDirection
                );

            float rotationT =
                1f -
                Mathf.Exp(
                    -rotationSmoothness *
                    Time.deltaTime
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    rotationT
                );
        }
    }

    private bool IsTargetCollider(
        Collider collider)
    {
        Transform colliderTransform =
            collider.transform;

        if (colliderTransform == target)
            return true;

        if (colliderTransform.IsChildOf(target))
            return true;

        return false;
    }
}