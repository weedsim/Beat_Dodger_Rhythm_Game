using UnityEngine;
using System.Collections.Generic;

public class RhythmColorSwitcher : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color colorA = Color.white;
    [SerializeField] private Color colorB = Color.black;
    
    [Header("Fever Colors")]
    [SerializeField] private Color feverColorA = new Color(0.3f, 0.05f, 0f); // Dark Red
    [SerializeField] private Color feverColorB = new Color(0.1f, 0.02f, 0f); // Deep Red

    [Header("Configuration")]
    [SerializeField] private int switchIntervalBeats = 1; // 몇 박자마다 색을 바꿀지 설정
    [SerializeField] private bool isAlternate;
    
    [Tooltip("Row 오브젝트들을 여기에 드래그하세요.")]
    [SerializeField] private GameObject[] rowObjects;

    private readonly List<MeshRenderer[]> laneRenderers = new List<MeshRenderer[]>();
    private MaterialPropertyBlock propertyBlock;
    private int beatCounter = 0;
    
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        if (rowObjects == null) return;

        laneRenderers.Clear();
        foreach (var row in rowObjects)
        {
            if (row == null) continue;
            MeshRenderer[] renderers = row.GetComponentsInChildren<MeshRenderer>();
            if (renderers != null && renderers.Length > 0)
            {
                laneRenderers.Add(renderers);
            }
        }
    }

    private void OnEnable()
    {
        NewRhythmManager.OnBeat += HandleBeat;
        NewRhythmManager.OnFeverStateChanged += HandleFeverStateChanged;
    }

    private void OnDisable()
    {
        NewRhythmManager.OnBeat -= HandleBeat;
        NewRhythmManager.OnFeverStateChanged -= HandleFeverStateChanged;
    }

    private void Start()
    {
        if (laneRenderers.Count == 0) Initialize();
        ApplyColors();
    }

    private void HandleBeat()
    {
        beatCounter++;

        // 설정된 박자 주기에 도달했을 때만 색상 전환
        if (beatCounter >= switchIntervalBeats)
        {
            SwitchColors();
            beatCounter = 0;
        }
    }

    private void HandleFeverStateChanged(bool active)
    {
        ApplyColors();
    }

    private void SwitchColors()
    {
        isAlternate = !isAlternate;
        ApplyColors();
    }

    private void ApplyColors()
    {
        if (laneRenderers == null || laneRenderers.Count == 0) return;

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        for (int r = 0; r < laneRenderers.Count; r++)
        {
            MeshRenderer[] renderersInRow = laneRenderers[r];
            if (renderersInRow == null) continue;

            for (int c = 0; c < renderersInRow.Length; c++)
            {
                MeshRenderer renderer = renderersInRow[c];
                if (renderer == null) continue;

                bool useColorA = ((r + c) % 2 == 0) ^ isAlternate;
                
                Color currentA = NewRhythmManager.Instance != null && NewRhythmManager.Instance.IsFeverTime ? feverColorA : colorA;
                Color currentB = NewRhythmManager.Instance != null && NewRhythmManager.Instance.IsFeverTime ? feverColorB : colorB;
                
                Color targetColor = useColorA ? currentA : currentB;

                renderer.GetPropertyBlock(propertyBlock);
                
                propertyBlock.SetColor(ColorId, targetColor);
                propertyBlock.SetColor(BaseColorId, targetColor);
                
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (this == null || gameObject == null) return;
        
        UnityEditor.EditorApplication.delayCall += () => {
            if (this == null || gameObject == null) return;
            Initialize();
            ApplyColors();
        };
#endif
    }
}