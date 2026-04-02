using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace BeatDodger.Game
{
    public enum JudgmentType { Perfect, Excellent, Good, Bad, Miss, None }

    public class RhythmManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private Transform[] judgePoints;
        [SerializeField] private BoxCollider[] enemySpawnZones; // 4 zones: Lane 1 to 4
        [SerializeField] private GuitarZone[] guitarZones;
        [SerializeField] private GameObject reflectVfxPrefab;
        [SerializeField] private ComboUI comboUI;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Pool Parents")]
        [SerializeField] private Transform enemyPoolParent;
        [SerializeField] private Transform projectilePoolParent;

        [Header("Note Mapping")]
        [SerializeField] private BeatDodger.Core.NoteMapData mapData;
        [SerializeField] private AudioSource musicSource;
        
        [Header("Settings")]
        [SerializeField] private float bpm = 128f;
        [SerializeField] private float travelDuration = 1.25f;
        [SerializeField] private float gameIntroDelay = 3.0f; // New: Delay before music starts
        [SerializeField] private int enemiesPerLane = 5; // New army size parameter
        [SerializeField] private float launchZ = 25f;    // New: Unified starting line for projectiless
        
        [Header("Judgment Windows (Seconds)")]
        [SerializeField] private float perfectWindow = 0.033f;   // ±33ms
        [SerializeField] private float excellentWindow = 0.075f; // ±75ms
        [SerializeField] private float goodWindow = 0.095f;      // ±95ms
        [SerializeField] private float badWindowLimit = 0.200f;  // ±200ms
        
        [Header("Score & Combo")]
        [SerializeField] private int baseNoteScore = 100;
        [SerializeField] private long currentScore = 0;
        [SerializeField] private ScoreUI scoreUI;
        
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private int currentHealth;
        [SerializeField] private HealthUI healthUI;
        
        [Header("Status")]
        [SerializeField] private int currentCombo = 0;
        [SerializeField] private int maxCombo = 0;

        private int currentNoteIndex = 0;
        private bool isGameStarted = false;
        private bool isMusicPlayed = false;
        private float gameTimer = 0f;

        [Header("Keys (Legacy Input for OnGUI Rebind)")]
        [SerializeField] private KeyCode[] laneKeys = { KeyCode.S, KeyCode.D, KeyCode.K, KeyCode.L };
        private int bindingLaneIndex = -1; // -1 means not binding

        private IObjectPool<Projectile> projectilePool;
        private IObjectPool<Enemy> enemyPool;
        private List<Enemy>[] activeEnemies; // List of active enemies per lane
        private List<Projectile>[] activeProjectiles;
        private InputAction[] keyActions;

        private void Awake()
        {
            Debug.Log("<color=green>[RhythmManager]</color> Awake Starting...");
            
            // Create pool parents if not assigned to keep hierarchy clean
            if (enemyPoolParent == null) enemyPoolParent = new GameObject("EnemyPool").transform;
            if (projectilePoolParent == null) projectilePoolParent = new GameObject("ProjectilePool").transform;

            currentHealth = maxHealth;
            if (healthUI != null) healthUI.Init(maxHealth);
            
            InitializePool();
            InitializeInput();
            InitializeLanes();
        }

        private void InitializePool()
        {
            if (projectilePrefab == null || enemyPrefab == null)
            {
                Debug.LogError("<color=red>[RhythmManager]</color> Prefabs are missing in Inspector!");
                return;
            }
            
            projectilePool = new ObjectPool<Projectile>(
                createFunc: () => {
                    var p = Instantiate(projectilePrefab, projectilePoolParent);
                    p.Setup(projectilePool, this);
                    return p;
                },
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => Destroy(p.gameObject),
                defaultCapacity: 32
            );

            enemyPool = new ObjectPool<Enemy>(
                createFunc: () => Instantiate(enemyPrefab, enemyPoolParent),
                actionOnGet: e => e.gameObject.SetActive(true),
                actionOnRelease: e => e.gameObject.SetActive(false),
                actionOnDestroy: e => Destroy(e.gameObject),
                defaultCapacity: 4
            );
        }

        private void InitializeInput()
        {
            // We'll use polling in Update with laneKeys for easy rebinding via OnGUI
            Debug.Log("<color=cyan>[RhythmManager]</color> Input initialized with Rebindable Keys: " + string.Join(", ", laneKeys));
        }

        private void InitializeLanes()
        {
            activeProjectiles = new List<Projectile>[4];
            activeEnemies = new List<Enemy>[4];
            for (int i = 0; i < 4; i++)
            {
                activeProjectiles[i] = new List<Projectile>();
                activeEnemies[i] = new List<Enemy>();
                
                // Spawn the initial army in each lane
                for (int j = 0; j < enemiesPerLane; j++)
                {
                    SpawnEnemyInLane(i);
                }
            }
        }

        private void SpawnEnemyInLane(int laneIndex)
        {
            if (enemySpawnZones == null || enemySpawnZones.Length <= laneIndex || enemySpawnZones[laneIndex] == null) return;

            var enemy = enemyPool.Get();
            enemy.Init(laneIndex, this);
            enemy.transform.position = GetRandomPointInBox(enemySpawnZones[laneIndex]);
            activeEnemies[laneIndex].Add(enemy);
        }

        private Vector3 GetRandomPointInBox(BoxCollider box)
        {
            Vector3 center = box.center + box.transform.position;
            Vector3 size = box.size;
            
            return new Vector3(
                Random.Range(center.x - size.x / 2f, center.x + size.x / 2f),
                1.0f, // Y is fixed at 1.0f as requested
                Random.Range(center.z - size.z / 2f, center.z + size.z / 2f)
            );
        }

        private void OnGUI()
        {
            if (isGameStarted) return;

            GUI.backgroundColor = Color.black;
            GUI.Box(new Rect(10, 10, 250, 200), "GAME SETUP");

            if (GUI.Button(new Rect(20, 40, 230, 40), "START GAME"))
            {
                StartGame();
            }

            for (int i = 0; i < 4; i++)
            {
                string btnText = bindingLaneIndex == i ? "Press any key..." : $"Lane {i + 1}: {laneKeys[i]}";
                if (GUI.Button(new Rect(20, 90 + (i * 25), 230, 22), btnText))
                {
                    bindingLaneIndex = i;
                }
            }

            // Key Bind Logic
            if (bindingLaneIndex != -1 && Event.current.isKey && Event.current.type == EventType.KeyDown)
            {
                laneKeys[bindingLaneIndex] = Event.current.keyCode;
                bindingLaneIndex = -1;
            }
        }

        private void StartGame()
        {
            if (musicSource != null && mapData != null)
            {
                musicSource.clip = mapData.music;
                musicSource.playOnAwake = false;
                musicSource.Stop();
                
                gameTimer = -gameIntroDelay;
                isGameStarted = true;
            }
        }

        public void OnEnemyDied(Enemy enemy, int laneIndex)
        {
            if (activeEnemies[laneIndex].Contains(enemy))
            {
                activeEnemies[laneIndex].Remove(enemy);
                enemyPool.Release(enemy);
                
                // Keep army count constant by spawning a replacement
                SpawnEnemyInLane(laneIndex);
            }
        }

        private void OnEnable()
        {
        }
 
        private void OnDisable()
        {
        }

        private void Update()
        {
            if (!isGameStarted || mapData == null) return;

            gameTimer += Time.deltaTime;

            // Handle Key Input (Polling)
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown(laneKeys[i]))
                {
                    OnKeyPress(i);
                }
            }

            // Trigger music at the exact center (0s)
            if (!isMusicPlayed && gameTimer >= 0)
            {
                musicSource.Play();
                isMusicPlayed = true;
            }

            // Sync with music if playing to prevent drift
            float syncTime = isMusicPlayed ? musicSource.time : gameTimer;

            // Shared trajectory values for consistent simultaneous notes (Chords)
            float sharedArc = Random.Range(projectilePrefab.MinArcHeight, projectilePrefab.MaxArcHeight);
            float sharedSwerve = Random.Range(-projectilePrefab.SideSwerveAmount, projectilePrefab.SideSwerveAmount);

            while (currentNoteIndex < mapData.notes.Count && mapData.notes[currentNoteIndex].time - travelDuration <= syncTime)
            {
                var note = mapData.notes[currentNoteIndex];
                Spawn(note.lane, note.lane, sharedArc, sharedSwerve);
                currentNoteIndex++;
            }
        }

        public void Spawn(int sourceLaneIndex, int targetLaneIndex, float arc = -1f, float swerve = -999f)
        {
            // Find an idle enemy in the source lane
            Enemy shooter = activeEnemies[sourceLaneIndex].Find(e => !e.IsFiring);
            
            // If all enemies are busy, spawn a temporary extra combatant
            if (shooter == null)
            {
                SpawnEnemyInLane(sourceLaneIndex);
                shooter = activeEnemies[sourceLaneIndex].Find(e => !e.IsFiring);
            }

            if (shooter == null) return;

            shooter.SetFiring(true); // Lock the enemy for this note duration

            // Force the projectile to start from a unified "Muzzle Line" for visual clarity
            Vector3 unifiedStartPos = shooter.transform.position;
            unifiedStartPos.z = launchZ;

            var p = projectilePool.Get();
            p.Initialize(
                shooter, 
                unifiedStartPos, 
                judgePoints[targetLaneIndex].position, 
                travelDuration, 
                targetLaneIndex,
                arc,
                swerve
            );
            
            activeProjectiles[targetLaneIndex].Add(p);
        }

        private void OnKeyPress(int lane)
        {
            var laneList = activeProjectiles[lane];
            if (laneList.Count == 0) return;

            Projectile nearest = null;
            float minSqrDist = float.MaxValue;
            Vector3 judgePos = judgePoints[lane].position;

            for (int i = 0; i < laneList.Count; i++)
            {
                if (laneList[i].IsReflected) continue;

                float sqrDist = (laneList[i].transform.position - judgePos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = laneList[i];
                }
            }

            if (nearest != null)
            {
                float timeDiff = nearest.ArrivalTime - Time.time;
                float absDiff = Mathf.Abs(timeDiff);

                // Ignore logic changed: Anything within badWindowLimit is at least Bad
                // Anything beyond that is a silent ignore (Ghost Tap)
                if (timeDiff > badWindowLimit) return; 

                JudgmentType judgment = JudgmentType.None;

                if (absDiff <= perfectWindow) judgment = JudgmentType.Perfect;
                else if (absDiff <= excellentWindow) judgment = JudgmentType.Excellent;
                else if (absDiff <= goodWindow) judgment = JudgmentType.Good;
                else judgment = JudgmentType.Bad;

                ProcessJudgment(judgment, lane, nearest);
            }
        }

        private void ProcessJudgment(JudgmentType type, int lane, Projectile target)
        {
            // 1. Combo Handling (Perfect to Good increases combo, Bad resets combo)
            if (type == JudgmentType.Perfect || type == JudgmentType.Excellent || type == JudgmentType.Good)
            {
                currentCombo++;
                if (currentCombo > maxCombo) maxCombo = currentCombo;
            }
            else
            {
                currentCombo = 0;
            }

            // 2. Score Calculation
            int multiplier = type switch
            {
                JudgmentType.Perfect => 3,
                JudgmentType.Excellent => 2,
                JudgmentType.Good => 1,
                _ => 0
            };

            long addedScore = 0;
            if (multiplier > 0)
            {
                addedScore = baseNoteScore * multiplier;
                currentScore += addedScore;
                if (scoreUI != null) scoreUI.UpdateScore(currentScore);
            }

            // 3. UI & Logging
            if (comboUI != null) comboUI.UpdateUI(currentCombo, type);

            string color = type == JudgmentType.Perfect ? "cyan" : type == JudgmentType.Excellent ? "green" : type == JudgmentType.Good ? "yellow" : "red";
            Debug.Log($"<color={color}>[{type}]</color> Lane {lane + 1}! Combo: {currentCombo} | Score: {currentScore} (+{addedScore})");

            // 4. Effects & Action
            if (type != JudgmentType.Bad && type != JudgmentType.Miss)
            {
                target.Reflect();
                PlayHitEffects(lane);
            }
            else
            {
                // Bad or Manual Miss (Too Late is handled by NoteMissed)
                DecreaseHealth();
                target.ReturnToPool(); 
            }
        }

        public void NoteMissed(int lane)
        {
            currentCombo = 0;
            if (comboUI != null) comboUI.UpdateUI(0, JudgmentType.Miss);
            DecreaseHealth();
            Debug.Log($"<color=red>[Miss (Auto)]</color> Lane {lane + 1}! Combo: 0");
        }

        private void DecreaseHealth()
        {
            currentHealth = Mathf.Max(0, currentHealth - 1);
            if (healthUI != null) healthUI.UpdateUI(currentHealth);
            
            if (currentHealth <= 0)
            {
                OnGameOver();
            }
        }

        private void OnGameOver()
        {
            Debug.Log("<color=red>[GAME OVER]</color>");
            // TODO: Implement Game Over logic (pause, show result, etc.)
        }

        public void RemoveFromActiveList(Projectile p, int lane)
        {
            if (lane >= 0 && lane < 4) activeProjectiles[lane].Remove(p);
        }

        private void PlayHitEffects(int lane)
        {
            if (reflectVfxPrefab != null && lane < judgePoints.Length && judgePoints[lane] != null)
            {
                Instantiate(reflectVfxPrefab, judgePoints[lane].position, Quaternion.identity);
            }

            if (guitarZones != null && lane < guitarZones.Length && guitarZones[lane] != null)
            {
                guitarZones[lane].VibrateAll();
            }
        }
    }
}
