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
        private float arrivalTime; 
        private float travelDuration;
        private float targetNoteTime; 

        public float TargetNoteTime => targetNoteTime;

        [Header("Approach Circle")]
        [SerializeField] private GameObject approachCircle;
        [SerializeField] private float startScale = 1.5f;
        [SerializeField] private float endScale = 1.0f;
        [SerializeField] private Vector3 approachCircleRotationOffset = new Vector3(90f, 0f, 0f);
        [Range(0.01f, 1.0f)]
        [SerializeField] private float showThreshold = 0.3f; 
        [SerializeField] private bool useFadeIn = true;

        [Header("Physics Optimization")]
        [SerializeField] private float colliderEnableThreshold = 0.85f; // 판정 부근에서만 활성화 (성능 최적화)
        private Collider _projectileCollider;
        private Rigidbody _rigidbody;

        private SpriteRenderer _circleRenderer;
        private bool isReflected;
        private bool isActive;

        public bool IsReflected => isReflected;
        public int LaneIndex => laneIndex;

        public void Setup(IObjectPool<Projectile> pool, RhythmManager manager)
        {
            this.pool = pool;
            this.manager = manager;
            
            // 물리 최적화 초기 세팅
            if (TryGetComponent(out _projectileCollider)) _projectileCollider.enabled = false;
            if (TryGetComponent(out _rigidbody))
            {
                _rigidbody.isKinematic = true; // 무리한 물리 연산 방지
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }
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
            
            if (_projectileCollider != null) _projectileCollider.enabled = false; // 시작 시 비활성화

            CalculateControlPoint(customArc);
            
            if (approachCircle != null)
            {
                if (_circleRenderer == null) _circleRenderer = approachCircle.GetComponent<SpriteRenderer>();
                
                approachCircle.transform.position = targetPos;
                approachCircle.transform.rotation = Quaternion.Euler(approachCircleRotationOffset);
                approachCircle.transform.localScale = Vector3.one * startScale;
                
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

                // 물리 최적화: 판정 구간에서만 콜라이더 활성화
                if (_projectileCollider != null && !_projectileCollider.enabled && normalizedTime >= colliderEnableThreshold)
                {
                    _projectileCollider.enabled = true;
                }

                if (normalizedTime > 1.1f) OnMiss();
            }
            else
            {
                if (approachCircle != null && approachCircle.activeSelf) approachCircle.SetActive(false);
                if (_projectileCollider != null && !_projectileCollider.enabled) _projectileCollider.enabled = true;

                float returnTime = travelDuration / manager.ReflectionSpeedMultiplier;
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
            arrivalTime = Time.time; 
            if (_projectileCollider != null) _projectileCollider.enabled = true;
        }

        public void ReturnToPool()
        {
            if (!isActive) return;
            isActive = false;

            if (approachCircle != null) approachCircle.SetActive(false);
            if (_projectileCollider != null) _projectileCollider.enabled = false;

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
            var renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in renderers)
            {
                SetMaterialColor(mr.material, laneColor);
            }
        }

        private void SetMaterialColor(Material mat, Color targetColor)
        {
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
