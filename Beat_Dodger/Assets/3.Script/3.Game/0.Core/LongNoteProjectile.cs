using UnityEngine;
using UnityEngine.Pool;

namespace BeatDodger.Game
{
    public class LongNoteProjectile : MonoBehaviour
    {
        private TubeMeshGenerator _tubeGenerator;
        private MeshRenderer _renderer;
        
        [Header("Settings")]
        [SerializeField] private float _radius = 0.4f;
        [SerializeField] private int _pathResolution = 20;
        [SerializeField] private AnimationCurve _taperingCurve = AnimationCurve.Linear(0, 1, 1, 1); 

        [Header("Visuals")]
        [SerializeField] private GameObject _headObject;
        [SerializeField] private GameObject _tailObject;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _activeColor = Color.cyan;

        [Header("Approach Circle (Osu Style)")]
        [SerializeField] private GameObject approachCircle;
        [SerializeField] private float startScale = 1.5f;
        [SerializeField] private float endScale = 1.0f;
        [SerializeField] private Vector3 approachCircleRotationOffset = new Vector3(90f, 0f, 0f);
        [Range(0.01f, 1.0f)]
        [SerializeField] private float showThreshold = 0.3f;
        [SerializeField] private bool useFadeIn = true;
        
        private SpriteRenderer _circleRenderer;
        private float _noteStartTime;
        private float _duration;
        private float _travelDuration;
        private float _spawnTime;
        private int _laneIndex;
        private RhythmManager _manager;
        private IObjectPool<LongNoteProjectile> _pool;

        private Vector3 _startPos;
        private Vector3 _targetPos;
        private Vector3 _controlPoint;

        private bool _isHolding = false;
        private bool _isSatisfied = false; 
        private bool _isActive = false;
        private float _totalHeldTime = 0f;
        private JudgmentType _startJudgment;
        
        // Reflection fields
        private Enemy _sourceEnemy;
        private bool _isReflected = false;
        private float _reflectionStartTime;

        public float StartTime => _noteStartTime;
        public float EndTime => _noteStartTime + _duration;
        public int LaneIndex => _laneIndex;
        public bool IsHolding => _isHolding;
        public JudgmentType StartJudgment => _startJudgment;

