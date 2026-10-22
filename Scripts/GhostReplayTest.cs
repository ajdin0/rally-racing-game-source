
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GhostReplayTest : MonoBehaviour
{
    [Header("Cars")]
    public Transform sourceCar;
    public Transform ghostCar;

    [Header("Recording")]
    public float sampleInterval = 0.05f;
    public bool loopPlayback = true;

    private struct GhostFrame
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;

        public GhostFrame(float t, Vector3 p, Quaternion r)
        {
            time = t;
            position = p;
            rotation = r;
        }
    }

    private readonly List<GhostFrame> frames =
        new List<GhostFrame>();

    private Renderer[] ghostRenderers;

    private bool recording;
    private bool playing;

    private float recordTime;
    private float nextSampleTime;
    private float playbackTime;

    private int playbackIndex;

    private void Start()
    {
        if (ghostCar != null)
        {
            ghostRenderers =
                ghostCar.GetComponentsInChildren<Renderer>(true);

            SetGhostVisible(false);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f5Key.wasPressedThisFrame)
                ToggleRecording();

            if (Keyboard.current.f6Key.wasPressedThisFrame)
                StartPlayback();
        }

        if (playing)
            UpdatePlayback();
    }

    private void FixedUpdate()
    {
        if (!recording || sourceCar == null)
            return;

        recordTime += Time.fixedDeltaTime;

        if (recordTime >= nextSampleTime)
        {
            AddFrame(recordTime);
            nextSampleTime += Mathf.Max(0.02f, sampleInterval);
        }
    }

    private void ToggleRecording()
    {
        if (recording)
        {
            recording = false;

            Debug.Log(
                "Ghost recording finished: " +
                frames.Count + " frames"
            );

            return;
        }

        if (sourceCar == null ||
            !sourceCar.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("Select and activate the source car first.");
            return;
        }

        playing = false;
        recording = true;

        frames.Clear();

        recordTime = 0f;
        nextSampleTime = Mathf.Max(0.02f, sampleInterval);

        AddFrame(0f);
        SetGhostVisible(false);

        Debug.Log("Ghost recording started - press F5 to stop.");
    }

    private void AddFrame(float time)
    {
        frames.Add(
            new GhostFrame(
                time,
                sourceCar.position,
                sourceCar.rotation
            )
        );
    }

    private void StartPlayback()
    {
        if (recording)
        {
            Debug.LogWarning("Stop recording with F5 first.");
            return;
        }

        if (ghostCar == null || frames.Count < 2)
        {
            Debug.LogWarning("No ghost recording available.");
            return;
        }

        playing = true;
        playbackTime = 0f;
        playbackIndex = 0;

        ghostCar.SetPositionAndRotation(
            frames[0].position,
            frames[0].rotation
        );

        SetGhostVisible(true);

        Debug.Log("Ghost playback started.");
    }

    private void UpdatePlayback()
    {
        if (ghostCar == null || frames.Count < 2)
            return;

        playbackTime += Time.deltaTime;

        float duration = frames[frames.Count - 1].time;

        if (playbackTime >= duration)
        {
            if (loopPlayback && duration > 0f)
            {
                playbackTime %= duration;
                playbackIndex = 0;
            }
            else
            {
                playing = false;
                SetGhostVisible(false);
                return;
            }
        }

        while (playbackIndex < frames.Count - 2 &&
               frames[playbackIndex + 1].time < playbackTime)
        {
            playbackIndex++;
        }

        GhostFrame a = frames[playbackIndex];
        GhostFrame b = frames[playbackIndex + 1];

        float t = Mathf.InverseLerp(
            a.time,
            b.time,
            playbackTime
        );

        ghostCar.position =
            Vector3.Lerp(a.position, b.position, t);

        ghostCar.rotation =
            Quaternion.Slerp(a.rotation, b.rotation, t);
    }

    private void SetGhostVisible(bool visible)
    {
        if (ghostRenderers == null)
            return;

        foreach (Renderer renderer in ghostRenderers)
        {
            renderer.enabled = visible;
        }
    }
}
