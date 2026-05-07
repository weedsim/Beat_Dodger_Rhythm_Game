using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.Events;

namespace BeatDodger.UI
{
    /// <summary>
    /// 사진 형태의 수평 이동형 싱크 보정 컨트롤러
    /// </summary>
    public class SyncCalibrationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip tickSound;
        [SerializeField] private TextMeshProUGUI statusText;
        
        [Header("Horizontal UI Elements")]
        [SerializeField] private RectTransform ballPrefab;     // 날아오는 공 (UI)
        [SerializeField] private RectTransform targetLine;     // 목표 선 (분홍색 선)
        [SerializeField] private RectTransform spawnPoint;      // 시작 지점
        [SerializeField] private RectTransform container;       // 공이 생성될 부모

        [Header("Settings")]
        [SerializeField] private float bpm = 120f;
        [SerializeField] private int samplesNeeded = 10;
        [SerializeField] private float travelDuration = 1.0f;   // 공이 이동하는 시간 (초)
        
        public UnityEvent OnCalibrationComplete;
        
        private float _beatInterval;
        private double _nextHitTargetTime;
        private List<float> _samples = new List<float>();
        private bool _isActive;

        private void Awake()
        {
            _beatInterval = 60f / bpm;
            if (ballPrefab != null) ballPrefab.gameObject.SetActive(false);
        }

        public void StartCalibration()
        {
            _samples.Clear();
            _isActive = true;
            
            // 첫 번째 공의 타격 목표 시간을 현재로부터 약간 뒤로 설정
            _nextHitTargetTime = AudioSettings.dspTime + travelDuration + 1.0; 
            
            UpdateUI();
            Debug.Log("[Sync] Horizontal Calibration Started.");
        }

        public void StopCalibration()
        {
            _isActive = false;
        }

        private void Update()
        {
            if (!_isActive) return;

            double currentTime = AudioSettings.dspTime;

            // 공 생성 스케줄링 (타격 시간 - 이동 시간)
            if (currentTime >= _nextHitTargetTime - travelDuration)
            {
                SpawnBall(_nextHitTargetTime);
                _nextHitTargetTime += _beatInterval;
            }

            // 입력 기록
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0))
            {
                RecordSample(currentTime);
            }
        }

        private void SpawnBall(double targetTime)
        {
            if (ballPrefab == null || container == null || spawnPoint == null || targetLine == null) return;

            RectTransform ball = Instantiate(ballPrefab, container);
            ball.gameObject.SetActive(true);
            ball.position = spawnPoint.position;

            // 소리 재생 (타격 시점에 맞춰 재생하거나 생성 시점에 맞춰 스케줄링)
            // 여기서는 정밀도를 위해 타격 시점에 소리가 나도록 dspTime 기반으로 PlayScheduled를 고려할 수도 있으나, 
            // 일단 간단하게 생성 시 호출하고 PlayOneShot을 사용 (또는 타격 시점에 호출)
            
            // 공 이동 로직
            StartCoroutine(MoveBall(ball, targetTime));
        }

        private System.Collections.IEnumerator MoveBall(RectTransform ball, double targetTime)
        {
            Vector3 startPos = spawnPoint.position;
            Vector3 endPos = targetLine.position;
            Image ballImage = ball.GetComponent<Image>();
            bool soundPlayed = false;

            while (ball != null)
            {
                double currentTime = AudioSettings.dspTime;
                float elapsed = (float)(currentTime - (targetTime - travelDuration));
                float t = elapsed / travelDuration;

                // Play sound when reaching target
                if (!soundPlayed && t >= 1.0f)
                {
                    if (audioSource != null && tickSound != null)
                        audioSource.PlayOneShot(tickSound);
                    soundPlayed = true;
                }

                // Fade out after passing the target line
                if (ballImage != null && t > 1.0f)
                {
                    Color c = ballImage.color;
                    c.a = Mathf.Lerp(1f, 0f, (t - 1.0f) / 1.0f); // Fade out from t=1.0 to t=2.0
                    ballImage.color = c;
                }

                // Use LerpUnclamped to move past the target line
                ball.position = Vector3.LerpUnclamped(startPos, endPos, t);

                // Destroy after passing far enough
                if (t >= 2.0f)
                {
                    Destroy(ball.gameObject);
                    yield break;
                }
                yield return null;
            }
        }

        private void RecordSample(double inputTime)
        {
            // Find the closest target time among active and upcoming balls
            double closestTarget = _nextHitTargetTime;
            double minDiff = double.MaxValue;

            // Check several past beat intervals based on travelDuration to find the intended ball
            int checkCount = Mathf.CeilToInt(travelDuration / _beatInterval) + 2;
            
            for (int i = 0; i < checkCount; i++)
            {
                double target = _nextHitTargetTime - (i * _beatInterval);
                double diff = System.Math.Abs(inputTime - target);
                
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestTarget = target;
                }
            }

            float offset = (float)(inputTime - closestTarget);
            _samples.Add(offset);

            UpdateUI();

            if (_samples.Count >= samplesNeeded)
            {
                ApplyAndFinish();
            }
        }

        private void UpdateUI()
        {
            if (statusText == null) return;

            if (_samples.Count == 0)
            {
                statusText.text = "Press <color=#FFFB00>ANY KEY</color> to match the ball.\n" +
                                 $"<size=30>Progress: ( 0 / {samplesNeeded} )</size>";
            }
            else
            {
                float lastMs = _samples.Last() * 1000f;
                string color = lastMs > 0 ? "#FF4D4D" : "#4DFF4D";
                statusText.text = $"Calibrating... Press <color=#FFFB00>ANY KEY</color>\n" +
                                 $"<size=30>Progress: ( {_samples.Count} / {samplesNeeded} )</size>\n" +
                                 $"<size=25>Last Offset: <color={color}>{lastMs:F0}ms</color></size>";
            }
        }

        private void ApplyAndFinish()
        {
            _isActive = false;

            float finalOffset = 0;
            if (_samples.Count > 0)
            {
                var sorted = _samples.OrderBy(x => x).ToList();
                if (sorted.Count >= 6)
                    finalOffset = sorted.Skip(2).Take(sorted.Count - 4).Average();
                else
                    finalOffset = sorted.Average();
            }

            RhythmConfig.Instance.GlobalSyncOffset = finalOffset;
            PlayerPrefs.SetFloat("GlobalSyncOffset", finalOffset);
            PlayerPrefs.Save();

            if (statusText != null)
            {
                statusText.text = $"<color=#4DFF4D>CALIBRATION DONE!</color>\n" +
                                 "<size=25>Press 'RETURN' to go back.</size>";
            }

            OnCalibrationComplete?.Invoke();
            
            Debug.Log($"[Sync] Horizontal Calibration Finished. Offset: {finalOffset * 1000f:F1}ms");
        }
    }
}
