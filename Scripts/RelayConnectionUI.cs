using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

// Relay Host/Join screen: compact during racing, expanded in the lobby.
// The network connection and join-code behavior is unchanged.
public class RelayConnectionUI : MonoBehaviour
{
    [Header("Relay")]
    [Range(2, 6)] public int maxPlayers = 6;
    public string connectionType = "dtls";

    [Header("Screen Layout")]
    [Tooltip("Collapse this connection screen during a race.")]
    public bool compactDuringRace = true;
    public float panelWidth = 295f;

    private string codeToJoin = "";
    private string activeJoinCode = "";
    private string message = "Choose HOST or enter a join code.";
    private bool busy;
    private bool clientCallbackRegistered;
    private bool expandedDuringRace;

    private void OnEnable()
    {
        RegisterClientCallback();
    }

    private void RegisterClientCallback()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || clientCallbackRegistered)
            return;
        network.OnClientConnectedCallback += OnClientConnected;
        network.OnClientDisconnectCallback += OnClientDisconnected;
        clientCallbackRegistered = true;
    }

    private void OnDisable()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network != null && clientCallbackRegistered)
        {
            network.OnClientConnectedCallback -= OnClientConnected;
            network.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        clientCallbackRegistered = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network != null && clientId == network.LocalClientId && !network.IsHost)
            message = "Connected to Host!";
    }

    private void OnClientDisconnected(ulong clientId)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network != null && !network.IsServer && clientId == network.LocalClientId)
            message = "Disconnected from Host.";
    }

    private async void HostRelay()
    {
        if (busy) return;
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || network.IsListening) return;
        UnityTransport transport = network.GetComponent<UnityTransport>();
        if (transport == null)
        {
            message = "UnityTransport is missing on NetworkManager.";
            return;
        }

        busy = true;
        message = "Signing in and creating Relay allocation...";
        try
        {
            await EnsureUnityServicesReady();
            var allocation = await RelayService.Instance.CreateAllocationAsync(
                Mathf.Clamp(maxPlayers, 2, 6) - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(
                allocation.AllocationId);

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, connectionType));

            if (!network.StartHost())
            {
                message = "Could not start Host. Check Console.";
                return;
            }

            activeJoinCode = joinCode;
            message = "Host running. Share this code with your friend.";
        }
        catch (Exception ex)
        {
            message = "Host failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private async void JoinRelay()
    {
        if (busy) return;
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || network.IsListening) return;
        UnityTransport transport = network.GetComponent<UnityTransport>();
        if (transport == null)
        {
            message = "UnityTransport is missing on NetworkManager.";
            return;
        }

        string requestedCode = codeToJoin.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(requestedCode))
        {
            message = "Enter the Host's join code first.";
            return;
        }

        busy = true;
        message = "Joining Relay allocation...";
        try
        {
            await EnsureUnityServicesReady();
            var allocation = await RelayService.Instance.JoinAllocationAsync(requestedCode);
            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, connectionType));

            if (!network.StartClient())
            {
                message = "Could not start Client. Check Console.";
                return;
            }

            message = "Connecting to Host...";
        }
        catch (Exception ex)
        {
            message = "Join failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private static async System.Threading.Tasks.Task EnsureUnityServicesReady()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private bool IsRaceRunning()
    {
        NetworkRaceCoordinator coordinator =
            FindFirstObjectByType<NetworkRaceCoordinator>();
        return coordinator != null && coordinator.IsSpawned &&
               coordinator.RaceLaunchOrdered;
    }

    private void OnGUI()
    {
        RegisterClientCallback();
        NetworkManager network = NetworkManager.Singleton;
        bool connected = network != null && network.IsListening;
        bool playingRace = connected && compactDuringRace && IsRaceRunning();

        if (!playingRace)
            expandedDuringRace = false;

        // Compact badge only while a race is underway.
        if (playingRace && !expandedDuringRace)
        {
            float width = Mathf.Min(150f, Mathf.Max(80f, Screen.width - 20f));
            Rect compact = new Rect(Screen.width - width - 10f, 10f, width, 42f);
            GUILayout.BeginArea(compact, GUI.skin.box);
            if (GUILayout.Button("ONLINE  +", GUILayout.Height(30f)))
                expandedDuringRace = true;
            GUILayout.EndArea();
            return;
        }

        float widthFull = Mathf.Min(Mathf.Max(230f, panelWidth),
                                   Mathf.Max(100f, Screen.width - 20f));
        float heightFull = playingRace ? 190f : (!connected ? 210f : 188f);
        heightFull = Mathf.Min(heightFull, Mathf.Max(100f, Screen.height - 20f));
        Rect area = new Rect(Screen.width - widthFull - 10f, 10f,
                             widthFull, heightFull);

        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("INTERNET MULTIPLAYER");
        if (playingRace && GUILayout.Button("HIDE", GUILayout.Width(55f)))
            expandedDuringRace = false;
        GUILayout.EndHorizontal();

        if (network == null)
        {
            GUILayout.Label("NetworkManager missing!");
        }
        else if (connected)
        {
            GUILayout.Label(network.IsHost ? "HOST CONNECTED" : "CLIENT CONNECTED");
            if (network.IsHost && !string.IsNullOrEmpty(activeJoinCode))
            {
                GUILayout.Label("Join code:");
                GUILayout.TextField(activeJoinCode);
            }
            else if (network.IsClient)
            {
                GUILayout.Label(network.IsConnectedClient
                    ? "Connected to Host" : "Connecting...");
            }

            if (GUILayout.Button("DISCONNECT", GUILayout.Height(28f)))
            {
                network.Shutdown();
                activeJoinCode = "";
                message = "Disconnected. You can Host/Join again.";
            }
        }
        else
        {
            GUI.enabled = !busy;
            if (GUILayout.Button("HOST INTERNET RACE", GUILayout.Height(28f)))
                HostRelay();
            GUILayout.Label("Friend's join code:");
            codeToJoin = GUILayout.TextField(codeToJoin, 20);
            if (GUILayout.Button("JOIN INTERNET RACE", GUILayout.Height(28f)))
                JoinRelay();
            GUI.enabled = true;
        }

        GUILayout.Label(message);
        GUILayout.EndArea();
    }
}
