using UnityEngine;

public class RallyWheelVisuals : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;

    [Header("Visible Wheels")]
    public Transform visualFL;
    public Transform visualFR;
    public Transform visualRL;
    public Transform visualRR;

    private Quaternion offsetFL;
    private Quaternion offsetFR;
    private Quaternion offsetRL;
    private Quaternion offsetRR;

    private void Start()
    {
        offsetFL = GetRotationOffset(wheelFL, visualFL);
        offsetFR = GetRotationOffset(wheelFR, visualFR);
        offsetRL = GetRotationOffset(wheelRL, visualRL);
        offsetRR = GetRotationOffset(wheelRR, visualRR);
    }

    private void LateUpdate()
    {
        UpdateWheel(wheelFL, visualFL, offsetFL);
        UpdateWheel(wheelFR, visualFR, offsetFR);
        UpdateWheel(wheelRL, visualRL, offsetRL);
        UpdateWheel(wheelRR, visualRR, offsetRR);
    }

    private Quaternion GetRotationOffset(
        WheelCollider wheelCollider,
        Transform wheelVisual)
    {
        if (wheelCollider == null || wheelVisual == null)
            return Quaternion.identity;

        wheelCollider.GetWorldPose(
            out Vector3 position,
            out Quaternion rotation
        );

        return Quaternion.Inverse(rotation) *
               wheelVisual.rotation;
    }

    private void UpdateWheel(
        WheelCollider wheelCollider,
        Transform wheelVisual,
        Quaternion rotationOffset)
    {
        if (wheelCollider == null || wheelVisual == null)
            return;

        wheelCollider.GetWorldPose(
            out Vector3 position,
            out Quaternion rotation
        );

        wheelVisual.position = position;
        wheelVisual.rotation =
            rotation * rotationOffset;
    }
}