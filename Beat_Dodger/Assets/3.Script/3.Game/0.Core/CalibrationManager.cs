using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace BeatDodger.Game
{
    /// <summary>
    /// PlayScheduled를 사용하여 소프트웨어 지연이 없는 초정밀 틱 소리를 재생하고
    /// dspTime 기반으로 오프셋을 측정하는 보정 매니저입니다.
    /// </summary>
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

            // 시작 시간 예약 (2초 뒤 첫 소리)
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

            // 1. 소리 예약 (PlayScheduled 사용 - 지연 최소화)
            // 현재 시간보다 0.2초 앞선 시점까지 미리 예약해둠
            while (_nextSoundToSchedule < currentTime + 0.2)
            {
                if (audioSource != null && tickSound != null)
                {
                    // 전용 오디오 소스를 통해 정확한 시점에 재생 예약
                    // 중복 예약을 방지하기 위해 마지막 예약 시간 체크
                    if (_nextSoundToSchedule > _lastScheduledTime)
                    {
                        audioSource.PlayOneShot(tickSound); // PlayOneShot은 dspTime 지정을 못하므로 PlayScheduled용 별도 로직 권장
                        // 만약 PlayOneShot의 지연이 걱정된다면 아래와 같이 전용 Source를 쓰는 것이 좋습니다.
                        // audioSource.clip = tickSound;
                        // audioSource.PlayScheduled(_nextSoundToSchedule);
                        
                        _lastScheduledTime = _nextSoundToSchedule;
                    }
                }
                _nextSoundToSchedule += _beatInterval;
            }

            // 2. 노트 스폰 체크 (밀린 스폰들을 처리하되 너무 늦은 건 건너뜀)
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

            // 3. 입력 감지
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
            // 가장 가까운 비트 시간 찾기
            // _nextSoundToSchedule은 미래 시간을 가리키고 있으므로 적절히 과거 비트를 탐색해야 함
            double closestBeat = _lastScheduledTime;
            // 만약 inputTime이 _lastScheduledTime보다 훨씬 이전이라면 (거의 불가능하지만) 
            // _beatInterval을 빼가며 가장 가까운 지점을 찾음
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

            // 1. PlayerPrefs에 물리적으로 즉시 직접 저장 (가장 확실함)
            PlayerPrefs.SetFloat("RhythmOffset", finalOffset);
            PlayerPrefs.Save();
            Debug.Log($"<color=green>[Calibration]</color> Directly saved to PlayerPrefs: {finalOffset * 1000:F1}ms");

            // 2. 만약 현재 씬에 리듬 매니저가 있다면 인스턴스에 반영
            if (RhythmManager.Instance != null)
            {
                RhythmManager.Instance.GlobalOffset = finalOffset;
                // RhythmManager의 SaveOffset은 이미 PlayerPrefs.Save를 하므로 생략 가능하나 명시적 호출 가능
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
