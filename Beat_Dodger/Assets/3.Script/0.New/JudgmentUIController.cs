using UnityEngine;
using TMPro;
using DG.Tweening;

public class JudgmentUIController : MonoBehaviour
{
    public static JudgmentUIController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI[] laneTexts; // Text per lane
    
    [Header("Visual Settings")]
    [SerializeField] private TMP_FontAsset[] judgmentFonts; // Order: Perfect, Great, Good, Miss
    [SerializeField] private TMP_FontAsset feverFont;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private float punchScaleAmount = 0.3f;
    [SerializeField] private float fadeDuration = 0.4f;

    private Sequence[] laneSequences;
    private Vector3[] initialScales;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        laneSequences = new Sequence[laneTexts.Length];
        initialScales = new Vector3[laneTexts.Length];

        for (int i = 0; i < laneTexts.Length; i++)
        {
            if (laneTexts[i] != null)
                initialScales[i] = laneTexts[i].transform.localScale;
        }

        ClearAll();
    }

    public void DisplayJudgment(int laneIndex, Judgment judgment, bool isFever = false)
    {
        if (laneIndex < 0 || laneIndex >= laneTexts.Length || (judgment == Judgment.None && !isFever)) return;

        TextMeshProUGUI targetText = laneTexts[laneIndex];
        if (targetText == null) return;

        // Kill existing animation
        laneSequences[laneIndex]?.Kill(true);
        
        // Text and Font setup
        if (isFever)
        {
            targetText.text = "FEVER";
            if (feverFont != null) targetText.font = feverFont;
        }
        else
        {
            targetText.text = judgment.ToString().ToUpper();
            int fontIndex = (int)judgment - 1; 
            if (judgmentFonts != null && fontIndex >= 0 && fontIndex < judgmentFonts.Length)
            {
                if (judgmentFonts[fontIndex] != null)
                    targetText.font = judgmentFonts[fontIndex];
            }
        }

        // New animation sequence
        Sequence seq = DOTween.Sequence();
        
        // Use initial localScale instead of Vector3.one
        Vector3 baseScale = initialScales[laneIndex];
        targetText.transform.localScale = baseScale;

        CanvasGroup cg = targetText.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        // Animation: Scale up by punchScaleAmount then back to base
        seq.Append(targetText.transform.DOScale(baseScale * (1f + punchScaleAmount), animationDuration * 0.5f).SetEase(Ease.OutQuad));
        seq.Append(targetText.transform.DOScale(baseScale, animationDuration * 0.5f).SetEase(Ease.InQuad));
        
        if (cg != null)
        {
            seq.Append(cg.DOFade(0, fadeDuration).SetDelay(0.1f));
        }
        else
        {
            seq.AppendInterval(fadeDuration).OnComplete(() => targetText.text = "");
        }

        laneSequences[laneIndex] = seq;
    }

    public void ClearAll()
    {
        foreach (var text in laneTexts)
        {
            if (text != null) text.text = "";
        }
    }
}
