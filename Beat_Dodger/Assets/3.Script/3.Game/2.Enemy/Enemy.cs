using UnityEngine;

namespace BeatDodger.Game
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField] private GameObject deathVfxPrefab;
        [SerializeField] private int laneIndex;

        private RhythmManager manager;
        private bool isFiring;
        private bool isDead;

        public int LaneIndex => laneIndex;
        public int SessionID { get; private set; } // Unique ID for each 'life'
        public bool IsFiring => isFiring;
        public bool IsDead => isDead;

        public void TakeDamage()
        {
            if (isDead) return;
            isDead = true;
            
            if (deathVfxPrefab != null)
            {
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            }

            // Inform manager to handle pooling and spawning new one
            if (manager != null)
            {
                manager.OnEnemyDied(this, laneIndex);
            }
        }

        public void Init(int index, RhythmManager manager)
        {
            this.laneIndex = index;
            this.manager = manager;
            this.isDead = false;
            this.isFiring = false; // Reset firing state on (re)spawn
            
            SessionID++; 
            
            // Fix Y height to 1.0 during spawning
            Vector3 pos = transform.position;
            pos.y = 1.0f;
            transform.position = pos;
            
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
