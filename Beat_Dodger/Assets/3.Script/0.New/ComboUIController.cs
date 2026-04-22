using UnityEngine;
using TMPro;
using DG.Tweening;

public class ComboUIController : MonoBehaviour
{
    [Header("Combo Text References")]
    [SerializeField] private TextMeshProUGUI coreText;
    [SerializeField] private TextMeshProUGUI glowText;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private float punchScaleAmount = 0.3f;

    [Header("Fever Effects")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color feverColor = new Color(1f, 0.5f, 0f); // Orange for Fever
    [SerializeField] private float feverScaleMultiplier = 1.3f;

    private bool isFeverMode;

    private Sequence comboSequence;

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
        UpdateComboText(combo);
    }

    private void UpdateComboText(int combo)
    {
        if (coreText == null || glowText == null) return;

        if (combo <= 0)
        {
            coreText.text = "";
            glowText.text = "";
            return;
        }

        string comboString = combo.ToString();
        coreText.text = comboString;
        glowText.text = comboString;

        Color targetColor = isFeverMode ? feverColor : normalColor;
        coreText.color = targetColor;
        glowText.color = targetColor;

        // Combo text animation
        comboSequence?.Kill(true);
        comboSequence = DOTween.Sequence();

        transform.localScale = isFeverMode ? Vector3.one * feverScaleMultiplier : Vector3.one;
        float punchAmount = isFeverMode ? punchScaleAmount * 1.5f : punchScaleAmount;
        comboSequence.Append(transform.DOPunchScale(Vector3.one * punchAmount, animationDuration, 10, 1));
    }

    private void HandleFeverStateChanged(bool active)
    {
        isFeverMode = active;
        
        // Update color and scale immediately upon Fever entry/exit
        Color targetColor = active ? feverColor : normalColor;
        coreText.DOColor(targetColor, 0.3f);
        glowText.DOColor(targetColor, 0.3f);
        
        float targetScale = active ? feverScaleMultiplier : 1.0f;
        transform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack);
    }

    private void ClearUI()
    {
        if (coreText != null) coreText.text = "";
        if (glowText != null) glowText.text = "";
    }
}
