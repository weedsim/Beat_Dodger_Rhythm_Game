using UnityEngine;

namespace BeatDodger.Game
{
    /// <summary>
    /// Handles procedural tube mesh and holding logic for long notes.
    /// Manages the visual length and holding state within the rhythm game.
    /// </summary>
    /// <summary>
    /// Handles procedural tube mesh and curved movement for long notes.
    /// Follows a Quadratic Bezier path and updates its mesh to flow along the curve.
    /// </summary>
    public class LongNoteProjectile : MonoBehaviour
    {
        private TubeMeshGenerator _tubeGenerator;
        [SerializeField] private float _radius = 0.4f;
        [SerializeField] private int _pathResolution = 20; // Number of segments in the curve
        
        private float _noteStartTime;
        private float _duration;
        private float _travelDuration;
        private float _spawnTime;
        private int _laneIndex;
        private RhythmManager _manager;

        private Vector3 _startPos;
        private Vector3 _targetPos;
        private Vector3 _controlPoint;

        private bool _isHolding = false;
        private bool _isSatisfied = false; 
        private bool _isActive = false;
        private float _totalHeldTime = 0f; // 추가: 실제 누적 홀딩 시간
        private JudgmentType _startJudgment; // 추가: 시작 판정 기억

        public float StartTime => _noteStartTime;
        public float EndTime => _noteStartTime + _duration;
        public int LaneIndex => _laneIndex;
        public bool IsHolding => _isHolding;

        public void Initialize(Vector3 start, Vector3 target, float arc, float swerve, float startTime, float duration, float travelDur, int lane, RhythmManager manager)
        {
            _startPos = start;
            _targetPos = target;
            _noteStartTime = startTime;
            _duration = duration;
            _travelDuration = travelDur;
            _laneIndex = lane;
            _manager = manager;
            
            _spawnTime = Time.time;
            _totalHeldTime = 0f; 
            _isSatisfied = false; 
            _isActive = true;
            _startJudgment = JudgmentType.None; // 초기화

            // Projectile과 동일한 곡선 제어점 계산
            Vector3 midPoint = (_startPos + _targetPos) / 2f;
            Vector3 direction = (_targetPos - _startPos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            _controlPoint = midPoint + (Vector3.up * arc) + (right * swerve);

            if (_tubeGenerator == null) _tubeGenerator = GetComponent<TubeMeshGenerator>();
            Update(); // Initial position/mesh
        }

        public void OnHit(JudgmentType judgment)
        {
            _startJudgment = judgment;
            _isHolding = true;
        }

        public void SetHolding(bool holding)
        {
            _isHolding = holding;
        }

        public JudgmentType StartJudgment => _startJudgment;

        private void Update()
        {
            if (!_isActive) return;

            float elapsed = Time.time - _spawnTime;
            
            // 롱노트의 머리(Head)와 꼬리(Tail)의 진행도(0~1) 계산
            float headT = elapsed / _travelDuration;
            float tailT = (elapsed - _duration) / _travelDuration;

            // 화면에 보이는 구간의 포인트를 추출하여 메시 생성
            GenerateCurvedMesh(Mathf.Clamp01(tailT), Mathf.Clamp01(headT));

            // 오브젝트의 위치는 머리에 맞춤 (파티클 등 연출용)
            transform.position = CalculateBezierPoint(Mathf.Clamp01(headT), _startPos, _controlPoint, _targetPos);

            // [추가] 75% 이상 눌렀는지 체크 및 홀딩 시간 누적
            if (_isHolding)
            {
                _totalHeldTime += Time.deltaTime;
                float progress = (Time.time - _noteStartTime) / _duration;
                if (progress >= 0.75f) _isSatisfied = true;
            }

            // 꼬리까지 판정선을 지나가면 소멸
            if (tailT > 1.05f)
            {
                OnFinish(); // OnMiss 대신 OnFinish 호출
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

            _tubeGenerator.Generate(pathSteps, _radius);
        }

        private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            Vector3 p = uu * p0;
            p += 2 * u * t * p1;
            p += tt * p2;
            return p;
        }

        private void OnFinish() // OnMiss에서 OnFinish로 변경 및 로직 강화
        {
            // 최종 홀딩 비율 계산 (0.0 ~ 1.0)
            float ratio = Mathf.Clamp01(_totalHeldTime / _duration);
            
            JudgmentType finalJudgment = JudgmentType.Miss;

            // 100% Perfect, 85% Excellent, 75% Good 판정
            if (ratio >= 0.99f) finalJudgment = JudgmentType.Perfect;
            else if (ratio >= 0.85f) finalJudgment = JudgmentType.Excellent;
            else if (ratio >= 0.75f) finalJudgment = JudgmentType.Good;

            // 매니저를 통해 정산
            _manager.ResolveLongNoteEnd(finalJudgment, _laneIndex, this);
        }

        public void ReturnToPool()
        {
            _isActive = false;
            gameObject.SetActive(false);
        }
    }
}
