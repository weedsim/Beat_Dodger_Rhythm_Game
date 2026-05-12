using UnityEngine;
using TMPro;
using DG.Tweening;

public class ComboUIController : MonoBehaviour
{
    [Header("UI Objects")]
    [SerializeField] private GameObject comboGroup;      // Parent object of Core/Glow combo texts
    [SerializeField] private GameObject feverUIObject;   // Separate object for "FEVER" text

    [Header("References")]
    [SerializeField] private TextMeshProUGUI coreText;
    [SerializeField] private TextMeshProUGUI glowText;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private float punchScaleAmount = 0.3f;
    [SerializeField] private float feverScaleMultiplier = 1.0f;

    private bool isFeverMode;
    private int lastCombo;
    private Vector3 originalPosition;
    private Sequence comboSequence;

    private void Awake()
    {
        originalPosition = transform.localPosition;
    }

    private void Start()
    {
        // Subscribe to Manager events
        if (NewRhythmManager.Instance != null)
        {
            NewRhythmManager.Instance.OnNoteHit += UpdateComboUI;
            NewRhythmManager.OnFeverStateChanged += HandleFeverStateChanged;
        }

        ClearUI();
    }

    private void OnDestroy()
    {
        if (NewRhythmManager.Instance != null)
        {
            NewRhythmManager.Instance.OnNoteHit -= UpdateComboUI;
            NewRhythmManager.OnFeverStateChanged -= HandleFeverStateChanged;
        }
        
        comboSequence?.Kill();
    }

    public void UpdateComboUI(Judgment judgment, int combo)
    {
        lastCombo = combo;
        UpdateComboText(combo);
    }

    private void UpdateComboText(int combo)
    {
        if (coreText == null || glowText == null) return;
        if (isFeverMode) return; // Don't update combo text during fever

        if (combo <= 0)
        {
            coreText.text = "";
            glowText.text = "";
            return;
        }

        string comboString = combo.ToString();
        coreText.text = comboString;
        glowText.text = comboString;

        // Combo text animation
        comboSequence?.Kill(true);
        comboSequence = DOTween.Sequence();

        float targetScale = isFeverMode ? feverScaleMultiplier : 1.0f;
        transform.localScale = Vector3.one * targetScale;
        
        float punchAmount = punchScaleAmount;
        comboSequence.Append(transform.DOPunchScale(Vector3.one * punchAmount, animationDuration, 10, 1));
    }

    private void HandleFeverStateChanged(bool active)
    {
        isFeverMode = active;
        
        // Toggle UI objects instead of changing text
        if (comboGroup != null) comboGroup.SetActive(!active);
        if (feverUIObject != null) feverUIObject.SetActive(active);

        if (!active)
        {
            // Restore combo display when exiting fever
            UpdateComboText(lastCombo);
            transform.localScale = Vector3.one;
            transform.localPosition = originalPosition;
        }
        else
        {
            // Reset scale/position for fever UI if needed (or handle within its own object)
        }
    }

    private void ClearUI()
    {
        if (coreText != null) coreText.text = "";
        if (glowText != null) glowText.text = "";
    }
}
