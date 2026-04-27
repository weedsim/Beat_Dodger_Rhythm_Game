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

    private float autoThreshold = 0.5f;
    private float offBeatThreshold = 0.6f;
    private float span2Threshold = 1.4f;
    private float span4Threshold = 2.0f;
    private float specialNoteChance = 0.2f;
    private float lastSpan4Time = -10f; 
    private float lastSpan2Time = -10f; // Cooldown for 2-player notes

    // FFT Data
    private struct Complex 
    { 
        public float r; 
        public float i; 
    }
    
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
        EditorGUILayout.LabelField("자동 노트 생성 설정", EditorStyles.boldLabel);
        autoThreshold = EditorGUILayout.Slider("기본 감도 (Global)", autoThreshold, 0.01f, 1f);
        span2Threshold = EditorGUILayout.Slider("2인 같이치기 감도", span2Threshold, 0.1f, 50.0f);
        span4Threshold = EditorGUILayout.Slider("4인 같이치기 감도", span4Threshold, 0.1f, 50.0f);
        specialNoteChance = EditorGUILayout.Slider("특수노트 확률", specialNoteChance, 0f, 1f);

        if (GUILayout.Button("자동 노트 생성 실행", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("자동 생성", "기본 설정을 기반으로 노트를 생성하시겠습니까?", "생성", "취소"))
            {
                AutoGenerateNotes();
            }
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

    private void AutoGenerateNotes()
    {
        if (targetChart == null || targetChart.musicClip == null) return;

        Undo.RecordObject(targetChart, "Auto Generate Notes (Co-op Optimized)");

        AudioClip clip = targetChart.musicClip;
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        float[] samples = new float[clip.samples * channels];
        clip.GetData(samples, 0);

        float secondsPerBeat = 60f / targetChart.bpm;
        
        // Reset cooldowns for a fresh generation run
        lastSpan2Time = -100f;
        lastSpan4Time = -100f;

        float snapInterval = secondsPerBeat; 
        int stepSamples = (int)(snapInterval * sampleRate);
        
        // FFT Settings
        int fftSize = 1024; 
        
        // Lane balancing data
        int[] laneNoteCounts = new int[4];
        HashSet<float> existingTimes = new HashSet<float>();
        foreach (var n in targetChart.notes)
        {
            existingTimes.Add(Mathf.Round((float)n.time * 1000f) / 1000f);
            if (n.lane >= 0 && n.lane < 4) laneNoteCounts[n.lane]++;
        }

        // Scan at 0.5 beat (8th note) resolution for a cleaner chart
        int microStep = stepSamples / 2; 
        float lastNoteTime = -1f;
        int lastLane = -1;

        for (int i = 0; i < samples.Length - stepSamples - fftSize; i += microStep)
        {
            // 1. Calculate RMS for Timing
            float sum = 0;
            for (int j = 0; j < microStep; j++)
            {
                float s = samples[i + j];
                sum += s * s;
            }
            float rms = Mathf.Sqrt(sum / microStep);

            if (rms > autoThreshold)
            {
                float time = (float)i / (sampleRate * channels);
                float halfBeatInterval = secondsPerBeat / 2f;
                float snappedTime = Mathf.Round(time / halfBeatInterval) * halfBeatInterval;
                float key = Mathf.Round(snappedTime * 1000f) / 1000f;

                if (!existingTimes.Contains(key))
                {
                    // Density Control: If a note was added very recently, require much higher energy
                    float timeSinceLastNote = snappedTime - lastNoteTime;
                    if (timeSinceLastNote < secondsPerBeat * 0.49f && rms < autoThreshold * 1.5f) continue;

                    // 2. Perform FFT for Frequency
                    float dominantFreq = GetDominantFrequency(samples, i, fftSize, sampleRate, channels);
                    
                    // 3. Determine Note Type & Span
                    NoteType type = NoteType.Normal;
                    int span = 1;
                    float ratio = rms / autoThreshold;

                    // 엇박 제외 (정박 체크)
                    float beatPos = snappedTime / secondsPerBeat;
                    bool isMainBeat = Mathf.Approximately(beatPos % 1.0f, 0);
                    
                    if (!isMainBeat) continue;

                    // Together hits (Span 2/4) only appear on main beats
                    if (ratio >= span4Threshold && (snappedTime - lastSpan4Time >= secondsPerBeat * 3.9f)) 
                    {
                        span = 4;
                        lastSpan4Time = snappedTime;
                    }
                    else if (ratio >= span2Threshold && (snappedTime - lastSpan2Time >= secondsPerBeat * 0.95f)) 
                    {
                        // Increased probability to 90%
                        if (UnityEngine.Random.value < 0.9f)
                        {
                            span = 2;
                            lastSpan2Time = snappedTime;
                        }
                    }
                    
                    // 4. Select Fairest Start Lane
                    int startLane = SelectFairestStartLane(span, dominantFreq, laneNoteCounts, lastLane);

                    // Add variety using specialNoteChance
                    if (type == NoteType.Normal)
                    {
                        float typeRand = UnityEngine.Random.value;
                        if (typeRand < specialNoteChance * 0.5f) type = NoteType.Dash;
                        else if (typeRand < specialNoteChance) type = NoteType.Double;
                    }

                    targetChart.notes.Add(new NoteData(snappedTime, startLane, span, type));
                    
                    for (int l = startLane; l < startLane + span && l < 4; l++) laneNoteCounts[l]++;
                    existingTimes.Add(key);
                    lastNoteTime = snappedTime;
                    lastLane = startLane;
                }
            }
        }

        targetChart.SortNotes();
        EditorUtility.SetDirty(targetChart);
        AssetDatabase.SaveAssets();
        Repaint();
        Debug.Log("<color=green>[AutoMap]</color> Generated with Co-op Balance Logic.");
    }

    private float GetDominantFrequency(float[] samples, int startIdx, int fftSize, int sampleRate, int channels)
    {
        Complex[] complexData = new Complex[fftSize];
        for (int i = 0; i < fftSize; i++)
        {
            int idx = startIdx + (i * channels);
            if (idx < samples.Length)
            {
                // Applying Hamming window
                float window = 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * i / (fftSize - 1));
                complexData[i].r = samples[idx] * window;
            }
        }

        PerformFFT(complexData);

        float maxMag = 0;
        int maxBin = 0;
        for (int i = 0; i < fftSize / 2; i++)
        {
            float mag = Mathf.Sqrt(complexData[i].r * complexData[i].r + complexData[i].i * complexData[i].i);
            if (mag > maxMag)
            {
                maxMag = mag;
                maxBin = i;
            }
        }

        return (float)maxBin * sampleRate / fftSize;
    }

    private int SelectFairestStartLane(int span, float freq, int[] currentCounts, int lastLane)
    {
        if (span >= 4) return 0;

        int bestStartLane = 0;
        float minScore = float.MaxValue;

        int maxStartLane = 4 - span;
        int preferredSide = (freq < 1000f) ? 0 : 2; 

        for (int i = 0; i <= maxStartLane; i++)
        {
            float avgCount = 0;
            for (int l = i; l < i + span; l++) avgCount += currentCounts[l];
            avgCount /= span;

            // 1. Fairness Score (Base)
            float score = avgCount;

            // 2. Frequency Bias
            if (i >= preferredSide && i < preferredSide + 2) score -= 0.3f;

            // 3. Sequential Penalty (Prevent 1->2->3->4 stairs)
            // If this lane is right next to the last one, add a penalty
            if (lastLane != -1)
            {
                int dist = Mathf.Abs(i - lastLane);
                if (dist == 1) score += 0.8f; // Strong penalty for adjacent lanes
                else if (dist == 0) score += 1.5f; // Very strong penalty for same lane
            }

            // 4. Random Noise (Break deterministic ties)
            score += UnityEngine.Random.value * 0.2f;

            if (score < minScore)
            {
                minScore = score;
                bestStartLane = i;
            }
        }

        return bestStartLane;
    }

    private void PerformFFT(Complex[] data)
    {
        int n = data.Length;
        int m = (int)Mathf.Log(n, 2);

        // Bit-reversal permutation
        for (int i = 0; i < n; i++)
        {
            int j = 0;
            for (int k = 0; k < m; k++) j |= ((i >> k) & 1) << (m - 1 - k);
            if (j > i) { var temp = data[i]; data[i] = data[j]; data[j] = temp; }
        }

        // Cooley-Tukey FFT
        for (int i = 0; i < m; i++)
        {
            int step = 1 << (i + 1);
            int halfStep = 1 << i;
            float angle = -2 * Mathf.PI / step;
            for (int k = 0; k < halfStep; k++)
            {
                Complex w = new Complex { r = Mathf.Cos(k * angle), i = Mathf.Sin(k * angle) };
                for (int j = k; j < n; j += step)
                {
                    Complex u = data[j];
                    Complex v = new Complex 
                    { 
                        r = data[j + halfStep].r * w.r - data[j + halfStep].i * w.i, 
                        i = data[j + halfStep].r * w.i + data[j + halfStep].i * w.r 
                    };
                    data[j].r = u.r + v.r;
                    data[j].i = u.i + v.i;
                    data[j + halfStep].r = u.r - v.r;
                    data[j + halfStep].i = u.i - v.i;
                }
            }
        }
    }

    private void StopPreview()
    {
        if (previewSource != null) previewSource.Pause();
        isPlaying = false;
    }
}
