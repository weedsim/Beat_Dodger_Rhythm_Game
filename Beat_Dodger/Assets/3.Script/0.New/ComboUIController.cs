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

    private Sequence comboSequence;

    private void Start()
    {
        // Manager 이벤트 구독
        if (NewRhythmManager.Instance != null)
        {
            NewRhythmManager.Instance.OnNoteHit += UpdateComboUI;
        }

        ClearUI();
    }

    private void OnDestroy()
    {
        if (NewRhythmManager.Instance != null)
        {
            NewRhythmManager.Instance.OnNoteHit -= UpdateComboUI;
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

        // 콤보 텍스트 애니메이션
        comboSequence?.Kill(true);
        comboSequence = DOTween.Sequence();

        transform.localScale = Vector3.one;
        comboSequence.Append(transform.DOPunchScale(Vector3.one * punchScaleAmount, animationDuration, 10, 1));
    }

    private void ClearUI()
    {
        if (coreText != null) coreText.text = "";
        if (glowText != null) glowText.text = "";
    }
}
