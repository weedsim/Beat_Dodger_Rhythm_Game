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
        private Vector2 scrollPos;

        [Header("Auto Map Settings")]
        private float autoMapThreshold = 1.6f; 
        private float chordChance = 0.2f;    // Chance for simultaneous notes
        private float patternSwapChance = 0.3f; // Chance to switch between Trill/Stair/Stream

        private enum AutoPattern { Stream, Trill, Stair, Denim, Random }
        private AutoPattern currentPattern = AutoPattern.Random;

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
            // Row 1: Asset & Playback
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUIUtility.labelWidth = 60;
                currentMap = (NoteMapData)EditorGUILayout.ObjectField("Data", currentMap, typeof(NoteMapData), false, GUILayout.Width(250));
                
                GUILayout.Space(10);
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(50))) TogglePlay();
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(50))) StopPlay();

                GUILayout.FlexibleSpace();
                GUILayout.Label($"Zoom:", EditorStyles.miniLabel);
                zoom = GUILayout.HorizontalSlider(zoom, 10f, 300f, GUILayout.Width(100));
            }

            // Row 2: Auto Map Settings
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUIUtility.labelWidth = 40;
                autoMapThreshold = EditorGUILayout.Slider("Sens.", autoMapThreshold, 1.0f, 4.0f, GUILayout.Width(150));
                
                GUILayout.Space(15);
                EditorGUIUtility.labelWidth = 50;
                chordChance = EditorGUILayout.Slider("Chord", chordChance, 0f, 1.0f, GUILayout.Width(150));

                GUILayout.Space(20);
                if (GUILayout.Button("Smart Auto Map", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    if (EditorUtility.DisplayDialog("Smart Auto Map", "Generate professional patterns? Existing notes will be cleared.", "Yes", "Cancel")) RunSmartAutoMapper();
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Time: {currentTime:F2}s", EditorStyles.miniLabel, GUILayout.Width(80));
            }
        }

        private void DrawTimeline()
        {
            Rect timelineRect = GUILayoutUtility.GetRect(0, position.width, 180, 180);
            GUI.Box(timelineRect, "", EditorStyles.helpBox);
            if (currentMap.music == null) return;

            using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos, GUILayout.Height(200)))
            {
                scrollPos = scroll.scrollPosition;
                float totalWidth = currentMap.music.length * zoom;
                Rect innerRect = GUILayoutUtility.GetRect(totalWidth, 180);

                Event e = Event.current;
                if (innerRect.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                {
                    currentTime = Mathf.Clamp((e.mousePosition.x - innerRect.x) / zoom, 0, currentMap.music.length - 0.01f);
                    if (previewSource != null) previewSource.time = currentTime;
                    Repaint();
                    e.Use();
                }

                float beatInterval = 60f / currentMap.bpm;
                for (float t = 0; t < currentMap.music.length; t += beatInterval)
                {
                    float x = t * zoom;
                    Handles.color = new Color(1, 1, 1, 0.1f);
                    Handles.DrawLine(new Vector3(innerRect.x + x, innerRect.y), new Vector3(innerRect.x + x, innerRect.yMax));
                }

                for (int i = 0; i < currentMap.notes.Count; i++)
                {
                    var note = currentMap.notes[i];
                    Rect noteRect = new Rect(innerRect.x + note.time * zoom - 6, innerRect.y + (note.lane * 35) + 15, 12, 25);
                    EditorGUI.DrawRect(noteRect, GetLaneColor(note.lane));
                }

                float px = currentTime * zoom;
                Handles.color = Color.red;
                Handles.DrawLine(new Vector3(innerRect.x + px, innerRect.y), new Vector3(innerRect.x + px, innerRect.yMax));
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
        private void TogglePlay() { if (isPlaying) { previewSource.Pause(); isPlaying = false; } else { previewSource.clip = currentMap.music; previewSource.time = Mathf.Clamp(currentTime, 0, currentMap.music.length - 0.01f); previewSource.Play(); isPlaying = true; } }
        private void StopPlay() { if (previewSource != null) previewSource.Stop(); isPlaying = false; currentTime = 0; }
        private Color GetLaneColor(int lane) => lane switch { 0 => Color.cyan, 1 => Color.green, 2 => Color.yellow, 3 => Color.red, _ => Color.white };
    }
}
