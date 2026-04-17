using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace BeatDodger.Game
{
    public enum JudgmentType { Perfect, Excellent, Good, Bad, Miss, None }

    public class RhythmManager : MonoBehaviour
    {
        public static RhythmManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private LongNoteProjectile longNotePrefab;
        [SerializeField] private Enemy[] enemyPrefabs; 
        [SerializeField] private Transform[] judgePoints;
        [SerializeField] private BoxCollider[] enemySpawnZones; 
        [SerializeField] private GuitarZone[] guitarZones;
        [SerializeField] private GameObject reflectVfxPrefab;
        [SerializeField] private ComboUI comboUI;
        private double musicStartDspTime = 0;

        [Header("Pool Parents")]
        [SerializeField] private Transform enemyPoolParent;
        [SerializeField] private Transform projectilePoolParent;
        [SerializeField] private Transform longNotePoolParent; 

        [Header("Note Mapping")]
        [SerializeField] private BeatDodger.Core.NoteMapData mapData;
        [SerializeField] private AudioSource musicSource;
        
        [Header("Settings")]
        [SerializeField] private float bpm = 128f;
        [SerializeField] private float travelDuration = 1.25f;
        [SerializeField] private float gameIntroDelay = 3.0f; 
        [SerializeField] private int enemiesPerLane = 5; 
        [SerializeField] private float launchZ = 25f;    
        [SerializeField] private float _reflectionSpeedMultiplier = 2.0f;
        [SerializeField] private float minNoteInterval = 0.1f; 

        public float ReflectionSpeedMultiplier => _reflectionSpeedMultiplier;
        public Vector3 GetJudgePosition(int lane) => (lane >= 0 && lane < judgePoints.Length) ? judgePoints[lane].position : Vector3.zero;
        
        [Header("Judgment Windows (Seconds)")]
        [SerializeField] private float perfectWindow = 0.033f;   
        [SerializeField] private float excellentWindow = 0.075f; 
        [SerializeField] private float goodWindow = 0.095f;      
        [SerializeField] private float badWindowLimit = 0.200f;  
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private bool showDebugGUI = false;
        [SerializeField] private bool autoStart = true; 
        
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
        [SerializeField] private float globalOffset = 0f; 

        [Header("Audio Filter Settings")]
        [SerializeField] private AudioLowPassFilter musicFilter;
        [SerializeField] private float normalCutoff = 22000f;
        [SerializeField] private float missCutoff = 800f;
        [SerializeField] private float recoverySpeed = 3f;
        private float _currentFilterCutoff;

        public float GlobalOffset 
        { 
            get => globalOffset; 
            set 
            {
                globalOffset = value;
            } 
        }
        public float SyncTime {
            get {
                if (!isGameStarted || !isMusicPlayed) return gameTimer + globalOffset;
                return (float)(AudioSettings.dspTime - musicStartDspTime) + globalOffset;
            }
        }

        private int currentNoteIndex = 0;
        private bool isGameStarted = false;
        private bool isMusicPlayed = false;
        private float gameTimer = 0f;
        private float[] lastLaneSpawnTime = new float[4];

        [Header("Keys (Legacy Input)")]
        [SerializeField] private KeyCode[] laneKeys = { KeyCode.S, KeyCode.D, KeyCode.K, KeyCode.L };
        private int bindingLaneIndex = -1; 

        private IObjectPool<Projectile> projectilePool;
        private IObjectPool<LongNoteProjectile> longNotePool; 
        private List<IObjectPool<Enemy>> enemyPools; 
        private List<Enemy>[] activeEnemies; 
        private List<Projectile>[] activeProjectiles;
        private List<LongNoteProjectile>[] activeLongNotes; 
        private float[] lastHoldComboTime; 

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (enemyPoolParent == null) enemyPoolParent = new GameObject("EnemyPool").transform;
            if (projectilePoolParent == null) projectilePoolParent = new GameObject("ProjectilePool").transform;
            if (longNotePoolParent == null) longNotePoolParent = new GameObject("LongNotePool").transform;

            currentHealth = maxHealth;
            if (healthUI != null) healthUI.Init(maxHealth);
            
            InitializePool();
            InitializeLanes();
            LoadOffset(); 
            
            lastHoldComboTime = new float[4];
            _currentFilterCutoff = normalCutoff;
            if (musicFilter != null) musicFilter.cutoffFrequency = normalCutoff;
        }

        private void InitializePool()
        {
            projectilePool = new ObjectPool<Projectile>(
                createFunc: () => {
                    var p = Instantiate(projectilePrefab, projectilePoolParent);
                    p.Setup(projectilePool, this);
                    return p;
                },
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                defaultCapacity: 32
            );

            if (longNotePrefab != null)
            {
                longNotePool = new ObjectPool<LongNoteProjectile>(
                    createFunc: () => Instantiate(longNotePrefab, longNotePoolParent),
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    defaultCapacity: 8
                );
            }

            enemyPools = new List<IObjectPool<Enemy>>();
            foreach (var prefab in enemyPrefabs)
            {
                var targetPrefab = prefab; 
                var pool = new ObjectPool<Enemy>(
                    createFunc: () => {
                        var e = Instantiate(targetPrefab, enemyPoolParent);
                        e.gameObject.SetActive(false); 
                        return e;
                    },
                    actionOnGet: e => { }, 
                    actionOnRelease: e => e.gameObject.SetActive(false),
                    defaultCapacity: 4
                );
                enemyPools.Add(pool);
            }
        }

        private void InitializeLanes()
        {
            activeProjectiles = new List<Projectile>[4];
            activeLongNotes = new List<LongNoteProjectile>[4];
            activeEnemies = new List<Enemy>[4];
            for (int i = 0; i < 4; i++)
            {
                activeProjectiles[i] = new List<Projectile>();
                activeLongNotes[i] = new List<LongNoteProjectile>();
                activeEnemies[i] = new List<Enemy>();
                lastLaneSpawnTime[i] = -100f;
                for (int j = 0; j < enemiesPerLane; j++) SpawnEnemyInLane(i);
            }
        }

        private void Start()
        {
            if (autoStart) StartGame();
        }

        private void SpawnEnemyInLane(int laneIndex)
        {
            if (enemySpawnZones == null || enemySpawnZones.Length <= laneIndex) return;
            int poolIndex = Random.Range(0, enemyPools.Count);
            var enemy = enemyPools[poolIndex].Get();
            Vector3 spawnPos = GetRandomPointInBox(enemySpawnZones[laneIndex]);
            enemy.Init(laneIndex, this, spawnPos, poolIndex);
            activeEnemies[laneIndex].Add(enemy);
        }

        private Vector3 GetRandomPointInBox(BoxCollider box)
        {
            Vector3 center = box.center + box.transform.position;
            Vector3 size = box.size;
            float x = Random.Range(center.x - size.x / 2f, center.x + size.x / 2f);
            float z = Random.Range(center.z - size.z / 2f, center.z + size.z / 2f);
            float y = center.y;
            Vector3 rayStart = new Vector3(x, center.y + (size.y * 0.4f), z);
            LayerMask mask = groundLayer.value == 0 ? (LayerMask)~0 : groundLayer;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, size.y + 5f, mask, QueryTriggerInteraction.Ignore)) y = hit.point.y;
            return new Vector3(x, y, z);
        }

        private void OnGUI()
        {
            if (isGameStarted) return;
            GUI.backgroundColor = Color.black;
            GUI.Box(new Rect(10, 10, 250, 200), "GAME SETUP");
            if (GUI.Button(new Rect(20, 40, 230, 40), "START GAME")) StartGame();
            for (int i = 0; i < 4; i++)
            {
                string btnText = bindingLaneIndex == i ? "Press any key..." : $"Lane {i + 1}: {laneKeys[i]}";
                if (GUI.Button(new Rect(20, 90 + (i * 25), 230, 22), btnText)) bindingLaneIndex = i;
            }
            if (bindingLaneIndex != -1 && Event.current.isKey && Event.current.type == EventType.KeyDown) { laneKeys[bindingLaneIndex] = Event.current.keyCode; bindingLaneIndex = -1; }
        }

        private void StartGame()
        {
            if (musicSource != null && mapData != null)
            {
                musicSource.clip = mapData.music;
                musicSource.playOnAwake = false;
                musicSource.Stop();
                currentNoteIndex = 0;
                isMusicPlayed = false;
                musicStartDspTime = AudioSettings.dspTime + gameIntroDelay; // 미래 시점으로 설정

                for (int i = 0; i < 4; i++) { activeProjectiles[i].Clear(); activeLongNotes[i].Clear(); lastLaneSpawnTime[i] = -100f; }
                gameTimer = -gameIntroDelay;
                isGameStarted = true;
            }
        }

        public void OnEnemyDied(Enemy enemy, int laneIndex)
        {
            if (activeEnemies[laneIndex].Contains(enemy))
            {
                activeEnemies[laneIndex].Remove(enemy);
                if (enemy.OriginPoolId >= 0 && enemy.OriginPoolId < enemyPools.Count) enemyPools[enemy.OriginPoolId].Release(enemy);
                else enemy.gameObject.SetActive(false);
                SpawnEnemyInLane(laneIndex);
            }
        }

        private void Update()
        {
            if (!isGameStarted || mapData == null) return;

            gameTimer += Time.deltaTime;
            UpdateAudioFilter();

            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown(laneKeys[i])) OnKeyPress(i);
                if (Input.GetKey(laneKeys[i])) OnKeyHold(i);
                if (Input.GetKeyUp(laneKeys[i])) OnKeyRelease(i);
            }

            if (!isMusicPlayed && gameTimer >= 0)
            {
                double scheduledPlayTime = AudioSettings.dspTime + 0.05;
                musicSource.PlayScheduled(scheduledPlayTime);
                musicStartDspTime = scheduledPlayTime;
                isMusicPlayed = true;
            }

            // 안전장치: 한 프레임에 최대 생성되는 노트 수 제한
            int spawnLimit = 10;
            int currentFrameSpawnCount = 0;
            float masterSyncTime = SyncTime;

            while (currentNoteIndex < mapData.notes.Count && currentFrameSpawnCount < spawnLimit)
            {
                var note = mapData.notes[currentNoteIndex];
                
                // 만약 노트 타임이 현재 시간보다 너무 과거라면 (도메인 리로드 등으로 인한 무한 루프 방지)
                if (note.time < masterSyncTime - 1.0f) 
                {
                    currentNoteIndex++;
                    continue;
                }

                if (note.time - travelDuration <= masterSyncTime)
                {
                    if (note.time - lastLaneSpawnTime[note.lane] >= minNoteInterval)
                    {
                        Random.InitState((int)(note.time * 1000f));
                        if (note.duration > 0)
                        {
                            float lnArc = Random.Range(longNotePrefab.MinArcHeight, longNotePrefab.MaxArcHeight);
                            SpawnLongNote(note.lane, note.time, note.duration, lnArc);
                        }
                        else
                        {
                            float pArc = Random.Range(projectilePrefab.MinArcHeight, projectilePrefab.MaxArcHeight);
                            Spawn(note.lane, note.lane, pArc, note.time);
                        }
                        lastLaneSpawnTime[note.lane] = note.time;
                        currentFrameSpawnCount++;
                    }
                    currentNoteIndex++;
                }
                else break;
            }
        }

        private void UpdateAudioFilter()
        {
            if (musicFilter == null) return;
            if (Mathf.Abs(_currentFilterCutoff - normalCutoff) > 1f)
            {
                _currentFilterCutoff = Mathf.Lerp(_currentFilterCutoff, normalCutoff, Time.deltaTime * recoverySpeed);
                musicFilter.cutoffFrequency = _currentFilterCutoff;
            }
        }

        private void SpawnLongNote(int lane, float time, float duration, float arc)
        {
            if (longNotePool == null) return;
            Enemy shooter = activeEnemies[lane].Find(e => !e.IsFiring);
            if (shooter == null) { SpawnEnemyInLane(lane); shooter = activeEnemies[lane].Find(e => !e.IsFiring); }
            if (shooter == null) return;

            shooter.PlayFireAnimation();
            Vector3 start = shooter.transform.position;
            start.z = launchZ;
            Vector3 target = judgePoints[lane].position;
            var lp = longNotePool.Get();
            lp.Initialize(shooter, start, target, arc, time, duration, travelDuration, lane, this, longNotePool);
            activeLongNotes[lane].Add(lp);
        }

        public void Spawn(int sourceLaneIndex, int targetLaneIndex, float arc = -1f, float targetNoteTime = 0f)
        {
            Enemy shooter = activeEnemies[sourceLaneIndex].Find(e => !e.IsFiring);
            if (shooter == null) { SpawnEnemyInLane(sourceLaneIndex); shooter = activeEnemies[sourceLaneIndex].Find(e => !e.IsFiring); }
            if (shooter == null) return;

            shooter.PlayFireAnimation();
            shooter.SetFiring(true);
            Vector3 unifiedStartPos = shooter.transform.position;
            unifiedStartPos.z = launchZ;
            var p = projectilePool.Get();
            p.Initialize(shooter, unifiedStartPos, judgePoints[targetLaneIndex].position, travelDuration, targetLaneIndex, arc, targetNoteTime);
            activeProjectiles[targetLaneIndex].Add(p);
        }

        private void OnKeyPress(int lane)
        {
            var laneList = activeProjectiles[lane];
            if (laneList.Count > 0)
            {
                Projectile nearest = null;
                float minDiff = float.MaxValue;
                float currentSync = SyncTime;
                for (int i = 0; i < laneList.Count; i++)
                {
                    if (laneList[i].IsReflected) continue;
                    float diff = Mathf.Abs(laneList[i].TargetNoteTime - currentSync);
                    if (diff < minDiff) { minDiff = diff; nearest = laneList[i]; }
                }
                if (nearest != null && minDiff <= badWindowLimit) ProcessJudgment(CalculateJudgment(nearest.TargetNoteTime - currentSync), lane, nearest);
            }
            
            var lnList = activeLongNotes[lane];
            if (lnList.Count > 0)
            {
                for (int i = lnList.Count - 1; i >= 0; i--)
                {
                    var ln = lnList[i];
                    float timeDiff = ln.StartTime - SyncTime;
                    if (Mathf.Abs(timeDiff) <= badWindowLimit) 
                    {
                        JudgmentType judgment = CalculateJudgment(timeDiff);
                        if (judgment != JudgmentType.Bad && judgment != JudgmentType.Miss) { ln.OnHit(judgment); ProcessJudgment(judgment, lane, null); break; }
                    }
                }
            }
        }

        private void OnKeyHold(int lane)
        {
            var lnList = activeLongNotes[lane];
            for (int i = lnList.Count - 1; i >= 0; i--)
            {
                var ln = lnList[i];
                if (ln.IsHolding && Time.time - lastHoldComboTime[lane] > 0.1f)
                {
                    ProcessJudgment(ln.StartJudgment, lane, null);
                    lastHoldComboTime[lane] = Time.time;
                }
            }
        }

        private void OnKeyRelease(int lane)
        {
            var lnList = activeLongNotes[lane];
            for (int i = lnList.Count - 1; i >= 0; i--) lnList[i].SetHolding(false);
        }

        private JudgmentType CalculateJudgment(float timeDiff)
        {
            float absDiff = Mathf.Abs(timeDiff);
            if (absDiff <= perfectWindow) return JudgmentType.Perfect;
            if (absDiff <= excellentWindow) return JudgmentType.Excellent;
            if (absDiff <= goodWindow) return JudgmentType.Good;
            return JudgmentType.Bad;
        }

        private void ProcessJudgment(JudgmentType type, int lane, Projectile target)
        {
            if (type == JudgmentType.Perfect || type == JudgmentType.Excellent || type == JudgmentType.Good)
            {
                currentCombo++;
                if (currentCombo > maxCombo) maxCombo = currentCombo;
            }
            else currentCombo = 0;

            int multiplier = type switch { JudgmentType.Perfect => 3, JudgmentType.Excellent => 2, JudgmentType.Good => 1, _ => 0 };
            if (multiplier > 0) {
                currentScore += baseNoteScore * multiplier;
                if (scoreUI != null) scoreUI.UpdateScore(currentScore);
            }

            if (comboUI != null) comboUI.UpdateUI(currentCombo, type);

            if (target != null)
            {
                if (type != JudgmentType.Bad && type != JudgmentType.Miss) { target.Reflect(); PlayHitEffects(lane); }
                else { DecreaseHealth(); target.ReturnToPool(); }
            }
        }

        public void ResolveLongNoteEnd(JudgmentType finalJudgment, int lane, LongNoteProjectile lp)
        {
            if (finalJudgment == JudgmentType.Miss) NoteMissed(lane);
            else ProcessJudgment(finalJudgment, lane, null);
            RemoveLongNote(lp, lane);
        }

        public void RemoveLongNote(LongNoteProjectile lp, int lane) { if (lane >= 0 && lane < activeLongNotes.Length) activeLongNotes[lane].Remove(lp); }

        public void NoteMissed(int lane)
        {
            currentCombo = 0;
            if (comboUI != null) comboUI.UpdateUI(0, JudgmentType.Miss);
            DecreaseHealth();
            ApplyMissFilter();
        }

        private void ApplyMissFilter()
        {
            _currentFilterCutoff = missCutoff;
            if (musicFilter != null) musicFilter.cutoffFrequency = missCutoff;
        }
        
        private void DecreaseHealth()
        {
            currentHealth = Mathf.Max(0, currentHealth - 1);
            if (healthUI != null) healthUI.UpdateUI(currentHealth);
            if (currentHealth <= 0) OnGameOver();
        }

        private void OnGameOver() {  }
        public void RemoveFromActiveList(Projectile p, int lane) { if (lane >= 0 && lane < 4) activeProjectiles[lane].Remove(p); }

        private void PlayHitEffects(int lane)
        {
            if (reflectVfxPrefab != null && lane < judgePoints.Length && judgePoints[lane] != null) Instantiate(reflectVfxPrefab, judgePoints[lane].position, Quaternion.identity);
            if (guitarZones != null && lane < guitarZones.Length && guitarZones[lane] != null) guitarZones[lane].VibrateAll();
        }
        public void SaveOffset() { PlayerPrefs.SetFloat("RhythmOffset", globalOffset); PlayerPrefs.Save(); }
        private void LoadOffset() { GlobalOffset = PlayerPrefs.GetFloat("RhythmOffset", 0f); }
    }
}
