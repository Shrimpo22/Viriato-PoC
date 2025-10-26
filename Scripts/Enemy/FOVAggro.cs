using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Demo.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(MeleeAgent))]
public class FOVAggro : MonoBehaviour
{
    [Header("Master Toggle")]
    [SerializeField, Tooltip("When OFF, vision & detection logic is skipped entirely. Use SetDetectionEnabled() at runtime.")]
    private bool detectionEnabled = true;

    [Header("Targeting")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Tooltip("If true, the enemy is permanently aggroed and will always attack the player.")]
    private bool alwaysAlert = false;

    [Header("Vision")]
    [SerializeField] private float viewDistance = 12f;
    [SerializeField, Range(0, 360)] private float viewAngle = 110f;
    [SerializeField] private float checkInterval = 0.1f;

    [Header("Line of Sight")]
    [SerializeField, Tooltip("Layers that block vision (e.g., Default, Environment). Do NOT include the Player/Character layers.")]
    private LayerMask occlusionMask;

    [Header("Detection")]
    [SerializeField, Tooltip("Minimum fraction of visible rays required to start increasing detection.")]
    [Range(0f, 1f)] private float visibleFractionToRise = 0.5f;
    [SerializeField, Tooltip("Base rate the detection meter rises per second when above threshold.")]
    private float detectionRisePerSecond = 40f;
    [SerializeField, Tooltip("How fast the detection meter decays per second when below threshold.")]
    private float detectionDecayPerSecond = 20f;
    [SerializeField, Tooltip("Maximum value of the detection meter.")]
    private float detectionMax = 100f;

    [Header("Distance Scaling")]
    [SerializeField, Tooltip("Multiply detection rise based on distance (closer = faster).")]
    private bool scaleRiseByDistance = true;
    [SerializeField, Tooltip("Distance (m) at which the rise multiplier is at its maximum.")]
    private float nearDistance = 1.5f;
    [SerializeField, Tooltip("Distance (m) at which the rise multiplier is at its minimum. Leave 0 to use viewDistance.")]
    private float farDistance = 0f;
    [SerializeField, Tooltip("Minimum multiplier when far away.")]
    private float minRiseMultiplier = 0.5f;
    [SerializeField, Tooltip("Maximum multiplier when very close.")]
    private float maxRiseMultiplier = 2.0f;

    [Header("On Detection Reached Max (UI + Flow)")]
    [SerializeField, Tooltip("Show the fade-to-black 'Detected' overlay when caught.")]
    private bool showDetectedOverlay = true;
    [SerializeField, Tooltip("Text shown on the overlay.")]
    private string detectedUIMessage = "DETECTED";
    [SerializeField] private int detectedUIFontSize = 56;
    [SerializeField, Tooltip("Seconds to fade in to black.")]
    private float fadeInDuration = 0.6f;
    [SerializeField, Tooltip("Seconds to hold at black (message visible).")]
    private float holdBlackDuration = 0.8f;
    [SerializeField, Tooltip("Seconds to fade back in after respawn.")]
    private float fadeOutDuration = 0.6f;
    [SerializeField, Tooltip("If true, also reload the scene after invoking onDetected (usually leave OFF if onDetected handles respawn).")]
    private bool reloadSceneOnDetected = false;

    [Header("Events")]
    [SerializeField, Tooltip("Invoked once when detection reaches max (e.g., hook your Player.Respawn()).")]
    private UnityEvent onDetected;

    [Header("De-aggro (legacy compatibility)")]
    [SerializeField, Tooltip("How long after losing sight to stop attacking (legacy aggro).")]
    private float loseSightAfter = 2f;

    [Header("Ray Sampling")]
    [SerializeField, Tooltip("Origin for the vision rays (e.g., head/eyes). If null, uses LocalLookSource.LookTransform or transform.")]
    private Transform rayOrigin;
    [SerializeField, Tooltip("Rays on middle ring around capsule.")]
    [Range(4, 32)] private int middleRingRays = 12;
    [SerializeField, Tooltip("Add top and bottom rings for better coverage.")]
    private bool useTopBottomRings = true;
    [SerializeField, Tooltip("Rays per top/bottom ring (used only if 'useTopBottomRings' is true).")]
    [Range(4, 32)] private int topBottomRingRays = 8;

