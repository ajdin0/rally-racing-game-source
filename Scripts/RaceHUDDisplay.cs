using TMPro;
using UnityEngine;

public class RaceHUDDisplay : MonoBehaviour
{
    [Header("Race")]
    public RouteManager routeManager;

    [Header("UI")]
    public TMP_Text lapText;
    public TMP_Text raceTimeText;

    private void Update()
    {
        if (routeManager == null)
        {
            HideHUD();
            return;
        }

        RouteRace route =
            routeManager.GetCurrentRoute();

        if (route == null)
        {
            HideHUD();
            return;
        }

        // Hide before the race begins.
        // Keep it visible after the finish so
        // the player can see the final time.
        bool shouldShow =
            route.raceStarted ||
            route.raceFinished;

        if (!shouldShow)
        {
            HideHUD();
            return;
        }

        ShowHUD();

        UpdateLap(route);
        UpdateTime(route);
    }

    private void UpdateLap(RouteRace route)
    {
        if (lapText == null)
            return;

        int displayedLap =
            Mathf.Clamp(
                route.currentLap,
                1,
                route.lapsToComplete
            );

        lapText.text =
            "Lap " +
            displayedLap +
            " / " +
            route.lapsToComplete;
    }

    private void UpdateTime(RouteRace route)
    {
        if (raceTimeText == null)
            return;

        raceTimeText.text =
            FormatTime(route.raceTime);
    }

    private string FormatTime(float totalSeconds)
    {
        if (totalSeconds < 0f)
            totalSeconds = 0f;

        int minutes =
            Mathf.FloorToInt(
                totalSeconds / 60f
            );

        float seconds =
            totalSeconds -
            (minutes * 60f);

        return string.Format(
            "{0:00}:{1:00.00}",
            minutes,
            seconds
        );
    }

    private void ShowHUD()
    {
        if (lapText != null &&
            !lapText.gameObject.activeSelf)
        {
            lapText.gameObject.SetActive(true);
        }

        if (raceTimeText != null &&
            !raceTimeText.gameObject.activeSelf)
        {
            raceTimeText.gameObject.SetActive(true);
        }
    }

    private void HideHUD()
    {
        if (lapText != null &&
            lapText.gameObject.activeSelf)
        {
            lapText.gameObject.SetActive(false);
        }

        if (raceTimeText != null &&
            raceTimeText.gameObject.activeSelf)
        {
            raceTimeText.gameObject.SetActive(false);
        }
    }
}