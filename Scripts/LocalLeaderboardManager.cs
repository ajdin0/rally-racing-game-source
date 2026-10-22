using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public class LocalLeaderboardManager : MonoBehaviour
{
    public static LocalLeaderboardManager Instance { get; private set; }

    [Header("Local Player")]
    public string localUsername = "Player";

    [Header("Results UI")]
    public GameObject resultsPanel;
    public TMP_Text finalTimeText;
    public TMP_Text bestTimesText;

    [Header("New Race")]
    public CarSelectionManager carSelectionManager;

    private LeaderboardData data;

    private string SavePath
    {
        get
        {
            return Path.Combine(
                Application.persistentDataPath,
                "local_leaderboard.json"
            );
        }
    }

    [Serializable]
    public class LeaderboardEntry
    {
        public string routeName;
        public string carName;
        public int lapCount;

        public string username;
        public float timeSeconds;
    }

    [Serializable]
    public class LeaderboardData
    {
        public List<LeaderboardEntry> entries =
            new List<LeaderboardEntry>();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Load();

        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }
    }

    public void RecordLocalResult(
        string routeName,
        string carName,
        int lapCount,
        float timeSeconds)
    {
        LeaderboardEntry newEntry =
            new LeaderboardEntry();

        newEntry.routeName = routeName;
        newEntry.carName = carName;
        newEntry.lapCount = lapCount;

        newEntry.username =
            string.IsNullOrWhiteSpace(localUsername)
                ? "Player"
                : localUsername;

        newEntry.timeSeconds = timeSeconds;

        data.entries.Add(newEntry);

        SortAndTrim(
            routeName,
            carName,
            lapCount
        );

        Save();

        ShowResults(
            routeName,
            carName,
            lapCount,
            timeSeconds
        );
    }

    public void NewRace()
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }

        if (carSelectionManager != null)
        {
            carSelectionManager.ReturnToSelection();
        }
    }

    private void SortAndTrim(
        string routeName,
        string carName,
        int lapCount)
    {
        data.entries.Sort(
            (a, b) =>
                a.timeSeconds.CompareTo(
                    b.timeSeconds
                )
        );

        int matchingCount = 0;

        for (int i = 0;
             i < data.entries.Count;
             i++)
        {
            LeaderboardEntry entry =
                data.entries[i];

            bool sameLeaderboard =
                entry.routeName == routeName &&
                entry.carName == carName &&
                entry.lapCount == lapCount;

            if (!sameLeaderboard)
                continue;

            matchingCount++;

            if (matchingCount > 5)
            {
                data.entries.RemoveAt(i);
                i--;
            }
        }
    }

    private void ShowResults(
        string routeName,
        string carName,
        int lapCount,
        float finalTime)
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(true);
        }

        if (finalTimeText != null)
        {
            finalTimeText.text =
                "Finished: " +
                FormatTime(finalTime);
        }

        if (bestTimesText == null)
            return;

        List<LeaderboardEntry> topTimes =
            GetTopTimes(
                routeName,
                carName,
                lapCount
            );

        string text = "Best Times\n\n";

        for (int i = 0;
             i < topTimes.Count;
             i++)
        {
            LeaderboardEntry entry =
                topTimes[i];

            text +=
                (i + 1) +
                ". " +
                entry.username +
                " - " +
                FormatTime(
                    entry.timeSeconds
                ) +
                "\n";
        }

        for (int i = topTimes.Count;
             i < 5;
             i++)
        {
            text +=
                (i + 1) +
                ". ---\n";
        }

        bestTimesText.text = text;
    }

    public List<LeaderboardEntry> GetTopTimes(
        string routeName,
        string carName,
        int lapCount)
    {
        List<LeaderboardEntry> result =
            new List<LeaderboardEntry>();

        foreach (LeaderboardEntry entry
                 in data.entries)
        {
            if (entry.routeName != routeName)
                continue;

            if (entry.carName != carName)
                continue;

            if (entry.lapCount != lapCount)
                continue;

            result.Add(entry);
        }

        result.Sort(
            (a, b) =>
                a.timeSeconds.CompareTo(
                    b.timeSeconds
                )
        );

        if (result.Count > 5)
        {
            result.RemoveRange(
                5,
                result.Count - 5
            );
        }

        return result;
    }

    private void Save()
    {
        string json =
            JsonUtility.ToJson(
                data,
                true
            );

        File.WriteAllText(
            SavePath,
            json
        );
    }

    private void Load()
    {
        if (!File.Exists(SavePath))
        {
            data =
                new LeaderboardData();

            return;
        }

        try
        {
            string json =
                File.ReadAllText(
                    SavePath
                );

            data =
                JsonUtility.FromJson<LeaderboardData>(
                    json
                );

            if (data == null)
            {
                data =
                    new LeaderboardData();
            }

            if (data.entries == null)
            {
                data.entries =
                    new List<LeaderboardEntry>();
            }
        }
        catch
        {
            data =
                new LeaderboardData();
        }
    }

    private string FormatTime(
        float totalSeconds)
    {
        int minutes =
            Mathf.FloorToInt(
                totalSeconds / 60f
            );

        float seconds =
            totalSeconds -
            minutes * 60f;

        return string.Format(
            "{0:00}:{1:00.00}",
            minutes,
            seconds
        );
    }
}