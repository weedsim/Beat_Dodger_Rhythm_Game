using UnityEngine;
using TMPro;
using System.Collections;

namespace BeatDodger.Game
{
    /// <summary>
    /// Professional Combo UI with punchy animations and color-coded judgments.
    /// Supports 5-tier judgment system: Perfect, Excellent, Good, Bad, Miss.
    /// </summary>
    public class ComboUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text judgmentText;
        
        [Header("Animation Settings")]
        [SerializeField] private float punchScale = 1.35f;
        [SerializeField] private float punchDuration = 0.12f;
        
        private Vector3 comboInitialScale;
        private Vector3 judgeInitialScale;
        private Coroutine comboCoroutine;
        private Coroutine judgeCoroutine;

        private void Awake()
        {
            if (comboText != null)
            {
                comboInitialScale = comboText.transform.localScale;
                comboText.gameObject.SetActive(false);
            }
            if (judgmentText != null)
            {
                judgeInitialScale = judgmentText.transform.localScale;
                judgmentText.gameObject.SetActive(false);
            }
        }

        public void UpdateUI(int combo, JudgmentType type)
        {
            // 1. Judgment Update & Animation
            if (judgmentText != null && type != JudgmentType.None)
            {
                judgmentText.gameObject.SetActive(true);
                judgmentText.text = type.ToString().ToUpper();
                judgmentText.color = GetJudgmentColor(type);
                
                if (gameObject.activeInHierarchy)
                {
                    if (judgeCoroutine != null) StopCoroutine(judgeCoroutine);
                    judgeCoroutine = StartCoroutine(PunchAnimation(judgmentText.transform, judgeInitialScale));
                }
            }

            // 2. Combo Update & Animation
            if (comboText != null)
            {
                if (combo > 0)
                {
                    comboText.gameObject.SetActive(true);
                    comboText.text = combo.ToString() + " COMBO";
                    
                    if (gameObject.activeInHierarchy)
                    {
                        if (comboCoroutine != null) StopCoroutine(comboCoroutine);
                        comboCoroutine = StartCoroutine(PunchAnimation(comboText.transform, comboInitialScale));
                    }
                }
                else
                {
                    comboText.gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator PunchAnimation(Transform target, Vector3 initialScale)
        {
            float elapsed = 0f;
            Vector3 startScale = initialScale * punchScale;
            target.localScale = startScale;
            
            while (elapsed < punchDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / punchDuration;
                target.localScale = Vector3.Lerp(startScale, initialScale, t);
                yield return null;
            }
            
            target.localScale = initialScale;
        }

        private Color GetJudgmentColor(JudgmentType type)
        {
            return type switch
            {
                JudgmentType.Perfect => Color.cyan,
                JudgmentType.Excellent => Color.green,
                JudgmentType.Good => Color.yellow,
                JudgmentType.Bad => new Color(0.6f, 0f, 0.6f), // Purple for Bad
                JudgmentType.Miss => Color.red,
                _ => Color.white
            };
        }
    }
}
