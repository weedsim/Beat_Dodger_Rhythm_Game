using UnityEngine;

public class RhythmConfig : MonoBehaviour
{
    public static RhythmConfig Instance { get; private set; }

    [Header("Lane Settings")]
    public int LaneCount = 4;
    public float LaneSpacing = 2.4f;
    public float YOffset = 0f;
    
    [Header("Position Settings")]
    public float SpawnLineZ = 20f;
    public float JudgeLineZ = -6f;

    [Header("Judgment Thresholds (Seconds)")]
    public float PerfectThreshold = 0.05f;
    public float GreatThreshold = 0.1f;
    public float GoodThreshold = 0.15f;
    public float MissThreshold = 0.2f;

    [Header("Sync Settings")]
    public float GlobalSyncOffset = 0f; // Seconds (Positive = Audio delay, Negative = Visual delay)

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float BGMVolume = 1f;
    [Range(0f, 1f)] public float SFXVolume = 1f;

    [Header("Player Settings")]
    public string PlayerNickname = "Dodger";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else if (Instance != this) Destroy(gameObject);
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("GlobalSyncOffset", GlobalSyncOffset);
        PlayerPrefs.SetFloat("BGMVolume", BGMVolume);
        PlayerPrefs.SetFloat("SFXVolume", SFXVolume);
        PlayerPrefs.SetString("PlayerNickname", PlayerNickname);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        GlobalSyncOffset = PlayerPrefs.GetFloat("GlobalSyncOffset", 0f);
        BGMVolume = PlayerPrefs.GetFloat("BGMVolume", 1f);
        SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        PlayerNickname = PlayerPrefs.GetString("PlayerNickname", "Dodger");
    }
}

public enum Judgment
{
    None,
    Perfect,
    Great,
    Good,
    Miss
}