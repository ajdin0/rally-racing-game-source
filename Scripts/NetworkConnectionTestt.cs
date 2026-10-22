
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkConnectionTest : MonoBehaviour
{
    private string message = "Not connected";

    private void OnGUI()
    {
        NetworkManager network = NetworkManager.Singleton;

        GUILayout.BeginArea(
            new Rect(Screen.width - 280, 10, 270, 185),
            GUI.skin.box
        );

        GUILayout.Label("MULTIPLAYER TEST");

        if (network == null)
        {
            GUILayout.Label("NetworkManager not found!");
            GUILayout.EndArea();
            return;
        }

        if (!network.IsListening)
        {
            if (GUILayout.Button("HOST", GUILayout.Height(35)))
            {
                StartHost(network);
            }

            if (GUILayout.Button("JOIN CLIENT", GUILayout.Height(35)))
            {
                StartClient(network);
            }
        }
        else
        {
            if (network.IsHost)
            {
                GUILayout.Label("Mode: HOST");
            }
            else if (network.IsClient)
            {
                GUILayout.Label("Mode: CLIENT");
            }

            if (network.IsServer)
            {
                GUILayout.Label(
                    "Connected players: " +
                    network.ConnectedClientsIds.Count
                );
            }
            else
            {
                GUILayout.Label(
                    network.IsConnectedClient
                        ? "Connected to host!"
                        : "Connecting..."
                );
            }

            if (GUILayout.Button("DISCONNECT"))
            {
                network.Shutdown();
                message = "Disconnected";
            }
        }

        GUILayout.Label(message);
        GUILayout.EndArea();
    }

    private void StartHost(NetworkManager network)
    {
        UnityTransport transport =
            network.GetComponent<UnityTransport>();

        if (transport == null)
        {
            message = "Unity Transport missing!";
            return;
        }

        transport.SetConnectionData(
            "127.0.0.1",
            7777,
            "0.0.0.0"
        );

        message = network.StartHost()
            ? "Host started"
            : "Host failed";
    }

    private void StartClient(NetworkManager network)
    {
        UnityTransport transport =
            network.GetComponent<UnityTransport>();

        if (transport == null)
        {
            message = "Unity Transport missing!";
            return;
        }

        transport.SetConnectionData(
            "127.0.0.1",
            7777
        );

        message = network.StartClient()
            ? "Connecting to host..."
            : "Client failed";
    }
}
