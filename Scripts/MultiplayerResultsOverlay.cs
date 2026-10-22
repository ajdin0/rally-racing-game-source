using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Shared multiplayer results, compact while driving.
// Does not save local records: RouteRace + LocalLeaderboardManager already do that.
public class MultiplayerResultsOverlay : MonoBehaviour
{
    [Header("Scene References")]
    public NetworkRaceCoordinator raceCoordinator;

    [Tooltip("Optional old single-player NEW RACE button in ResultsPanel.")]
    public Button singlePlayerNewRaceButton;

    [Header("Compact Race UI")]
    public bool showCompactRaceButton = true;
    public bool automaticallyExpandWhenLocalPlayerFinishes = false;

    private bool? previouslyConnected;
    private bool manuallyExpanded;
    private bool wasRaceInProgress;

    private void Update()
    {
        NetworkManager network = NetworkManager.Singleton;
        bool connected = network != null && network.IsListening;

        if (previouslyConnected != connected)
        {
            previouslyConnected = connected;
            if (singlePlayerNewRaceButton != null)
                singlePlayerNewRaceButton.gameObject.SetActive(!connected);
        }

        if (!connected)
        {
            manuallyExpanded = false;
            wasRaceInProgress = false;
            return;
        }

        FindCoordinator();
        bool raceInProgress = raceCoordinator != null &&
                              raceCoordinator.IsSpawned &&
                              raceCoordinator.RaceLaunchOrdered;

        if (wasRaceInProgress && !raceInProgress)
            manuallyExpanded = false;

        wasRaceInProgress = raceInProgress;
    }

    private void FindCoordinator()
    {
        if (raceCoordinator == null)
            raceCoordinator = FindFirstObjectByType<NetworkRaceCoordinator>();
    }

    private void OnGUI()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening)
            return;

        FindCoordinator();
        if (raceCoordinator == null || !raceCoordinator.IsSpawned ||
            !raceCoordinator.RaceLaunchOrdered)
            return;

        bool completed = raceCoordinator.RaceComplete;
        bool showFull = completed || manuallyExpanded;

        if (!showFull && automaticallyExpandWhenLocalPlayerFinishes)
        {
            RouteManager routes = FindFirstObjectByType<RouteManager>();
            RouteRace route = routes != null ? routes.GetCurrentRoute() : null;
            showFull = route != null && route.raceFinished;
        }

        // During driving, default is just one little button at the bottom-right.
        if (!showFull)
        {
            if (!showCompactRaceButton)
                return;

            float width = Mathf.Min(165f, Mathf.Max(80f, Screen.width - 20f));
            Rect badge = new Rect(
                Screen.width - width - 10f,
                Screen.height - 47f,
                width,
                36f
            );

            GUILayout.BeginArea(badge, GUI.skin.box);
            if (GUILayout.Button("STANDINGS  +", GUILayout.Height(26f)))
                manuallyExpanded = true;
            GUILayout.EndArea();
            return;
        }

        // Expanded panel remains at bottom-right so it doesn't cover the
        // normal lap/time HUD or the Relay status in the upper-right.
        float panelWidth = Mathf.Min(285f, Mathf.Max(160f, Screen.width - 20f));
        float panelHeight = Mathf.Min(completed ? 278f : 220f,
                                    Mathf.Max(120f, Screen.height - 20f));

        Rect panel = new Rect(
            Screen.width - panelWidth - 10f,
            Screen.height - panelHeight - 10f,
            panelWidth,
            panelHeight
        );

        GUILayout.BeginArea(panel, GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(completed ? "FINAL STANDINGS" : "LIVE STANDINGS");
        if (!completed && GUILayout.Button("-", GUILayout.Width(28f)))
            manuallyExpanded = false;
        GUILayout.EndHorizontal();

        GUILayout.Space(5f);
        GUILayout.Label(raceCoordinator.BuildStandingsText(), GUILayout.MinHeight(85f));

        if (completed)
        {
            if (network.IsServer)
            {
                if (GUILayout.Button("RETURN ALL TO LOBBY", GUILayout.Height(36f)))
                    raceCoordinator.ReturnEveryoneToLobbyAsHost();
            }
            else
            {
                GUILayout.Label("Waiting for host to return to lobby...");
            }
        }
        else
        {
            GUILayout.Label("Others may still be racing.");
        }

        GUILayout.EndArea();
    }
}
