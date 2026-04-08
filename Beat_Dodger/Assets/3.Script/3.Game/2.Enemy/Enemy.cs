using UnityEngine;
using System.Collections;

namespace BeatDodger.Game
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float dieAnimDuration = 1.0f;
        [SerializeField] private GameObject deathVfxPrefab;
        [SerializeField] private int laneIndex;
        
        [Header("Snapping")]
        [SerializeField] private float groundOffset = 0.02f; // Helps prevent being half-buried
        
        private static readonly int IdleHash = Animator.StringToHash("Idle");

        private RhythmManager manager;
        private bool isFiring;
        private bool isDead;
        private bool _canFireExternally; 

        public int LaneIndex => laneIndex;
        public int SessionID { get; private set; } 
        public bool IsFiring => isFiring; // Simplified to allow immediate firing after spawn
        public bool IsDead => isDead;
        public int OriginPoolId { get; private set; } 

        public void PlayFireAnimation()
        {
            if (animator != null) animator.SetTrigger("Fire");
        }

        public void TakeDamage()
        {
            if (isDead) return;
            isDead = true;
            _canFireExternally = false;
            
            if (animator != null) animator.SetTrigger("Die");
            if (deathVfxPrefab != null) Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);

            StartCoroutine(DieProcess());
        }

        private IEnumerator DieProcess()
        {
            yield return new WaitForSeconds(dieAnimDuration);
            if (manager != null) manager.OnEnemyDied(this, laneIndex);
        }

        public void Init(int index, RhythmManager manager, Vector3 spawnPos, int poolId)
        {
            this.laneIndex = index;
            this.manager = manager;
            this.OriginPoolId = poolId;
            this.isDead = false;
            this.isFiring = false; 
            this._canFireExternally = false;

            // Position and Rotate BEFORE animator setup
            transform.position = spawnPos + Vector3.up * groundOffset;
            
            // 판정 지점을 바라보도록 회전 (Y축만 고려하여 기울어짐 방지)
            if (manager != null)
            {
                Vector3 targetPos = manager.GetJudgePosition(laneIndex);
                targetPos.y = transform.position.y;
                transform.LookAt(targetPos);
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }

            gameObject.SetActive(true);
            
            StopAllCoroutines(); 

            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            
            if (animator != null)
            {
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // Ensure it starts immediately
                animator.applyRootMotion = false;
                
                animator.Rebind();
                animator.Update(0f); // Force state machine to reset to entry
                
                animator.Play(IdleHash, 0, 0f); // Use Hash for performance
                animator.Update(0f); // Apply first frame of Idle
                
                animator.ResetTrigger("Fire");
                animator.ResetTrigger("Die");
            }

            SessionID++; 
            StartCoroutine(GracePeriod());
        }

        private IEnumerator GracePeriod()
        {
            yield return new WaitForSeconds(0.1f);
            // Re-enabling root motion removed to prevent rotation drift in rhythm game
            _canFireExternally = true;
        }

        public void SetFiring(bool state)
        {
            isFiring = state;
        }

        public void ResetEnemy()
        {
            isDead = false;
            gameObject.SetActive(true);
        }
    }
}
