using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace BeatDodger.Game
{
    
    
    
    
    public class CalibrationManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip tickSound;
        [SerializeField] private GameObject notePrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform judgePoint;

        [Header("Settings")]
        [SerializeField] private float bpm = 120f;
        [SerializeField] private int samplesNeeded = 12;
        [SerializeField] private float travelDuration = 1.25f;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject calibrationPanel;

        private float _beatInterval;
        private double _nextSoundToSchedule;
        private double _nextSpawnTargetTime;
        private double _lastScheduledTime = -1;

        private List<float> _offsetSamples = new List<float>();
        private bool _isCalibrating = false;

        private float _prefabArcHeight = 5f;
        private float _prefabSwerve = 0f;

        public void StartCalibration()
        {
            _beatInterval = 60f / bpm;
            
            if (RhythmManager.Instance != null)
            {
                RhythmManager.Instance.GlobalOffset = 0f;
                Debug.Log("<color=yellow>[Calibration] Offset temporarily reset to 0.</color>");
            }

            if (notePrefab != null && notePrefab.TryGetComponent<Projectile>(out var p))
            {
                _prefabArcHeight = (p.MinArcHeight + p.MaxArcHeight) / 2f;
            }

            
            _nextSoundToSchedule = AudioSettings.dspTime + 2.0;
            _nextSpawnTargetTime = _nextSoundToSchedule - travelDuration;
            _lastScheduledTime = -1;

            _offsetSamples.Clear();
            _isCalibrating = true;
            
            if (calibrationPanel != null) calibrationPanel.SetActive(true);
            UpdateStatusUI();
            
            Debug.Log($"[Calibration] Started. BPM: {bpm}, Interval: {_beatInterval:F3}s");
        }

        private void Update()
        {
            if (!_isCalibrating) return;

            double currentTime = AudioSettings.dspTime;

            
            
            while (_nextSoundToSchedule < currentTime + 0.2)
            {
                if (audioSource != null && tickSound != null)
                {
                    
                    
                    if (_nextSoundToSchedule > _lastScheduledTime)
                    {
                        audioSource.PlayOneShot(tickSound); 
                        
                        
                        
                        
                        _lastScheduledTime = _nextSoundToSchedule;
                    }
                }
                _nextSoundToSchedule += _beatInterval;
            }

            
            while (currentTime >= _nextSpawnTargetTime)
            {
                double hitTime = _nextSpawnTargetTime + travelDuration;
                double remainingTime = hitTime - currentTime;
                
                if (remainingTime > travelDuration * 0.8)
                {
                    SpawnCalibrationNote((float)hitTime);
                }
                
                _nextSpawnTargetTime += _beatInterval;
            }

            
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0))
            {
                RecordSample(currentTime);
            }
        }

        private void SpawnCalibrationNote(float targetHitTime)
        {
            if (notePrefab == null) return;
            GameObject note = Instantiate(notePrefab, spawnPoint.position, Quaternion.identity);
            if (note.TryGetComponent<Projectile>(out var p)) p.enabled = false;
            var mover = note.AddComponent<CalibrationNoteMover>();
            mover.Initialize(spawnPoint.position, judgePoint.position, travelDuration, targetHitTime, _prefabArcHeight, _prefabSwerve);
        }

        private void RecordSample(double inputTime)
        {
            
            
            double closestBeat = _lastScheduledTime;
            
            
            while (inputTime < closestBeat - _beatInterval * 0.5) closestBeat -= _beatInterval;
            while (inputTime > closestBeat + _beatInterval * 0.5) closestBeat += _beatInterval;

            float diff = (float)(closestBeat - inputTime);
            _offsetSamples.Add(diff);
            
            Debug.Log($"[Calibration] Sample: {diff * 1000:F1}ms (Input: {inputTime:F3}, Beat: {closestBeat:F3})");
            UpdateStatusUI();

            if (_offsetSamples.Count >= samplesNeeded) Finish();
        }

        private void UpdateStatusUI()
        {
            if (statusText != null)
                statusText.text = $"Calibration ( {_offsetSamples.Count} / {samplesNeeded} )\nLast: {(_offsetSamples.Count > 0 ? (_offsetSamples.Last()*1000).ToString("F0") : "0")}ms\n<size=20>Hit WITH the Sound, don't react!</size>";
        }

        private void Finish()
        {
            _isCalibrating = false;
            float finalOffset = 0f;
            if (_offsetSamples.Count > 0)
            {
                var sorted = _offsetSamples.OrderBy(x => x).ToList();
                if (sorted.Count >= 8) finalOffset = sorted.Skip(2).Take(sorted.Count - 4).Average();
                else finalOffset = sorted.Average();
            }

            
            PlayerPrefs.SetFloat("RhythmOffset", finalOffset);
            PlayerPrefs.Save();
            Debug.Log($"<color=green>[Calibration]</color> Directly saved to PlayerPrefs: {finalOffset * 1000:F1}ms");

            
            if (RhythmManager.Instance != null)
            {
                RhythmManager.Instance.GlobalOffset = finalOffset;
                
                Debug.Log($"<color=yellow>[Calibration]</color> Final value also reflected to active RhythmManager instance.");
            }

            if (statusText != null)
                statusText.text = $"DONE!\nNew Offset: {finalOffset * 1000:F1}ms";
        }
    }

    public class CalibrationNoteMover : MonoBehaviour
    {
        private Vector3 _start, _end, _control;
        private float _duration, _hitTime;

        public void Initialize(Vector3 start, Vector3 end, float duration, float hitTime, float arc, float swerve)
        {
            _start = start;
            _end = end;
            _duration = duration;
            _hitTime = hitTime;
            Vector3 mid = (start + end) / 2f;
            _control = mid + Vector3.up * arc + Vector3.right * swerve;
            Destroy(gameObject, duration + 1f);
        }

        private void Update()
        {
            float startTime = _hitTime - _duration;
            float elapsed = (float)(AudioSettings.dspTime - startTime);
            float t = elapsed / _duration;
            Vector3 m1 = Vector3.Lerp(_start, _control, t);
            Vector3 m2 = Vector3.Lerp(_control, _end, t);
            transform.position = Vector3.Lerp(m1, m2, t);
            if (t >= 1.15f) Destroy(gameObject);
        }
    }
}
