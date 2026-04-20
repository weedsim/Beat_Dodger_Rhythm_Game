using UnityEngine;
using TMPro;
using System.Collections;

namespace BeatDodger.Game
{
    
    
    
    
    public class ScoreUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text scoreText;

        [Header("Animation Settings")]
        [SerializeField] private float countDuration = 0.5f;
        [SerializeField] private float punchScale = 1.15f;
        [SerializeField] private float punchDuration = 0.1f;
        
        private long displayedScore = 0;
        private long targetScore = 0;
        private Coroutine countCoroutine;
        private Coroutine punchCoroutine;
        
        private Vector3 initialScale;

        private void Awake()
        {
            if (scoreText != null)
            {
                initialScale = scoreText.transform.localScale;
                scoreText.text = "0";
            }
        }

        public void UpdateScore(long newTotalScore)
        {
            targetScore = newTotalScore;

            
            if (countCoroutine != null) StopCoroutine(countCoroutine);
            if (gameObject.activeInHierarchy) countCoroutine = StartCoroutine(CountToScore());

            
            if (gameObject.activeInHierarchy)
            {
                if (punchCoroutine != null) StopCoroutine(punchCoroutine);
                punchCoroutine = StartCoroutine(PunchAnimation());
            }
        }

        private IEnumerator CountToScore()
        {
            long startScore = displayedScore;
            float elapsed = 0f;

            while (elapsed < countDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / countDuration;
                
                t = t * t * (3f - 2f * t);
                
                displayedScore = (long)Mathf.Lerp(startScore, targetScore, t);
                UpdateText();
                yield return null;
            }

            displayedScore = targetScore;
            UpdateText();
        }

        private IEnumerator PunchAnimation()
        {
            float elapsed = 0f;
            Vector3 startScale = initialScale * punchScale;
            
            while (elapsed < punchDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / punchDuration;
                scoreText.transform.localScale = Vector3.Lerp(startScale, initialScale, t);
                yield return null;
            }
            
            scoreText.transform.localScale = initialScale;
        }

        private void UpdateText()
        {
            if (scoreText != null)
            {
                
                scoreText.text = displayedScore.ToString("N0");
            }
        }
    }
}
