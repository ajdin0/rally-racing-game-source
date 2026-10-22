using UnityEngine;

public class RallyAntiRoll : MonoBehaviour
{
    [Header("Front Axle")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;

    [Header("Rear Axle")]
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Anti-Roll Strength")]
    public float frontAntiRoll = 7000f;
    public float rearAntiRoll = 5500f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        ApplyAntiRoll(
            frontLeft,
            frontRight,
            frontAntiRoll
        );

        ApplyAntiRoll(
            rearLeft,
            rearRight,
            rearAntiRoll
        );
    }

    private void ApplyAntiRoll(
        WheelCollider leftWheel,
        WheelCollider rightWheel,
        float antiRollStrength)
    {
        if (leftWheel == null || rightWheel == null)
            return;

        float leftTravel = 1f;
        float rightTravel = 1f;

        bool leftGrounded =
            leftWheel.GetGroundHit(out WheelHit leftHit);

        bool rightGrounded =
            rightWheel.GetGroundHit(out WheelHit rightHit);

        if (leftGrounded)
        {
            Vector3 localHit =
                leftWheel.transform.InverseTransformPoint(
                    leftHit.point
                );

            leftTravel =
                (-localHit.y - leftWheel.radius)
                / leftWheel.suspensionDistance;
        }

        if (rightGrounded)
        {
            Vector3 localHit =
                rightWheel.transform.InverseTransformPoint(
                    rightHit.point
                );

            rightTravel =
                (-localHit.y - rightWheel.radius)
                / rightWheel.suspensionDistance;
        }

        float antiRollForce =
            (leftTravel - rightTravel)
            * antiRollStrength;

        if (leftGrounded)
        {
            rb.AddForceAtPosition(
                leftWheel.transform.up * -antiRollForce,
                leftWheel.transform.position
            );
        }

        if (rightGrounded)
        {
            rb.AddForceAtPosition(
                rightWheel.transform.up * antiRollForce,
                rightWheel.transform.position
            );
        }
    }
}