using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Race result belongs to one network participant. Only the server edits this list.
public struct NetworkRaceResult : INetworkSerializable, IEquatable<NetworkRaceResult>
{
    public ulong clientId;
    public FixedString64Bytes username;
    public float timeSeconds;
    // 0 = racing, 1 = finished, 2 = did not finish / disconnected
    public byte status;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref username);
        serializer.SerializeValue(ref timeSeconds);
        serializer.SerializeValue(ref status);
    }

    public bool Equals(NetworkRaceResult other)
    {
        return clientId == other.clientId &&
               username.Equals(other.username) &&
               timeSeconds.Equals(other.timeSeconds) &&
               status == other.status;
    }
}

[RequireComponent(typeof(NetworkObject))]
public class NetworkRaceCoordinator : NetworkBehaviour
{
    [Header("Scene References")]
    public RouteManager routeManager;
    public CarSelectionManager carSelectionManager;

    [Header("Lobby UI (inside selection panel)")]
    public Button hostStartButton;
    public TMP_Text readyStatusText;

    [Header("Synchronized Countdown")]
    public float startBufferSeconds = 1f;
    public float countdownSeconds = 3f;

    [Header("Race Completion")]
    [Tooltip("After the first finisher, unfinished racers receive DNF after this many seconds.")]
    [Min(15f)] public float finishWindowSeconds = 120f;
    [Tooltip("Maximum race duration after GO, to avoid waiting forever if nobody finishes.")]
    [Min(60f)] public float maxRaceDurationSeconds = 900f;

    // These are synchronized. The host alone edits them.
    private readonly NetworkList<ulong> readyClientIds = new NetworkList<ulong>();
    private readonly NetworkList<NetworkRaceResult> raceResults =
        new NetworkList<NetworkRaceResult>();

    private readonly NetworkVariable<int> connectedPlayerCount =
        new NetworkVariable<int>(0);
    private readonly NetworkVariable<bool> raceLaunchOrdered =
        new NetworkVariable<bool>(false);
    private readonly NetworkVariable<bool> raceComplete =
        new NetworkVariable<bool>(false);

    private readonly Dictionary<ulong, string> playerNames =
        new Dictionary<ulong, string>();

    private int previousRouteIndex = -1;
    private double serverGoTime;
    private double firstFinisherServerTime = -1;
    private bool localFinishSubmitted;
    private bool locallyStoppedAtEnd;

    public bool LocalPlayerReady
    {
        get
        {
            return IsSpawned && NetworkManager != null &&
                   readyClientIds.Contains(NetworkManager.LocalClientId);
        }
    }

    public bool RaceLaunchOrdered => IsSpawned && raceLaunchOrdered.Value;
    public bool RaceComplete => IsSpawned && raceComplete.Value;

    private void Awake()
    {
        FindReferences();
    }

