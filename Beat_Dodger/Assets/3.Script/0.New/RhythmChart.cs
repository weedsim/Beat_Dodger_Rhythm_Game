using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NoteData
{
    public double time;      // Hit time (in seconds)
    public int lane;         // Start lane index
    public int span = 1;     // How many lanes it spans
    public NoteType type = NoteType.Normal;

    public NoteData(double time, int lane, int span = 1, NoteType type = NoteType.Normal)
    {
        this.time = time;
        this.lane = lane;
        this.span = span;
        this.type = type;
    }
}

[CreateAssetMenu(fileName = "NewRhythmChart", menuName = "Rhythm/Chart")]
public class RhythmChart : ScriptableObject
{
    public string audioName;
    public AudioClip musicClip;
    public float bpm = 120f;
    public List<NoteData> notes = new List<NoteData>();

    public void SortNotes()
    {
        notes.Sort((a, b) => a.time.CompareTo(b.time));
    }

    // Help for Editor: Get note at specific beat/lane
    public NoteData GetNoteAt(double time, int lane, float threshold = 0.01f)
    {
        return notes.Find(n => Math.Abs(n.time - time) < threshold && n.lane == lane);
    }
}
