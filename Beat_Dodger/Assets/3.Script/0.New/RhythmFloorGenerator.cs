using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RhythmFloorGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject quadPrefab;
    [SerializeField] private NewRhythmManager rhythmManager;
    [SerializeField] private RhythmConfig config;
    [SerializeField] private Transform judgeLineTransform;

    [Header("Generation Settings")]
    [SerializeField] private int totalRows = 50; 
    [SerializeField] private float yOffsetStep = 0.001f;

    [ContextMenu("Generate Floor")]
    public void GenerateFloor()
    {
        if (transform.localScale != Vector3.one)
        {
            Debug.LogWarning($"[RhythmFloorGenerator] {gameObject.name}의 Scale이 (1,1,1)이 아닙니다! 바닥 칸과 노트의 위치가 어긋날 수 있으니 (1,1,1)로 수정 후 다시 생성하세요.");
        }

        if (config == null) config = FindFirstObjectByType<RhythmConfig>();
        if (rhythmManager == null) rhythmManager = FindFirstObjectByType<NewRhythmManager>();
        
        if (quadPrefab == null || rhythmManager == null || config == null)
        {
            Debug.LogError("[RhythmFloorGenerator] 레퍼런스가 누락되었습니다! (Prefab, Manager, 또는 Config를 확인하세요)");
            return;
        }

        ClearExistingFloor();

        float spawnZ = config.SpawnLineZ;
        float judgeZ = config.JudgeLineZ;
        int beatsToArrive = rhythmManager.BeatsToArrive;

        // 한 박자당 이동하는 물리적 거리 계산
        float totalDist = spawnZ - judgeZ;
        float stepDist = totalDist / beatsToArrive;

        // 적이 칸 중앙에 오도록, 칸의 길이를 정확히 한 박자 이동 거리(stepDist)로 설정
        // 너비는 레인 간격(LaneSpacing)으로 설정
        Vector3 finalScale = new Vector3(config.LaneSpacing, Mathf.Abs(stepDist), 1f);

        List<GameObject> allRows = new List<GameObject>();

        for (int r = 0; r < totalRows; r++)
        {
            GameObject rowObj = new GameObject($"Row{r}");
            rowObj.transform.SetParent(transform);
            
            // 판정선(judgeZ)부터 r박자만큼 떨어진 위치
            float zPos = judgeZ + (r * stepDist);
            rowObj.transform.localPosition = new Vector3(0, r * yOffsetStep, zPos);
            allRows.Add(rowObj);


            for (int c = 0; c < config.LaneCount; c++)
            {
                GameObject quad = Instantiate(quadPrefab, rowObj.transform);
                quad.name = $"Quad_{r}_{c}";
                
                float xPos = (c - (config.LaneCount / 2f - 0.5f)) * config.LaneSpacing;
                
                quad.transform.localPosition = new Vector3(xPos, c * yOffsetStep * 0.1f, 0);
                quad.transform.localScale = finalScale;
                quad.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
        }

        UpdateColorSwitcher(allRows);
        UpdateJudgeLineLayout();

        Debug.Log($"[RhythmFloorGenerator] 생성 완료 (Rows: {totalRows})");
    }

    private void UpdateJudgeLineLayout()
    {
        if (judgeLineTransform == null || config == null) return;

        // 1. 부모(JudgeLine) 위치 및 전체 크기 동기화
        float totalWidth = config.LaneCount * config.LaneSpacing;
        
        judgeLineTransform.localPosition = new Vector3(0, 0.01f, config.JudgeLineZ);
        judgeLineTransform.localScale = new Vector3(totalWidth, judgeLineTransform.localScale.y, judgeLineTransform.localScale.z);

        // 2. 자식들(InputEffect) 각 레벨 위치에 정렬
        int childCount = judgeLineTransform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            if (i >= config.LaneCount) break;
            
            Transform child = judgeLineTransform.GetChild(i);
            
            // 부모의 scale X가 totalWidth이므로, 자식의 localX는 -0.5 ~ 0.5 사이의 비율로 계산해야 함
            // 하지만 계산 편의를 위해 자식의 위치를 계산하고 부모 scale로 나누어 적용
            float xPos = (i - (config.LaneCount / 2f - 0.5f)) * config.LaneSpacing;
            
            // 부모의 스케일 영향을 상쇄하여 배치
            child.localPosition = new Vector3(xPos / totalWidth, 0, 0);
            
            // 각 자식의 너비는 1개 레인만큼 (부모 대비 1/LaneCount 비율)
            child.localScale = new Vector3(1f / config.LaneCount, child.localScale.y, child.localScale.z);
        }
    }

    private void ClearExistingFloor()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    private void UpdateColorSwitcher(List<GameObject> rows)
    {
        if (TryGetComponent<RhythmColorSwitcher>(out var switcher))
        {
            var field = typeof(RhythmColorSwitcher).GetField("rowObjects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(switcher, rows.ToArray());
#if UNITY_EDITOR
                EditorUtility.SetDirty(switcher);
#endif
            }
        }
    }
}
