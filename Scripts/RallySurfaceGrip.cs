using UnityEngine;

public class RallySurfaceGrip : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;

    [Header("Asphalt")]
    public float asphaltForwardGrip = 1.22f;
    public float asphaltFrontSideGrip = 1.18f;
    public float asphaltRearSideGrip = 1.08f;
    public float asphaltDriveMultiplier = 1.00f;
    public float asphaltSpeedMultiplier = 1.00f;
    public float asphaltResistance = 2f;

    [Header("Gravel")]
    public float gravelForwardGrip = 1.10f;
    public float gravelFrontSideGrip = 1.05f;
    public float gravelRearSideGrip = 0.92f;
    public float gravelDriveMultiplier = 0.88f;
    public float gravelSpeedMultiplier = 0.92f;
    public float gravelResistance = 10f;

    [Header("Dirt")]
    public float dirtForwardGrip = 0.95f;
    public float dirtFrontSideGrip = 0.90f;
    public float dirtRearSideGrip = 0.78f;
    public float dirtDriveMultiplier = 0.75f;
    public float dirtSpeedMultiplier = 0.85f;
    public float dirtResistance = 18f;

    [Header("Grass")]
    public float grassForwardGrip = 0.80f;
    public float grassFrontSideGrip = 0.76f;
    public float grassRearSideGrip = 0.65f;
    public float grassDriveMultiplier = 0.65f;
    public float grassSpeedMultiplier = 0.75f;
    public float grassResistance = 24f;

    [Header("Debug")]
    public string currentSurface = "Unknown";
    public string currentCollider = "None";

    public float CurrentDriveMultiplier
    {
        get;
        private set;
    } = 1f;

    public float CurrentSpeedMultiplier
    {
        get;
        private set;
    } = 1f;

    public float CurrentRollingResistance
    {
        get;
        private set;
    } = 2f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float driveTotal = 0f;
        float speedTotal = 0f;
        float resistanceTotal = 0f;

        int groundedWheels = 0;

        string detectedSurface = "Unknown";

        UpdateWheel(
            wheelFL,
            true,
            ref driveTotal,
            ref speedTotal,
            ref resistanceTotal,
            ref groundedWheels,
            ref detectedSurface
        );

        UpdateWheel(
            wheelFR,
            true,
            ref driveTotal,
            ref speedTotal,
            ref resistanceTotal,
            ref groundedWheels,
            ref detectedSurface
        );

        UpdateWheel(
            wheelRL,
            false,
            ref driveTotal,
            ref speedTotal,
            ref resistanceTotal,
            ref groundedWheels,
            ref detectedSurface
        );

        UpdateWheel(
            wheelRR,
            false,
            ref driveTotal,
            ref speedTotal,
            ref resistanceTotal,
            ref groundedWheels,
            ref detectedSurface
        );

        if (groundedWheels > 0)
        {
            CurrentDriveMultiplier =
                driveTotal / groundedWheels;

            CurrentSpeedMultiplier =
                speedTotal / groundedWheels;

            CurrentRollingResistance =
                resistanceTotal / groundedWheels;

            currentSurface =
                detectedSurface;
        }
        else
        {
            currentSurface = "Unknown";
        }

        ApplyRollingResistance();
    }

    private void UpdateWheel(
        WheelCollider wheel,
        bool isFrontWheel,
        ref float driveTotal,
        ref float speedTotal,
        ref float resistanceTotal,
        ref int groundedWheels,
        ref string detectedSurface)
    {
        if (wheel == null)
            return;

        if (!wheel.GetGroundHit(out WheelHit hit))
            return;

        currentCollider = hit.collider.name;

        // -------------------------------------------------
        // ORIGINAL RALLY SURFACE SYSTEM
        // -------------------------------------------------

        RallySurface roadSurface =
            hit.collider.GetComponent<RallySurface>();

        if (roadSurface != null)
        {
            ApplyRoadSurface(
                wheel,
                isFrontWheel,
                roadSurface.surfaceType,
                ref driveTotal,
                ref speedTotal,
                ref resistanceTotal
            );

            detectedSurface =
                roadSurface.surfaceType.ToString();

            groundedWheels++;
            return;
        }

        // -------------------------------------------------
        // ASSET STORE TRACK
        // Detect surface from collider name.
        // -------------------------------------------------

        string colliderName =
            hit.collider.name.ToUpperInvariant();

        // Asphalt
        // Examples:
        // 1TARMAC_oval
        // 1TARMAC_inner
        if (
            colliderName.Contains("TARMAC") ||
            colliderName.Contains("PITLANE") ||
            colliderName.Contains("CONCRETE") ||
            colliderName.Contains("KERB")
            )
        {
            ApplyRoadSurface(
                wheel,
                isFrontWheel,
                RallySurfaceType.Asphalt,
                ref driveTotal,
                ref speedTotal,
                ref resistanceTotal
            );

            detectedSurface = "Asphalt";
        }

        // Gravel
        // Example:
        // 1GRAVEL
        else if (colliderName.Contains("GRAVEL"))
        {
            ApplyRoadSurface(
                wheel,
                isFrontWheel,
                RallySurfaceType.Gravel,
                ref driveTotal,
                ref speedTotal,
                ref resistanceTotal
            );

            detectedSurface = "Gravel";
        }

        // Grass
        // Examples:
        // 1GRASS
        // 1GRASS_2
        else if (colliderName.Contains("GRASS"))
        {
            ApplyGrass(
                wheel,
                isFrontWheel,
                ref driveTotal,
                ref speedTotal,
                ref resistanceTotal
            );

            detectedSurface = "Grass";
        }

        // Keep compatibility with our old Unity Terrain.
        else if (
            hit.collider.GetComponent<TerrainCollider>()
            != null)
        {
            ApplyGrass(
                wheel,
                isFrontWheel,
                ref driveTotal,
                ref speedTotal,
                ref resistanceTotal
            );

            detectedSurface = "Grass";
        }

        // Wall, barrier, road edge, fence, etc.
        else
        {
            return;
        }

        groundedWheels++;
    }

    private void ApplyRoadSurface(
        WheelCollider wheel,
        bool isFrontWheel,
        RallySurfaceType type,
        ref float driveTotal,
        ref float speedTotal,
        ref float resistanceTotal)
    {
        switch (type)
        {
            case RallySurfaceType.Asphalt:

                ApplyValues(
                    wheel,
                    isFrontWheel,
                    asphaltForwardGrip,
                    asphaltFrontSideGrip,
                    asphaltRearSideGrip
                );

                driveTotal +=
                    asphaltDriveMultiplier;

                speedTotal +=
                    asphaltSpeedMultiplier;

                resistanceTotal +=
                    asphaltResistance;

                break;

            case RallySurfaceType.Gravel:

                ApplyValues(
                    wheel,
                    isFrontWheel,
                    gravelForwardGrip,
                    gravelFrontSideGrip,
                    gravelRearSideGrip
                );

                driveTotal +=
                    gravelDriveMultiplier;

                speedTotal +=
                    gravelSpeedMultiplier;

                resistanceTotal +=
                    gravelResistance;

                break;

            case RallySurfaceType.Dirt:

                ApplyValues(
                    wheel,
                    isFrontWheel,
                    dirtForwardGrip,
                    dirtFrontSideGrip,
                    dirtRearSideGrip
                );

                driveTotal +=
                    dirtDriveMultiplier;

                speedTotal +=
                    dirtSpeedMultiplier;

                resistanceTotal +=
                    dirtResistance;

                break;
        }
    }

    private void ApplyGrass(
        WheelCollider wheel,
        bool isFrontWheel,
        ref float driveTotal,
        ref float speedTotal,
        ref float resistanceTotal)
    {
        ApplyValues(
            wheel,
            isFrontWheel,
            grassForwardGrip,
            grassFrontSideGrip,
            grassRearSideGrip
        );

        driveTotal +=
            grassDriveMultiplier;

        speedTotal +=
            grassSpeedMultiplier;

        resistanceTotal +=
            grassResistance;
    }

    private void ApplyValues(
        WheelCollider wheel,
        bool isFrontWheel,
        float forwardGrip,
        float frontSideGrip,
        float rearSideGrip)
    {
        WheelFrictionCurve forward =
            wheel.forwardFriction;

        forward.stiffness =
            forwardGrip;

        wheel.forwardFriction =
            forward;

        WheelFrictionCurve sideways =
            wheel.sidewaysFriction;

        sideways.stiffness =
            isFrontWheel
                ? frontSideGrip
                : rearSideGrip;

        wheel.sidewaysFriction =
            sideways;
    }

    private void ApplyRollingResistance()
    {
        if (rb == null)
            return;

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(
                rb.linearVelocity,
                Vector3.up
            );

        rb.AddForce(
            -horizontalVelocity *
            CurrentRollingResistance,
            ForceMode.Force
        );
    }
}