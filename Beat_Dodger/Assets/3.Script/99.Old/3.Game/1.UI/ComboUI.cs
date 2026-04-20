using UnityEngine;
using TMPro;
using System.Collections;
using BeatDodger.UI;

namespace BeatDodger.Game
{
    public class ComboUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FakeNeonTextController comboNeonController;
        [SerializeField] private TMP_Text judgmentText;
        
        [Header("Font Assets per Judgment")]
        [SerializeField] private TMP_FontAsset _perfectFont;
        [SerializeField] private TMP_FontAsset _excellentFont;
        [SerializeField] private TMP_FontAsset _goodFont;
        [SerializeField] private TMP_FontAsset _badFont;
        [SerializeField] private TMP_FontAsset _missFont;

        [Header("Animation Settings")]
        [SerializeField] private float punchScale = 1.35f;
        [SerializeField] private float punchDuration = 0.12f;
        
        private Vector3 comboInitialScale;
        private Vector3 judgeInitialScale;
        private Coroutine comboCoroutine;
        private Coroutine judgeCoroutine;

        private void Awake()
        {
            if (comboNeonController != null)
            {
                comboInitialScale = comboNeonController.transform.localScale;
                comboNeonController.gameObject.SetActive(false);
            }
            if (judgmentText != null)
            {
                judgeInitialScale = judgmentText.transform.localScale;
                judgmentText.gameObject.SetActive(false);
            }
        }

        public void UpdateUI(int combo, JudgmentType type)
        {
            
            TMP_FontAsset targetFont = type switch
            {
                JudgmentType.Perfect => _perfectFont,
                JudgmentType.Excellent => _excellentFont,
                JudgmentType.Good => _goodFont,
                JudgmentType.Bad => _badFont,
                JudgmentType.Miss => _missFont,
                _ => _perfectFont
            };

            
            if (judgmentText != null && type != JudgmentType.None)
            {
                judgmentText.gameObject.SetActive(true);
                judgmentText.font = targetFont; 
                judgmentText.text = type.ToString().ToUpper();
                
                Color judgeColor = GetJudgmentColor(type);
                judgmentText.color = judgeColor;
                
                if (gameObject.activeInHierarchy)
                {
                    if (judgeCoroutine != null) StopCoroutine(judgeCoroutine);
                    judgeCoroutine = StartCoroutine(PunchAnimation(judgmentText.transform, judgeInitialScale));
                }
            }

            
            if (comboNeonController != null)
            {
                if (combo > 0)
                {
                    comboNeonController.gameObject.SetActive(true);
                    comboNeonController.SetText(combo.ToString());
                    
                    
                    
                    float hue = (combo * 0.1f) % 1.0f; 
                    Color dynamicColor = Color.HSVToRGB(hue, 0.85f, 1.0f);
                    comboNeonController.SetColor(dynamicColor);
                    
                    if (gameObject.activeInHierarchy)
                    {
                        if (comboCoroutine != null) StopCoroutine(comboCoroutine);
                        comboCoroutine = StartCoroutine(PunchAnimation(comboNeonController.transform, comboInitialScale));
                    }
                }
                else
                {
                    comboNeonController.gameObject.SetActive(false);
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
                JudgmentType.Excellent => Color.yellow,
                JudgmentType.Good => Color.green,
                JudgmentType.Bad => new Color(0.6f, 0f, 0.6f),
                JudgmentType.Miss => Color.red,
                _ => Color.white
            };
        }
    }
}