using UnityEngine;

/// Drag + auto-orbit camera rig for the Codex preview.
/// - Auto orbits slowly around the pivot (uses unscaled time so it works while paused)
/// - Left-drag: orbit (yaw/pitch overrides auto)
/// - Mouse wheel: zoom (persp distance / ortho size)
/// - Middle-drag: pan (optional)
/// - R: reset to framed pose
[DisallowMultipleComponent]
public class PreviewOrbit : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;

    [Header("Orbit")]
    [SerializeField] private float orbitSpeed = 120f;
    [SerializeField] private float pitchMin = -80f;
    [SerializeField] private float pitchMax = 80f;

    [Header("Auto Orbit")]
    [SerializeField] private bool  autoOrbitEnabled = true;
    [SerializeField] private float autoOrbitSpeedDeg = 12f;
    [SerializeField] private float autoResumeAfterIdle = 2.0f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2.0f;
    [SerializeField] private Vector2 distanceClamp = new Vector2(0.15f, 200f);
    [SerializeField] private Vector2 orthoSizeClamp = new Vector2(0.05f, 50f);

    [Header("Pan")]
    [SerializeField] private bool enablePan = true;
    [SerializeField] private float panSpeed = 1.0f;

    [SerializeField] private RectTransform interactRect;
    [SerializeField] private bool restrictToInteractRect = true;

    public void SetInteractRect(RectTransform rt) { interactRect = rt; }

    private Vector3 pivot;
    private float yaw;
    private float pitch;
    private float distance;
    private float defaultDistance;
    private Vector3 defaultPivot;
    private float defaultYaw, defaultPitch, defaultOrthoSize;

    private float lastUserInputTime = -999f;

    public void Init(Camera camera, Vector3 pivotPoint, float framedDistance, Vector3 forwardHint)
    {
        cam = camera != null ? camera : cam;
        if (!cam) return;

        pivot = pivotPoint;
        defaultPivot = pivot;

        if (cam.orthographic)
        {
            defaultOrthoSize = cam.orthographicSize;
        }
        else
        {
            distance = Mathf.Clamp(framedDistance, distanceClamp.x, distanceClamp.y);
            defaultDistance = distance;
        }

        var dir = (forwardHint == Vector3.zero ? -cam.transform.forward : forwardHint).normalized;
        pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        yaw   = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        defaultYaw = yaw; defaultPitch = pitch;

        ApplyTransform();
        lastUserInputTime = Time.unscaledTime - autoResumeAfterIdle;
    }

    public void SetPivot(Vector3 p, bool snap = true) { pivot = p; if (snap) ApplyTransform(); }
    public void SetDistance(float d, bool snap = true)
    {
        if (cam && !cam.orthographic) { distance = Mathf.Clamp(d, distanceClamp.x, distanceClamp.y); if (snap) ApplyTransform(); }
    }
    public void SetYawPitch(float newYaw, float newPitch, bool snap = true)
    {
        yaw = newYaw; pitch = Mathf.Clamp(newPitch, pitchMin, pitchMax); if (snap) ApplyTransform();
    }

    private void Update()
    {
        if (!cam) return;

        bool hadUserInput = false;
        float dt = Time.unscaledDeltaTime;
        bool over = IsPointerOverInteractRect();

        if (over && Input.GetMouseButton(0))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            if (Mathf.Abs(dx) > 0.0001f || Mathf.Abs(dy) > 0.0001f)
            {
                yaw   += dx * orbitSpeed;
                pitch -= dy * orbitSpeed;
                pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
                hadUserInput = true;
            }
        }

        float wheel = Input.mouseScrollDelta.y;
        if (over && Mathf.Abs(wheel) > 0.0001f)
        {
            if (cam.orthographic)
            {
                float size = cam.orthographicSize;
                size *= Mathf.Pow(1f / zoomSpeed, wheel);
                cam.orthographicSize = Mathf.Clamp(size, orthoSizeClamp.x, orthoSizeClamp.y);
            }
            else
            {
                distance *= Mathf.Pow(1f / zoomSpeed, wheel);
                distance = Mathf.Clamp(distance, distanceClamp.x, distanceClamp.y);
            }
            hadUserInput = true;
        }

        if ( over && enablePan && Input.GetMouseButton(2))
        {
            float dx = -Input.GetAxis("Mouse X");
            float dy = -Input.GetAxis("Mouse Y");
            if (Mathf.Abs(dx) > 0.0001f || Mathf.Abs(dy) > 0.0001f)
            {
                var right = cam.transform.right;
                var up    = cam.transform.up;
                float units = panSpeed * (cam.orthographic ? cam.orthographicSize : Mathf.Max(distance, 0.0001f));
                pivot += (right * dx + up * dy) * units * 0.01f;
                hadUserInput = true;
            }
        }

        if (over && Input.GetKeyDown(KeyCode.R))
        {
            pivot = defaultPivot;
            yaw = defaultYaw; pitch = defaultPitch;
            if (cam.orthographic) cam.orthographicSize = defaultOrthoSize;
            else distance = defaultDistance;
            hadUserInput = true;
        }

        if (hadUserInput) lastUserInputTime = Time.unscaledTime;

        if (autoOrbitEnabled && (Time.unscaledTime - lastUserInputTime) >= autoResumeAfterIdle)
        {
            yaw += autoOrbitSpeedDeg * dt;
        }

        ApplyTransform();
    }

    private void ApplyTransform()
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        if (cam.orthographic)
        {
            float offset = Mathf.Max(0.01f, cam.nearClipPlane + 0.05f);
            cam.transform.position = pivot - rot * Vector3.forward * offset;
        }
        else
        {
            cam.transform.position = pivot - rot * Vector3.forward * Mathf.Max(distance, 0.01f);
        }
        cam.transform.rotation = rot;
    }

    private bool IsPointerOverInteractRect()
    {
    if (!restrictToInteractRect || !interactRect) return true;

    var canvas = interactRect.GetComponentInParent<Canvas>();
    var eventCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                   ? canvas.worldCamera
                   : null;

    return RectTransformUtility.RectangleContainsScreenPoint(
        interactRect, Input.mousePosition, eventCam);
    }
}
