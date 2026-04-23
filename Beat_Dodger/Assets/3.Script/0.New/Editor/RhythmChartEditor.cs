using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class RhythmChartEditor : EditorWindow
{
    private RhythmChart targetChart;
    private Vector2 scrollPos;
    
    private const float LaneWidth = 60f;
    private float pixelsPerSecond = 200f;
    private const float TimelineWidth = 300f;
    private const float SidebarWidth = 200f;

    private AudioSource previewSource;
    private bool isPlaying = false;
    private float lastEditorTime = 0;

    private NoteType selectedType = NoteType.Normal;
    private int selectedSpan = 1;

    private float[] waveformData;
    private AudioClip lastWaveformClip;
    private const int WaveformRes = 2; // Pixels per sample point
    
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
            EditorGUILayout.HelpBox("편집할 RhythmChart를 선택해주세요.", MessageType.Info);
            targetChart = (RhythmChart)EditorGUILayout.ObjectField("대상 차트", targetChart, typeof(RhythmChart), false);
            return;
        }

        // Ensure preview source is ready if we have a clip
        if (previewSource == null && targetChart.musicClip != null)
        {
            InitializePreviewSource();
        }

        EditorGUILayout.BeginHorizontal();
        DrawTimeline();
        DrawSidebar();
        EditorGUILayout.EndHorizontal();

        UpdatePlayback();
        
        if (targetChart.musicClip != lastWaveformClip)
        {
            UpdateWaveform();
        }

        // Always repaint to keep playhead smooth, or at least when clip is assigned
        if (GUI.changed || isPlaying || (previewSource != null && previewSource.clip != null)) Repaint();
    }

    private void OnDisable()
    {
        StopPreview();
    }

    private void UpdatePlayback()
    {
        if (!isPlaying || previewSource == null) return;

        float time = previewSource.time;
        float totalHeight = targetChart.musicClip.length * pixelsPerSecond;
        float playheadY = totalHeight - (time * pixelsPerSecond);

        // Auto-scroll to keep playhead in view
        float windowHeight = position.height;
        float scrollY = playheadY - windowHeight * 0.8f;
        scrollPos.y = scrollY;
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        targetChart = (RhythmChart)EditorGUILayout.ObjectField("차트", targetChart, typeof(RhythmChart), false, GUILayout.Width(250));
        if (targetChart != null)
        {
            targetChart.musicClip = (AudioClip)EditorGUILayout.ObjectField("클립", targetChart.musicClip, typeof(AudioClip), false);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTimeline()
    {
        float totalHeight = targetChart.musicClip != null ? targetChart.musicClip.length * pixelsPerSecond : 5000f;
        
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

        for (float t = 0; t < totalHeight / pixelsPerSecond; t += secondsPerBeat)
        {
            float y = totalHeight - (t * pixelsPerSecond);
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
                float y = totalHeight - ((float)note.time * pixelsPerSecond);
                if (y < viewStart - 50 || y > viewEnd + 50) continue; // Culling

                float x = note.lane * LaneWidth;
                float width = note.span * LaneWidth;
                float height = Mathf.Max(5f, pixelsPerSecond * 0.1f); // Scale with zoom, min 5px

                Color noteColor = GetNoteColor(note.type);
                EditorGUI.DrawRect(new Rect(x + 2, y - height/2, width - 4, height), noteColor);
            }
        }

        // Draw Playhead
        float curTime = (previewSource != null) ? previewSource.time : 0f;
        float playheadY = totalHeight - (curTime * pixelsPerSecond);
        
        // Draw playhead with a thin outline for better contrast
        EditorGUI.DrawRect(new Rect(0, playheadY - 2f, TimelineWidth, 4f), new Color(0.1f, 0.1f, 0.1f, 0.5f)); // Outline
        EditorGUI.DrawRect(new Rect(0, playheadY - 1f, TimelineWidth, 2f), Color.red); // Core
        
        // Add time label with a background box for visibility
        GUI.Box(new Rect(TimelineWidth + 5, playheadY - 10, 60, 22), $"{curTime:F2}s", EditorStyles.helpBox);

        // Draw Waveform
        DrawWaveform(timelineRect);

        // --- NEW: Handle inputs inside the scroll view context ---
        HandleInputs(timelineRect);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
        EditorGUILayout.LabelField("리듬 에디터 설정", EditorStyles.boldLabel);
        
        if (GUILayout.Button("노트 초기화"))
        {
            if (EditorUtility.DisplayDialog("노트 초기화", "정말 모든 노트를 지우시겠습니까?", "네", "아니오"))
            {
                targetChart.notes.Clear();
                EditorUtility.SetDirty(targetChart);
            }
        }

        EditorGUILayout.Space();
        if (isPlaying)
        {
            if (GUILayout.Button("정지", GUILayout.Height(40))) StopPreview();
        }
        else
        {
            if (GUILayout.Button("재생", GUILayout.Height(40))) StartPreview();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("단축키:");
        EditorGUILayout.LabelField("Shift + 좌클릭: 노트 추가");
        EditorGUILayout.LabelField("좌클릭: 재생바 조절");
        EditorGUILayout.LabelField("우클릭: 노트 지우기");
        EditorGUILayout.LabelField("Ctrl + 휠: 확대 축소");
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("노트 종류", EditorStyles.boldLabel);
        
        // Note Type Selection
        DrawTypeButton("일반 노트", NoteType.Normal);
        DrawTypeButton("연타 노트", NoteType.Double);
        DrawTypeButton("가속 노트", NoteType.Dash);
        DrawTypeButton("엇박 노트", NoteType.OffBeat);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("같이치기 길이 조절");
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

        // Zoom logic with Ctrl + Scroll
        if (e.type == EventType.ScrollWheel && e.control)
        {
            float zoomDelta = -e.delta.y * 20f;
            float oldPPS = pixelsPerSecond;
            pixelsPerSecond = Mathf.Clamp(pixelsPerSecond + zoomDelta, 50f, 1000f);

            if (Mathf.Abs(oldPPS - pixelsPerSecond) > 0.01f)
            {
                // Re-generate waveform data for new resolution
                UpdateWaveform();
                Repaint();
                e.Use();
                return;
            }
        }

        if (!timelineRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
        {
            if (e.button == 0) // Left Click
            {
                if (e.shift)
                {
                    if (e.type == EventType.MouseDown)
                    {
                        TryAddNote(e.mousePosition, timelineRect.height);
                        e.Use();
                    }
                }
                else
                {
                    SetPlaybackTime(e.mousePosition, timelineRect.height);
                    e.Use();
                }
            }
            else if (e.button == 1 && e.type == EventType.MouseDown) // Right Click
            {
                TryRemoveNote(e.mousePosition, timelineRect.height);
                e.Use();
            }
        }
    }

    private void SetPlaybackTime(Vector2 mousePos, float totalHeight)
    {
        if (targetChart == null || targetChart.musicClip == null) return;

        float time = (totalHeight - mousePos.y) / pixelsPerSecond;
        time = Mathf.Clamp(time, 0, targetChart.musicClip.length - 0.01f);

        if (previewSource == null)
        {
            InitializePreviewSource();
        }

        previewSource.time = time;
        if (!isPlaying) Repaint();
    }

    private void TryAddNote(Vector2 mousePos, float totalHeight)
    {
        int lane = Mathf.FloorToInt(mousePos.x / LaneWidth);
        if (lane < 0 || lane >= 4) return;

        float time = (totalHeight - mousePos.y) / pixelsPerSecond;
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
        float time = (totalHeight - mousePos.y) / pixelsPerSecond;

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
            InitializePreviewSource();
        }

        previewSource.clip = targetChart.musicClip;
        
        // If time is 0 (first start), jump to visible area. Otherwise, continue from current time.
        if (previewSource.time <= 0.001f)
        {
            float targetTime = Mathf.Max(0, (targetChart.musicClip.length * pixelsPerSecond - (scrollPos.y + position.height * 0.5f)) / pixelsPerSecond);
            previewSource.time = targetTime;
        }
        
        previewSource.Play();
        isPlaying = true;
    }

    private void InitializePreviewSource()
    {
        if (previewSource != null) return;

        AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
        foreach (var s in sources)
        {
            if (s.name == "EditorPreviewAudio")
            {
                previewSource = s;
                previewSource.clip = targetChart.musicClip;
                return;
            }
        }

        GameObject go = new GameObject("EditorPreviewAudio");
        go.hideFlags = HideFlags.HideAndDontSave;
        previewSource = go.AddComponent<AudioSource>();
        previewSource.clip = targetChart.musicClip;
    }

    private void UpdateWaveform()
    {
        lastWaveformClip = targetChart.musicClip;
        if (lastWaveformClip == null)
        {
            waveformData = null;
            return;
        }

        // Downsample audio data for visualization
        int sampleCount = (int)(lastWaveformClip.length * pixelsPerSecond / WaveformRes);
        waveformData = new float[sampleCount];

        float[] samples = new float[lastWaveformClip.samples * lastWaveformClip.channels];
        lastWaveformClip.GetData(samples, 0);

        int samplesPerPoint = samples.Length / sampleCount;
        if (samplesPerPoint <= 0) samplesPerPoint = 1;

        for (int i = 0; i < sampleCount; i++)
        {
            float max = 0;
            for (int j = 0; j < samplesPerPoint; j++)
            {
                int idx = i * samplesPerPoint + j;
                if (idx < samples.Length)
                {
                    float val = Math.Abs(samples[idx]);
                    if (val > max) max = val;
                }
            }
            waveformData[i] = max;
        }
    }

    private void DrawWaveform(Rect rect)
    {
        if (waveformData == null || targetChart.musicClip == null) return;

        float totalHeight = rect.height;
        float viewStart = scrollPos.y;
        float viewEnd = scrollPos.y + position.height;

        float startX = 4 * LaneWidth;
        float width = TimelineWidth - startX;
        float centerX = startX + width / 2f;
        float maxHalfWidth = width / 2f;

        Handles.color = new Color(0.3f, 0.5f, 1f, 0.4f); // Semi-transparent blue

        for (int i = 0; i < waveformData.Length; i++)
        {
            float y = totalHeight - (i * WaveformRes);
            if (y < viewStart - 50 || y > viewEnd + 50) continue;

            float w = waveformData[i] * maxHalfWidth;
            if (w < 1f) w = 1f; // Minimum visibility

            Handles.DrawLine(new Vector2(centerX - w, y), new Vector2(centerX + w, y));
        }
    }

    private void StopPreview()
    {
        if (previewSource != null) previewSource.Pause();
        isPlaying = false;
    }
}
