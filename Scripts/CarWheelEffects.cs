using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(RallyCarController))]
public class CarWheelEffects : MonoBehaviour
{
    [Header("Materials (URP)")]
    public Material dustMaterial;
    public Material skidMaterial;

    [Header("Dust")]
    public float minimumDustSpeedKmh = 12f;
    [Range(0f, 35f)] public float gravelParticlesPerSecond = 16f;
    [Range(0f, 35f)] public float grassParticlesPerSecond = 9f;
    [Range(0f, 35f)] public float asphaltSmokeParticlesPerSecond = 8f;

    [Header("Visible Tire Marks")]
    public float minimumSkidSpeedKmh = 16f;
    public float sideSlipThreshold = 0.25f;
    public float forwardSlipThreshold = 0.36f;
    public bool markWhenHandbraking = true;
    public bool markWhenHardBraking = true;
    public float hardBrakeMinimumSpeedKmh = 40f;
    [Range(0.08f, 0.5f)] public float skidWidth = 0.22f;
    [Range(1f, 20f)] public float skidLifetimeSeconds = 7f;
    [Range(0.08f, 0.6f)] public float minimumMarkSpacing = 0.20f;
    [Range(40, 600)] public int maxMarkSegments = 350;

    [Header("Debug (read only while playing)")]
    public string debugSurface = "None";
    public bool debugCreatingMarks;
    public int debugVisibleSegments;

    private RallyCarController car;
    private Rigidbody body;
    private WheelCollider[] rearWheels;
    private ParticleSystem[] dustSystems;
    private float[] dustAccumulators;
    private readonly Vector3[] previousPoints = new Vector3[2];
    private readonly bool[] previousValid = new bool[2];
    private GameObject marksObject;
    private Mesh marksMesh;
    private readonly List<SkidSegment> segments = new List<SkidSegment>();
    private readonly List<Vector3> meshVertices = new List<Vector3>();
    private readonly List<int> meshTriangles = new List<int>();
    private readonly List<Vector2> meshUVs = new List<Vector2>();

    private struct SkidSegment
    {
        public Vector3 start;
        public Vector3 end;
        public Vector3 normal;
        public float createdAt;
    }

    private void Awake()
    {
        car = GetComponent<RallyCarController>();
        body = GetComponent<Rigidbody>();
        rearWheels = new[] { car.wheelRL, car.wheelRR };
        dustSystems = new ParticleSystem[2];
        dustAccumulators = new float[2];
        for (int i = 0; i < 2; i++)
            if (rearWheels[i] != null)
                dustSystems[i] = CreateDustSystem(i == 0 ? "Left" : "Right");
        CreateMarkMesh();
    }