    [Header("Debug Draw")]
    [SerializeField] private bool debugDrawRays = false;
    [SerializeField, Tooltip("Seconds to keep rays visible in the Scene view.")]
    private float debugRayDuration = 0.15f;
    [SerializeField, Tooltip("Draw tiny spheres at sampled capsule points.")]
    private bool debugDrawSamplePoints = false;
    [SerializeField, Tooltip("Sphere radius for sample point gizmos.")]
    private float debugSamplePointRadius = 0.03f;
    [SerializeField, Tooltip("Show the numeric detection bar (debug).")]
    private bool debugShowBar = false;

    private Transform _player;
    private CapsuleCollider _playerCapsule;
    private LocalLookSource _lookSource;
    private MeleeAgent _melee;
    private float _lastSeenTime = float.NegativeInfinity;
    private bool _isAggro;
    private float _detection = 0f;
    private bool _firingDetectedSequence = false;

    private Canvas _overlayCanvas;
    private CanvasGroup _overlayGroup;
    private Image _blackImage;
    private Text _messageText;

    void Awake() {
        _lookSource = GetComponent<LocalLookSource>();
        _melee = GetComponent<MeleeAgent>();
        if (rayOrigin == null) {
            if (_lookSource != null && _lookSource.LookTransform != null) rayOrigin = _lookSource.LookTransform;
            else rayOrigin = transform;
        }
    }

    void Start() {
        TryAcquirePlayer();
        InvokeRepeating(nameof(CheckVisionAndDetection), Random.value * checkInterval, checkInterval);

        if (alwaysAlert && _player != null) {
            StartCoroutine(DeferredForceAggro());
        }
    }

    // ---------- Public API ----------
    public void SetDetectionEnabled(bool enabled) {
        detectionEnabled = enabled;
        if (!detectionEnabled) {
            ResetDetectionCounter(clearAggro: true);
        }
    }

    public void ResetDetectionCounter(bool clearAggro = true, bool hideOverlay = true) {
        _detection = 0f;
        _firingDetectedSequence = false;

        if (clearAggro) {
            if (_isAggro) {
                _melee.CancelAttack();
                if (_lookSource != null) _lookSource.Target = null;
                _isAggro = false;
            }
        }

        if (hideOverlay) HideOverlayImmediate();
    }

