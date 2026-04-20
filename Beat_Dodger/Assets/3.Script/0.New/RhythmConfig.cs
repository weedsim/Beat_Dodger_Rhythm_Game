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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
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