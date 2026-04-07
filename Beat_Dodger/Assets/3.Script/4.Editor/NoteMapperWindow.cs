using UnityEditor;
using UnityEngine;
using BeatDodger.Core;
using System.Collections.Generic;

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

        // Waveform Visuals
        private Texture2D waveformTexture;
        private AudioClip lastWaveformClip;
        private const int WAVEFORM_RESOLUTION = 2048;

        // Scrubbing Feedback
        private float stopScrubTime = -1f;

        [Header("Auto Map Settings")]
        private float autoMapThreshold = 1.6f; 
        private float chordChance = 0.2f;    // Chance for simultaneous notes
        private float patternSwapChance = 0.3f; // Chance to switch between Trill/Stair/Stream

        private enum AutoPattern { Stream, Trill, Stair, Denim, Random }
        private AutoPattern currentPattern = AutoPattern.Random;

        // Interaction State
        private int selectedNoteIndex = -1;
        private bool isDragging = false;
        private bool isDraggingTail = false;
        private Vector2 dragOffset; // X/Y 오프셋 통합 관리

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
            
            // Handle Scrubbing Stop
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
                
                // 재생 속도 슬라이더 추가
                GUILayout.Label($"Speed: {playbackSpeed:F2}x", EditorStyles.miniLabel);
                float newSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.25f, 1.5f, GUILayout.Width(80));
                if (newSpeed != playbackSpeed)
                {
                    playbackSpeed = newSpeed;
                    if (previewSource != null) previewSource.pitch = playbackSpeed;
                }

                GUILayout.Space(10);
                GUILayout.Label($"Zoom:", EditorStyles.miniLabel);
                zoom = GUILayout.HorizontalSlider(zoom, 10f, 300f, GUILayout.Width(100));
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUIUtility.labelWidth = 40;
                autoMapThreshold = EditorGUILayout.Slider("Sens.", autoMapThreshold, 1.0f, 4.0f, GUILayout.Width(150));
                chordChance = EditorGUILayout.Slider("Chord", chordChance, 0f, 1.0f, GUILayout.Width(150));

                if (GUILayout.Button("Smart Auto Map", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    if (EditorUtility.DisplayDialog("Smart Auto Map", "Generate professional patterns? Existing notes will be cleared.", "Yes", "Cancel")) RunSmartAutoMapper();
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Time: {currentTime:F2}s", EditorStyles.miniLabel, GUILayout.Width(80));
            }
        }

        private void DrawTimeline()
        {
            if (currentMap.music == null) return;

            // 스크롤 뷰 시작 (전체 높이 220 고정)
            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos, GUILayout.Height(220)))
            {
                scrollPos = scroll.scrollPosition;
                
                float totalWidth = currentMap.music.length * zoom;
                Rect contentRect = GUILayoutUtility.GetRect(totalWidth, 200);
                GUI.Box(contentRect, "", EditorStyles.helpBox);

                // [파형 그리기]
                DrawWaveform(contentRect);

                Event e = Event.current;
                float beatInterval = 60f / currentMap.bpm;
                float snapInterval = beatInterval / 4f;

                Vector2 localMouse = e.mousePosition - contentRect.position;

                // [드래그 업데이트 로직] - 드로잉 루프 전에 실행하여 프레임 지연을 없앱니다.
                if (isDragging && selectedNoteIndex != -1)
                {
                    var note = currentMap.notes[selectedNoteIndex];
                    float newTime = localMouse.x / zoom;
                    newTime = Mathf.Round(newTime / snapInterval) * snapInterval; 
                    int newLane = Mathf.Clamp(Mathf.FloorToInt((localMouse.y - 10) / 40f), 0, 3);
                    
                    note.time = Mathf.Max(0, newTime);
                    note.lane = newLane;
                    currentMap.notes[selectedNoteIndex] = note; 

                    if (e.type == EventType.MouseUp) 
                    {
                        isDragging = false;
                        currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time));
                        EditorUtility.SetDirty(currentMap);
                    }
                    Repaint();
                }

                if (isDraggingTail && selectedNoteIndex != -1)
                {
                    var note = currentMap.notes[selectedNoteIndex];
                    float newTime = localMouse.x / zoom;
                    newTime = Mathf.Round(newTime / snapInterval) * snapInterval; 
                    note.duration = Mathf.Max(0, newTime - note.time);
                    currentMap.notes[selectedNoteIndex] = note;

                    if (e.type == EventType.MouseUp) 
                    {
                        isDraggingTail = false;
                        currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time));
                        EditorUtility.SetDirty(currentMap);
                    }
                    Repaint();
                }

                // [배경 그리드]
                for (float t = 0; t < currentMap.music.length; t += beatInterval)
                {
                    float x = t * zoom;
                    Handles.color = new Color(1, 1, 1, 0.15f);
                    Handles.DrawLine(new Vector3(contentRect.x + x, contentRect.y), new Vector3(contentRect.x + x, contentRect.yMax));
                }

                // [노트 그리기 및 인터랙션]
                for (int i = 0; i < currentMap.notes.Count; i++)
                {
                    var note = currentMap.notes[i];
                    float x = note.time * zoom;
                    float y = (note.lane * 40) + 10;
                    
                    Rect noteRect = new Rect(contentRect.x + x - 8, contentRect.y + y, 16, 30);
                    
                    // 롱노트 몸통 (있을 경우만 그림)
                    if (note.duration > 0)
                    {
                        Rect bodyRect = new Rect(noteRect.xMax, noteRect.y + 5, note.duration * zoom, 20);
                        EditorGUI.DrawRect(bodyRect, GetLaneColor(note.lane) * 0.4f);
                    }

                    // 꼬리 핸들 (모든 노트에 존재하여 롱노트로 변환 가능)
                    Rect tailHandleRect = new Rect(noteRect.xMax + (note.duration * zoom) - 5, noteRect.y, 10, noteRect.height);
                    EditorGUIUtility.AddCursorRect(tailHandleRect, MouseCursor.ResizeHorizontal);

                    if (tailHandleRect.Contains(e.mousePosition))
                    {
                        if (e.type == EventType.MouseDown && e.button == 0)
                        {
                            selectedNoteIndex = i;
                            isDraggingTail = true;
                            e.Use();
                        }
                    }

                    // 선택 가이드 및 헤드
                    if (i == selectedNoteIndex) EditorGUI.DrawRect(new Rect(noteRect.x - 2, noteRect.y - 2, noteRect.width + 4 + (note.duration * zoom), noteRect.height + 4), Color.white * 0.5f);
                    EditorGUI.DrawRect(noteRect, GetLaneColor(note.lane));
                    
                    // 몸통/헤드 클릭 (이동 및 삭제)
                    if (noteRect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
                    {
                        if (e.button == 0)
                        {
                            selectedNoteIndex = i;
                            isDragging = true;
                            e.Use();
                        }
                        else if (e.button == 1)
                        {
                            currentMap.notes.RemoveAt(i);
                            selectedNoteIndex = -1;
                            EditorUtility.SetDirty(currentMap);
                            e.Use(); break;
                        }
                    }
                }

                // 타임라인 탐색 및 노트 추가
                if (!isDragging && !isDraggingTail && contentRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                    {
                        if (e.button == 0)
                        {
                            // Shift + 클릭 시 신규 노트 생성
                            if (e.shift && e.type == EventType.MouseDown)
                            {
                                float newTime = Mathf.Round((localMouse.x / zoom) / snapInterval) * snapInterval;
                                int newLane = Mathf.Clamp(Mathf.FloorToInt((localMouse.y - 10) / 40f), 0, 3);
                                currentMap.notes.Add(new NoteInfo { time = newTime, lane = newLane });
                                currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time));
                                selectedNoteIndex = currentMap.notes.FindIndex(n => Mathf.Approximately(n.time, newTime) && n.lane == newLane);
                                EditorUtility.SetDirty(currentMap);
                            }
                            else // 일반 클릭 시 탐색(Scrubbing)
                            {
                                float lastTime = currentTime;
                                currentTime = Mathf.Clamp(localMouse.x / zoom, 0, currentMap.music.length - 0.01f);
                                if (previewSource != null) 
                                {
                                    previewSource.time = currentTime;
                                    if (!isPlaying && !Mathf.Approximately(lastTime, currentTime))
                                    {
                                        ScrubSound();
                                    }
                                }
                            }
                            Repaint();
                        }
                    }
                }

                float px = currentTime * zoom;
                Handles.color = Color.red;
                Handles.DrawLine(new Vector3(contentRect.x + px, contentRect.y), new Vector3(contentRect.x + px, contentRect.yMax));
            }
        }

        private void RunSmartAutoMapper()
        {
            if (currentMap == null || currentMap.music == null) return;
            currentMap.notes.Clear();

            AudioClip clip = currentMap.music;
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            float beatInterval = 60f / currentMap.bpm;
            float snapInterval = beatInterval / 4f; // Quantize to 1/16th (considering 4 notes per beat)
            int samplesPerCheck = (int)(snapInterval * clip.frequency * clip.channels);

            float lastEnergy = 0;
            int lastLane = 0;
            int stairDir = 1;
            int trillToggle = 0;
            int patternStepCount = 0;

            for (int i = 0; i < samples.Length; i += samplesPerCheck)
            {
                float currentEnergy = 0;
                int end = Mathf.Min(i + samplesPerCheck, samples.Length);
                for (int j = i; j < end; j++) currentEnergy += Mathf.Abs(samples[j]);
                currentEnergy /= samplesPerCheck;

                if (i > 0 && currentEnergy > lastEnergy * autoMapThreshold)
                {
                    float exactTime = (float)i / (clip.frequency * clip.channels);
                    float quantizedTime = Mathf.Round(exactTime / snapInterval) * snapInterval;

                    if (quantizedTime < clip.length && currentMap.notes.FindIndex(n => Mathf.Approximately(n.time, quantizedTime)) == -1)
                    {
                        // Pattern Swapping Logic
                        if (patternStepCount <= 0)
                        {
                            currentPattern = (AutoPattern)Random.Range(0, 5);
                            patternStepCount = Random.Range(4, 9); // Pattern length (4 ~ 8 notes)
                        }

                        switch (currentPattern)
                        {
                            case AutoPattern.Trill:
                                lastLane = (trillToggle == 0) ? Random.Range(0, 2) : Random.Range(2, 4);
                                trillToggle = 1 - trillToggle;
                                currentMap.notes.Add(new NoteInfo { time = quantizedTime, lane = lastLane });
                                break;
                            case AutoPattern.Stair:
                                lastLane = Mathf.Clamp(lastLane + stairDir, 0, 3);
                                if (lastLane == 3) stairDir = -1; else if (lastLane == 0) stairDir = 1;
                                currentMap.notes.Add(new NoteInfo { time = quantizedTime, lane = lastLane });
                                break;
                            case AutoPattern.Denim:
                                currentMap.notes.Add(new NoteInfo { time = quantizedTime, lane = (trillToggle == 0) ? 0 : 1 });
                                currentMap.notes.Add(new NoteInfo { time = quantizedTime, lane = (trillToggle == 0) ? 2 : 3 });
                                trillToggle = 1 - trillToggle;
                                break;
                            default: // Random & Stream
                                lastLane = Random.Range(0, 4);
                                currentMap.notes.Add(new NoteInfo { time = quantizedTime, lane = lastLane });
                                break;
                        }

                        // Add Chords (Simultaneous notes) on high energy peaks
                        if (Random.value < chordChance && currentPattern != AutoPattern.Denim)
                        {
                            int extraLane = (lastLane + Random.Range(1, 4)) % 4;
                            currentMap.notes.Add(new NoteInfo { time = quantizedTime, lane = extraLane });
                        }

                        patternStepCount--;
                    }
                }
                lastEnergy = currentEnergy;
            }

            EditorUtility.SetDirty(currentMap);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=cyan>[SmartAutoMap]</color> Success! Generated {currentMap.notes.Count} notes with custom patterns.");
        }

        private void HandleInput()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha4) { AddNote(currentTime, e.keyCode - KeyCode.Alpha1); e.Use(); }
                if (e.keyCode == KeyCode.Space) { TogglePlay(); e.Use(); }
            }
        }

        private void AddNote(float time, int lane) { currentMap.notes.Add(new NoteInfo { time = time, lane = lane }); currentMap.notes.Sort((a, b) => a.time.CompareTo(b.time)); EditorUtility.SetDirty(currentMap); }
        private void TogglePlay() { if (isPlaying) { previewSource.Pause(); isPlaying = false; } else { previewSource.clip = currentMap.music; previewSource.time = Mathf.Clamp(currentTime, 0, currentMap.music.length - 0.01f); previewSource.pitch = playbackSpeed; previewSource.Play(); isPlaying = true; } }
        private void StopPlay() { if (previewSource != null) previewSource.Stop(); isPlaying = false; currentTime = 0; }
        private Color GetLaneColor(int lane) => lane switch { 0 => Color.cyan, 1 => Color.green, 2 => Color.yellow, 3 => Color.red, _ => Color.white };

        private void ScrubSound()
        {
            if (previewSource == null || currentMap.music == null) return;
            previewSource.pitch = playbackSpeed;
            previewSource.Play();
            stopScrubTime = Time.realtimeSinceStartup + 0.1f; // Play for exactly 100ms
        }

        private void DrawWaveform(Rect rect)
        {
            if (currentMap.music == null) return;
            if (waveformTexture == null || lastWaveformClip != currentMap.music)
            {
                GenerateWaveform();
            }

            if (waveformTexture != null)
            {
                GUI.color = new Color(1, 0.5f, 0, 0.4f); // Orange with transparency
                GUI.DrawTexture(rect, waveformTexture);
                GUI.color = Color.white;
            }
        }

        private void GenerateWaveform()
        {
            AudioClip clip = currentMap.music;
            lastWaveformClip = clip;
            
            int width = WAVEFORM_RESOLUTION;
            int height = 128;
            waveformTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            Color[] colors = new Color[width * height];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.clear;

            int packSize = (clip.samples * clip.channels) / width;
            for (int x = 0; x < width; x++)
            {
                float max = 0;
                for (int i = 0; i < packSize; i++)
                {
                    float val = Mathf.Abs(samples[x * packSize + i]);
                    if (val > max) max = val;
                }

                int barHeight = Mathf.CeilToInt(max * height);
                for (int y = 0; y < barHeight; y++)
                {
                    int topY = (height / 2) + (y / 2);
                    int bottomY = (height / 2) - (y / 2);
                    if (topY < height) colors[topY * width + x] = Color.white;
                    if (bottomY >= 0) colors[bottomY * width + x] = Color.white;
                }
            }

            waveformTexture.SetPixels(colors);
            waveformTexture.Apply();
        }
    }
}
