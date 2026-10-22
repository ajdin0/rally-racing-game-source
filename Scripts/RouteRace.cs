using UnityEngine;

public class RouteRace : MonoBehaviour
{
    [Header("Route")]
    public Transform startPoint;
    public RaceCheckpoint[] checkpoints;

    [Header("Starting Grid")]
    public Transform[] gridPositions;

    [Header("Race Settings")]
    [Min(1)]
    public int lapsToComplete = 3;

    [Header("Race State")]
    public bool raceStarted;
    public bool raceFinished;

    public bool lapActive;

    public int currentLap;
    public int nextCheckpoint;

    public float raceTime;

    private GameObject racingCar;

    private void Update()
    {
        if (raceStarted && !raceFinished)
        {
            raceTime += Time.deltaTime;
        }
    }

    public void BeginRace(GameObject car)
    {
        if (car == null)
            return;

        racingCar = car;

        raceStarted = true;
        raceFinished = false;

        // Lap 1 starts as soon as the race begins (at GO).
        lapActive = true;

        currentLap = 1;
        nextCheckpoint = 0;

        raceTime = 0f;

        Debug.Log(
            "RACE STARTED - LAP 1 / " + lapsToComplete
        );
    }

    public void CrossFinish(GameObject car)
    {
        if (!raceStarted ||
            raceFinished)
        {
            return;
        }

        if (car != racingCar)
            return;

        // Lap 1 already began at GO. Each valid finish crossing
        // completes the current lap, including the very first one.
        if (!lapActive)
            return;

        // All checkpoints must be completed.
        if (nextCheckpoint <
            checkpoints.Length)
        {
            Debug.Log(
                "Finish ignored - lap checkpoints incomplete."
            );

            return;
        }

        // Final lap completed.
        if (currentLap >= lapsToComplete)
        {
            float finalTime = raceTime;

            raceFinished = true;
            raceStarted = false;
            lapActive = false;

            FreezeFinishedCar();

            Debug.Log(
                "RACE FINISHED! Time: " +
                finalTime.ToString("F2") +
                " seconds"
            );

            if (LocalLeaderboardManager.Instance != null)
            {
                LocalLeaderboardManager.Instance
                    .RecordLocalResult(
                        gameObject.name,
                        car.name,
                        lapsToComplete,
                        finalTime
                    );
            }

            return;
        }

        // Start next lap.
        currentLap++;
        nextCheckpoint = 0;

        lapActive = true;

        Debug.Log(
            "LAP COMPLETED - STARTING LAP " +
            currentLap +
            " / " +
            lapsToComplete
        );
    }

    public void PassCheckpoint(
        int checkpointIndex,
        GameObject car)
    {
        if (!raceStarted ||
            raceFinished ||
            !lapActive)
        {
            return;
        }

        if (car != racingCar)
            return;

        if (checkpointIndex != nextCheckpoint)
            return;

        nextCheckpoint++;

        Debug.Log(
            "Checkpoint " +
            nextCheckpoint +
            " / " +
            checkpoints.Length
        );
    }

    private void FreezeFinishedCar()
    {
        if (racingCar == null)
            return;

        RallyCarController controller =
            racingCar.GetComponent<RallyCarController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        Rigidbody rb =
            racingCar.GetComponent<Rigidbody>();

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.isKinematic = true;
        }
    }

    public void ResetRace()
    {
        raceStarted = false;
        raceFinished = false;
        lapActive = false;

        currentLap = 0;
        nextCheckpoint = 0;

        raceTime = 0f;

        racingCar = null;
    }
}