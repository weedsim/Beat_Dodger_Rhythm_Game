using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BeatDodger.UI
{
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class UIInputModuleFixer : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actionsAsset;

        private void Awake()
        {
            if (_actionsAsset == null)
            {
                Debug.LogError("[UIInputModuleFixer] _actionsAsset이 비어 있습니다. InputAction.inputactions를 할당하세요.");
                return;
            }

            var module = GetComponent<InputSystemUIInputModule>();

            module.point       = Ref("UI/Point");
            module.leftClick   = Ref("UI/Click");
            module.rightClick  = Ref("UI/RightClick");
            module.middleClick = Ref("UI/MiddleClick");
            module.move        = Ref("UI/Navigate");
            module.submit      = Ref("UI/Submit");
            module.cancel      = Ref("UI/Cancel");
            module.scrollWheel = Ref("UI/ScrollWheel");

            Debug.Log("[UIInputModuleFixer] UIInputModule action 참조 복구 완료.");
        }

        private InputActionReference Ref(string path)
        {
            var action = _actionsAsset.FindAction(path);
            if (action == null)
                Debug.LogWarning($"[UIInputModuleFixer] Action을 찾을 수 없음: {path}");
            return action != null ? InputActionReference.Create(action) : null;
        }
    }
}
