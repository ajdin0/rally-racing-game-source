using UnityEngine;
using UnityEngine.InputSystem;

// Audio for the LOCAL, real car only. Do not place on network ghost prefabs.
// Tire squeal is driven by the very same boolean that creates road marks.
[RequireComponent(typeof(RallyCarController))]
[DefaultExecutionOrder(100)] // CarWheelEffects.LateUpdate runs before our LateUpdate.
public class CarAudioEffects : MonoBehaviour
{
    [Header("Audio Clips")]
    public AudioClip engineLoop;
    public AudioClip tireSquealLoop;
    public AudioClip gravelLoop;

    [Header("Engine")]
    [Range(0f, 1f)] public float engineVolume = 0.45f;
    public float idlePitch = 0.80f;
    public float topSpeedPitch = 1.55f;
    public float extraThrottlePitch = 0.10f;

    [Header("Launch Audio Only (boost physics is unchanged)")]
    [Range(0.05f, 1f)] public float launchSoundDuration = 0.25f;
    [Range(0f, 0.7f)] public float launchPitchBump = 0.24f;

    [Header("Tire Sound - ONLY When Road Marks Are Being Made")]
    [Range(0f, 1f)] public float tireVolume = 0.5f;
    public float minimumTireSoundSpeedKmh = 12f;
    public float fullTireSoundSpeedKmh = 45f;
    [Tooltip("Volume units per second; e.g. 5 gives a quick fade-in")]
    public float tireFadeInPerSecond = 5f;
    [Tooltip("Volume units per second; e.g. 4 gives a short fade-out")]
    public float tireFadeOutPerSecond = 4f;

    [Header("Gravel Rolling Sound")]
    [Range(0f, 1f)] public float gravelVolume = 0.22f;

    [Header("Debug (Play mode)")]
    [SerializeField] private bool debugRoadMarksActive;
    [SerializeField] private float debugTireTargetVolume;
    [SerializeField] private float debugCurrentTireVolume;

    private RallyCarController controller;
    private CarWheelEffects wheelEffects;
    private WheelCollider[] wheels;
    private AudioSource engineSource;
    private AudioSource tireSource;
    private AudioSource gravelSource;
    private bool wasBoostActive;
    private float launchSoundEndsAt;
    private bool warnedMissingWheelEffects;

    private void Awake()
    {
        controller = GetComponent<RallyCarController>();
        wheelEffects = GetComponent<CarWheelEffects>();
        wheels = GetComponentsInChildren<WheelCollider>(true);
        engineSource = CreateSource("Engine Audio", engineLoop);
        tireSource = CreateSource("Tire Audio", tireSquealLoop);
        gravelSource = CreateSource("Gravel Audio", gravelLoop);
    }

    private AudioSource CreateSource(string objectName, AudioClip clip)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f; // Local-only sound, never a remote car.
        source.volume = 0f;
        source.clip = clip;
        if (clip != null) source.Play();
        return source;
    }

    // Cars that are not selected are disabled by CarSelectionManager.
    // Unity stops their AudioSources on deactivation, so resume the loops
    // whenever this car becomes the selected (active) car again.
    private void OnEnable()
    {
        ResumeLoops();
    }

    private void ResumeLoops()
    {
        ResumeLoop(engineSource);
        ResumeLoop(tireSource);
        ResumeLoop(gravelSource);
    }

    private void ResumeLoop(AudioSource source)
    {
        if (source != null && source.clip != null && !source.isPlaying &&
            isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            source.Play();
        }
    }

    private void LateUpdate()
    {
        // Also cover a source Unity suspended before this component was
        // enabled. No new AudioSources are created, and muted clips stay muted.
        ResumeLoops();
        bool active = controller != null && controller.enabled;
        float speedKmh = active ? Mathf.Abs(controller.currentSpeedKmh) : 0f;
        Keyboard keyboard = Keyboard.current;
        bool throttleHeld = active && keyboard != null &&
            (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ||
             keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);

        bool boostNow = active && controller.launchBoostActive;
        if (boostNow && !wasBoostActive)
            launchSoundEndsAt = Time.time + launchSoundDuration;
        wasBoostActive = boostNow;

        float speedFraction = active
            ? Mathf.Clamp01(speedKmh / Mathf.Max(1f, controller.maxForwardSpeed))
            : 0f;
        float engineTargetPitch = Mathf.Lerp(idlePitch, topSpeedPitch, speedFraction);
        if (throttleHeld) engineTargetPitch += extraThrottlePitch;
        if (active && Time.time < launchSoundEndsAt)
        {
            float remaining = (launchSoundEndsAt - Time.time) /
                              Mathf.Max(0.05f, launchSoundDuration);
            engineTargetPitch += launchPitchBump * Mathf.Clamp01(remaining);
        }
        if (engineSource != null)
        {
            engineSource.pitch = Mathf.MoveTowards(engineSource.pitch,
                engineTargetPitch, Time.deltaTime * 5f);
            engineSource.volume = Mathf.MoveTowards(engineSource.volume,
                active ? engineVolume : 0f, Time.deltaTime * 3f);
        }

        // IMPORTANT: This is the ONLY condition enabling squeal.
        // CarWheelEffects uses actual wheel contact/speed/slip/braking on asphalt
        // to decide when it is creating skid marks. No direct brake-key audio.
        if (wheelEffects == null)
            wheelEffects = GetComponent<CarWheelEffects>();
        if (wheelEffects == null && !warnedMissingWheelEffects && tireSquealLoop != null)
        {
            warnedMissingWheelEffects = true;
            Debug.LogWarning("CarAudioEffects on '" + name +
                "': add CarWheelEffects to the same car root to enable tire squeal.", this);
        }
        debugRoadMarksActive = active && wheelEffects != null &&
                               wheelEffects.enabled && wheelEffects.debugCreatingMarks;
        float speedLevel = Mathf.InverseLerp(minimumTireSoundSpeedKmh,
            Mathf.Max(minimumTireSoundSpeedKmh + 1f, fullTireSoundSpeedKmh), speedKmh);
        float tireTarget = debugRoadMarksActive ? tireVolume * speedLevel : 0f;
        debugTireTargetVolume = tireTarget;
        if (tireSource != null)
        {
            float fade = tireTarget > tireSource.volume
                ? tireFadeInPerSecond : tireFadeOutPerSecond;
            tireSource.volume = Mathf.MoveTowards(tireSource.volume,
                tireTarget, Mathf.Max(0.01f, fade) * Time.deltaTime);
            debugCurrentTireVolume = tireSource.volume;
        }

        bool anyGravel = false;
        if (active)
        {
            foreach (WheelCollider wheel in wheels)
            {
                if (wheel == null || !wheel.GetGroundHit(out WheelHit hit) ||
                    hit.collider == null) continue;
                string surface = hit.collider.name.ToUpperInvariant();
                if (surface.Contains("GRAVEL") || surface.Contains("DIRT"))
                {
                    anyGravel = true;
                    break;
                }
            }
        }
        if (gravelSource != null)
        {
            float target = active && anyGravel
                ? gravelVolume * Mathf.Clamp01(speedKmh / 60f) : 0f;
            gravelSource.volume = Mathf.MoveTowards(gravelSource.volume,
                target, Time.deltaTime * 4f);
        }
    }

    private void OnDisable()
    {
        if (engineSource != null) engineSource.volume = 0f;
        if (tireSource != null) tireSource.volume = 0f;
        if (gravelSource != null) gravelSource.volume = 0f;
        wasBoostActive = false;
        launchSoundEndsAt = 0f;
        debugRoadMarksActive = false;
    }
}
