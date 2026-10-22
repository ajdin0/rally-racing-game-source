using UnityEngine;
using UnityEngine.Rendering;

// Lightweight visuals for remote non-colliding ghost cars.
// No Rigidbody, WheelColliders, audio, or network messages are needed.
public class GhostDustEffects : MonoBehaviour
{
    [Header("URP Dust Material")]
    public Material dustMaterial;

    [Header("Effects")]
    public float minimumSpeedKmh = 15f;
    [Range(0f, 25f)] public float looseSurfaceParticlesPerSecond = 9f;
    [Range(0f, 25f)] public float driftSmokeParticlesPerSecond = 5f;
    public float smokeLateralSpeedMps = 5f;
    public Vector3 effectOffset = new Vector3(0f, 0f, -1.2f);

    private ParticleSystem dust;
    private Vector3 previousPosition;
    private bool hasPreviousPosition;
    private float emissionAccumulator;

    private void Awake()
    {
        GameObject go = new GameObject("Remote_Dust");
        go.transform.SetParent(transform, false);
        dust = go.AddComponent<ParticleSystem>();

        var main = dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .55f);
        main.startSize = new ParticleSystem.MinMaxCurve(.16f, .35f);
        main.startColor = new Color(.76f, .68f, .55f, .36f);
        main.gravityModifier = -.08f;
        main.maxParticles = 70;

        var emission = dust.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = dust.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .15f;

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (dustMaterial != null) r.sharedMaterial = dustMaterial;
        dust.Play();
    }

    private void OnEnable()
    {
        hasPreviousPosition = false;
        emissionAccumulator = 0f;
    }

    private void LateUpdate()
    {
        if (!hasPreviousPosition)
        {
            previousPosition = transform.position;
            hasPreviousPosition = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, .001f);
        Vector3 velocity = (transform.position - previousPosition) / dt;
        previousPosition = transform.position;

        float speedKmh = velocity.magnitude * 3.6f;
        // Avoid huge bursts on network snap/teleport.
        if (speedKmh < minimumSpeedKmh || speedKmh > 220f || dustMaterial == null)
            return;

        Vector3 origin = transform.TransformPoint(effectOffset) + Vector3.up * 4f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                12f, ~0, QueryTriggerInteraction.Ignore))
            return;

        string surface = hit.collider != null ?
            hit.collider.name.ToUpperInvariant() : "";
        bool loose = surface.Contains("GRAVEL") || surface.Contains("DIRT") ||
                     surface.Contains("GRASS");
        bool asphalt = surface.Contains("TARMAC") || surface.Contains("PITLANE") ||
                       surface.Contains("CONCRETE") || surface.Contains("KERB");
        float lateralSpeed = Mathf.Abs(Vector3.Dot(transform.right, velocity));

        float rate = loose ? looseSurfaceParticlesPerSecond :
            asphalt && lateralSpeed > smokeLateralSpeedMps ? driftSmokeParticlesPerSecond : 0f;
        if (rate <= 0f) return;

        dust.transform.position = hit.point + hit.normal * .06f;
        emissionAccumulator += rate * Mathf.Clamp01(speedKmh / 55f) * dt;
        int emitCount = Mathf.Min(4, Mathf.FloorToInt(emissionAccumulator));
        if (emitCount > 0)
        {
            emissionAccumulator -= emitCount;
            dust.Emit(emitCount);
        }
    }
}