        public void Initialize(Enemy source, Vector3 start, Vector3 target, float arc, float swerve, float startTime, float duration, float travelDur, int lane, RhythmManager manager, IObjectPool<LongNoteProjectile> pool)
        {
            _sourceEnemy = source;
            _pool = pool;
            _startPos = start;
            _targetPos = target;
            _noteStartTime = startTime;
            _duration = duration;
            _travelDuration = travelDur;
            _laneIndex = lane;
            _manager = manager;

            // 레인에 따른 색상 설정
            _normalColor = GetLaneColor(lane);
            _activeColor = _normalColor; // 일단 동일하게 설정 (필요 시 더 밝게 조정 가능)
            
            _spawnTime = Time.time;
            _totalHeldTime = 0f; 
            _isSatisfied = false; 
            _isActive = true;
            _isHolding = false;
            _isReflected = false;
            _startJudgment = JudgmentType.None;

            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            Vector3 midPoint = (_startPos + _targetPos) / 2f;
            Vector3 direction = (_targetPos - _startPos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            _controlPoint = midPoint + (Vector3.up * arc) + (right * swerve);

            if (_tubeGenerator == null) _tubeGenerator = GetComponent<TubeMeshGenerator>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            
            if (_tubeGenerator != null) _tubeGenerator.enabled = true;
            if (_renderer != null) _renderer.enabled = true;
            if (gameObject.activeSelf == false) gameObject.SetActive(true);

            if (approachCircle != null)
            {
                if (_circleRenderer == null) _circleRenderer = approachCircle.GetComponent<SpriteRenderer>();

                approachCircle.transform.position = _targetPos;
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

            UpdateVisualState(false);
            ApplyColorToObjects();

            if (_headObject != null) _headObject.SetActive(false);
            if (_tailObject != null) _tailObject.SetActive(true); 
            
            Update(); 
        }

        public void OnHit(JudgmentType judgment)
        {
            _startJudgment = judgment;
            _isHolding = true;
            UpdateVisualState(true);
            
            if (approachCircle != null) approachCircle.SetActive(false);
        }

        public void SetHolding(bool holding)
        {
            if (_isHolding != holding)
            {
                _isHolding = holding;
                UpdateVisualState(_isHolding);
            }
        }

        private void UpdateVisualState(bool active)
        {
            if (_renderer != null)
            {
                Color targetColor = active ? _activeColor : _normalColor;
                SetMaterialColor(_renderer.material, targetColor, active);
            }
        }

        private void SetMaterialColor(Material mat, Color targetColor, bool active)
        {
            // URP (_BaseColor) 및 Standard (_Color) 모두 대응
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetColor);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", targetColor);
            
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", active ? targetColor * 2f : Color.black);
        }

        private void Update()
        {
            if (!_isActive) return;

            if (!_isReflected)
            {
                HandleFlowState();
            }
            else
            {
                HandleReflectionState();
            }
        }

        private void HandleFlowState()
        {
            float elapsed = Time.time - _spawnTime;
            float headT = elapsed / _travelDuration;
            float tailT = (elapsed - _duration) / _travelDuration;

            float clampedHeadT = Mathf.Clamp01(headT);
            float clampedTailT = Mathf.Clamp01(tailT);

            Vector3 worldHeadPos = CalculateBezierPoint(clampedHeadT, _startPos, _controlPoint, _targetPos);
            transform.position = worldHeadPos;

            GenerateCurvedMesh(clampedTailT, clampedHeadT);

            HandleApproachCircle(clampedHeadT);

            if (_headObject != null)
            {
                _headObject.SetActive(headT > 0 && headT < 1.05f);
                _headObject.transform.position = worldHeadPos;
            }
            if (_tailObject != null)
            {
                Vector3 worldTailPos = CalculateBezierPoint(clampedTailT, _startPos, _controlPoint, _targetPos);
                _tailObject.SetActive(tailT > -0.1f && tailT < 1.05f);
                _tailObject.transform.position = worldTailPos;
            }

            if (_isHolding)
            {
                _totalHeldTime += Time.deltaTime;
                float progress = (Time.time - _noteStartTime) / _duration;
                if (progress >= 0.75f) _isSatisfied = true;
            }

            if (tailT > 1.05f) OnFinish();
        }

        private void HandleApproachCircle(float normalizedTime)
        {
            if (approachCircle == null || _isHolding) return;

            float showStartTime = 1.0f - showThreshold;

            if (normalizedTime >= showStartTime && normalizedTime <= 1.05f)
            {
                if (!approachCircle.activeSelf) approachCircle.SetActive(true);
                
                approachCircle.transform.position = _targetPos;
                approachCircle.transform.rotation = Quaternion.Euler(approachCircleRotationOffset);
                
                float currentScale = Mathf.Lerp(startScale, endScale, normalizedTime);
                approachCircle.transform.localScale = Vector3.one * currentScale;

                if (useFadeIn && _circleRenderer != null)
                {
                    float alphaT = (normalizedTime - showStartTime) / showThreshold;
                    Color c = _normalColor;
                    c.a = Mathf.Clamp01(alphaT);
                    _circleRenderer.color = c;
                }
            }
            else
            {
                if (approachCircle.activeSelf) approachCircle.SetActive(false);
            }
        }

        private void HandleReflectionState()
        {
            if (approachCircle != null) approachCircle.SetActive(false);

            float returnTime = _travelDuration / _manager.ReflectionSpeedMultiplier;
            float reflectionT = (Time.time - _reflectionStartTime) / returnTime;
            
            Vector3 currentEnemyPos = _startPos;
            if (_sourceEnemy != null) currentEnemyPos = _sourceEnemy.transform.position;

            if (_tailObject != null)
            {
                _tailObject.transform.position = Vector3.Lerp(_targetPos, currentEnemyPos, reflectionT);
                _tailObject.transform.Rotate(Vector3.up * 720f * Time.deltaTime);
            }

            if (reflectionT >= 1.0f)
            {
                if (_sourceEnemy != null) _sourceEnemy.TakeDamage();
                ReturnToPool();
            }
        }

        private void OnFinish()
        {
            if (approachCircle != null) approachCircle.SetActive(false);

            float ratio = Mathf.Clamp01(_totalHeldTime / _duration);
            JudgmentType finalJudgment = JudgmentType.Miss;

            if (ratio >= 0.99f) finalJudgment = JudgmentType.Perfect;
            else if (ratio >= 0.85f) finalJudgment = JudgmentType.Excellent;
            else if (ratio >= 0.75f) finalJudgment = JudgmentType.Good;

            _manager.ResolveLongNoteEnd(finalJudgment, _laneIndex, this);

            if (finalJudgment != JudgmentType.Miss && finalJudgment != JudgmentType.None)
            {
                _isReflected = true;
                _reflectionStartTime = Time.time;
                _isHolding = false;
                
                if (_renderer != null) _renderer.enabled = false;
                if (_tubeGenerator != null) _tubeGenerator.enabled = false;
                
                if (_headObject != null) _headObject.SetActive(false);
                if (_tailObject != null) _tailObject.SetActive(true); 
            }
            else
            {
                ReturnToPool();
            }
        }

        private void GenerateCurvedMesh(float startT, float endT)
        {
            if (endT <= startT) return;

            Vector3[] pathSteps = new Vector3[_pathResolution];
            for (int i = 0; i < _pathResolution; i++)
            {
                float t = Mathf.Lerp(startT, endT, (float)i / (_pathResolution - 1));
                pathSteps[i] = CalculateBezierPoint(t, _startPos, _controlPoint, _targetPos);
            }

            _tubeGenerator.Generate(pathSteps, _radius, _taperingCurve);
        }

        private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            Vector3 p = uu * p0 + 2 * u * t * p1 + tt * p2;
            return p;
        }

        public void ReturnToPool()
        {
            _isActive = false;
            if (approachCircle != null) approachCircle.SetActive(false);
            if (_pool != null) _pool.Release(this);
            else gameObject.SetActive(false);
        }

        private void ApplyColorToObjects()
        {
            // Head/Tail 오브젝트 및 모든 자식 MeshRenderer 색상 적용
            foreach (var obj in new GameObject[] { _headObject, _tailObject })
            {
                if (obj == null) continue;
                var renderers = obj.GetComponentsInChildren<MeshRenderer>();
                foreach (var mr in renderers)
                {
                    SetMaterialColor(mr.material, _normalColor, false);
                }
            }
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
