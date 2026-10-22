using System;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;

// Add ONCE to a scene GameObject (not to any car or network ghost).
// A one-time mandatory name prompt, a small in-game rename/minimize/close toolbar.
// No Unity UI Canvas, TextMeshPro or scene object references required.
public class PlayerNameWindowUI : MonoBehaviour
{
    private const string PlayerNameKey = "Rally_PlayerUsername_v1";
    private const int MaximumNameLength = 22; // Same limit as NetworkRaceCoordinator.

    [Header("Window Controls")]
    public bool showWindowButtons = true;
    public bool allowRenameDuringRace = false;

    [Header("Player Name")]
    public bool requireNameOnFirstLaunch = true;

    private string currentName = "";
    private string typedName = "";
    private bool showNameEntry;
    private bool showQuitConfirmation;
    private string nameError = "";
    private bool triedToFocusName;
    private bool hasSyncedName;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
    private const int MinimizeWindowCommand = 6;
#endif

    private void Awake()
    {
        currentName = PlayerPrefs.GetString(PlayerNameKey, "").Trim();
        if (!IsValidName(currentName)) currentName = "";
        typedName = currentName;
        showNameEntry = requireNameOnFirstLaunch && string.IsNullOrEmpty(currentName);
        ApplyNameToLeaderboard();
    }

    private void Update()
    {
        // LocalLeaderboardManager may initialize AFTER this component.
        // Keep its existing username synchronized for offline Top 5 AND
        // NetworkRaceCoordinator.ToggleLocalReady() multiplayer standings.
        if (!string.IsNullOrEmpty(currentName))
            ApplyNameToLeaderboard();
    }

    private void ApplyNameToLeaderboard()
    {
        if (string.IsNullOrEmpty(currentName)) return;
        LocalLeaderboardManager manager = LocalLeaderboardManager.Instance;
        if (manager == null) return;

        // Supports both a public field and a writable property called localUsername.
        // No modifications to the user's existing leaderboard script are necessary.
        Type type = manager.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = type.GetField("localUsername", flags);
        if (field != null && field.FieldType == typeof(string))
        {
            string previous = (string)field.GetValue(manager);
            if (previous != currentName)
                field.SetValue(manager, currentName);
            hasSyncedName = true;
            return;
        }
        PropertyInfo prop = type.GetProperty("localUsername", flags);
        if (prop != null && prop.PropertyType == typeof(string) && prop.CanWrite)
        {
            string previous = (string)prop.GetValue(manager, null);
            if (previous != currentName)
                prop.SetValue(manager, currentName, null);
            hasSyncedName = true;
        }
        else if (!hasSyncedName)
        {
            Debug.LogWarning("PlayerNameWindowUI could not find a writable localUsername on LocalLeaderboardManager.", this);
            hasSyncedName = true; // Only warn once.
        }
    }

    private static bool IsValidName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().Length >= 2 &&
               !value.Trim().Equals("Player", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNameLocked()
    {
        NetworkRaceCoordinator coordinator = FindFirstObjectByType<NetworkRaceCoordinator>();
        return coordinator != null && coordinator.IsSpawned &&
               (coordinator.RaceLaunchOrdered || coordinator.LocalPlayerReady);
    }

    private void SaveName()
    {
        string cleaned = typedName.Trim().Replace("<", "_").Replace(">", "_")
            .Replace("\r", " ").Replace("\n", " ");
        if (cleaned.Length > MaximumNameLength)
            cleaned = cleaned.Substring(0, MaximumNameLength);
        if (!IsValidName(cleaned))
        {
            nameError = "Enter at least 2 characters; don't use 'Player'.";
            return;
        }
        currentName = cleaned;
        typedName = cleaned;
        PlayerPrefs.SetString(PlayerNameKey, currentName);
        PlayerPrefs.Save();
        ApplyNameToLeaderboard();
        showNameEntry = false;
        nameError = "";
        triedToFocusName = false;
        GUI.FocusControl(null);
    }

    private void MinimizeGame()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr window = GetActiveWindow();
        if (window == IntPtr.Zero) window = GetForegroundWindow();
        if (window != IntPtr.Zero)
            ShowWindow(window, MinimizeWindowCommand);
#else
        Debug.Log("Minimize is supported by this button in the Windows standalone build. In the Unity Editor, use the Editor window controls.");
#endif
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnGUI()
    {
        // Higher priority than default OnGUI overlays.
        GUI.depth = -90;
        float toolWidth = 156f;
        float left = Mathf.Max(8f, (Screen.width - toolWidth) * 0.5f);
        bool nameLocked = IsNameLocked();

        if (showWindowButtons && Screen.width >= 180)
        {
            GUILayout.BeginArea(new Rect(left, 6f, toolWidth, 34f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (!nameLocked || allowRenameDuringRace)
            {
                if (GUILayout.Button("NAME", GUILayout.Width(62f), GUILayout.Height(23f)))
                {
                    typedName = currentName;
                    showNameEntry = true;
                    triedToFocusName = false;
                }
            }
            else
                GUILayout.Space(62f);

            if (GUILayout.Button("-", GUILayout.Width(31f), GUILayout.Height(23f)))
                MinimizeGame();
            if (GUILayout.Button("X", GUILayout.Width(31f), GUILayout.Height(23f)))
                showQuitConfirmation = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        if (showNameEntry)
            DrawNamePrompt();
        else if (showQuitConfirmation)
            DrawQuitPrompt();
    }

    private void DrawNamePrompt()
    {
        float width = Mathf.Min(380f, Screen.width - 24f);
        float height = 196f;
        Rect rect = new Rect((Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f, width, height);
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Space(8f);
        GUILayout.Label("CHOOSE YOUR PLAYER NAME");
        GUILayout.Label("Shown in race results and your local records.");
        GUI.SetNextControlName("PlayerNameInput");
        typedName = GUILayout.TextField(typedName, MaximumNameLength,
            GUILayout.Height(30f));
        if (!triedToFocusName)
        {
            GUI.FocusControl("PlayerNameInput");
            triedToFocusName = true;
        }
        if (!string.IsNullOrEmpty(nameError))
            GUILayout.Label(nameError);
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SAVE NAME", GUILayout.Height(30f)))
            SaveName();
        if (!string.IsNullOrEmpty(currentName) &&
            GUILayout.Button("CANCEL", GUILayout.Width(100f), GUILayout.Height(30f)))
        {
            showNameEntry = false;
            nameError = "";
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        Event ev = Event.current;
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Return)
        {
            SaveName();
            ev.Use();
        }
    }

    private void DrawQuitPrompt()
    {
        float width = Mathf.Min(290f, Screen.width - 24f);
        float height = 124f;
        Rect rect = new Rect((Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f, width, height);
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Label("Exit the game?");
        GUILayout.Label("Your current race will end.");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CANCEL", GUILayout.Height(30f)))
            showQuitConfirmation = false;
        if (GUILayout.Button("QUIT", GUILayout.Height(30f)))
            QuitGame();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
