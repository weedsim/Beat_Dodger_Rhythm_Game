using UnityEditor;
using UnityEngine;
using BeatDodger.Core;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
namespace BeatDodger.Editor
{
    public class NoteMapperWindow : EditorWindow
    {
        private NoteMapData currentMap;
        private AudioSource previewSource;
        private float currentTime = 0f;
        private bool isPlaying = false;
        private float zoom = 50f;
        private float playbackSpeed = 1f; 
        private Vector2 scrollPos;

        private Texture2D waveformTexture;
        private AudioClip lastWaveformClip;
        private const int WAVEFORM_RESOLUTION = 2048;
        private float stopScrubTime = -1f;

        [Header("Auto Map Settings")]
        private float autoMapThreshold = 1.6f; 
        private float chordChance = 0.2f;    
        private float longNoteChance = 0.25f; 

        private int selectedNoteIndex = -1;
        private bool isDragging = false;
        private bool isDraggingTail = false;
        private HashSet<int> overlappingIndices = new HashSet<int>();

        [MenuItem("BeatDodger/Note Mapper")]
        public static void ShowWindow() => GetWindow<NoteMapperWindow>("Note Mapper");

        private void OnEnable()
        {
            EditorApplication.update += UpdatePlayback;
            SetupAudioSource();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePlayback;
            if (previewSource != null) DestroyImmediate(previewSource.gameObject);
        }

        private void SetupAudioSource()
        {
            var sourceObj = GameObject.Find("NoteMapperPreviewSource");
            if (sourceObj == null) 
            {
                sourceObj = new GameObject("NoteMapperPreviewSource");
                sourceObj.hideFlags = HideFlags.HideAndDontSave;
            }
            
            previewSource = sourceObj.GetComponent<AudioSource>();
            if (previewSource == null) previewSource = sourceObj.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.spatialBlend = 0f;
        }

