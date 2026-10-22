using UnityEngine;

public class GhostCar : MonoBehaviour
{
    [Header("Target Data")]
    public Transform target;

    [Header("Smoothing")]
    public float positionSmoothness = 12f;
    public float rotationSmoothness = 12f;

    private void Update()
    {
        if (target == null)
            return;

        float positionT =
            1f - Mathf.Exp(
                -positionSmoothness *
                Time.deltaTime
            );

        float rotationT =
            1f - Mathf.Exp(
                -rotationSmoothness *
                Time.deltaTime
            );

        transform.position =
            Vector3.Lerp(
                transform.position,
                target.position,
                positionT
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                target.rotation,
                rotationT
            );
    }
}