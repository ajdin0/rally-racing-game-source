using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Local physics never goes over the network: we send only pose + car type.
// Remote cars are visual-only, smoothed using a short snapshot buffer.
[RequireComponent(typeof(NetworkObject))]
public class NetworkGhostPlayer : NetworkBehaviour
{
    [Header("Ghost Car Prefabs (same order as CarSelectionManager)")]
    public GameObject[] ghostCarPrefabs;

    [Header("Network Updates")]
    [Range(2f, 30f)]
    public float updatesPerSecond = 20f;

    [Header("Snapshot Interpolation")]
    [Tooltip("How long behind the latest network time to render, in seconds. " +
             "Increase to 0.20-0.25 if Relay packets are uneven; decrease for less delay.")]
    [Range(0.05f, 0.35f)]
    public float interpolationDelay = 0.15f;

    [Tooltip("Extra visual position smoothing. 20-30 is a good first range.")]
    public float positionSmoothness = 25f;
    [Tooltip("Extra visual rotation smoothing.")]
    public float rotationSmoothness = 25f;

    private CarSelectionManager carSelection;
    private GameObject ghostInstance;
    private int currentGhostCarIndex = -1;
    private bool ghostVisible;
    private float sendTimer;

    private struct PoseSnapshot
    {
        public double timestamp;
        public Vector3 position;
        public Quaternion rotation;

        public PoseSnapshot(double t, Vector3 p, Quaternion r)
        {
            timestamp = t;
            position = p;
            rotation = r;
        }
    }