        private void UpdatePlayback()
        {
            if (previewSource == null) SetupAudioSource();
            if (!isPlaying && stopScrubTime > 0 && Time.realtimeSinceStartup >= stopScrubTime)
            {
                previewSource.Pause();
                stopScrubTime = -1f;
            }
            if (isPlaying)
            {
                if (previewSource.isPlaying) currentTime = previewSource.time;
                else { isPlaying = false; if (previewSource.time >= (currentMap?.music?.length ?? 0)) currentTime = 0; }
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (currentMap == null)
            {
                EditorGUILayout.HelpBox("Assign a NoteMapData asset.", MessageType.Warning);
                currentMap = (NoteMapData)EditorGUILayout.ObjectField("Map Data", currentMap, typeof(NoteMapData), false);
                return;
            }
            DrawTimeline();
            HandleInput();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUIUtility.labelWidth = 40;
                currentMap = (NoteMapData)EditorGUILayout.ObjectField("Data", currentMap, typeof(NoteMapData), false, GUILayout.Width(250));
                GUILayout.Space(10);
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(50))) TogglePlay();
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(50))) StopPlay();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Speed: {playbackSpeed:F2}x", EditorStyles.miniLabel);
                float newSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.25f, 1.5f, GUILayout.Width(80));
                if (newSpeed != playbackSpeed) { playbackSpeed = newSpeed; if (previewSource != null) previewSource.pitch = playbackSpeed; }
                GUILayout.Space(10);
                GUILayout.Label($"Zoom:", EditorStyles.miniLabel);
                zoom = GUILayout.HorizontalSlider(zoom, 10f, 300f, GUILayout.Width(100));
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUIUtility.labelWidth = 40;
                autoMapThreshold = EditorGUILayout.Slider("FFT Sens.", autoMapThreshold, 0.5f, 5.0f, GUILayout.Width(150));
                chordChance = EditorGUILayout.Slider("Chord", chordChance, 0f, 1.0f, GUILayout.Width(150));
                longNoteChance = EditorGUILayout.Slider("Long", longNoteChance, 0f, 1.0f, GUILayout.Width(150));
                if (GUILayout.Button("FFT Auto Map (No Overlap)", EditorStyles.toolbarButton, GUILayout.Width(170)))
                {
                    if (EditorUtility.DisplayDialog("FFT Auto Map", "Generate notes safely with overlap prevention?", "Yes", "Cancel")) RunFFTAutoMapper();
                }
                GUILayout.FlexibleSpace();
                string overlapMsg = overlappingIndices.Count > 0 ? $"Overlap: {overlappingIndices.Count}" : "";
                var overlapStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.red } };
                EditorGUILayout.LabelField(overlapMsg, overlapStyle, GUILayout.Width(100));
                EditorGUILayout.LabelField($"Time: {currentTime:F2}s", EditorStyles.miniLabel, GUILayout.Width(80));
            }
        }

        private void DrawTimeline()
        {
            if (currentMap.music == null) return;
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos, GUILayout.Height(220)))
            {
                scrollPos = scroll.scrollPosition;
                float totalWidth = currentMap.music.length * zoom;
                Rect contentRect = GUILayoutUtility.GetRect(totalWidth, 200);
                GUI.Box(contentRect, "", EditorStyles.helpBox);
                DrawWaveform(contentRect);
                Event e = Event.current;
                float beatInterval = 60f / currentMap.bpm;
                float snapInterval = beatInterval / 4f;
                Vector2 localMouse = e.mousePosition - contentRect.position;

                if (isDragging && selectedNoteIndex != -1)
                {
                    var note = currentMap.notes[selectedNoteIndex];
                    float newTime = localMouse.x / zoom;
                    newTime = Mathf.Round(newTime / snapInterval) * snapInterval; 
                    int newLane = Mathf.Clamp(Mathf.FloorToInt((localMouse.y - 10) / 40f), 0, 3);
                    note.time = Mathf.Max(0, newTime); note.lane = newLane;
                    currentMap.notes[selectedNoteIndex] = note; 
                    if (e.type == EventType.MouseUp) { isDragging = false; currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time)); CheckOverlaps(); EditorUtility.SetDirty(currentMap); }
                    Repaint();
                }

                if (isDraggingTail && selectedNoteIndex != -1)
                {
                    var note = currentMap.notes[selectedNoteIndex];
                    float newTime = localMouse.x / zoom;
                    newTime = Mathf.Round(newTime / snapInterval) * snapInterval; 
                    note.duration = Mathf.Max(0, newTime - note.time);
                    currentMap.notes[selectedNoteIndex] = note;
                    if (e.type == EventType.MouseUp) { isDraggingTail = false; currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time)); CheckOverlaps(); EditorUtility.SetDirty(currentMap); }
                    Repaint();
                }

                CheckOverlaps(); 
                for (float t = 0; t < currentMap.music.length; t += beatInterval) { float x = t * zoom; Handles.color = new Color(1, 1, 1, 0.15f); Handles.DrawLine(new Vector3(contentRect.x + x, contentRect.y), new Vector3(contentRect.x + x, contentRect.yMax)); }

                for (int i = 0; i < currentMap.notes.Count; i++)
                {
                    var note = currentMap.notes[i];
                    float x = note.time * zoom; float y = (note.lane * 40) + 10;
                    Rect noteRect = new Rect(contentRect.x + x - 8, contentRect.y + y, 16, 30);
                    if (note.duration > 0) { Rect bodyRect = new Rect(noteRect.xMax, noteRect.y + 5, note.duration * zoom, 20); EditorGUI.DrawRect(bodyRect, GetLaneColor(note.lane) * 0.4f); }
                    Rect tailHandleRect = new Rect(noteRect.xMax + (note.duration * zoom) - 5, noteRect.y, 10, noteRect.height);
                    EditorGUIUtility.AddCursorRect(tailHandleRect, MouseCursor.ResizeHorizontal);
                    if (tailHandleRect.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 0) { selectedNoteIndex = i; isDraggingTail = true; e.Use(); }

                    if (overlappingIndices.Contains(i)) EditorGUI.DrawRect(new Rect(noteRect.x - 3, noteRect.y - 3, noteRect.width + 6 + (note.duration * zoom), noteRect.height + 6), new Color(1, 0, 0, 0.8f));
                    if (i == selectedNoteIndex) EditorGUI.DrawRect(new Rect(noteRect.x - 2, noteRect.y - 2, noteRect.width + 4 + (note.duration * zoom), noteRect.height + 4), Color.white * 0.5f);
                    EditorGUI.DrawRect(noteRect, GetLaneColor(note.lane));
                    
                    if (noteRect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
                    {
                        if (e.button == 0) { selectedNoteIndex = i; isDragging = true; e.Use(); }
                        else if (e.button == 1) { currentMap.notes.RemoveAt(i); selectedNoteIndex = -1; CheckOverlaps(); EditorUtility.SetDirty(currentMap); e.Use(); break; }
                    }
                }

                if (!isDragging && !isDraggingTail && contentRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                    {
                        if (e.button == 0)
                        {
                            if (e.shift && e.type == EventType.MouseDown)
                            {
                                float newTime = Mathf.Round((localMouse.x / zoom) / snapInterval) * snapInterval;
                                int newLane = Mathf.Clamp(Mathf.FloorToInt((localMouse.y - 10) / 40f), 0, 3);
                                currentMap.notes.Add(new NoteInfo { time = newTime, lane = newLane });
                                currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time));
                                selectedNoteIndex = currentMap.notes.FindIndex(n => Mathf.Approximately(n.time, newTime) && n.lane == newLane);
                                CheckOverlaps(); EditorUtility.SetDirty(currentMap);
                            }
                            else { float lastTime = currentTime; currentTime = Mathf.Clamp(localMouse.x / zoom, 0, currentMap.music.length - 0.01f); if (previewSource != null) { previewSource.time = currentTime; if (!isPlaying && !Mathf.Approximately(lastTime, currentTime)) ScrubSound(); } }
                            Repaint();
                        }
                    }
                }
                float px = currentTime * zoom; Handles.color = Color.red; Handles.DrawLine(new Vector3(contentRect.x + px, contentRect.y), new Vector3(contentRect.x + px, contentRect.yMax));
            }
        }

        #region FFT Hybrid Auto Mapping

        private struct Complex { public float r; public float i; }

        private void RunFFTAutoMapper()
        {
            if (currentMap == null || currentMap.music == null) return;
            currentMap.notes.Clear();

            AudioClip clip = currentMap.music;
            int channels = clip.channels; int frequency = clip.frequency;
            float[] allSamples = new float[clip.samples * channels]; clip.GetData(allSamples, 0);

            int fftSize = 1024; int stepSize = fftSize / 2;
            float beatInterval = 60f / currentMap.bpm;
            float minNoteDist = beatInterval / 4.1f; 

            int numWindows = (allSamples.Length / channels) / stepSize - 1;
            float[] spectralFlux = new float[numWindows];
            float[] dominantFreqs = new float[numWindows];
            float[] prevMagnitudes = new float[fftSize / 2];
            float[,] fullSpectrogram = new float[numWindows, fftSize / 2];

            for (int w = 0; w < numWindows; w++)
            {
                Complex[] complexSamples = new Complex[fftSize];
                int startIdx = (w * stepSize) * channels;
                for (int i = 0; i < fftSize; i++) { int idx = startIdx + (i * channels); if (idx < allSamples.Length) complexSamples[i].r = allSamples[idx] * (0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * i / (fftSize - 1))); }
                PerformFFT(complexSamples);
                float flux = 0; float maxMag = 0; int maxBin = 0;
                for (int i = 0; i < fftSize / 2; i++) {
                    float mag = Mathf.Sqrt(complexSamples[i].r * complexSamples[i].r + complexSamples[i].i * complexSamples[i].i);
                    fullSpectrogram[w, i] = mag; float diff = mag - prevMagnitudes[i]; if (diff > 0) flux += diff;
                    if (mag > maxMag) { maxMag = mag; maxBin = i; } prevMagnitudes[i] = mag;
                }
                spectralFlux[w] = flux; dominantFreqs[w] = (float)maxBin * frequency / fftSize;
            }

            float meanFlux = spectralFlux.Average();
            float[] lastLaneNoteEndTime = new float[4] { -1f, -1f, -1f, -1f }; // 레인별 마지막 노트 종료 시간 기록
            int lastTargetLane = -1; int[] laneFatigue = new int[4]; 

            for (int w = 1; w < numWindows - 1; w++)
            {
                if (spectralFlux[w] > spectralFlux[w - 1] && spectralFlux[w] > spectralFlux[w + 1])
                {
                    if (spectralFlux[w] > meanFlux * autoMapThreshold)
                    {
                        float time = (float)(w * stepSize) / frequency;
                        float freq = dominantFreqs[w];
                        int targetLaneGroup = (freq < 450f) ? 0 : (freq < 2500f) ? 1 : 2;

                        int bestLane = SelectBestBalancedLane(targetLaneGroup, lastTargetLane, laneFatigue);
                        
                        // 레인별 중첩 체크 (쿨타임 및 롱노트 영역 보호)
                        if (time - lastLaneNoteEndTime[bestLane] >= minNoteDist)
                        {
                            float duration = 0;
                            if (Random.value < longNoteChance) { duration = CalculateLongNoteDuration(w, (int)(freq * fftSize / frequency), fullSpectrogram, frequency, stepSize, numWindows); }

                            currentMap.notes.Add(new NoteInfo { time = time, lane = bestLane, duration = duration });
                            
                            if (Random.value < chordChance && duration <= 0) {
                                int chordLane = (bestLane + 2) % 4;
                                if (time - lastLaneNoteEndTime[chordLane] >= minNoteDist) {
                                    currentMap.notes.Add(new NoteInfo { time = time, lane = chordLane });
                                    laneFatigue[chordLane]++; lastLaneNoteEndTime[chordLane] = time + 0.05f;
                                }
                            }

                            laneFatigue[bestLane]++; lastTargetLane = bestLane;
                            lastLaneNoteEndTime[bestLane] = time + duration + 0.1f; // 롱노트가 끝나고 0.1초의 여유를 둠

                            for (int i = 0; i < 4; i++) if (laneFatigue[i] > 0) laneFatigue[i]--;
                        }
                    }
                }
            }
            CheckOverlaps(); EditorUtility.SetDirty(currentMap); AssetDatabase.SaveAssets();
            Debug.Log($"<color=cyan>[SafeAutoMap]</color> Success! Overlaps prevented.");
        }

        private float CalculateLongNoteDuration(int startWindow, int binIdx, float[,] spectrogram, int freq, int stepSize, int maxWindows)
        {
            float startMag = spectrogram[startWindow, binIdx]; int windowCount = 0; int maxCheck = 20;
            for (int i = 1; i < maxCheck; i++) {
                int nextW = startWindow + i; if (nextW >= maxWindows) break;
                if (spectrogram[nextW, binIdx] > startMag * 0.65f) windowCount++; else break;
            }
            if (windowCount < 4) return 0;
            return (float)(windowCount * stepSize) / freq;
        }

        private int SelectBestBalancedLane(int group, int lastLane, int[] fatigue)
        {
            int[] candidates = group switch { 0 => new int[] { 0, 1, 2 }, 1 => new int[] { 1, 2, 0, 3 }, _ => new int[] { 3, 2, 1 } };
            int selected = candidates[0]; int minFatigue = int.MaxValue;
            foreach (int c in candidates) { if (c == lastLane) continue; if (fatigue[c] < minFatigue) { minFatigue = fatigue[c]; selected = c; } else if (fatigue[c] == minFatigue && Random.value < 0.5f) selected = c; }
            return selected;
        }

        private void PerformFFT(Complex[] data)
        {
            int n = data.Length; int m = (int)Mathf.Log(n, 2);
            for (int i = 0; i < n; i++) { int j = 0; for (int k = 0; k < m; k++) j |= ((i >> k) & 1) << (m - 1 - k); if (j > i) { var temp = data[i]; data[i] = data[j]; data[j] = temp; } }
            for (int i = 0; i < m; i++) {
                int step = 1 << (i + 1); int halfStep = 1 << i; float angle = -2 * Mathf.PI / step;
                for (int k = 0; k < halfStep; k++) {
                    Complex w = new Complex { r = Mathf.Cos(k * angle), i = Mathf.Sin(k * angle) };
                    for (int j = k; j < n; j += step) {
                        Complex u = data[j]; Complex v = new Complex { r = data[j + halfStep].r * w.r - data[j + halfStep].i * w.i, i = data[j + halfStep].r * w.i + data[j + halfStep].i * w.r };
                        data[j].r = u.r + v.r; data[j].i = u.i + v.i; data[j + halfStep].r = u.r - v.r; data[j + halfStep].i = u.i - v.i;
                    }
                }
            }
        }
        #endregion

        private void CheckOverlaps()
        {
            overlappingIndices.Clear(); if (currentMap == null || currentMap.notes.Count < 2) return;
            List<NoteInfo>[] laneNotes = new List<NoteInfo>[4]; List<int>[] laneIndices = new List<int>[4];
            for (int i = 0; i < 4; i++) { laneNotes[i] = new List<NoteInfo>(); laneIndices[i] = new List<int>(); }
            for (int i = 0; i < currentMap.notes.Count; i++) { int lane = currentMap.notes[i].lane; laneNotes[lane].Add(currentMap.notes[i]); laneIndices[lane].Add(i); }
            for (int l = 0; l < 4; l++) {
                var notes = laneNotes[l]; var indices = laneIndices[l];
                for (int i = 0; i < notes.Count; i++) {
                    for (int j = i + 1; j < notes.Count; j++) {
                        float s1 = notes[i].time; float e1 = s1 + notes[i].duration; float s2 = notes[j].time; float e2 = s2 + notes[j].duration;
                        float overlapStart = Mathf.Max(s1, s2); float overlapEnd = Mathf.Min(e1, e2);
                        if (overlapStart < overlapEnd - 0.001f || Mathf.Approximately(s1, s2)) { overlappingIndices.Add(indices[i]); overlappingIndices.Add(indices[j]); }
                    }
                }
            }
        }
        private void HandleInput() { Event e = Event.current; if (e.type == EventType.KeyDown) { if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha4) { AddNote(currentTime, e.keyCode - KeyCode.Alpha1); e.Use(); } if (e.keyCode == KeyCode.Space) { TogglePlay(); e.Use(); } } }
        private void AddNote(float time, int lane) { currentMap.notes.Add(new NoteInfo { time = time, lane = lane }); currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time)); CheckOverlaps(); EditorUtility.SetDirty(currentMap); }
        private void TogglePlay() { if (isPlaying) { previewSource.Pause(); isPlaying = false; } else { if (currentMap.music == null) return; previewSource.clip = currentMap.music; previewSource.time = Mathf.Clamp(currentTime, 0, currentMap.music.length - 0.01f); previewSource.pitch = playbackSpeed; previewSource.Play(); isPlaying = true; } }
        private void StopPlay() { if (previewSource != null) previewSource.Stop(); isPlaying = false; currentTime = 0; }
        private Color GetLaneColor(int lane) => lane switch { 0 => Color.cyan, 1 => Color.green, 2 => Color.yellow, 3 => Color.red, _ => Color.white };
        private void ScrubSound() { if (previewSource == null || currentMap.music == null) return; previewSource.pitch = playbackSpeed; previewSource.Play(); stopScrubTime = Time.realtimeSinceStartup + 0.1f; }
        private void DrawWaveform(Rect rect) { if (currentMap.music == null) return; if (waveformTexture == null || lastWaveformClip != currentMap.music) GenerateWaveform(); if (waveformTexture != null) { GUI.color = new Color(1, 0.5f, 0, 0.4f); GUI.DrawTexture(rect, waveformTexture); GUI.color = Color.white; } }
        private void GenerateWaveform() {
            AudioClip clip = currentMap.music; lastWaveformClip = clip;
            int width = WAVEFORM_RESOLUTION; int height = 128;
            waveformTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float[] samples = new float[clip.samples * clip.channels]; clip.GetData(samples, 0);
            Color[] colors = new Color[width * height]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;
            int packSize = (clip.samples * clip.channels) / width;
            for (int x = 0; x < width; x++) {
                float max = 0; for (int i = 0; i < packSize; i++) { float val = Mathf.Abs(samples[x * packSize + i]); if (val > max) max = val; }
                int barHeight = Mathf.CeilToInt(max * height);
                for (int y = 0; y < barHeight; y++) {
                    int topY = (height / 2) + (y / 2); int bottomY = (height / 2) - (y / 2);
                    if (topY < height) colors[topY * width + x] = Color.white; if (bottomY >= 0) colors[bottomY * width + x] = Color.white;
                }
            }
            waveformTexture.SetPixels(colors); waveformTexture.Apply();
        }
    }
}
#endif
