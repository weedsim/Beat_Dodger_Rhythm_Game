using UnityEngine;
using UnityEngine.Pool;
using System.Collections;

namespace BeatDodger.Game
{
    public class Projectile : MonoBehaviour
    {
        private IObjectPool<Projectile> pool;
        private Enemy sourceEnemy;
        private RhythmManager manager;
        private int laneIndex;

        [Header("Dynamic Trajectory Settings")]
        [SerializeField] private AnimationCurve speedCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private float minArcHeight = 1.5f;
        [SerializeField] private float maxArcHeight = 4.0f;
        [SerializeField] private Vector3 rotationSpeed = new Vector3(360f, 360f, 0);

        public float MinArcHeight => minArcHeight;
        public float MaxArcHeight => maxArcHeight;

        private Vector3 controlPoint; 
        private Vector3 startPos;
        private Vector3 targetPos;
        private float arrivalTime; // Used as reflection start time
        private float travelDuration;
        private float targetNoteTime; // Music time when it should hit

        public float TargetNoteTime => targetNoteTime;

        [Header("Approach Circle")]
        [SerializeField] private GameObject approachCircle;
        [SerializeField] private float startScale = 1.5f;
        [SerializeField] private float endScale = 1.0f;
        [SerializeField] private Vector3 approachCircleRotationOffset = new Vector3(90f, 0f, 0f);
        [Range(0.01f, 1.0f)]
        [SerializeField] private float showThreshold = 0.3f; // Show only in the last 30% of travel
        [SerializeField] private bool useFadeIn = true;

        private SpriteRenderer _circleRenderer;
        private bool isReflected;
        private bool isActive;

        public bool IsReflected => isReflected;
        public int LaneIndex => laneIndex;

        public void Setup(IObjectPool<Projectile> pool, RhythmManager manager)
        {
            this.pool = pool;
            this.manager = manager;
        }

        public void Initialize(Enemy source, Vector3 start, Vector3 target, float duration, int lane, float customArc = -1f, float targetNoteTimeValue = 0f)
        {
            sourceEnemy = source;
            startPos = start;
            targetPos = target;
            targetNoteTime = targetNoteTimeValue;
            travelDuration = duration;
            laneIndex = lane;
            ApplyLaneColor(lane);
            
            arrivalTime = targetNoteTime;
            
            isReflected = false;
            isActive = true;
            
            CalculateControlPoint(customArc);
            
            if (approachCircle != null)
            {
                if (_circleRenderer == null) _circleRenderer = approachCircle.GetComponent<SpriteRenderer>();
                
                approachCircle.transform.position = targetPos;
                approachCircle.transform.rotation = Quaternion.Euler(approachCircleRotationOffset);
                approachCircle.transform.localScale = Vector3.one * startScale;
                
                // Initially hide or set alpha to 0
                if (useFadeIn && _circleRenderer != null)
                {
                    Color c = _circleRenderer.color;
                    c.a = 0;
                    _circleRenderer.color = c;
                }
                approachCircle.SetActive(false);
            }

            transform.position = startPos;
            gameObject.SetActive(true);
        }

        private void CalculateControlPoint(float customArc)
        {
            Vector3 midPoint = (startPos + targetPos) / 2f;
            float height = (customArc >= 0) ? customArc : Random.Range(minArcHeight, maxArcHeight);
            
            controlPoint = midPoint + (Vector3.up * height);
        }

        private void Update()
        {
            if (!isActive) return;

            float syncTime = manager.SyncTime;
            float normalizedTime = (syncTime - (targetNoteTime - travelDuration)) / travelDuration;
            float easedT = speedCurve.Evaluate(normalizedTime);

            if (!isReflected)
            {
                transform.position = CalculateBezierPoint(easedT, startPos, controlPoint, targetPos);
                transform.Rotate(rotationSpeed * Time.deltaTime);

                HandleApproachCircle(normalizedTime);

                if (normalizedTime > 1.1f) OnMiss();
            }
            else
            {
                if (approachCircle != null && approachCircle.activeSelf) approachCircle.SetActive(false);

                float returnTime = travelDuration / manager.ReflectionSpeedMultiplier;
                // Reflection movement can keep using real-time for smoothness
                float reflectionT = (Time.time - arrivalTime) / returnTime; 
                
                Vector3 currentEnemyPos = startPos;
                if (sourceEnemy != null) currentEnemyPos = sourceEnemy.transform.position;

                transform.position = Vector3.Lerp(targetPos, currentEnemyPos, reflectionT);
                transform.Rotate(rotationSpeed * 2f * Time.deltaTime);

                if (reflectionT >= 1.0f) OnHitEnemy();
            }
        }

        private void HandleApproachCircle(float normalizedTime)
        {
            if (approachCircle == null) return;

            float showStartTime = 1.0f - showThreshold;

            if (normalizedTime >= showStartTime && normalizedTime <= 1.05f)
            {
                if (!approachCircle.activeSelf) approachCircle.SetActive(true);
                
                approachCircle.transform.position = targetPos;
                approachCircle.transform.rotation = Quaternion.Euler(approachCircleRotationOffset);
                
                float currentScale = Mathf.Lerp(startScale, endScale, normalizedTime);
                approachCircle.transform.localScale = Vector3.one * currentScale;

                if (useFadeIn && _circleRenderer != null)
                {
                    // Map [showStartTime, 1.0] to alpha [0, 1]
                    float alphaT = (normalizedTime - showStartTime) / showThreshold;
                    Color c = GetLaneColor(laneIndex);
                    c.a = Mathf.Clamp01(alphaT);
                    _circleRenderer.color = c;
                }
            }
            else
            {
                if (approachCircle.activeSelf) approachCircle.SetActive(false);
            }
        }

        private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            Vector3 p = uu * p0 + 2 * u * t * p1 + tt * p2;
            return p;
        }

        public void Reflect()
        {
            if (isReflected || !isActive) return;
            isReflected = true;
            arrivalTime = Time.time; // Record real-time for reflection animation
        }

        public void ReturnToPool()
        {
            if (!isActive) return;
            isActive = false;

            if (approachCircle != null) approachCircle.SetActive(false);

            if (sourceEnemy != null && !sourceEnemy.IsDead)
            {
                sourceEnemy.SetFiring(false);
            }

            manager.RemoveFromActiveList(this, laneIndex);
            pool.Release(this);
        }

        private void OnMiss()
        {
            manager.NoteMissed(laneIndex);
            ReturnToPool();
        }

        private void OnHitEnemy()
        {
            if (sourceEnemy != null) sourceEnemy.TakeDamage();
            ReturnToPool();
        }

        private void ApplyLaneColor(int lane)
        {
            Color laneColor = GetLaneColor(lane);
            
            // 모든 자식 MeshRenderer 색상 적용
            var renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in renderers)
            {
                SetMaterialColor(mr.material, laneColor);
            }
        }

        private void SetMaterialColor(Material mat, Color targetColor)
        {
            // URP (_BaseColor) 및 Standard (_Color) 모두 대응
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetColor);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", targetColor);
            
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", targetColor * 2f);
        }

        private Color GetLaneColor(int lane) => lane switch { 
            0 => Color.cyan, 
            1 => Color.green, 
            2 => Color.yellow, 
            3 => Color.red, 
            _ => Color.white 
        };
    }
}