    private void CreateMarkMesh()
    {
        // Detached from the moving car: skid marks must remain on the ROAD.
        marksObject = new GameObject(name + "_TemporarySkidMarks");
        marksObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        MeshFilter filter = marksObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = marksObject.AddComponent<MeshRenderer>();
        marksMesh = new Mesh { name = "Temporary Skid Mark Mesh" };
        marksMesh.MarkDynamic();
        filter.sharedMesh = marksMesh;
        if (skidMaterial != null) renderer.sharedMaterial = skidMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private ParticleSystem CreateDustSystem(string side)
    {
        GameObject go = new GameObject("Dust_" + side);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.68f, 0.60f, 0.45f, 0.30f),
            new Color(0.82f, 0.76f, 0.63f, 0.48f));
        main.maxParticles = 90;
        main.gravityModifier = -0.10f;
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 1.4f)));
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (dustMaterial != null) renderer.sharedMaterial = dustMaterial;
        ps.Play();
        return ps;
    }

    private void LateUpdate()
    {
        bool driving = car != null && car.enabled &&
                       body != null && !body.isKinematic;
        float speedKmh = driving ? body.linearVelocity.magnitude * 3.6f : 0f;
        bool handbrakeHeld = Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        bool brakeHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        debugCreatingMarks = false;
        debugSurface = "None";

        for (int i = 0; i < 2; i++)
        {
            WheelCollider wheel = rearWheels[i];
            if (!driving || wheel == null || !wheel.GetGroundHit(out WheelHit hit))
            {
                previousValid[i] = false;
                continue;
            }

            string surface = hit.collider != null
                ? hit.collider.name.ToUpperInvariant() : "";
            bool grass = surface.Contains("GRASS");
            bool loose = grass || surface.Contains("GRAVEL") || surface.Contains("DIRT");
            bool asphalt = !loose &&
                (surface.Contains("TARMAC") || surface.Contains("ASPHALT") ||
                 surface.Contains("PITLANE") || surface.Contains("CONCRETE") ||
                 surface.Contains("KERB") || surface.Contains("ROAD"));
            debugSurface = surface;

            float normalizedSlip = Mathf.Max(
                Mathf.Abs(hit.sidewaysSlip) / Mathf.Max(0.01f, sideSlipThreshold),
                Mathf.Abs(hit.forwardSlip) / Mathf.Max(0.01f, forwardSlipThreshold));
            bool skidding = normalizedSlip >= 1f;

            if (dustSystems[i] != null && dustMaterial != null &&
                speedKmh >= minimumDustSpeedKmh)
            {
                float rate = loose
                    ? (grass ? grassParticlesPerSecond : gravelParticlesPerSecond)
                    : asphalt && skidding ? asphaltSmokeParticlesPerSecond : 0f;
                if (rate > 0f)
                {
                    dustSystems[i].transform.position = hit.point + hit.normal * 0.05f;
                    dustAccumulators[i] += rate * Mathf.Clamp01(speedKmh / 55f) * Time.deltaTime;
                    int count = Mathf.Min(6, Mathf.FloorToInt(dustAccumulators[i]));
                    if (count > 0)
                    {
                        dustAccumulators[i] -= count;
                        dustSystems[i].Emit(count);
                    }
                }
            }

            bool hardBrake = markWhenHardBraking && brakeHeld &&
                             speedKmh >= hardBrakeMinimumSpeedKmh;
            bool handbrake = markWhenHandbraking && handbrakeHeld;
            bool drawMark = skidMaterial != null && asphalt &&
                            speedKmh >= minimumSkidSpeedKmh &&
                            (skidding || hardBrake || handbrake);
            if (!drawMark)
            {
                previousValid[i] = false;
                continue;
            }

            debugCreatingMarks = true;
            Vector3 point = hit.point + hit.normal * 0.045f;
            if (previousValid[i])
            {
                float distance = Vector3.Distance(previousPoints[i], point);
                if (distance >= minimumMarkSpacing && distance < 3f)
                {
                    segments.Add(new SkidSegment
                    {
                        start = previousPoints[i],
                        end = point,
                        normal = hit.normal,
                        createdAt = Time.time
                    });
                    previousPoints[i] = point;
                }
                else if (distance >= 3f)
                    previousPoints[i] = point; // Respawn/teleport: no huge streak.
            }
            else
            {
                previousPoints[i] = point;
                previousValid[i] = true;
            }
        }

        // A single mesh for both rear tires; no new GameObject every frame.
        for (int i = segments.Count - 1; i >= 0; i--)
            if (Time.time - segments[i].createdAt > skidLifetimeSeconds)
                segments.RemoveAt(i);
        while (segments.Count > maxMarkSegments)
            segments.RemoveAt(0);
        debugVisibleSegments = segments.Count;
        RebuildMarkMesh();
    }

    private void RebuildMarkMesh()
    {
        if (marksMesh == null) return;
        meshVertices.Clear();
        meshTriangles.Clear();
        meshUVs.Clear();
        foreach (SkidSegment s in segments)
        {
            Vector3 direction = s.end - s.start;
            if (direction.sqrMagnitude < 0.0001f) continue;
            Vector3 side = Vector3.Cross(direction.normalized, s.normal.normalized).normalized;
            if (side.sqrMagnitude < 0.0001f) continue;
            side *= skidWidth * 0.5f;
            int n = meshVertices.Count;
            meshVertices.Add(s.start - side);
            meshVertices.Add(s.start + side);
            meshVertices.Add(s.end - side);
            meshVertices.Add(s.end + side);
            meshUVs.Add(new Vector2(0f, 0f));
            meshUVs.Add(new Vector2(1f, 0f));
            meshUVs.Add(new Vector2(0f, 1f));
            meshUVs.Add(new Vector2(1f, 1f));
            // Upward-facing triangles; visible when looking down at the road.
            meshTriangles.Add(n);
            meshTriangles.Add(n + 1);
            meshTriangles.Add(n + 2);
            meshTriangles.Add(n + 2);
            meshTriangles.Add(n + 1);
            meshTriangles.Add(n + 3);
        }
        marksMesh.Clear();
        marksMesh.SetVertices(meshVertices);
        marksMesh.SetUVs(0, meshUVs);
        marksMesh.SetTriangles(meshTriangles, 0);
        if (meshVertices.Count > 0) marksMesh.RecalculateBounds();
    }

    private void OnDisable()
    {
        previousValid[0] = false;
        previousValid[1] = false;
        segments.Clear();
        if (marksMesh != null) marksMesh.Clear();
    }

    private void OnDestroy()
    {
        if (marksObject != null) Destroy(marksObject);
        if (marksMesh != null) Destroy(marksMesh);
    }
}