    public override void OnNetworkSpawn()
    {
        FindReferences();
        localFinishSubmitted = false;
        locallyStoppedAtEnd = false;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

            readyClientIds.Clear();
            raceResults.Clear();
            connectedPlayerCount.Value = NetworkManager.ConnectedClientsIds.Count;
            raceLaunchOrdered.Value = false;
            raceComplete.Value = false;
            previousRouteIndex = routeManager != null
                ? routeManager.CurrentRouteIndex : -1;
        }
    }

    private void FindReferences()
    {
        if (routeManager == null)
            routeManager = FindFirstObjectByType<RouteManager>();
        if (carSelectionManager == null)
            carSelectionManager = FindFirstObjectByType<CarSelectionManager>();
    }

    private void Update()
    {
        if (!IsSpawned)
        {
            if (hostStartButton != null)
                hostStartButton.gameObject.SetActive(false);
            if (readyStatusText != null)
                readyStatusText.text = "";
            return;
        }

        if (IsServer && !raceLaunchOrdered.Value && routeManager != null)
        {
            int route = routeManager.CurrentRouteIndex;
            if (route != previousRouteIndex)
            {
                previousRouteIndex = route;
                readyClientIds.Clear();
            }
        }

        bool showStart = IsServer && !raceLaunchOrdered.Value;
        if (hostStartButton != null)
        {
            if (hostStartButton.gameObject.activeSelf != showStart)
                hostStartButton.gameObject.SetActive(showStart);
            if (showStart)
                hostStartButton.interactable = EveryoneReady();
        }

        if (readyStatusText != null)
        {
            if (raceComplete.Value)
                readyStatusText.text = "Race completed";
            else if (raceLaunchOrdered.Value)
                readyStatusText.text = "Race in progress";
            else
                readyStatusText.text = "Ready: " + readyClientIds.Count +
                                       " / " + connectedPlayerCount.Value;
        }

        // Each computer reports ONLY its own RouteRace finish.
        // The existing RouteRace also saves ONLY the local player's Top 5.
        if (raceLaunchOrdered.Value && !raceComplete.Value &&
            !localFinishSubmitted && routeManager != null)
        {
            RouteRace route = routeManager.GetCurrentRoute();
            if (route != null && route.raceFinished)
            {
                localFinishSubmitted = true;
                SubmitFinishRpc(route.raceTime);
            }
        }

        if (IsServer && raceLaunchOrdered.Value && !raceComplete.Value)
            CheckRaceTimeouts();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        connectedPlayerCount.Value = NetworkManager.ConnectedClientsIds.Count;
        if (!raceLaunchOrdered.Value)
            readyClientIds.Clear();
        // Joins after race start are not added to its competitor roster.
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        connectedPlayerCount.Value = NetworkManager.ConnectedClientsIds.Count;

        if (!raceLaunchOrdered.Value)
        {
            readyClientIds.Clear();
            playerNames.Remove(clientId);
            return;
        }

        int index = FindResultIndex(clientId);
        if (index >= 0)
        {
            NetworkRaceResult entry = raceResults[index];
            if (entry.status == 0)
            {
                entry.status = 2;
                raceResults[index] = entry;
                CheckEveryoneResolved();
            }
        }
    }

    public void ToggleLocalReady()
    {
        if (!IsSpawned || raceLaunchOrdered.Value || raceComplete.Value)
            return;

        string username = "Player";
        if (LocalLeaderboardManager.Instance != null)
            username = LocalLeaderboardManager.Instance.localUsername;
        ToggleReadyRpc(SanitizeUsername(username));
    }

    private static string SanitizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Player";

        string cleaned = username.Trim().Replace("<", "_").Replace(">", "_")
            .Replace("\n", " ").Replace("\r", " ");
        if (cleaned.Length > 22)
            cleaned = cleaned.Substring(0, 22);
        return cleaned;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleReadyRpc(string nickname, RpcParams rpcParams = default)
    {
        if (raceLaunchOrdered.Value || raceComplete.Value) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        // Check the connected client IDs explicitly. IReadOnlyList<ulong>
        // does not have a direct Contains method without LINQ.
        bool senderConnected = false;
        foreach (ulong connectedId in NetworkManager.ConnectedClientsIds)
        {
            if (connectedId == sender)
            {
                senderConnected = true;
                break;
            }
        }
        if (!senderConnected) return;

        playerNames[sender] = SanitizeUsername(nickname);
        if (readyClientIds.Contains(sender))
            readyClientIds.Remove(sender);
        else
            readyClientIds.Add(sender);
    }

    private bool EveryoneReady()
    {
        if (!IsSpawned || !IsServer || raceLaunchOrdered.Value) return false;
        int count = NetworkManager.ConnectedClientsIds.Count;
        if (count < 1 || count > 6 || readyClientIds.Count != count)
            return false;

        foreach (ulong id in NetworkManager.ConnectedClientsIds)
            if (!readyClientIds.Contains(id)) return false;
        return true;
    }

    // Existing START RACE button remains wired to this method.
    public void StartRaceAsHost()
    {
        if (!EveryoneReady())
        {
            Debug.LogWarning("Cannot start: all connected players must be READY.");
            return;
        }

        FindReferences();
        RouteRace route = routeManager != null ? routeManager.GetCurrentRoute() : null;
        if (route == null)
        {
            Debug.LogWarning("No active route.");
            return;
        }

        List<ulong> clients = new List<ulong>(NetworkManager.ConnectedClientsIds);
        clients.Sort();
        int count = clients.Count;

        if (route.gridPositions == null || route.gridPositions.Length < count)
        {
            Debug.LogWarning("This route needs " + count + " assigned grid positions.");
            return;
        }
        for (int i = 0; i < count; i++)
        {
            if (route.gridPositions[i] == null)
            {
                Debug.LogWarning("Grid position P" + (i + 1) + " is missing.");
                return;
            }
        }

        List<int> positions = new List<int>();
        for (int i = 0; i < count; i++) positions.Add(i);
        for (int i = count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int swap = positions[i];
            positions[i] = positions[j];
            positions[j] = swap;
        }

        raceResults.Clear();
        foreach (ulong id in clients)
        {
            string displayName;
            if (!playerNames.TryGetValue(id, out displayName))
                displayName = "Player " + id;
            raceResults.Add(new NetworkRaceResult
            {
                clientId = id,
                username = new FixedString64Bytes(SanitizeUsername(displayName)),
                timeSeconds = 0f,
                status = 0
            });
        }

        firstFinisherServerTime = -1;
        serverGoTime = NetworkManager.ServerTime.Time +
                       Mathf.Max(0.5f, startBufferSeconds) +
                       Mathf.Max(3f, countdownSeconds);
        raceComplete.Value = false;
        raceLaunchOrdered.Value = true;

        for (int i = 0; i < count; i++)
        {
            Debug.Log("Player " + clients[i] + " assigned P" + (positions[i] + 1));
            PlacePlayerOnGridRpc(clients[i], positions[i]);
        }

        BeginSharedCountdownRpc(serverGoTime);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlacePlayerOnGridRpc(ulong clientId, int gridIndex)
    {
        if (NetworkManager.LocalClientId != clientId) return;
        FindReferences();
        if (carSelectionManager != null)
            carSelectionManager.PlaceAtNetworkGridPosition(gridIndex);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BeginSharedCountdownRpc(double goTimestamp)
    {
        FindReferences();
        localFinishSubmitted = false;
        locallyStoppedAtEnd = false;
        if (carSelectionManager != null)
            carSelectionManager.BeginNetworkCountdown(goTimestamp);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitFinishRpc(float resultSeconds, RpcParams rpcParams = default)
    {
        if (!IsServer || !raceLaunchOrdered.Value || raceComplete.Value) return;
        if (NetworkManager.ServerTime.Time < serverGoTime) return;
        if (float.IsNaN(resultSeconds) || float.IsInfinity(resultSeconds) ||
            resultSeconds <= 0.01f || resultSeconds > maxRaceDurationSeconds + 30f)
            return;

        ulong sender = rpcParams.Receive.SenderClientId;
        int index = FindResultIndex(sender);
        if (index < 0) return;

        NetworkRaceResult entry = raceResults[index];
        if (entry.status != 0) return; // already finished or DNF

        entry.status = 1;
        entry.timeSeconds = resultSeconds;
        raceResults[index] = entry;

        if (firstFinisherServerTime < 0)
            firstFinisherServerTime = NetworkManager.ServerTime.Time;

        Debug.Log("Multiplayer finisher: " + entry.username +
                  " in " + resultSeconds.ToString("F2") + "s");
        CheckEveryoneResolved();
    }

    private int FindResultIndex(ulong clientId)
    {
        for (int i = 0; i < raceResults.Count; i++)
            if (raceResults[i].clientId == clientId) return i;
        return -1;
    }

    private void CheckRaceTimeouts()
    {
        double now = NetworkManager.ServerTime.Time;
        bool wholeRaceTimedOut = now - serverGoTime >= maxRaceDurationSeconds;
        bool finishWindowExpired = firstFinisherServerTime >= 0 &&
            now - firstFinisherServerTime >= finishWindowSeconds;

        if (!wholeRaceTimedOut && !finishWindowExpired) return;

        for (int i = 0; i < raceResults.Count; i++)
        {
            NetworkRaceResult entry = raceResults[i];
            if (entry.status == 0)
            {
                entry.status = 2;
                raceResults[i] = entry;
            }
        }
        CheckEveryoneResolved();
    }

    private void CheckEveryoneResolved()
    {
        if (!IsServer || raceComplete.Value || !raceLaunchOrdered.Value) return;
        if (raceResults.Count == 0) return;
        foreach (NetworkRaceResult entry in raceResults)
            if (entry.status == 0) return;

        raceComplete.Value = true;
        CompleteRoundRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void CompleteRoundRpc()
    {
        if (locallyStoppedAtEnd) return;
        locallyStoppedAtEnd = true;
        localFinishSubmitted = true; // never report a DNF as a valid finish
        FindReferences();

        RouteRace route = routeManager != null ? routeManager.GetCurrentRoute() : null;
        if (route == null || route.raceFinished) return;

        // End a timed-out driver's race WITHOUT calling RouteRace.CrossFinish,
        // so no local Top 5 result is saved for a DNF.
        route.raceStarted = false;
        route.lapActive = false;

        if (carSelectionManager != null && carSelectionManager.cars != null)
        {
            foreach (GameObject car in carSelectionManager.cars)
            {
                if (car == null || !car.activeInHierarchy) continue;
                RallyCarController driver = car.GetComponent<RallyCarController>();
                if (driver != null) driver.enabled = false;
                Rigidbody body = car.GetComponent<Rigidbody>();
                if (body != null)
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    body.isKinematic = true;
                }
            }
        }
    }

    public bool CanReturnToLobby => IsSpawned && IsServer &&
                                     raceLaunchOrdered.Value && raceComplete.Value;

    // Called from the shared multiplayer results overlay, HOST ONLY.
    public void ReturnEveryoneToLobbyAsHost()
    {
        if (!CanReturnToLobby)
        {
            Debug.LogWarning("Wait for everyone to finish or DNF before returning.");
            return;
        }

        readyClientIds.Clear();
        raceResults.Clear();
        firstFinisherServerTime = -1;
        raceLaunchOrdered.Value = false;
        raceComplete.Value = false;
        previousRouteIndex = routeManager != null ? routeManager.CurrentRouteIndex : -1;
        BackToLobbyRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BackToLobbyRpc()
    {
        localFinishSubmitted = false;
        locallyStoppedAtEnd = false;
        FindReferences();

        if (LocalLeaderboardManager.Instance != null &&
            LocalLeaderboardManager.Instance.resultsPanel != null)
        {
            LocalLeaderboardManager.Instance.resultsPanel.SetActive(false);
        }

        if (carSelectionManager != null)
            carSelectionManager.ReturnToSelection();
    }

    public string BuildStandingsText()
    {
        List<NetworkRaceResult> rows = new List<NetworkRaceResult>();
        foreach (NetworkRaceResult entry in raceResults) rows.Add(entry);
        rows.Sort((a, b) =>
        {
            int ar = Rank(a.status), br = Rank(b.status);
            if (ar != br) return ar.CompareTo(br);
            if (a.status == 1) return a.timeSeconds.CompareTo(b.timeSeconds);
            return a.clientId.CompareTo(b.clientId);
        });

        if (rows.Count == 0) return "Waiting for participants...";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int place = 1;
        foreach (NetworkRaceResult entry in rows)
        {
            string name = entry.username.ToString();
            if (entry.status == 1)
            {
                sb.Append(place++).Append(". ").Append(name).Append("  ")
                  .Append(FormatTime(entry.timeSeconds));
            }
            else if (entry.status == 2)
            {
                sb.Append("-  ").Append(name).Append("  DNF");
            }
            else
            {
                sb.Append("-  ").Append(name).Append("  Racing");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static int Rank(byte status)
    {
        if (status == 1) return 0;
        if (status == 0) return 1;
        return 2;
    }

    private static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        return string.Format("{0:00}:{1:00.00}", minutes, seconds - 60 * minutes);
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null && IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if (hostStartButton != null)
            hostStartButton.gameObject.SetActive(false);
        if (readyStatusText != null)
            readyStatusText.text = "";
    }

    public override void OnDestroy()
    {
        readyClientIds.Dispose();
        raceResults.Dispose();
        base.OnDestroy();
    }
}