    private readonly List<PoseSnapshot> snapshots = new List<PoseSnapshot>(32);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            carSelection = FindFirstObjectByType<CarSelectionManager>();
            Debug.Log("LOCAL network player: " + OwnerClientId);
        }
        else
        {
            Debug.Log("REMOTE network player: " + OwnerClientId);
        }
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsOwner)
            UpdateLocalPlayer();
        else
            UpdateRemoteGhost();
    }

    private void UpdateLocalPlayer()
    {
        sendTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(2f, updatesPerSecond);
        if (sendTimer < interval)
            return;

        // Avoid catch-up bursts if the laptop pauses for a frame.
        sendTimer = 0f;

        int carIndex;
        Transform localCar = GetLocalDrivingCar(out carIndex);
        if (localCar == null)
        {
            SendCarPoseRpc(Vector3.zero, Quaternion.identity,
                           -1, false, NetworkManager.ServerTime.Time);
            return;
        }

        SendCarPoseRpc(localCar.position, localCar.rotation,
                       carIndex, true, NetworkManager.ServerTime.Time);
    }

    private Transform GetLocalDrivingCar(out int selectedCarIndex)
    {
        selectedCarIndex = -1;
        if (carSelection == null)
            carSelection = FindFirstObjectByType<CarSelectionManager>();

        if (carSelection == null || carSelection.cars == null)
            return null;

        for (int i = 0; i < carSelection.cars.Length; i++)
        {
            GameObject car = carSelection.cars[i];
            if (car == null || !car.activeInHierarchy)
                continue;

            RallyCarController driver = car.GetComponent<RallyCarController>();
            if (driver == null || !driver.enabled)
                continue;

            selectedCarIndex = i;
            return car.transform;
        }

        return null;
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
    private void SendCarPoseRpc(Vector3 position, Quaternion rotation,
                                int carIndex, bool visible, double timestamp)
    {
        BroadcastCarPoseRpc(position, rotation, carIndex, visible, timestamp);
    }

    [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
    private void BroadcastCarPoseRpc(Vector3 position, Quaternion rotation,
                                     int carIndex, bool visible, double timestamp)
    {
        if (IsOwner)
            return;

        if (!visible || ghostCarPrefabs == null ||
            carIndex < 0 || carIndex >= ghostCarPrefabs.Length)
        {
            HideGhost();
            return;
        }

        if (currentGhostCarIndex != carIndex || ghostInstance == null)
            CreateGhost(carIndex);

        if (ghostInstance == null)
            return;

        // Ignore late/out-of-order unreliable packets. Preserves smooth motion.
        if (snapshots.Count > 0 &&
            timestamp <= snapshots[snapshots.Count - 1].timestamp)
            return;

        snapshots.Add(new PoseSnapshot(timestamp, position, rotation));
        if (snapshots.Count > 32)
            snapshots.RemoveAt(0);

        if (!ghostVisible)
        {
            ghostInstance.transform.SetPositionAndRotation(position, rotation);
            ghostInstance.SetActive(true);
            ghostVisible = true;
        }
    }

    private void CreateGhost(int carIndex)
    {
        if (ghostInstance != null)
            Destroy(ghostInstance);

        ghostInstance = null;
        currentGhostCarIndex = -1;
        ghostVisible = false;
        snapshots.Clear();

        GameObject prefab = ghostCarPrefabs[carIndex];
        if (prefab == null)
        {
            Debug.LogWarning("Ghost prefab missing at index " + carIndex);
            return;
        }

        ghostInstance = Instantiate(prefab);
        ghostInstance.name = "RemoteGhost_" + OwnerClientId + "_" + prefab.name;
        ghostInstance.SetActive(false);
        currentGhostCarIndex = carIndex;
    }

    private void UpdateRemoteGhost()
    {
        if (ghostInstance == null || !ghostVisible || snapshots.Count == 0)
            return;

        // ServerTime is the shared clock, so each side renders delayed poses
        // at the same point in the remote player's history.
        double renderTime = NetworkManager.ServerTime.Time - interpolationDelay;

        // Keep the most recent sample before renderTime, plus the sample after.
        while (snapshots.Count >= 3 && snapshots[1].timestamp <= renderTime)
            snapshots.RemoveAt(0);

        Vector3 desiredPosition;
        Quaternion desiredRotation;

        if (snapshots.Count == 1 || renderTime <= snapshots[0].timestamp)
        {
            desiredPosition = snapshots[0].position;
            desiredRotation = snapshots[0].rotation;
        }
        else if (renderTime >= snapshots[snapshots.Count - 1].timestamp)
        {
            // Short pause if a packet is late, instead of jumping forward.
            PoseSnapshot last = snapshots[snapshots.Count - 1];
            desiredPosition = last.position;
            desiredRotation = last.rotation;
        }
        else
        {
            int index = 0;
            while (index < snapshots.Count - 2 &&
                   snapshots[index + 1].timestamp < renderTime)
                index++;

            PoseSnapshot from = snapshots[index];
            PoseSnapshot to = snapshots[index + 1];
            double duration = to.timestamp - from.timestamp;
            float t = duration > 0.00001
                ? Mathf.Clamp01((float)((renderTime - from.timestamp) / duration))
                : 1f;

            desiredPosition = Vector3.Lerp(from.position, to.position, t);
            desiredRotation = Quaternion.Slerp(from.rotation, to.rotation, t);
        }

        float positionT = 1f - Mathf.Exp(-Mathf.Max(0f, positionSmoothness) * Time.deltaTime);
        float rotationT = 1f - Mathf.Exp(-Mathf.Max(0f, rotationSmoothness) * Time.deltaTime);

        ghostInstance.transform.position = Vector3.Lerp(
            ghostInstance.transform.position, desiredPosition,
            positionSmoothness <= 0f ? 1f : positionT);
        ghostInstance.transform.rotation = Quaternion.Slerp(
            ghostInstance.transform.rotation, desiredRotation,
            rotationSmoothness <= 0f ? 1f : rotationT);
    }

    private void HideGhost()
    {
        if (ghostInstance != null)
            ghostInstance.SetActive(false);
        ghostVisible = false;
        snapshots.Clear();
    }

    public override void OnNetworkDespawn()
    {
        if (ghostInstance != null)
            Destroy(ghostInstance);
        ghostInstance = null;
        currentGhostCarIndex = -1;
        ghostVisible = false;
        snapshots.Clear();
    }
}
