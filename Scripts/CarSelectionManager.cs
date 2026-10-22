using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarSelectionManager : MonoBehaviour
{
    [Header("Cars")]
    public GameObject[] cars;

    [Header("Spawn")]
    public Transform spawnPoint;

    [Header("Camera")]
    public RallyCameraFollow cameraFollow;

    [Header("UI")]
    public GameObject selectionPanel;

    [Tooltip("Optional: the Text (TMP) child of your existing DRIVE button.")]
    public TMP_Text driveButtonLabel;

    [Header("Race Start")]
    public RaceCountdown raceCountdown;

    [Header("Race")]
    public RouteManager routeManager;

    [Header("Multiplayer")]
    public NetworkRaceCoordinator networkRaceCoordinator;

    [Header("Automatic Ground Placement")]
    public float groundRayHeight = 10f;
    public float groundClearance = 0.05f;

    private int selectedIndex = 0;
    private GameObject activeCar;
    private Quaternion[] carRotationOffsets;
    private bool drivingStarted = false;

    private bool MultiplayerConnected
    {
        get
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening;
        }
    }

    private void Awake()
    {
        carRotationOffsets = new Quaternion[cars.Length];

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] == null)
                continue;

            carRotationOffsets[i] = spawnPoint != null
                ? Quaternion.Inverse(spawnPoint.rotation) * cars[i].transform.rotation
                : cars[i].transform.rotation;
        }
    }

    private void Start()
    {
        ShowCar(selectedIndex);
    }

    private void Update()
    {
        UpdateDriveButtonLabel();

        if (!drivingStarted)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            RespawnCar();
        }
    }

    private void FindNetworkCoordinator()
    {
        if (networkRaceCoordinator == null)
            networkRaceCoordinator = FindFirstObjectByType<NetworkRaceCoordinator>();
    }

    private void UpdateDriveButtonLabel()
    {
        if (driveButtonLabel == null)
            return;

        string wantedText = "DRIVE";

        if (MultiplayerConnected)
        {
            FindNetworkCoordinator();
            wantedText = networkRaceCoordinator != null &&
                         networkRaceCoordinator.LocalPlayerReady
                ? "CANCEL READY"
                : "READY";
        }

        if (driveButtonLabel.text != wantedText)
            driveButtonLabel.text = wantedText;
    }

    public void NextCar()
    {
        if (MultiplayerConnected)
        {
            FindNetworkCoordinator();
            if (networkRaceCoordinator != null &&
                (networkRaceCoordinator.LocalPlayerReady ||
                 networkRaceCoordinator.RaceLaunchOrdered))
                return;
        }

        if (cars == null || cars.Length == 0)
            return;

        selectedIndex = (selectedIndex + 1) % cars.Length;
        ShowCar(selectedIndex);
    }

    public void PreviousCar()
    {
        if (MultiplayerConnected)
        {
            FindNetworkCoordinator();
            if (networkRaceCoordinator != null &&
                (networkRaceCoordinator.LocalPlayerReady ||
                 networkRaceCoordinator.RaceLaunchOrdered))
                return;
        }

        if (cars == null || cars.Length == 0)
            return;

        selectedIndex = (selectedIndex - 1 + cars.Length) % cars.Length;
        ShowCar(selectedIndex);
    }

    // Keep your existing DRIVE button connected to this method.
    public void StartDriving()
    {
        if (activeCar == null)
            return;

        if (MultiplayerConnected)
        {
            FindNetworkCoordinator();

            if (networkRaceCoordinator == null || !networkRaceCoordinator.IsSpawned)
            {
                Debug.LogWarning("Multiplayer coordinator is not ready yet.");
                return;
            }

            // In multiplayer the DRIVE button becomes READY / CANCEL READY.
            networkRaceCoordinator.ToggleLocalReady();
            return;
        }

        // Existing offline behavior is unchanged.
        drivingStarted = false;
        MoveCarToSpawn();
        SnapActiveCarToGround();

        Rigidbody rb = activeCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        RallyCarController controller = activeCar.GetComponent<RallyCarController>();
        if (controller != null)
            controller.enabled = true;

        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        if (raceCountdown != null)
            raceCountdown.BeginCountdown(activeCar, this);
        else
        {
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            NotifyRaceStarted();
        }
    }

    // Called by the Host's coordinator on ONE specific client via an RPC.
    public void PlaceAtNetworkGridPosition(int positionIndex)
    {
        if (activeCar == null || spawnPoint == null || routeManager == null)
            return;

        RouteRace route = routeManager.GetCurrentRoute();
        if (route == null || route.gridPositions == null ||
            positionIndex < 0 || positionIndex >= route.gridPositions.Length)
            return;

        Transform position = route.gridPositions[positionIndex];
        if (position == null)
            return;

        spawnPoint.SetPositionAndRotation(position.position, position.rotation);
        RefreshSelectedCarAtSpawn();

        Debug.Log("Local player placed at P" + (positionIndex + 1));
    }

    // Called by the synchronized START RPC on each computer.
    public void BeginNetworkCountdown(double goServerTime)
    {
        if (activeCar == null)
            return;

        drivingStarted = false;
        MoveCarToSpawn();
        SnapActiveCarToGround();

        if (selectionPanel != null)
            selectionPanel.SetActive(false);

        if (raceCountdown != null)
            raceCountdown.BeginNetworkCountdown(activeCar, this, goServerTime);
        else
            Debug.LogError("RaceCountdown reference is missing!");
    }

    public void NotifyRaceStarted()
    {
        drivingStarted = true;

        if (routeManager != null)
        {
            RouteRace route = routeManager.GetCurrentRoute();
            if (route != null)
                route.BeginRace(activeCar);
        }
    }

    public void ReturnToSelection()
    {
        drivingStarted = false;

        if (routeManager != null)
        {
            RouteRace route = routeManager.GetCurrentRoute();
            if (route != null)
                route.ResetRace();
        }

        if (selectionPanel != null)
            selectionPanel.SetActive(true);

        ShowCar(selectedIndex);
    }

    public void RefreshSelectedCarAtSpawn()
    {
        if (activeCar == null)
            return;

        MoveCarToSpawn();
        SnapActiveCarToGround();

        Rigidbody rb = activeCar.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cameraFollow != null)
            cameraFollow.target = activeCar.transform;
    }

    private void ShowCar(int index)
    {
        drivingStarted = false;

        if (cars == null || cars.Length == 0 || index < 0 || index >= cars.Length)
            return;

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] != null)
                cars[i].SetActive(i == index);
        }

        activeCar = cars[index];
        if (activeCar == null)
            return;

        // Ensure a frozen car is safely repositioned on its next selection.
        Rigidbody rb = activeCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        RallyCarController controller = activeCar.GetComponent<RallyCarController>();
        if (controller != null)
            controller.enabled = false;

        MoveCarToSpawn();
        SnapActiveCarToGround();

        if (cameraFollow != null)
            cameraFollow.target = activeCar.transform;
    }

    private void RespawnCar()
    {
        if (activeCar == null || spawnPoint == null)
            return;

        Rigidbody rb = activeCar.GetComponent<Rigidbody>();
        bool wasKinematic = rb != null && rb.isKinematic;

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        MoveCarToSpawn();
        SnapActiveCarToGround();
        Physics.SyncTransforms();

        if (rb != null)
        {
            rb.position = activeCar.transform.position;
            rb.rotation = activeCar.transform.rotation;
            rb.isKinematic = wasKinematic;

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void MoveCarToSpawn()
    {
        if (spawnPoint == null || activeCar == null)
            return;

        activeCar.transform.SetPositionAndRotation(
            spawnPoint.position,
            spawnPoint.rotation * carRotationOffsets[selectedIndex]);

        Physics.SyncTransforms();
    }

    private void SnapActiveCarToGround()
    {
        if (activeCar == null)
            return;

        Physics.SyncTransforms();

        WheelCollider[] wheels =
            activeCar.GetComponentsInChildren<WheelCollider>(true);
        if (wheels.Length == 0)
            return;

        float lowestWheelY = float.PositiveInfinity;
        foreach (WheelCollider wheel in wheels)
        {
            float wheelBottom = wheel.transform.position.y - wheel.radius;
            if (wheelBottom < lowestWheelY)
                lowestWheelY = wheelBottom;
        }

        Vector3 rayOrigin = activeCar.transform.position +
                            Vector3.up * groundRayHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            groundRayHeight + 30f,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool foundGround = false;
        float closestDistance = float.PositiveInfinity;
        RaycastHit bestHit = default;

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;

            if (hitTransform == activeCar.transform ||
                hitTransform.IsChildOf(activeCar.transform))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                bestHit = hit;
                foundGround = true;
            }
        }

        if (!foundGround)
            return;

        float desiredBottom = bestHit.point.y + groundClearance;
        activeCar.transform.position +=
            Vector3.up * (desiredBottom - lowestWheelY);

        Physics.SyncTransforms();
    }
}
