using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.Events;

namespace BeatDodger.UI
{
    public class SyncCalibrationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip tickSound;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("3D Note Prefabs")]
        [SerializeField] private GameObject normalPrefab;
        [SerializeField] private GameObject doublePrefab;
        [SerializeField] private GameObject dashPrefab;

        [Header("Settings")]
        [SerializeField] private float bpm = 120f;
        [SerializeField] private int samplesPerPhase = 5;
        [SerializeField] private int beatsToArrive = 4;

        [Header("Tutorial Texts")]
        [TextArea] [SerializeField] private string normalText = "기본 노트입니다.\n판정선에 닿을 때 아무 키나 누르세요!";
        [TextArea] [SerializeField] private string doubleText = "2연타 노트입니다.\n판정선에 닿을 때 빠르게 두 번 누르세요!";
        [TextArea] [SerializeField] private string dashText = "가속 노트입니다.\n갑자기 빨라지니 주의해서 누르세요!";

        public UnityEvent OnCalibrationComplete;

        private enum SyncPhase { Normal, Double, Dash, Complete }
        private enum PhaseState { SpawningTutorial, WaitingForTutorialHit, SpawningCalibration, Calibrating }

        private SyncPhase _currentPhase;
        private PhaseState _currentState;

        private float _beatInterval;
        private float _travelDuration;
        private double _startTime;
        private double _nextHitTargetTime;
        private double _nextBeatTime;
        private List<float> _samples = new List<float>();
        private bool _isActive;

        private NoteEnemy _activeTutorialNote;
        private double _tutorialHitTime;
        private int _calibrationCount;
        private int _tutorialHitCount;
        private List<double> _activeTargets = new List<double>();
        private Dictionary<double, int> _targetHitCounts = new Dictionary<double, int>();
        private List<NoteEnemy> _activeNotes = new List<NoteEnemy>();

        // Environment Variables
        private GameObject _sceneRoot;
        private GameObject _floorRoot;
        private List<Renderer> _floorRenderers = new List<Renderer>();
        private float _floorStepDist;
        private Vector3 _floorInitialPos;
        private bool _floorColorFlip = false;
        private int _lastFloorGlobalStep = -1;

        private void Awake()
        {
            _beatInterval = 60f / bpm;
            _travelDuration = _beatInterval * beatsToArrive;
        }

        public void StartCalibration()
        {
            _samples.Clear();
            _activeTargets.Clear();
            _targetHitCounts.Clear();
            _tutorialHitCount = 0;
            ClearActiveNotes();
            
            _currentPhase = SyncPhase.Normal;
            _isActive = true;

            GenerateEnvironment();

            _startTime = AudioSettings.dspTime;
            _nextBeatTime = _startTime + _beatInterval;
            _lastFloorGlobalStep = 0;

            // 튜토리얼 첫 노트 스폰 스케줄링
            _tutorialHitTime = _startTime + _travelDuration + (_beatInterval * 2);
            _activeTutorialNote = SpawnNote(GetPrefabForPhase(_currentPhase), _tutorialHitTime);
            
            _currentState = PhaseState.WaitingForTutorialHit;
            UpdateUI();
            
            Debug.Log("[Sync] Tutorial & Calibration Started.");
        }

        public void StopCalibration()
        {
            _isActive = false;
            if (_activeTutorialNote != null) Destroy(_activeTutorialNote.gameObject);
            ClearActiveNotes();
            if (_sceneRoot != null) Destroy(_sceneRoot);
        }

        private void ClearActiveNotes()
        {
            foreach (var note in _activeNotes)
            {
                if (note != null) Destroy(note.gameObject);
            }
            _activeNotes.Clear();
        }

        private void Update()
        {
            if (!_isActive) return;

            double currentTime = AudioSettings.dspTime;

            // 바닥 애니메이션 비트 제어 (DOTween 대신 dspTime 기반 정밀 계산)
            UpdateFloorAnimation(currentTime);
            
            if (currentTime >= _nextBeatTime)
            {
                _nextBeatTime += _beatInterval;
            }

            switch (_currentState)
            {
                case PhaseState.SpawningTutorial:
                    // 페이즈가 넘어갔을 때 다음 튜토리얼 노트 스폰
                    _tutorialHitTime = _nextBeatTime + _travelDuration + _beatInterval; 
                    _activeTutorialNote = SpawnNote(GetPrefabForPhase(_currentPhase), _tutorialHitTime);
                    _currentState = PhaseState.WaitingForTutorialHit;
                    UpdateUI();
                    break;

                case PhaseState.WaitingForTutorialHit:
                    if (currentTime >= _tutorialHitTime && _activeTutorialNote != null && !_activeTutorialNote.IsFrozen)
                    {
                        _activeTutorialNote.FreezeAt(1.0f);
                        if (audioSource && tickSound) audioSource.PlayOneShot(tickSound);
                    }

                    if (_activeTutorialNote != null && _activeTutorialNote.IsFrozen && Input.anyKeyDown && !Input.GetMouseButtonDown(0))
                    {
                        int req = _currentPhase == SyncPhase.Double ? 2 : 1;
                        _tutorialHitCount++;
                        
                        if (audioSource && tickSound) audioSource.PlayOneShot(tickSound);

                        if (_tutorialHitCount >= req)
                        {
                            Destroy(_activeTutorialNote.gameObject);
                            _activeTutorialNote = null;
                            _tutorialHitCount = 0;

                            _calibrationCount = 0;
                            _nextHitTargetTime = _nextBeatTime + _travelDuration + _beatInterval * 2f;
                            _currentState = PhaseState.SpawningCalibration;
                            UpdateUI();
                        }
                    }
                    break;

                case PhaseState.SpawningCalibration:
                    if (currentTime >= _nextHitTargetTime - _travelDuration)
                    {
                        NoteEnemy spawned = SpawnNote(GetPrefabForPhase(_currentPhase), _nextHitTargetTime);
                        if (spawned != null) _activeNotes.Add(spawned);
                        
                        _activeTargets.Add(_nextHitTargetTime);
                        
                        _calibrationCount++;
                        _nextHitTargetTime += _beatInterval * 2f;

                        if (_calibrationCount >= samplesPerPhase)
                        {
                            _currentState = PhaseState.Calibrating;
                        }
                    }
                    HandleCalibrationInput(currentTime);
                    break;

                case PhaseState.Calibrating:
                    HandleCalibrationInput(currentTime);
                    break;
            }

            float threshold = _beatInterval * 0.45f;

            _activeNotes.RemoveAll(n => {
                if (n == null) return true;
                if (currentTime - n.TargetHitTime > threshold)
                {
                    Destroy(n.gameObject);
                    return true;
                }
                return false;
            });
        }

        private void UpdateFloorAnimation(double currentTime)
        {
            if (_floorRoot == null) return;

            double timeSinceStart = currentTime - _startTime;
            float globalStep = (float)(timeSinceStart / _beatInterval);
            int floorGlobalStep = Mathf.FloorToInt(globalStep + 0.001f);
            float innerProgress = globalStep - floorGlobalStep;
            
            // 70% 대기, 30% 이동 곡선
            float slideStartThreshold = 0.7f;
            float movementCurve = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(slideStartThreshold, 1.0f, innerProgress));

            // Z 위치 적용
            float currentZ = _floorInitialPos.z - (_floorStepDist * movementCurve);
            _floorRoot.transform.localPosition = new Vector3(_floorInitialPos.x, _floorInitialPos.y, currentZ);

            // 색상 반전 처리 (정수 박자를 넘어갈 때만 단 1번)
            if (floorGlobalStep > _lastFloorGlobalStep)
            {
                _floorColorFlip = !_floorColorFlip;
                UpdateFloorColors();
                _lastFloorGlobalStep = floorGlobalStep;
            }
        }

        private void HandleCalibrationInput(double currentTime)
        {
            float threshold = _beatInterval * 0.45f;
            int requiredHits = _currentPhase == SyncPhase.Double ? 2 : 1;

            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0))
            {
                if (_activeTargets.Count > 0)
                {
                    double closestTarget = _activeTargets.OrderBy(t => System.Math.Abs(currentTime - t)).First();
                    double diff = System.Math.Abs(currentTime - closestTarget);

                    if (diff < threshold)
                    {
                        if (!_targetHitCounts.ContainsKey(closestTarget)) _targetHitCounts[closestTarget] = 0;
                        _targetHitCounts[closestTarget]++;

                        // 첫 번째 입력의 딜레이만 보정값으로 수집 (연타의 두번째 타격은 기계적 딜레이가 크므로 무시)
                        if (_targetHitCounts[closestTarget] == 1)
                        {
                            float offset = (float)(currentTime - closestTarget);
                            _samples.Add(offset);
                        }

                        if (audioSource && tickSound) audioSource.PlayOneShot(tickSound);
                        
                        // 요구 타격 수를 채우면 노트 파괴
                        if (_targetHitCounts[closestTarget] >= requiredHits)
                        {
                            _activeTargets.Remove(closestTarget);
                            _targetHitCounts.Remove(closestTarget);
                            
                            NoteEnemy noteToDestroy = _activeNotes.FirstOrDefault(n => n != null && System.Math.Abs(n.TargetHitTime - closestTarget) < 0.001);
                            if (noteToDestroy != null)
                            {
                                Destroy(noteToDestroy.gameObject);
                                _activeNotes.Remove(noteToDestroy);
                            }

                            UpdateUI();
                            CheckPhaseCompletion();
                        }
                    }
                }
            }

            int removed = _activeTargets.RemoveAll(t => currentTime - t > threshold);
            if (removed > 0)
            {
                CheckPhaseCompletion();
            }
        }

        private void CheckPhaseCompletion()
        {
            if (_currentState == PhaseState.Calibrating && _activeTargets.Count == 0)
            {
                AdvancePhase();
            }
        }

        private void AdvancePhase()
        {
            _currentPhase++;

            if (_currentPhase >= SyncPhase.Complete)
            {
                ApplyAndFinish();
            }
            else
            {
                _currentState = PhaseState.SpawningTutorial;
                UpdateUI();
            }
        }

        private GameObject GetPrefabForPhase(SyncPhase phase)
        {
            switch (phase)
            {
                case SyncPhase.Normal: return normalPrefab;
                case SyncPhase.Double: return doublePrefab;
                case SyncPhase.Dash: return dashPrefab;
                default: return normalPrefab;
            }
        }

        private NoteEnemy SpawnNote(GameObject prefab, double targetTime)
        {
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab);
            NoteEnemy note = instance.TryGetComponent<NoteEnemy>(out NoteEnemy n) ? n : instance.AddComponent<NoteEnemy>();
            
            int centerLane = RhythmConfig.Instance != null ? RhythmConfig.Instance.LaneCount / 2 : 2;
            
            NoteType type = _currentPhase switch {
                SyncPhase.Normal => NoteType.Normal,
                SyncPhase.Double => NoteType.Double,
                SyncPhase.Dash => NoteType.Dash,
                _ => NoteType.Normal
            };

            note.Initialize(null, centerLane, 1, targetTime, _travelDuration, beatsToArrive, type);
            
            StartCoroutine(PlaySoundAtTarget(targetTime));
            
            return note;
        }

        private IEnumerator PlaySoundAtTarget(double targetTime)
        {
            while (AudioSettings.dspTime < targetTime)
            {
                if (!_isActive) yield break;
                yield return null;
            }
            if (_isActive && audioSource && tickSound)
            {
                audioSource.PlayOneShot(tickSound);
            }
        }

        private void GenerateEnvironment()
        {
            if (_sceneRoot != null) Destroy(_sceneRoot);
            _sceneRoot = new GameObject("TutorialEnvironment");

            float spawnZ = RhythmConfig.Instance != null ? RhythmConfig.Instance.SpawnLineZ : 50f;
            float judgeZ = RhythmConfig.Instance != null ? RhythmConfig.Instance.JudgeLineZ : 0f;
            float yOffset = RhythmConfig.Instance != null ? RhythmConfig.Instance.YOffset : 0f;
            float laneSpacing = RhythmConfig.Instance != null ? RhythmConfig.Instance.LaneSpacing : 2f;
            int totalLanes = RhythmConfig.Instance != null ? RhythmConfig.Instance.LaneCount : 4;
            
            _floorStepDist = (spawnZ - judgeZ) / beatsToArrive;

            int centerLane = totalLanes / 2;
            float xPos = (centerLane - (totalLanes / 2f - 0.5f)) * laneSpacing;

            _floorRoot = new GameObject("FloorMover");
            _floorRoot.transform.SetParent(_sceneRoot.transform);
            _floorRoot.transform.position = Vector3.zero;

            _floorRenderers.Clear();
            for (int i = -1; i < beatsToArrive + 2; i++) 
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tile.transform.SetParent(_floorRoot.transform);
                Destroy(tile.GetComponent<Collider>());
                
                tile.transform.rotation = Quaternion.Euler(90, 0, 0);
                tile.transform.localScale = new Vector3(laneSpacing, _floorStepDist, 1f);
                
                float zPos = judgeZ + (i * _floorStepDist);
                tile.transform.position = new Vector3(xPos, yOffset - 0.01f, zPos);
                
                Renderer r = tile.GetComponent<Renderer>();
                _floorRenderers.Add(r);
            }

            _floorInitialPos = _floorRoot.transform.localPosition;
            UpdateFloorColors();

            // 판정선 (Judge Line)
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.transform.SetParent(_sceneRoot.transform);
            Destroy(line.GetComponent<Collider>());
            line.transform.rotation = Quaternion.Euler(90, 0, 0);
            line.transform.position = new Vector3(xPos, yOffset + 0.05f, judgeZ);
            line.transform.localScale = new Vector3(laneSpacing, 0.2f, 1f);
            
            Renderer lr = line.GetComponent<Renderer>();
            lr.material.color = new Color(1f, 0.2f, 0.6f, 0.8f);
        }

        private void UpdateFloorColors()
        {
            for (int i = 0; i < _floorRenderers.Count; i++)
            {
                bool isDark = (i % 2 == 0) ^ _floorColorFlip;
                _floorRenderers[i].material.color = isDark ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.5f, 0.5f, 0.5f);
            }
        }

        private void UpdateUI()
        {
            if (statusText == null) return;

            if (_currentState == PhaseState.SpawningTutorial || _currentState == PhaseState.WaitingForTutorialHit)
            {
                string text = _currentPhase switch
                {
                    SyncPhase.Normal => normalText,
                    SyncPhase.Double => doubleText,
                    SyncPhase.Dash => dashText,
                    _ => ""
                };
                statusText.text = $"<color=#FFFB00>[TUTORIAL]</color>\n{text}";
            }
            else
            {
                int currentPhaseSamples = _samples.Count % samplesPerPhase;
                if (_currentState == PhaseState.Calibrating && currentPhaseSamples == 0 && _samples.Count > 0)
                {
                    currentPhaseSamples = samplesPerPhase; 
                }

                statusText.text = $"<color=#00FFFF>[CALIBRATION: {_currentPhase}]</color>\n" +
                                 $"노트에 맞춰 키를 누르세요!\n" +
                                 $"<size=30>Progress: ( {currentPhaseSamples} / {samplesPerPhase} )</size>";
            }
        }

        private void ApplyAndFinish()
        {
            _isActive = false;
            if (_sceneRoot != null) Destroy(_sceneRoot);

            float finalOffset = 0;
            if (_samples.Count > 0)
            {
                var sorted = _samples.OrderBy(x => x).ToList();
                int skip = Mathf.RoundToInt(sorted.Count * 0.15f);
                if (sorted.Count > skip * 2)
                    finalOffset = sorted.Skip(skip).Take(sorted.Count - skip * 2).Average();
                else
                    finalOffset = sorted.Average();
            }

            if (RhythmConfig.Instance != null)
                RhythmConfig.Instance.GlobalSyncOffset = finalOffset;
            
            PlayerPrefs.SetFloat("GlobalSyncOffset", finalOffset);
            PlayerPrefs.Save();

            if (statusText != null)
            {
                statusText.text = $"<color=#4DFF4D>TUTORIAL & CALIBRATION DONE!</color>\n" +
                                 $"<size=25>Offset: {finalOffset * 1000f:F0}ms</size>\n" +
                                 "<size=25>Press 'RETURN' to go back.</size>";
            }

            OnCalibrationComplete?.Invoke();
            Debug.Log($"[Sync] Tutorial & Calibration Finished. Offset: {finalOffset * 1000f:F1}ms");
        }
    }
}
