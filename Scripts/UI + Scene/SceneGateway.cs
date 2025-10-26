using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SceneGateway : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Scene to load if player confirms.")]
    [SerializeField] private string nextSceneName = "YourNextScene";

    [Header("Trigger Filter")]
    [Tooltip("Only these layers can trigger.")]
    [SerializeField] private LayerMask triggerLayers = ~0;
    [Tooltip("Optional tag filter; leave empty to ignore.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Interaction")]
    [Tooltip("Legacy fallback key if you don't use the new Input System.")]
    [SerializeField] private KeyCode legacyInteractKey = KeyCode.E;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("New Input System action for Interact (performed to open confirm).")]
    [SerializeField] private InputActionReference interactAction;
#endif

    [Header("UI")]
    [Tooltip("Shown when player is in range: e.g., 'Press E to interact'")]
    [SerializeField] private GameObject pressPrompt;
    [Tooltip("Confirmation popup with Yes/No.")]
    [SerializeField] private CanvasGroup confirmPanel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    bool _inRange;
    bool _showing;

    void Awake()
    {
        HidePrompt();
        ClosePanelImmediate();

        if (yesButton) yesButton.onClick.AddListener(Confirm);
        if (noButton)  noButton.onClick.AddListener(Cancel);

#if ENABLE_INPUT_SYSTEM
        if (interactAction) interactAction.action.Enable();
#endif
    }

    void OnDestroy()
    {
#if ENABLE_INPUT_SYSTEM
        if (interactAction) interactAction.action.Disable();
#endif
        if (yesButton) yesButton.onClick.RemoveAllListeners();
        if (noButton)  noButton.onClick.RemoveAllListeners();
    }

    void Update()
    {
        if (!_inRange || _showing) return;

        bool pressed = Input.GetKeyDown(legacyInteractKey);
#if ENABLE_INPUT_SYSTEM
        if (interactAction) pressed |= interactAction.action.WasPerformedThisFrame();
#endif
        if (pressed) OpenPanel();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsAllowed(other.gameObject)) return;
        _inRange = true;
        ShowPrompt();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsAllowed(other.gameObject)) return;
        _inRange = false;
        HidePrompt();
        ClosePanelImmediate();
    }

    // --- helpers ---

    bool IsAllowed(GameObject go)
    {
        if ((triggerLayers.value & (1 << go.layer)) == 0) return false;
        if (!string.IsNullOrEmpty(playerTag) && !go.CompareTag(playerTag)) return false;
        return true;
    }

    void ShowPrompt()  { if (pressPrompt) pressPrompt.SetActive(true); }
    void HidePrompt()  { if (pressPrompt) pressPrompt.SetActive(false); }

    void OpenPanel()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _showing = true;
        HidePrompt();
        if (!confirmPanel) return;
        confirmPanel.alpha = 1f;
        confirmPanel.interactable = true;
        confirmPanel.blocksRaycasts = true;
    }

    void ClosePanelImmediate()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _showing = false;
        if (!confirmPanel) return;
        confirmPanel.alpha = 0f;
        confirmPanel.interactable = false;
        confirmPanel.blocksRaycasts = false;
    }

    void Confirm() { ClosePanelImmediate(); StartCoroutine(LoadAsync()); }

    System.Collections.IEnumerator LoadAsync()
    {
        var op = SceneManager.LoadSceneAsync(nextSceneName);
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
    }

    void Cancel()
    {
        ClosePanelImmediate();
        if (_inRange) ShowPrompt();
    }
}
