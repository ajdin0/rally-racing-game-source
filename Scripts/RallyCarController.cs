using UnityEngine;
using UnityEngine.InputSystem;

public class RallyCarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;

    [Header("Engine")]
    public float maxMotorTorque = 1200f;
    public float maxForwardSpeed = 160f;
    public float maxReverseSpeed = 40f;

    [Header("Brakes")]
    public float brakeTorque = 3400f;
    public float handbrakeTorque = 5500f;
    public float engineBrakeTorque = 220f;

    [Header("Steering")]
    public float maxSteerAngle = 24f;
    public float steeringResponse = 7f;

    [Tooltip("Below this speed the car can use the full steering angle.")]
    public float fullSteerUntilKmh = 35f;

    [Tooltip("At this speed the steering reaches the high-speed minimum.")]
    public float highSpeedSteerAtKmh = 140f;

    [Tooltip("Maximum steering angle at very high speed.")]
    public float highSpeedSteerAngle = 9f;

    [Header("Arcade Assists")]
    public float turnAssist = 1.6f;
    public float lateralStability = 0.65f;
    public float straighteningAssist = 1.5f;
    public float downforce = 0.03f;

    [Header("High-Speed Turn Assist")]
    public float turnAssistFadeStartKmh = 70f;
    public float turnAssistFadeEndKmh = 140f;

    [Range(0f, 1f)]
    public float highSpeedTurnAssistMultiplier = 0.45f;

    [Header("Launch Boost")]
    [Tooltip("Time required to fully charge the launch.")]
    public float launchChargeTime = 1.25f;

    [Tooltip("Extra forward acceleration at full launch charge.")]
    public float launchAcceleration = 7f;

    [Tooltip("How long the launch boost lasts.")]
    public float launchBoostDuration = 0.8f;

    [Tooltip("Launch can only charge below this speed.")]
    public float launchMaxBuildSpeedKmh = 6f;

    [Tooltip("Minimum charge required to trigger a launch.")]
    [Range(0f, 1f)]
    public float minimumLaunchCharge = 0.15f;

    [Header("Slide Recovery")]
    public float slideRecoveryTime = 1.8f;
    public float handbrakeStabilityMultiplier = 0.15f;

    [Header("Stability")]
    public Vector3 centerOfMassOffset =
        new Vector3(0f, -0.20f, 0f);

    [Header("Debug")]
    public float currentSpeedKmh;
    public float forwardSpeedKmh;
    public float recoveryAssist = 1f;

    public float currentSteerAngleLimit;

    public float launchCharge;
    public bool launchBoostActive;
    public float currentLaunchStrength;

    private Rigidbody rb;
    private RallySurfaceGrip surfaceGrip;

    private float throttleInput;
    private float steeringInput;
    private float currentSteeringInput;

    private bool footBrake;
    private bool handbrake;
    private bool previousFootBrake;

    private float launchBoostTimer;
    private float storedLaunchStrength;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        surfaceGrip =
            GetComponent<RallySurfaceGrip>();

        if (rb != null)
        {
            rb.centerOfMass += centerOfMassOffset;
        }
    }

    private void Update()
    {
        ReadInput();

    }

    private void FixedUpdate()
    {
        currentSpeedKmh =
            rb.linearVelocity.magnitude * 3.6f;

        forwardSpeedKmh =
            Vector3.Dot(
                transform.forward,
                rb.linearVelocity
            ) * 3.6f;

        UpdateRecoveryAssist();
        UpdateLaunchControl();

        ApplySteering();
        ApplyMotor();
        ApplyBrakes();

        ApplyLaunchBoost();

        ApplyTurnAssist();
        ApplyLateralStability();
        ApplyStraighteningAssist();
        ApplyDownforce();
    }

    private void ReadInput()
    {
        if (Keyboard.current == null)
            return;

        throttleInput = 0f;
        steeringInput = 0f;

        if (Keyboard.current.wKey.isPressed ||
            Keyboard.current.upArrowKey.isPressed)
        {
            throttleInput = 1f;
        }

        if (Keyboard.current.sKey.isPressed ||
            Keyboard.current.downArrowKey.isPressed)
        {
            throttleInput = -1f;
        }

        if (Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed)
        {
            steeringInput = -1f;
        }

        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            steeringInput = 1f;
        }

        footBrake =
            Keyboard.current.spaceKey.isPressed;

        handbrake =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;
    }

    private void UpdateRecoveryAssist()
    {
        if (handbrake)
        {
            recoveryAssist = 0f;
        }
        else
        {
            float recoverySpeed =
                1f / Mathf.Max(slideRecoveryTime, 0.01f);

            recoveryAssist =
                Mathf.MoveTowards(
                    recoveryAssist,
                    1f,
                    recoverySpeed * Time.fixedDeltaTime
                );
        }
    }

    private void UpdateLaunchControl()
    {
        float speed =
            Mathf.Abs(forwardSpeedKmh);

        bool canCharge =
            footBrake &&
            throttleInput > 0.1f &&
            speed <= launchMaxBuildSpeedKmh;

        if (canCharge)
        {
            float chargeRate =
                1f / Mathf.Max(launchChargeTime, 0.01f);

            launchCharge =
                Mathf.MoveTowards(
                    launchCharge,
                    1f,
                    chargeRate * Time.fixedDeltaTime
                );
        }

        bool brakeReleased =
            previousFootBrake &&
            !footBrake;

        if (brakeReleased)
        {
            bool validLaunch =
                throttleInput > 0.1f &&
                speed <= launchMaxBuildSpeedKmh &&
                launchCharge >= minimumLaunchCharge;

            if (validLaunch)
            {
                storedLaunchStrength =
                    launchCharge;

                launchBoostTimer =
                    launchBoostDuration;
            }

            launchCharge = 0f;
        }

        if (footBrake &&
            throttleInput <= 0.1f)
        {
            launchCharge =
                Mathf.MoveTowards(
                    launchCharge,
                    0f,
                    Time.fixedDeltaTime * 2f
                );
        }

        if (!footBrake &&
            launchBoostTimer <= 0f &&
            !brakeReleased)
        {
            launchCharge = 0f;
        }

        if (launchBoostTimer > 0f)
        {
            // Releasing throttle cancels the boost.
            if (throttleInput <= 0.1f)
            {
                launchBoostTimer = 0f;
                storedLaunchStrength = 0f;
            }
            else
            {
                launchBoostTimer -=
                    Time.fixedDeltaTime;
            }
        }

        if (launchBoostTimer > 0f)
        {
            launchBoostActive = true;

            currentLaunchStrength =
                storedLaunchStrength;
        }
        else
        {
            launchBoostTimer = 0f;

            launchBoostActive = false;

            storedLaunchStrength = 0f;

            currentLaunchStrength = 0f;
        }

        previousFootBrake =
            footBrake;
    }

    private void ApplyLaunchBoost()
    {
        if (!launchBoostActive)
            return;

        if (throttleInput <= 0.1f)
            return;

        float surfaceMultiplier = 1f;

        if (surfaceGrip != null)
        {
            surfaceMultiplier =
                surfaceGrip.CurrentDriveMultiplier;
        }

        float acceleration =
            launchAcceleration *
            currentLaunchStrength *
            surfaceMultiplier;

        rb.AddForce(
            transform.forward * acceleration,
            ForceMode.Acceleration
        );
    }

    private void ApplySteering()
    {
        currentSteeringInput =
            Mathf.MoveTowards(
                currentSteeringInput,
                steeringInput,
                steeringResponse *
                Time.fixedDeltaTime
            );

        float speed =
            Mathf.Abs(forwardSpeedKmh);

        float highSpeedFactor =
            Mathf.InverseLerp(
                fullSteerUntilKmh,
                highSpeedSteerAtKmh,
                speed
            );

        highSpeedFactor =
            Mathf.SmoothStep(
                0f,
                1f,
                highSpeedFactor
            );

        currentSteerAngleLimit =
            Mathf.Lerp(
                maxSteerAngle,
                highSpeedSteerAngle,
                highSpeedFactor
            );

        float steerAngle =
            currentSteeringInput *
            currentSteerAngleLimit;

        wheelFL.steerAngle = steerAngle;
        wheelFR.steerAngle = steerAngle;
    }

    private void ApplyMotor()
    {
        float driveMultiplier = 1f;
        float speedMultiplier = 1f;

        if (surfaceGrip != null)
        {
            driveMultiplier =
                surfaceGrip.CurrentDriveMultiplier;

            speedMultiplier =
                surfaceGrip.CurrentSpeedMultiplier;
        }

        float effectiveMaxForwardSpeed =
            maxForwardSpeed *
            speedMultiplier;

        float effectiveMaxReverseSpeed =
            maxReverseSpeed *
            speedMultiplier;

        float torque = 0f;

        if (throttleInput > 0f &&
            forwardSpeedKmh <
            effectiveMaxForwardSpeed)
        {
            float speedFactor =
                Mathf.Clamp01(
                    Mathf.Abs(forwardSpeedKmh) /
                    effectiveMaxForwardSpeed
                );

            float torqueFactor =
                Mathf.Lerp(
                    1f,
                    0.20f,
                    speedFactor
                );

            torque =
                throttleInput *
                maxMotorTorque *
                torqueFactor *
                driveMultiplier;
        }
        else if (
            throttleInput < 0f &&
            forwardSpeedKmh >
            -effectiveMaxReverseSpeed)
        {
            float reverseFactor =
                Mathf.Clamp01(
                    Mathf.Abs(forwardSpeedKmh) /
                    effectiveMaxReverseSpeed
                );

            float torqueFactor =
                Mathf.Lerp(
                    0.65f,
                    0.20f,
                    reverseFactor
                );

            torque =
                throttleInput *
                maxMotorTorque *
                torqueFactor *
                driveMultiplier;
        }

        float steeringTorqueMultiplier =
            Mathf.Lerp(
                1f,
                0.78f,
                Mathf.Abs(currentSteeringInput)
            );

        torque *= steeringTorqueMultiplier;

        wheelFL.motorTorque = torque;
        wheelFR.motorTorque = torque;

        if (handbrake)
        {
            wheelRL.motorTorque = 0f;
            wheelRR.motorTorque = 0f;
        }
        else
        {
            wheelRL.motorTorque = torque;
            wheelRR.motorTorque = torque;
        }
    }

    private void ApplyBrakes()
    {
        if (footBrake)
        {
            wheelFL.brakeTorque = brakeTorque;
            wheelFR.brakeTorque = brakeTorque;
            wheelRL.brakeTorque = brakeTorque;
            wheelRR.brakeTorque = brakeTorque;
        }
        else
        {
            wheelFL.brakeTorque = 0f;
            wheelFR.brakeTorque = 0f;

            if (handbrake)
            {
                wheelRL.brakeTorque =
                    handbrakeTorque;

                wheelRR.brakeTorque =
                    handbrakeTorque;
            }
            else
            {
                float coastBrake =
                    Mathf.Abs(throttleInput) < 0.01f
                        ? engineBrakeTorque
                        : 0f;

                wheelRL.brakeTorque = coastBrake;
                wheelRR.brakeTorque = coastBrake;
            }
        }
    }

    private void ApplyTurnAssist()
    {
        float speed =
            Mathf.Abs(forwardSpeedKmh);

        if (speed < 10f)
            return;

        float speedFactor =
            Mathf.Clamp01(
                speed / 80f
            );

        float direction =
            forwardSpeedKmh >= 0f
                ? 1f
                : -1f;

        float highSpeedReduction =
            Mathf.InverseLerp(
                turnAssistFadeStartKmh,
                turnAssistFadeEndKmh,
                speed
            );

        highSpeedReduction =
            Mathf.SmoothStep(
                0f,
                1f,
                highSpeedReduction
            );

        float assistStrength =
            Mathf.Lerp(
                1f,
                highSpeedTurnAssistMultiplier,
                highSpeedReduction
            );

        float assist =
            currentSteeringInput *
            direction *
            turnAssist *
            speedFactor *
            assistStrength;

        if (handbrake)
        {
            assist *= 0.85f;
        }

        rb.AddTorque(
            transform.up * assist,
            ForceMode.Acceleration
        );
    }

    private void ApplyLateralStability()
    {
        Vector3 localVelocity =
            transform.InverseTransformDirection(
                rb.linearVelocity
            );

        float assistStrength =
            Mathf.Lerp(
                handbrakeStabilityMultiplier,
                1f,
                recoveryAssist
            );

        Vector3 sidewaysVelocity =
            transform.right *
            localVelocity.x;

        rb.AddForce(
            -sidewaysVelocity *
            lateralStability *
            assistStrength,
            ForceMode.Acceleration
        );
    }

    private void ApplyStraighteningAssist()
    {
        if (Mathf.Abs(currentSteeringInput) > 0.05f)
            return;

        Vector3 localAngularVelocity =
            transform.InverseTransformDirection(
                rb.angularVelocity
            );

        float yawSpeed =
            localAngularVelocity.y;

        rb.AddTorque(
            -transform.up *
            yawSpeed *
            straighteningAssist *
            recoveryAssist,
            ForceMode.Acceleration
        );
    }

    private void ApplyDownforce()
    {
        float speed =
            rb.linearVelocity.magnitude;

        rb.AddForce(
            -transform.up *
            speed *
            downforce,
            ForceMode.Acceleration
        );
    }
}