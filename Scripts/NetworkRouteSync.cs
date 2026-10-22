
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class NetworkRouteSync : NetworkBehaviour
{
    [Header("Route Manager")]
    public RouteManager routeManager;

    private NetworkVariable<int> selectedRoute =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        selectedRoute.OnValueChanged += OnRouteChanged;

        if (routeManager == null)
        {
            routeManager =
                FindFirstObjectByType<RouteManager>();
        }

        if (IsServer)
        {
            if (routeManager != null)
            {
                selectedRoute.Value =
                    routeManager.CurrentRouteIndex;
            }
        }
        else
        {
            // A joining client applies the host's
            // currently selected route.
            ApplySelectedRoute(selectedRoute.Value);
        }
    }

    public void SetRouteFromHost(int index)
    {
        if (!IsSpawned || !IsServer)
            return;

        selectedRoute.Value = index;

        // Apply locally for the host too.
        ApplySelectedRoute(index);
    }

    private void OnRouteChanged(
        int previousRoute,
        int newRoute)
    {
        // Host already applies the change directly.
        if (IsServer)
            return;

        ApplySelectedRoute(newRoute);
    }

    private void ApplySelectedRoute(int index)
    {
        if (routeManager != null)
        {
            routeManager.ApplyNetworkRoute(index);
        }
    }

    public override void OnNetworkDespawn()
    {
        selectedRoute.OnValueChanged -= OnRouteChanged;
    }
}
