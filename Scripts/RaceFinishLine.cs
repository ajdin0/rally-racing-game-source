using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RaceFinishLine : MonoBehaviour
{
    public RouteRace routeRace;

    private void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null)
            box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;

        if (rb == null)
            return;

        RallyCarController car =
            rb.GetComponent<RallyCarController>();

        if (car == null)
            return;

        if (routeRace != null)
        {
            routeRace.CrossFinish(
                car.gameObject
            );
        }
    }
}