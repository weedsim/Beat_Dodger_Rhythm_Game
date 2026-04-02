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
        [SerializeField] private float sideSwerveAmount = 2.0f;
        [SerializeField] private Vector3 rotationSpeed = new Vector3(360f, 360f, 0);

        public float MinArcHeight => minArcHeight;
        public float MaxArcHeight => maxArcHeight;
        public float SideSwerveAmount => sideSwerveAmount;

        private Vector3 controlPoint; // For Bezier calculation
        private Vector3 startPos;
        private Vector3 targetPos;
        private float arrivalTime;
        private float travelDuration;
        private float spawnTime;

        private bool isReflected;
        private bool isActive;

        public bool IsReflected => isReflected;
        public float ArrivalTime => arrivalTime;
        public int LaneIndex => laneIndex;

        public void Setup(IObjectPool<Projectile> pool, RhythmManager manager)
        {
            this.pool = pool;
            this.manager = manager;
        }

        public void Initialize(Enemy source, Vector3 start, Vector3 target, float duration, int lane, float customArc = -1f, float customSwerve = -999f)
        {
            sourceEnemy = source;
            startPos = start;
            targetPos = target;
            travelDuration = duration;
            laneIndex = lane;
            
            spawnTime = Time.time;
            arrivalTime = spawnTime + duration;
            
            isReflected = false;
            isActive = true;
            
            // Generate a unique random control point for this flight path
            CalculateControlPoint(customArc, customSwerve);
            
            transform.position = startPos;
            gameObject.SetActive(true);
        }

        private void CalculateControlPoint(float customArc, float customSwerve)
        {
            Vector3 midPoint = (startPos + targetPos) / 2f;
            
            float height = (customArc >= 0) ? customArc : Random.Range(minArcHeight, maxArcHeight);
            float swerve = (customSwerve != -999f) ? customSwerve : Random.Range(-sideSwerveAmount, sideSwerveAmount);
            
            // Create a randomized curve point
            Vector3 direction = (targetPos - startPos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            
            controlPoint = midPoint + (Vector3.up * height) + (right * swerve);
        }

        private void Update()
        {
            if (!isActive) return;

            float currentTime = Time.time;
            float normalizedTime = (currentTime - spawnTime) / travelDuration;
            
            // 1. Apply Slingshot/Ease Curve for speed juice
            float easedT = speedCurve.Evaluate(normalizedTime);

            if (!isReflected)
            {
                // 2. Quadratic Bezier: (1-t)^2*P0 + 2(1-t)t*P1 + t^2*P2
                transform.position = CalculateBezierPoint(easedT, startPos, controlPoint, targetPos);
                
                transform.Rotate(rotationSpeed * Time.deltaTime);

                if (normalizedTime > 1.1f) OnMiss();
            }
            else
            {
                // Reflection: Returns simpler/faster for better feedback (or can be customized)
                float reflectionT = (currentTime - arrivalTime) / (travelDuration * 0.7f); // Returns slightly faster
                transform.position = Vector3.Lerp(targetPos, startPos, reflectionT);
                
                transform.Rotate(rotationSpeed * 2f * Time.deltaTime); // Spins faster when reflected

                if (reflectionT >= 1.0f) OnHitEnemy();
            }
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

        public void Reflect()
        {
            if (isReflected || !isActive) return;
            
            isReflected = true;
            arrivalTime = Time.time; 
        }

        public void ReturnToPool()
        {
            if (!isActive) return;
            isActive = false;

            // Unlock the enemy so they can fire again if they didn't die
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
            if (sourceEnemy != null)
            {
                sourceEnemy.TakeDamage();
            }
            ReturnToPool();
        }
    }
}
