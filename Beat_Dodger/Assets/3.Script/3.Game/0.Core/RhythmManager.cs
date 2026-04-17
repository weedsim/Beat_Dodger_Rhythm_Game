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
        [SerializeField] private Enemy[] enemyPrefabs; // 변경: 적 프리팹 배열화
        [SerializeField] private Transform[] judgePoints;
        [SerializeField] private BoxCollider[] enemySpawnZones; 
        [SerializeField] private GuitarZone[] guitarZones;
        [SerializeField] private GameObject reflectVfxPrefab;
        [SerializeField] private ComboUI comboUI;
        [SerializeField] private InputActionAsset inputActions;
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

        public float ReflectionSpeedMultiplier => _reflectionSpeedMultiplier;
        public Vector3 GetJudgePosition(int lane) => (lane >= 0 && lane < judgePoints.Length) ? judgePoints[lane].position : Vector3.zero;
        
        [Header("Judgment Windows (Seconds)")]
        [SerializeField] private float perfectWindow = 0.033f;   
        [SerializeField] private float excellentWindow = 0.075f; 
        [SerializeField] private float goodWindow = 0.095f;      
        [SerializeField] private float badWindowLimit = 0.200f;  
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private bool showDebugGUI = false;
        [SerializeField] private bool autoStart = true; // Enabled by default as per request
        
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
        [SerializeField] private float globalOffset = 0f; // Seconds

        public float GlobalOffset 
        { 
            get => globalOffset; 
            set 
            {
                globalOffset = value;
                Debug.Log($"<color=orange>[Offset-Tracker]</color> Value changed to: {value * 1000:F1}ms\nStack: {System.Environment.StackTrace}");
            } 
        }
        public float SyncTime {
            get {
                if (!isGameStarted) return 0f;
                if (!isMusicPlayed) return gameTimer + globalOffset;
                // DSP 기반 동기화: 오디오 하드웨어 시계와 물리적으로 완벽 히 일치
                return (float)(AudioSettings.dspTime - musicStartDspTime) + globalOffset;
            }
        }

        private int currentNoteIndex = 0;
        private bool isGameStarted = false;
        private bool isMusicPlayed = false;
        private float gameTimer = 0f;

        [Header("Keys (Legacy Input for OnGUI Rebind)")]
        [SerializeField] private KeyCode[] laneKeys = { KeyCode.S, KeyCode.D, KeyCode.K, KeyCode.L };
        private int bindingLaneIndex = -1; // -1 means not binding

        private IObjectPool<Projectile> projectilePool;
        private IObjectPool<LongNoteProjectile> longNotePool; 
        private List<IObjectPool<Enemy>> enemyPools; // 변경: 프리팹별 풀 리스트
        private List<Enemy>[] activeEnemies; 
        private List<Projectile>[] activeProjectiles;
        private List<LongNoteProjectile>[] activeLongNotes; 
        private float[] lastHoldComboTime; 

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject); // 필요 시 활성화
            }
            else
            {
                Debug.LogWarning($"<color=red>[RhythmManager]</color> Second instance detected on {gameObject.name}. Destroying duplicate.");
                Destroy(gameObject); 
                return; 
            }

            Debug.Log($"<color=green>[RhythmManager]</color> Awake Starting on {gameObject.name}...");
            
            if (enemyPoolParent == null) enemyPoolParent = new GameObject("EnemyPool").transform;
            if (projectilePoolParent == null) projectilePoolParent = new GameObject("ProjectilePool").transform;
            if (longNotePoolParent == null) longNotePoolParent = new GameObject("LongNotePool").transform;

            currentHealth = maxHealth;
            if (healthUI != null) healthUI.Init(maxHealth);
            
            InitializePool();
            InitializeInput();
            InitializeLanes();
            
            LoadOffset(); // 오프셋 설정 불러오기
            
            lastHoldComboTime = new float[4];
        }

        private void InitializePool()
        {
            if (projectilePrefab == null || enemyPrefabs == null || enemyPrefabs.Length == 0)
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

            if (longNotePrefab != null)
            {
                longNotePool = new ObjectPool<LongNoteProjectile>(
                    createFunc: () => Instantiate(longNotePrefab, longNotePoolParent),
                    actionOnGet: p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => Destroy(p.gameObject),
                    defaultCapacity: 8
                );
            }

            // 프리팹 개수만큼 풀을 생성합니다.
            enemyPools = new List<IObjectPool<Enemy>>();
            foreach (var prefab in enemyPrefabs)
            {
                var targetPrefab = prefab; // Closure 대응
                var pool = new ObjectPool<Enemy>(
                    createFunc: () => {
                        var e = Instantiate(targetPrefab, enemyPoolParent);
                        e.gameObject.SetActive(false); // 생성 즉시 비활성화하여 순간 노출 방지
                        return e;
                    },
                    actionOnGet: e => { }, 
                    actionOnRelease: e => e.gameObject.SetActive(false),
                    actionOnDestroy: e => Destroy(e.gameObject),
                    defaultCapacity: 4
                );
                enemyPools.Add(pool);
            }
        }

        private void InitializeInput()
        {
            Debug.Log("<color=cyan>[RhythmManager]</color> Input initialized via Legacy Polling.");
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
                
                for (int j = 0; j < enemiesPerLane; j++)
                {
                    SpawnEnemyInLane(i);
                }
            }
        }

        private void Start()
        {
            // LoadOffset() 삭제: Awake에서 이미 로드됨
            if (autoStart) StartGame();
        }

        private void SpawnEnemyInLane(int laneIndex)
        {
            if (enemySpawnZones == null || enemySpawnZones.Length <= laneIndex || enemySpawnZones[laneIndex] == null) return;
            if (enemyPools == null || enemyPools.Count == 0) return;

            // 랜덤하게 적 타입을 결정하여 풀에서 가져옵니다.
            int poolIndex = Random.Range(0, enemyPools.Count);
            var enemy = enemyPools[poolIndex].Get();
            
            // 위치를 먼저 계산한 뒤 Init에 전달합니다.
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

            // Ground Snapping Logic: Start ray slightly above the box's bottom to only hit what's below
            Vector3 rayStart = new Vector3(x, center.y + (size.y * 0.4f), z);
            LayerMask mask = groundLayer.value == 0 ? (LayerMask)~0 : groundLayer;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, size.y + 5f, mask, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
            }
            
            return new Vector3(x, y, z);
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
                
                currentNoteIndex = 0;
                isMusicPlayed = false;
                musicStartDspTime = 0;

                // Clear any existing active notes
                for (int i = 0; i < 4; i++)
                {
                    activeProjectiles[i].Clear();
                    activeLongNotes[i].Clear();
                }

                gameTimer = -gameIntroDelay;
                isGameStarted = true;
            }
        }

        public void OnEnemyDied(Enemy enemy, int laneIndex)
        {
            if (activeEnemies[laneIndex].Contains(enemy))
            {
                activeEnemies[laneIndex].Remove(enemy);
                
                // Release to the original pool for reuse
                if (enemy.OriginPoolId >= 0 && enemy.OriginPoolId < enemyPools.Count)
                {
                    enemyPools[enemy.OriginPoolId].Release(enemy);
                }
                else
                {
                    enemy.gameObject.SetActive(false);
                }

                SpawnEnemyInLane(laneIndex);
            }
        }

        private void Update()
        {
            if (!isGameStarted || mapData == null) return;

            gameTimer += Time.deltaTime;

            // Handle Input
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown(laneKeys[i]))
                {
                    OnKeyPress(i);
                }
                
                if (Input.GetKey(laneKeys[i]))
                {
                    OnKeyHold(i);
                }
                
                if (Input.GetKeyUp(laneKeys[i]))
                {
                    OnKeyRelease(i);
                }
            }

            if (!isMusicPlayed && gameTimer >= 0)
            {
                // 즉시 Play하지 않고 0.05초 뒤에 정확히 재생되도록 예약 (지터 방지)
                double scheduledPlayTime = AudioSettings.dspTime + 0.05;
                musicSource.PlayScheduled(scheduledPlayTime);
                musicStartDspTime = scheduledPlayTime;
                isMusicPlayed = true;
                Debug.Log($"<color=white>[RhythmManager]</color> Music Scheduled at DSP: {scheduledPlayTime:F3}");
            }

            float masterSyncTime = SyncTime;

            while (currentNoteIndex < mapData.notes.Count)
            {
                var note = mapData.notes[currentNoteIndex];
                
                // 오프셋이 적용된 masterSyncTime 기준으로 소환 시점 결정
                if (note.time - travelDuration <= masterSyncTime)
                {
                    // [궤적 동기화] 동일한 박자의 노트는 항상 같은 높이를 가지도록 시드 고정
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
                    
                    currentNoteIndex++;
                }
                else break;
            }

            // 시드 복구 (다른 시스템의 랜덤에 영향을 주지 않기 위함)
            Random.InitState((int)System.DateTime.Now.Ticks);
        }

        private void SpawnLongNote(int lane, float time, float duration, float arc)
        {
            if (longNotePool == null) return;

            // Find an idle enemy in the lane to act as the emitter
            Enemy shooter = activeEnemies[lane].Find(e => !e.IsFiring);
            if (shooter == null)
            {
                SpawnEnemyInLane(lane);
                shooter = activeEnemies[lane].Find(e => !e.IsFiring);
            }
            if (shooter == null) return;

            // 애니메이션 재생
            shooter.PlayFireAnimation();

            Vector3 start = shooter.transform.position;
            start.z = launchZ;
            Vector3 target = judgePoints[lane].position;

            var lp = longNotePool.Get();
            
            // actualArrivalTime은 이제 맵 데이터의 목표 시간(time)을 의미합니다.
            lp.Initialize(shooter, start, target, arc, time, duration, travelDuration, lane, this, longNotePool);
            
            activeLongNotes[lane].Add(lp);
        }

        public void Spawn(int sourceLaneIndex, int targetLaneIndex, float arc = -1f, float targetNoteTime = 0f)
        {
            Enemy shooter = activeEnemies[sourceLaneIndex].Find(e => !e.IsFiring);
            
            if (shooter == null)
            {
                SpawnEnemyInLane(sourceLaneIndex);
                shooter = activeEnemies[sourceLaneIndex].Find(e => !e.IsFiring);
            }

            if (shooter == null) return;

            // 애니메이션 재생
            shooter.PlayFireAnimation();
            shooter.SetFiring(true);

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
                targetNoteTime
            );
            
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

                        // 거리 기반이 아닌 시간 기반으로 가장 가까운 노트 탐색 (더 정확함)
                        float diff = Mathf.Abs(laneList[i].TargetNoteTime - currentSync);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            nearest = laneList[i];
                        }
                    }

                    if (nearest != null && minDiff <= badWindowLimit)
                    {
                        // 오차값 계산 후 판정 처리
                        JudgmentType judgment = CalculateJudgment(nearest.TargetNoteTime - currentSync);
                        ProcessJudgment(judgment, lane, nearest);
                    }
                }
            
            // 롱노트 시작 판정
            var lnList = activeLongNotes[lane];
            if (lnList.Count > 0)
            {
                for (int i = lnList.Count - 1; i >= 0; i--)
                {
                    var ln = lnList[i];
                    float timeDiff = ln.StartTime - SyncTime;
                    float absDiff = Mathf.Abs(timeDiff);
                    
                    if (absDiff <= badWindowLimit) // 범위 내에 들어오면 판정 계산
                    {
                        JudgmentType judgment = CalculateJudgment(timeDiff);
                        if (judgment != JudgmentType.Bad && judgment != JudgmentType.Miss)
                        {
                            ln.OnHit(judgment);
                            ProcessJudgment(judgment, lane, null); // 시작 판정 처리
                            Debug.Log($"<color=cyan>[Hold Start]</color> Lane {lane + 1} Success with {judgment}!");
                            break;
                        }
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
                if (ln.IsHolding)
                {
                    // 0.1초마다 지속 콤보 및 점수 상승
                    if (Time.time - lastHoldComboTime[lane] > 0.1f)
                    {
                        // SyncTime을 사용할 수도 있지만, 콤보 가산 주기는 실시간(Time.time)이 더 자연스럽습니다.
                        ProcessJudgment(ln.StartJudgment, lane, null);
                        lastHoldComboTime[lane] = Time.time;
                    }
                }
            }
        }

        private void OnKeyRelease(int lane)
        {
            var lnList = activeLongNotes[lane];
            for (int i = lnList.Count - 1; i >= 0; i--)
            {
                lnList[i].SetHolding(false);
            }
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
            else
            {
                currentCombo = 0;
            }

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

            if (comboUI != null) comboUI.UpdateUI(currentCombo, type);

            if (target != null)
            {
                if (type != JudgmentType.Bad && type != JudgmentType.Miss)
                {
                    target.Reflect();
                    PlayHitEffects(lane);
                }
                else
                {
                    DecreaseHealth();
                    target.ReturnToPool(); 
                }
            }
        }

        public void ResolveLongNoteEnd(JudgmentType finalJudgment, int lane, LongNoteProjectile lp)
        {
            if (finalJudgment == JudgmentType.Miss)
            {
                NoteMissed(lane);
            }
            else
            {
                // [추가] 최종 유지 비율에 따른 추가 정산
                ProcessJudgment(finalJudgment, lane, null);
            }
            
            RemoveLongNote(lp, lane);
        }

        public void RemoveLongNote(LongNoteProjectile lp, int lane)
        {
            if (lane >= 0 && lane < activeLongNotes.Length)
            {
                activeLongNotes[lane].Remove(lp);
                // longNotePool.Release(lp); // DELETED: Should be released by the object itself
            }
        }

        public void NoteMissed(int lane)
        {
            currentCombo = 0;
            if (comboUI != null) comboUI.UpdateUI(0, JudgmentType.Miss);
            DecreaseHealth();
        }
        
        private void DecreaseHealth()
        {
            currentHealth = Mathf.Max(0, currentHealth - 1);
            if (healthUI != null) healthUI.UpdateUI(currentHealth);
            
            if (currentHealth <= 0) OnGameOver();
        }

        private void OnGameOver() { /* Game Over logic */ }

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
        public void SaveOffset()
        {
            PlayerPrefs.SetFloat("RhythmOffset", globalOffset);
            PlayerPrefs.Save();
            Debug.Log($"<color=cyan>[RhythmManager]</color> Offset Saved: {globalOffset * 1000:F1}ms");
        }

        private void LoadOffset()
        {
            GlobalOffset = PlayerPrefs.GetFloat("RhythmOffset", 0f);
            Debug.Log($"<color=cyan>[RhythmManager]</color> Offset Loaded: {globalOffset * 1000:F1}ms on [ {gameObject.name} ]");
        }
    }
}
