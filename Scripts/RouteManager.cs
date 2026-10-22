
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class RouteManager : MonoBehaviour
{
    [Header("Routes")]
    public RouteRace[] routes;

    [Header("Starting Route")]
    public int startingRoute = 0;

    [Header("Car Spawn")]
    public Transform carSpawnPoint;

    [Header("Car Selection")]
    public CarSelectionManager carSelectionManager;

    [Header("Player Count - Testing")]
    [Range(1, 6)]
    public int playerCount = 1;

    [Header("Multiplayer Route Sync")]
    public NetworkRouteSync networkRouteSync;

    [Header("Route Selection Buttons")]
    public Button[] routeButtons;

    public int CurrentRouteIndex { get; private set; }

    private int currentGridPosition = -1;
    private bool? previousCanSelectRoutes;

    private void Start()
    {
        ApplyRoute(startingRoute);
    }

    private void Update()
    {
        bool canSelect = CanSelectRoutes();

        if (previousCanSelectRoutes != canSelect)
        {
            previousCanSelectRoutes = canSelect;

            if (routeButtons != null)
            {
                foreach (Button button in routeButtons)
                {
                    if (button != null)
                    {
                        button.interactable = canSelect;
                    }
                }
            }
        }
    }

    private bool CanSelectRoutes()
    {
        NetworkManager network =
            NetworkManager.Singleton;

        if (network == null || !network.IsListening)
            return true;

        return network.IsServer;
    }

    // Called by the existing UI route buttons.
    public void SelectRoute(int routeIndex)
    {
        if (!IsValidRoute(routeIndex))
            return;

        NetworkManager network =
            NetworkManager.Singleton;

        if (network != null && network.IsListening)
        {
            if (!network.IsServer)
            {
                Debug.Log(
                    "Only the Host can select a route."
                );
                return;
            }

            if (networkRouteSync == null ||
                !networkRouteSync.IsSpawned)
            {
                Debug.LogWarning(
                    "NetworkRouteSync is not ready."
                );
                return;
            }

            networkRouteSync.SetRouteFromHost(
                routeIndex
            );

            return;
        }

        // Offline testing still works.
        ApplyRoute(routeIndex);
    }

    // Called when the Host's route arrives
    // through the network.
    public void ApplyNetworkRoute(int routeIndex)
    {
        ApplyRoute(routeIndex);
    }

    private bool IsValidRoute(int index)
    {
        return routes != null &&
               index >= 0 &&
               index < routes.Length &&
               routes[index] != null;
    }

    private void ApplyRoute(int routeIndex)
    {
        if (!IsValidRoute(routeIndex))
            return;

        CurrentRouteIndex = routeIndex;

        for (int i = 0; i < routes.Length; i++)
        {
            if (routes[i] != null)
            {
                routes[i].gameObject.SetActive(
                    i == routeIndex
                );
            }
        }

        RouteRace selectedRoute = routes[routeIndex];

        selectedRoute.ResetRace();

        ChooseGridPosition(selectedRoute);

        if (carSelectionManager != null)
        {
            carSelectionManager.RefreshSelectedCarAtSpawn();
        }

        Debug.Log(
            "Selected Route " +
            (routeIndex + 1)
        );
    }

    private void ChooseGridPosition(RouteRace route)
    {
        currentGridPosition = -1;

        if (route.gridPositions != null &&
            route.gridPositions.Length > 0)
        {
            int usablePositions = Mathf.Min(
                playerCount,
                route.gridPositions.Length
            );

            if (usablePositions > 0)
            {
                currentGridPosition =
                    Random.Range(0, usablePositions);

                Transform grid =
                    route.gridPositions[currentGridPosition];

                if (grid != null &&
                    carSpawnPoint != null)
                {
                    carSpawnPoint.position = grid.position;
                    carSpawnPoint.rotation = grid.rotation;
                    return;
                }
            }
        }

        if (route.startPoint != null &&
            carSpawnPoint != null)
        {
            carSpawnPoint.position =
                route.startPoint.position;

            carSpawnPoint.rotation =
                route.startPoint.rotation;
        }
    }

    public RouteRace GetCurrentRoute()
    {
        if (!IsValidRoute(CurrentRouteIndex))
            return null;

        return routes[CurrentRouteIndex];
    }
}
