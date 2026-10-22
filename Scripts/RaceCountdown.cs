using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class RaceCountdown : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text countdownText;

    [Header("Offline Timing")]
    public float numberDuration = 1f;
    public float goDuration = 0.8f;

    private Coroutine countdownRoutine;
    private RigidbodyConstraints savedConstraints;
    private Rigidbody lockedBody;

    private void Awake()
    {
        HideText();
    }

    // Existing single-player/offline start is preserved.
    public void BeginCountdown(GameObject car, CarSelectionManager selection)
    {
        StopPreviousCountdown();
        countdownRoutine = StartCoroutine(OfflineCountdown(car, selection));
    }

    // In multiplayer, every computer uses the same server-synchronized GO timestamp.
    public void BeginNetworkCountdown(
        GameObject car,
        CarSelectionManager selection,
        double goServerTime)
    {
        StopPreviousCountdown();
        countdownRoutine = StartCoroutine(
            NetworkCountdown(car, selection, goServerTime));
    }

    private IEnumerator OfflineCountdown(GameObject car, CarSelectionManager selection)
    {
        if (car == null)
            yield break;

        LockCarWithoutDisablingInput(car);
        ShowText("3");
        yield return new WaitForSeconds(numberDuration);

        ShowText("2");
        yield return new WaitForSeconds(numberDuration);

        ShowText("1");
        yield return new WaitForSeconds(numberDuration);

        ReleaseCar();
        ShowText("GO!");
        selection?.NotifyRaceStarted();

        yield return new WaitForSeconds(goDuration);
        HideText();
        countdownRoutine = null;
    }

    private IEnumerator NetworkCountdown(
        GameObject car,
        CarSelectionManager selection,
        double goServerTime)
    {
        if (car == null)
            yield break;

        LockCarWithoutDisablingInput(car);

        while (true)
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening)
            {
                ReleaseCar();
                HideText();
                countdownRoutine = null;
                yield break;
            }

            double secondsRemaining = goServerTime - network.ServerTime.Time;
            if (secondsRemaining <= 0.0)
                break;

            // Values greater than 3 indicate a small pre-countdown buffer.
            int displayedNumber = Mathf.Clamp(
                Mathf.CeilToInt((float)secondsRemaining), 1, 3);
            ShowText(displayedNumber.ToString());
            yield return null;
        }

        ReleaseCar();
        ShowText("GO!");
        selection?.NotifyRaceStarted();

        yield return new WaitForSeconds(goDuration);
        HideText();
        countdownRoutine = null;
    }

    private void LockCarWithoutDisablingInput(GameObject car)
    {
        RallyCarController controller = car.GetComponent<RallyCarController>();
        if (controller != null)
            controller.enabled = true;

        Rigidbody body = car.GetComponent<Rigidbody>();
        lockedBody = body;
        if (body == null)
            return;

        // This is important: the controller can continue using AddForce and the
        // W + Space launch-charge logic, without writing to a kinematic body.
        if (body.isKinematic)
            body.isKinematic = false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        savedConstraints = body.constraints;
        body.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void ReleaseCar()
    {
        if (lockedBody == null)
            return;

        lockedBody.constraints = savedConstraints;
        lockedBody.linearVelocity = Vector3.zero;
        lockedBody.angularVelocity = Vector3.zero;
        lockedBody = null;
    }

    private void StopPreviousCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        ReleaseCar();
        HideText();
    }

    private void ShowText(string value)
    {
        if (countdownText == null)
            return;

        if (!countdownText.gameObject.activeSelf)
            countdownText.gameObject.SetActive(true);

        countdownText.text = value;
    }

    private void HideText()
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopPreviousCountdown();
    }
}
