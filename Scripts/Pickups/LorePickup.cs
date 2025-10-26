using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LorePickup : MonoBehaviour
{
    [Header("Codex")]
    [Tooltip("CodexEntry asset to unlock when picked up.")]
    public CodexEntry entry;
    [Tooltip("CodexUI in your pause canvas (auto-found if empty).")]
    public CodexUI codexUI;

    [Header("Pickup Filter")]
    [Tooltip("Only colliders on these layers can trigger.")]
    public LayerMask triggerLayers = ~0;
    [Tooltip("Optional tag filter (leave empty to ignore).")]
    public string playerTag = "PlayerCollider";

    [Header("Interaction")]
    [Tooltip("If true, player must press the Interact input while inside the trigger.")]
    public bool requireInteractPress = true;
    [Tooltip("Legacy Input Manager key (fallback).")]
    public KeyCode legacyInteractKey = KeyCode.E;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("New Input System action used for Interact.")]
    public InputActionReference interactAction;
#endif

    [Header("Behavior")]
    [Tooltip("Destroy this pickup after collecting.")]
    public bool destroyOnPickup = true;

    [Header("Feedback")]
    [Tooltip("Sound played at pickup position.")]
    public AudioClip pickupSound;
    [Tooltip("VFX spawned at pickup position (auto-destroyed).")]
    public GameObject pickupVFX;
    [Tooltip("Invoked after the entry is unlocked.")]
    public UnityEvent onPickedUp;

    [Header("Disable On Pickup")]
    [Tooltip("These GameObjects will be SetActive(false) after pickup.")]
    public GameObject[] disableGameObjects;
    [Tooltip("Seconds to wait before disabling.")]
    public float disableDelay = 0f;

    bool _consumed;
    bool _canInteract;
    GameObject _currentInteractor;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!entry) Debug.LogWarning($"{name}: LorePickup has no CodexEntry assigned.", this);
    }
#endif

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (interactAction != null) interactAction.action.Enable();
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (interactAction != null) interactAction.action.Disable();
#endif
    }

    void Update()
    {
        if (_consumed || !_canInteract) return;

        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        if (interactAction != null) pressed |= interactAction.action.WasPerformedThisFrame();
#endif
        pressed |= Input.GetKeyDown(legacyInteractKey);
        if (!requireInteractPress) pressed = true;

        if (pressed && _currentInteractor) DoPickup();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;
        if (!IsAllowed(other.gameObject)) return;
        _currentInteractor = other.gameObject;
        _canInteract = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject != _currentInteractor) return;
        _canInteract = false;
        _currentInteractor = null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_consumed) return;
        if (!IsAllowed(other.gameObject)) return;
        _currentInteractor = other.gameObject;
        _canInteract = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject != _currentInteractor) return;
        _canInteract = false;
        _currentInteractor = null;
    }

    bool IsAllowed(GameObject go)
    {
        if ((triggerLayers.value & (1 << go.layer)) == 0) return false;
        if (!string.IsNullOrEmpty(playerTag) && !go.CompareTag(playerTag)) return false;
        return true;
    }

    void DoPickup()
    {
        if (_consumed) return;
        _consumed = true;

        if (!codexUI) codexUI = FindObjectOfType<CodexUI>(true);
        if (codexUI && entry && !string.IsNullOrEmpty(entry.entryId)) codexUI.Unlock(entry.entryId);
        else Debug.LogWarning($"LorePickup on '{name}' could not unlock: CodexUI or CodexEntry missing.", this);

        if (pickupSound) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupVFX)
        {
            var vfx = Instantiate(pickupVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 5f);
        }

        onPickedUp?.Invoke();
        StartCoroutine(DisableAfterDelay());

        if (destroyOnPickup) Destroy(gameObject);
        else { _canInteract = false; _currentInteractor = null; }
    }

    System.Collections.IEnumerator DisableAfterDelay()
    {
        if (disableDelay > 0f) yield return new WaitForSeconds(disableDelay);
        if (disableGameObjects == null) yield break;
        for (int i = 0; i < disableGameObjects.Length; i++)
        {
            var go = disableGameObjects[i];
            if (go) go.SetActive(false);
        }
    }
}
