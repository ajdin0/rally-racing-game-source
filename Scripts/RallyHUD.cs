using UnityEngine;
using TMPro;

public class RallyHUD : MonoBehaviour
{
    public RallyCarController carController;
    public RallySurfaceGrip surfaceGrip;

    public TMP_Text speedText;
    public TMP_Text surfaceText;

    private void Update()
    {
        if (carController != null && speedText != null)
        {
            speedText.text =
                "Speed: " +
                Mathf.RoundToInt(
                    carController.currentSpeedKmh
                ) +
                " km/h";
        }

        if (surfaceGrip != null && surfaceText != null)
        {
            surfaceText.text =
                "Surface: " +
                surfaceGrip.currentSurface;
        }
    }
}