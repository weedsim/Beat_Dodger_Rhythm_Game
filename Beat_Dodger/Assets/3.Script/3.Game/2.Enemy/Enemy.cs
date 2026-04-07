using UnityEngine;

namespace BeatDodger.Game
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float dieAnimDuration = 1.0f;
        [SerializeField] private GameObject deathVfxPrefab;
        [SerializeField] private int laneIndex;

        private RhythmManager manager;
        private bool isFiring;
        private bool isDead;

        public int LaneIndex => laneIndex;
        public int SessionID { get; private set; } // Unique ID for each 'life'
        public bool IsFiring => isFiring;
        public bool IsDead => isDead;
        public int OriginPoolId { get; private set; } // 자기가 속한 풀의 인덱스

        public void PlayFireAnimation()
        {
            if (animator != null) animator.SetTrigger("Fire");
        }

        public void TakeDamage()
        {
            if (isDead) return;
            isDead = true;
            
            if (animator != null) animator.SetTrigger("Die");
            if (deathVfxPrefab != null) Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);

            StartCoroutine(DieProcess());
        }

        private System.Collections.IEnumerator DieProcess()
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

            if (animator == null) TryGetComponent(out animator);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            
            if (animator != null)
            {
                animator.ResetTrigger("Fire");
                animator.ResetTrigger("Die");
                animator.Play("Idle", 0, 0f);
            }

            SessionID++; 
            transform.position = spawnPos;
            
            StopAllCoroutines(); 
            gameObject.SetActive(true);
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
