using UnityEngine;
using TMPro;
using DG.Tweening;

public class JudgmentUIController : MonoBehaviour
{
    public static JudgmentUIController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI[] laneTexts; // 4개의 레인별 텍스트
    
    [Header("Visual Settings")]
    [SerializeField] private TMP_FontAsset[] judgmentFonts; // Perfect, Great, Good, Miss 순서
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

    public void DisplayJudgment(int laneIndex, Judgment judgment)
    {
        if (laneIndex < 0 || laneIndex >= laneTexts.Length || judgment == Judgment.None) return;

        TextMeshProUGUI targetText = laneTexts[laneIndex];
        if (targetText == null) return;

        // 기존 애니메이션 중지
        laneSequences[laneIndex]?.Kill(true);
        
        // 텍스트 및 폰트 설정
        targetText.text = judgment.ToString().ToUpper();
        int fontIndex = (int)judgment - 1; // None(0) 제외 1부터 시작
        if (judgmentFonts != null && fontIndex >= 0 && fontIndex < judgmentFonts.Length)
        {
            if (judgmentFonts[fontIndex] != null)
                targetText.font = judgmentFonts[fontIndex];
        }

        // 새 애니메이션 시퀀스
        Sequence seq = DOTween.Sequence();
        
        // 중요: Vector3.one 대신 원래 가지고 있던 작은 스케일(0.01 등)로 초기화
        Vector3 baseScale = initialScales[laneIndex];
        targetText.transform.localScale = baseScale;

        CanvasGroup cg = targetText.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        // 연출: 원래 크기에서 punchScaleAmount 배율만큼 커졌다가 돌아옴
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
