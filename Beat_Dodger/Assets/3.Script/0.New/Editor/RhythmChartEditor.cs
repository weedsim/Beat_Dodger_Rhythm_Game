using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class RhythmChartEditor : EditorWindow
{
    private RhythmChart targetChart;
    private Vector2 scrollPos;
    
    private const float LaneWidth = 60f;
    private const float PixelsPerSecond = 200f;
    private const float TimelineWidth = 300f;
    private const float SidebarWidth = 200f;

    private AudioSource previewSource;
    private bool isPlaying = false;
    private float lastEditorTime = 0;

    private NoteType selectedType = NoteType.Normal;
    private int selectedSpan = 1;
    
    [MenuItem("Rhythm/Chart Editor")]
    public static void Open()
    {
        GetWindow<RhythmChartEditor>("Rhythm Editor");
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (targetChart == null)
        {
            EditorGUILayout.HelpBox("Select a RhythmChart to edit.", MessageType.Info);
            targetChart = (RhythmChart)EditorGUILayout.ObjectField("Target Chart", targetChart, typeof(RhythmChart), false);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawTimeline();
        DrawSidebar();
        EditorGUILayout.EndHorizontal();

        UpdatePlayback();
        
        if (GUI.changed || isPlaying) Repaint();
    }

    private void OnDisable()
    {
        StopPreview();
    }

    private void UpdatePlayback()
    {
        if (!isPlaying || previewSource == null) return;

        float time = previewSource.time;
        float totalHeight = targetChart.musicClip.length * PixelsPerSecond;
        float playheadY = totalHeight - (time * PixelsPerSecond);

        // Auto-scroll to keep playhead in view
        float windowHeight = position.height;
        float scrollY = playheadY - windowHeight * 0.8f;
        scrollPos.y = scrollY;
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        targetChart = (RhythmChart)EditorGUILayout.ObjectField("Chart", targetChart, typeof(RhythmChart), false, GUILayout.Width(250));
        if (targetChart != null)
        {
            targetChart.bpm = EditorGUILayout.FloatField("BPM", targetChart.bpm, GUILayout.Width(100));
            targetChart.musicClip = (AudioClip)EditorGUILayout.ObjectField("Clip", targetChart.musicClip, typeof(AudioClip), false, GUILayout.Width(200));
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTimeline()
    {
        float totalHeight = targetChart.musicClip != null ? targetChart.musicClip.length * PixelsPerSecond : 5000f;
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(TimelineWidth + 50));
        
        Rect timelineRect = GUILayoutUtility.GetRect(TimelineWidth, totalHeight);
        
        // Draw Lanes
        for (int i = 0; i <= 4; i++)
        {
            float x = i * LaneWidth;
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(x, 0), new Vector2(x, totalHeight));
        }

        // Draw Beat Grid (with Culling)
        float secondsPerBeat = 60f / targetChart.bpm;
        float viewStart = scrollPos.y;
        float viewEnd = scrollPos.y + position.height;

        for (float t = 0; t < totalHeight / PixelsPerSecond; t += secondsPerBeat)
        {
            float y = totalHeight - (t * PixelsPerSecond);
            if (y < viewStart - 50 || y > viewEnd + 50) continue; // Culling

            Handles.color = new Color(0.3f, 0.3f, 0.3f);
            Handles.DrawLine(new Vector2(0, y), new Vector2(TimelineWidth, y));
            GUI.Label(new Rect(5, y - 20, 50, 20), $"{(t/secondsPerBeat):0}");
        }

        // Draw Notes (with Culling)
        if (targetChart.notes != null)
        {
            foreach (var note in targetChart.notes)
            {
                float y = totalHeight - ((float)note.time * PixelsPerSecond);
                if (y < viewStart - 50 || y > viewEnd + 50) continue; // Culling

                float x = note.lane * LaneWidth;
                float width = note.span * LaneWidth;
                float height = 20f;

                Color noteColor = GetNoteColor(note.type);
                EditorGUI.DrawRect(new Rect(x + 2, y - height/2, width - 4, height), noteColor);
            }
        }

        // Draw Playhead
        if (isPlaying && previewSource != null)
        {
            float playheadY = totalHeight - (previewSource.time * PixelsPerSecond);
            Handles.color = Color.red;
            Handles.DrawLine(new Vector2(0, playheadY), new Vector2(TimelineWidth, playheadY));
        }

        // --- NEW: Handle inputs inside the scroll view context ---
        HandleInputs(timelineRect);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
        EditorGUILayout.LabelField("Editor Settings", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Clear All Notes"))
        {
            if (EditorUtility.DisplayDialog("Clear Notes", "Are you sure?", "Yes", "No"))
            {
                targetChart.notes.Clear();
                EditorUtility.SetDirty(targetChart);
            }
        }

        EditorGUILayout.Space();
        if (isPlaying)
        {
            if (GUILayout.Button("STOP Preview", GUILayout.Height(40))) StopPreview();
        }
        else
        {
            if (GUILayout.Button("PLAY Preview", GUILayout.Height(40))) StartPreview();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shortcuts:");
        EditorGUILayout.LabelField("L-Click: Add Note");
        EditorGUILayout.LabelField("R-Click: Remove Note");
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Note Palette", EditorStyles.boldLabel);
        
        // Note Type Selection
        DrawTypeButton("Normal Note", NoteType.Normal);
        DrawTypeButton("Double Tap", NoteType.Double);
        DrawTypeButton("Dash Note", NoteType.Dash);
        DrawTypeButton("Off-Beat", NoteType.OffBeat);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lane Span (Chord)");
        selectedSpan = EditorGUILayout.IntSlider(selectedSpan, 1, 4);

        EditorGUILayout.EndVertical();
    }

    private void DrawTypeButton(string label, NoteType type)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        if (selectedType == type)
        {
            style.normal.textColor = Color.yellow;
            style.fontStyle = FontStyle.Bold;
        }

        if (GUILayout.Button(label, style, GUILayout.Height(30)))
        {
            selectedType = type;
        }
    }

    private void HandleInputs(Rect timelineRect)
    {
        Event e = Event.current;
        // Check if mouse is within the timeline area
        if (!timelineRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown)
        {
            if (e.button == 0) // Left Click
            {
                TryAddNote(e.mousePosition, timelineRect.height);
                e.Use();
            }
            else if (e.button == 1) // Right Click
            {
                TryRemoveNote(e.mousePosition, timelineRect.height);
                e.Use();
            }
        }
    }

    private void TryAddNote(Vector2 mousePos, float totalHeight)
    {
        int lane = Mathf.FloorToInt(mousePos.x / LaneWidth);
        if (lane < 0 || lane >= 4) return;

        float time = (totalHeight - mousePos.y) / PixelsPerSecond;
        if (time < 0) return;

        // Snap to grid
        float secondsPerBeat = 60f / targetChart.bpm;
        float snappedTime = Mathf.Round(time / (secondsPerBeat / 4f)) * (secondsPerBeat / 4f); // 1/4 beat snap

        Undo.RecordObject(targetChart, "Add Note");
        targetChart.notes.Add(new NoteData(snappedTime, lane, selectedSpan, selectedType));
        targetChart.SortNotes();
        EditorUtility.SetDirty(targetChart);
    }

    private void TryRemoveNote(Vector2 mousePos, float totalHeight)
    {
        int lane = Mathf.FloorToInt(mousePos.x / LaneWidth);
        float time = (totalHeight - mousePos.y) / PixelsPerSecond;

        NoteData toRemove = targetChart.notes.Find(n => Math.Abs(n.time - time) < 0.1f && n.lane == lane);
        if (toRemove != null)
        {
            Undo.RecordObject(targetChart, "Remove Note");
            targetChart.notes.Remove(toRemove);
            EditorUtility.SetDirty(targetChart);
        }
    }

    private Color GetNoteColor(NoteType type)
    {
        return type switch
        {
            NoteType.Dash => Color.red,
            NoteType.Double => Color.yellow,
            NoteType.OffBeat => Color.green,
            _ => new Color(0.2f, 0.6f, 1f)
        };
    }

    private void StartPreview()
    {
        if (targetChart.musicClip == null) return;

        if (previewSource == null)
        {
            GameObject go = new GameObject("EditorPreviewAudio");
            go.hideFlags = HideFlags.HideAndDontSave;
            previewSource = go.AddComponent<AudioSource>();
        }

        previewSource.clip = targetChart.musicClip;
        previewSource.time = Mathf.Max(0, (targetChart.musicClip.length * PixelsPerSecond - (scrollPos.y + position.height * 0.8f)) / PixelsPerSecond);
        previewSource.Play();
        isPlaying = true;
    }

    private void StopPreview()
    {
        if (previewSource != null) previewSource.Stop();
        isPlaying = false;
    }
}
