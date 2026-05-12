using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace BeatDodger.UI
{
    public class TitleManager : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button startButton;
        [SerializeField] private CanvasGroup transitionOverlay;
        [SerializeField] private float transitionDuration = 1.0f;

        [Header("Scene Names")]
        [SerializeField] private string tutorialSceneName = "Tutorial";
        [SerializeField] private string lobbySceneName = "2.Lobby";

        private void Start()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
            }

            if (transitionOverlay != null)
            {
                transitionOverlay.alpha = 1f;
                transitionOverlay.DOFade(0f, transitionDuration).OnComplete(() =>
                {
                    transitionOverlay.blocksRaycasts = false;
                });
            }
        }

        private void OnStartButtonClicked()
        {
            // Check if first time
            bool isFirstTime = PlayerPrefs.GetInt("IsFirstTime", 1) == 1;

            string nextScene = isFirstTime ? tutorialSceneName : lobbySceneName;

            if (transitionOverlay != null)
            {
                transitionOverlay.blocksRaycasts = true;
                transitionOverlay.DOFade(1f, transitionDuration).OnComplete(() =>
                {
                    SceneManager.LoadScene(nextScene);
                });
            }
            else
            {
                SceneManager.LoadScene(nextScene);
            }
        }
    }
}
