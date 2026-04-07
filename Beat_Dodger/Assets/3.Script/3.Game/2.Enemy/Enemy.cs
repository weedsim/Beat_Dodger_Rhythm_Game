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

        private RhythmManager manager;
        private bool isFiring;
        private bool isDead;
        private bool _canFireExternally; 

        public int LaneIndex => laneIndex;
        public int SessionID { get; private set; } 
        public bool IsFiring => isFiring || !_canFireExternally; // Critical: If true, Manager won't assign a note to this enemy
        public bool IsDead => isDead;
        public int OriginPoolId { get; private set; } 

        public void PlayFireAnimation()
        {
            if (animator != null && _canFireExternally) animator.SetTrigger("Fire");
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
            this._canFireExternally = false; // Prevents selection as shooter during spawn

            if (animator == null) TryGetComponent(out animator);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            
            if (animator != null)
            {
                animator.applyRootMotion = false; // Disable during positioning
                animator.Rebind();
                animator.ResetTrigger("Fire");
                animator.ResetTrigger("Die");
                animator.Play("Idle", 0, 0f);
                animator.Update(0f);
            }

            SessionID++; 
            // Position with a small vertical offset to prevent burying
            transform.position = spawnPos + Vector3.up * groundOffset;
            
            StopAllCoroutines(); 
            gameObject.SetActive(true);

            // Longer Grace Period for stability
            StartCoroutine(GracePeriod());
        }

        private IEnumerator GracePeriod()
        {
            yield return new WaitForSeconds(0.1f);
            if (animator != null) animator.applyRootMotion = true; // Re-enable if needed
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
