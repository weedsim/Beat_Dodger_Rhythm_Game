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
        private bool _isActive = false;

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
            _isActive = true;

            // Projectile과 동일한 곡선 제어점 계산
            Vector3 midPoint = (_startPos + _targetPos) / 2f;
            Vector3 direction = (_targetPos - _startPos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            _controlPoint = midPoint + (Vector3.up * arc) + (right * swerve);

            if (_tubeGenerator == null) _tubeGenerator = GetComponent<TubeMeshGenerator>();
            Update(); // Initial position/mesh
        }

        public void SetHolding(bool holding)
        {
            _isHolding = holding;
        }

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

            // 꼬리까지 판정선을 지나가면 소멸
            if (tailT > 1.05f)
            {
                OnMiss();
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

        private void OnMiss()
        {
            // 끝까지 성공적으로 누르고 있지 않았다면 미스로 처리
            if (!_isHolding) _manager.NoteMissed(_laneIndex);
            
            // 시각적 소멸 및 리스트에서 제거
            _manager.RemoveLongNote(this, _laneIndex);
        }

        public void ReturnToPool()
        {
            _isActive = false;
            gameObject.SetActive(false);
        }
    }
}