    // ---------- Core detection ----------
    private void TryAcquirePlayer() {
        if (_player == null) {
            var playerGO = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGO != null) {
                _player = playerGO.transform;
                _playerCapsule = _player.GetComponentInChildren<CapsuleCollider>();
            }
        } else if (_playerCapsule == null) {
            _playerCapsule = _player.GetComponentInChildren<CapsuleCollider>();
        }
    }

    private IEnumerator DeferredForceAggro() {
        yield return null;
        ForceAggro();
        _detection = detectionMax;
    }

    void CheckVisionAndDetection() {
        if (!detectionEnabled || _firingDetectedSequence) return;

        TryAcquirePlayer();
        if (_player == null || _playerCapsule == null) return;

        if (alwaysAlert) {
            if (!_isAggro) ForceAggro();
            else if (_lookSource != null && _lookSource.Target != _player) _lookSource.Target = _player;
            _lastSeenTime = Time.time;
            _detection = detectionMax;
            MaybeTriggerDetectedSequence();
            return;
        }

        if (!IsWithinViewCone(_player.position)) {
            ApplyDetectionDecay();
            MaybeDeAggro();
            return;
        }

        int totalRays, visibleRays;
        SampleVisibilityToCapsule(rayOrigin.position, _playerCapsule, out totalRays, out visibleRays);
        float fracVisible = (totalRays > 0) ? (visibleRays / (float)totalRays) : 0f;

        if (fracVisible >= visibleFractionToRise) {
            float baseRise = detectionRisePerSecond;

            if (scaleRiseByDistance) {
                Vector3 playerCenter = _playerCapsule.bounds.center;
                float d = Vector3.Distance(rayOrigin.position, playerCenter);
                float near = Mathf.Max(0.01f, nearDistance);
                float far = (farDistance > 0f ? farDistance : viewDistance);
                float t = Mathf.InverseLerp(far, near, d);
                float distMult = Mathf.Lerp(minRiseMultiplier, maxRiseMultiplier, t);
                baseRise *= distMult;
            }

            float visibilityBoost = Mathf.Lerp(0.5f, 1f, fracVisible);

            _detection = Mathf.Min(
                detectionMax,
                _detection + baseRise * visibilityBoost * checkInterval
            );

            _lastSeenTime = Time.time;

            if (!_isAggro) ForceAggro();
        } else {
            ApplyDetectionDecay();
        }

        MaybeDeAggro();
        MaybeTriggerDetectedSequence();
    }

    private void ApplyDetectionDecay() {
        if (_detection > 0f) {
            _detection = Mathf.Max(0f, _detection - detectionDecayPerSecond * checkInterval);
        }
    }

    private void MaybeDeAggro() {
        if (_isAggro && Time.time - _lastSeenTime > loseSightAfter) {
            _melee.CancelAttack();
            if (_lookSource != null) _lookSource.Target = null;
            _isAggro = false;
        }
    }

    private void MaybeTriggerDetectedSequence() {
        if (_firingDetectedSequence) return;
        if (_detection < detectionMax - 0.001f) return;

        _firingDetectedSequence = true;
        StartCoroutine(DetectedSequence());
    }

    // ---------- Detected Flow: Fade -> Event (respawn) -> Fade back -> Reset ----------
    private IEnumerator DetectedSequence() {
        if (_isAggro) {
            _melee.CancelAttack();
            if (_lookSource != null) _lookSource.Target = null;
            _isAggro = false;
        }

        if (showDetectedOverlay) {
            EnsureOverlay();
            yield return FadeOverlay(1f, fadeInDuration, detectedUIMessage);
            yield return new WaitForSecondsRealtime(holdBlackDuration);
        }

        if (onDetected != null) {
            try { onDetected.Invoke(); } catch (System.Exception e) { Debug.LogException(e, this); }
        }

        if (reloadSceneOnDetected) {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
            yield break;
        }

        ResetDetectionCounter(clearAggro: true, hideOverlay: false);

        if (showDetectedOverlay) {
            yield return FadeOverlay(0f, fadeOutDuration, detectedUIMessage);
            HideOverlayImmediate();
        }

        _firingDetectedSequence = false;
    }

    // ---------- Overlay creation & fading ----------
    private void EnsureOverlay() {
        if (_overlayCanvas != null) return;

        var canvasGO = new GameObject("DetectionOverlay");
        _overlayCanvas = canvasGO.AddComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.sortingOrder = 5000;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();
        _overlayGroup = canvasGO.AddComponent<CanvasGroup>();
        _overlayGroup.interactable = false;
        _overlayGroup.blocksRaycasts = false;
        _overlayGroup.alpha = 0f;

        var imgGO = new GameObject("Fade");
        imgGO.transform.SetParent(canvasGO.transform, false);
        _blackImage = imgGO.AddComponent<Image>();
        _blackImage.color = Color.black;
        var imgRT = _blackImage.rectTransform;
        imgRT.anchorMin = Vector2.zero; imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = Vector2.zero; imgRT.offsetMax = Vector2.zero;

        var txtGO = new GameObject("Message");
        txtGO.transform.SetParent(canvasGO.transform, false);
        _messageText = txtGO.AddComponent<Text>();
        _messageText.alignment = TextAnchor.MiddleCenter;
        _messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _messageText.fontSize = detectedUIFontSize;
        _messageText.color = Color.white;
        _messageText.supportRichText = true;
        _messageText.text = detectedUIMessage;
        var txtRT = _messageText.rectTransform;
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
    }

    private void HideOverlayImmediate() {
        if (_overlayGroup != null) _overlayGroup.alpha = 0f;
        if (_messageText != null) _messageText.enabled = false;
    }

    private IEnumerator FadeOverlay(float targetAlpha, float duration, string msg) {
        EnsureOverlay();
        if (_messageText != null) {
            _messageText.text = msg;
            _messageText.enabled = true;
        }
        float start = _overlayGroup.alpha;
        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            float u = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            u = u * u * (3f - 2f * u);
            _overlayGroup.alpha = Mathf.Lerp(start, targetAlpha, u);
            yield return null;
        }
        _overlayGroup.alpha = targetAlpha;
    }

    // ---------- Helpers ----------
    private bool IsWithinViewCone(Vector3 worldPos) {
        Vector3 dir = worldPos - transform.position;
        float distance = dir.magnitude;
        if (distance > viewDistance) return false;
        float angle = Vector3.Angle(transform.forward, dir.normalized);
        return angle <= (viewAngle * 0.5f);
    }

    private void SampleVisibilityToCapsule(Vector3 eye, CapsuleCollider capsule, out int totalRays, out int visibleRays) {
        totalRays = 0; visibleRays = 0;

        GetCapsuleWorldCenters(capsule, out Vector3 topCenter, out Vector3 bottomCenter, out float radius);

        List<Vector3> targets = new List<Vector3>(32);
        Vector3 mid = (topCenter + bottomCenter) * 0.5f;
        Vector3 axis = (topCenter - bottomCenter);
        float axisLen = Mathf.Max(0.0001f, axis.magnitude);
        Vector3 upAxis = axis / axisLen;

        targets.Add(topCenter);
        targets.Add(bottomCenter);

        Vector3 toMid = (mid - eye).normalized;
        Vector3 tmp = Mathf.Abs(Vector3.Dot(toMid, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 right = Vector3.Normalize(Vector3.Cross(tmp, toMid));
        Vector3 up = Vector3.Normalize(Vector3.Cross(toMid, right));


        AddRingPoints(targets, mid, right, up, radius, middleRingRays);

        if (useTopBottomRings) {
            float inset = Mathf.Min(radius * 0.5f, axisLen * 0.25f);
            Vector3 nearTop = topCenter - upAxis * inset;
            Vector3 nearBottom = bottomCenter + upAxis * inset;
            AddRingPoints(targets, nearTop, right, up, radius, topBottomRingRays);
            AddRingPoints(targets, nearBottom, right, up, radius, topBottomRingRays);
        }

        if (debugDrawSamplePoints) {
            for (int i = 0; i < targets.Count; i++) {
                DebugDrawSphere(targets[i], debugSamplePointRadius, Color.cyan, debugRayDuration);
            }
        }

        for (int i = 0; i < targets.Count; i++) {
            Vector3 target = targets[i];
            if (!IsWithinViewCone(target)) continue;

            Vector3 dir = (target - eye);
            float dist = dir.magnitude;
            if (dist > viewDistance || dist < 0.001f) continue;

            dir /= dist;
            totalRays++;

            bool blocked = Physics.Raycast(eye, dir, out RaycastHit hit, dist, occlusionMask, QueryTriggerInteraction.Ignore);
            if (!blocked) {
                visibleRays++;
            }

            if (debugDrawRays) {
                Color c = blocked ? Color.red : Color.green;
                Vector3 end = blocked ? hit.point : target;
                Debug.DrawLine(eye, end, c, debugRayDuration);
            }
        }
    }

    private static void AddRingPoints(List<Vector3> list, Vector3 center, Vector3 right, Vector3 up, float radius, int rays) {
        float step = 360f / Mathf.Max(1, rays);
        for (int k = 0; k < rays; k++) {
            float ang = step * k * Mathf.Deg2Rad;
            Vector3 offset = right * Mathf.Cos(ang) + up * Mathf.Sin(ang);
            list.Add(center + offset * radius);
        }
    }

    private static void GetCapsuleWorldCenters(CapsuleCollider col, out Vector3 top, out Vector3 bottom, out float worldRadius)
    {
        Transform t = col.transform;
        Vector3 centerLS = col.center;
        float radius = col.radius;
        float height = Mathf.Max(col.height, radius * 2f);

        Vector3 sc = t.lossyScale;
        float absX = Mathf.Abs(sc.x), absY = Mathf.Abs(sc.y), absZ = Mathf.Abs(sc.z);

        int dir = col.direction;
        Vector3 axisWS;
        float axisScale, rScaleA, rScaleB;

        if (dir == 0) { axisWS = t.right; axisScale = absX; rScaleA = absY; rScaleB = absZ; }
        else if (dir == 1) { axisWS = t.up; axisScale = absY; rScaleA = absX; rScaleB = absZ; }
        else { axisWS = t.forward; axisScale = absZ; rScaleA = absX; rScaleB = absY; }

        float rWorld = col.radius * Mathf.Max(
            Mathf.Abs(sc.y),
            Mathf.Abs(sc.z)
        );
        if (dir == 1) rWorld = col.radius * Mathf.Max(Mathf.Abs(sc.x), Mathf.Abs(sc.z));
        if (dir == 2) rWorld = col.radius * Mathf.Max(Mathf.Abs(sc.x), Mathf.Abs(sc.y));

        worldRadius = rWorld;
        float halfCyl = Mathf.Max(0f, height * (dir == 0 ? Mathf.Abs(sc.x) : (dir == 1 ? Mathf.Abs(sc.y) : Mathf.Abs(sc.z))) - 2f * worldRadius) * 0.5f;

        Vector3 centerWS = t.TransformPoint(centerLS);
        top = centerWS + (dir == 0 ? t.right : (dir == 1 ? t.up : t.forward)) * halfCyl;
        bottom = centerWS - (dir == 0 ? t.right : (dir == 1 ? t.up : t.forward)) * halfCyl;
    }

    private void ForceAggro() {
        if (_lookSource != null) _lookSource.Target = _player;
        _melee.Attack();
        _isAggro = true;
        _lastSeenTime = Time.time;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);
    }
#endif

    private void OnGUI() {
        if (!debugShowBar) return;

        const float w = 200f, h = 16f, pad = 12f;
        float pct = (_detection <= 0f) ? 0f : _detection / detectionMax;
        Rect bg = new Rect(pad, Screen.height - h - pad, w, h);
        Rect fg = new Rect(pad, Screen.height - h - pad, w * pct, h);
        Color prev = GUI.color;
        GUI.color = new Color(0,0,0,0.5f);
        GUI.Box(bg, GUIContent.none);
        GUI.color = Color.green;
        GUI.Box(fg, GUIContent.none);
        GUI.color = prev;
    }

    // ------- Debug helpers -------
    private void DebugDrawSphere(Vector3 center, float r, Color color, float duration) {
        const int seg = 12;
        float step = 2f * Mathf.PI / seg;

        Vector3 prev = center + new Vector3(Mathf.Cos(0) * r, Mathf.Sin(0) * r, 0f);
        for (int i = 1; i <= seg; i++) {
            Vector3 p = center + new Vector3(Mathf.Cos(step * i) * r, Mathf.Sin(step * i) * r, 0f);
            Debug.DrawLine(prev, p, color, duration);
            prev = p;
        }
        prev = center + new Vector3(0f, Mathf.Cos(0) * r, Mathf.Sin(0) * r);
        for (int i = 1; i <= seg; i++) {
            Vector3 p = center + new Vector3(0f, Mathf.Cos(step * i) * r, Mathf.Sin(step * i) * r);
            Debug.DrawLine(prev, p, color, duration);
            prev = p;
        }
        prev = center + new Vector3(Mathf.Cos(0) * r, 0f, Mathf.Sin(0) * r);
        for (int i = 1; i <= seg; i++) {
            Vector3 p = center + new Vector3(Mathf.Cos(step * i) * r, 0f, Mathf.Sin(step * i) * r);
            Debug.DrawLine(prev, p, color, duration);
            prev = p;
        }
    }
}
