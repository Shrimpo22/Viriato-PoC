using UnityEngine;

[DisallowMultipleComponent]
public class PickupGlow : MonoBehaviour
{
    [Header("Emission Pulse")]
    [Tooltip("Base emissive color (HDR).")]
    public Color baseEmission = new Color(1f, 0.85f, 0.4f, 1f);
    [Tooltip("Emission intensity at pulse peak.")]
    public float peakIntensity = 6f;
    [Tooltip("Emission intensity at pulse trough.")]
    public float minIntensity = 2f;
    [Tooltip("Pulse speed (Hz).")]
    public float pulseSpeed = 1.2f;

    [Header("Idle Motion (optional)")]
    public bool bobAndRotate = true;
    public float bobAmplitude = 0.05f;
    public float bobSpeed = 1.0f;
    public float rotateSpeed = 30f; 

    [Header("Renderer Targeting")]
    [Tooltip("Leave empty to auto-grab all child renderers.")]
    public Renderer[] targetRenderers;

    [Header("Stop Behavior")]
    [Tooltip("Fade out the glow after pickup (seconds).")]
    public float fadeOutTime = 0.25f;
    [Tooltip("Disable renderers after fade.")]
    public bool hideMeshesAfter = false;

    private MaterialPropertyBlock _mpb;
    private int _emissionColorId;
    private float _t0;
    private Vector3 _startPos;
    private bool _active = true;
    private float _currentMultiplier = 1f;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _emissionColorId = Shader.PropertyToID("_EmissionColor");
        _t0 = Time.time;
        _startPos = transform.localPosition;

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        var lp = GetComponent<LorePickup>();
        if (lp != null) lp.onPickedUp.AddListener(StopGlow);

        foreach (var r in targetRenderers)
        {
            if (!r) continue;
            foreach (var mat in r.sharedMaterials)
            {
                if (!mat) continue;
                if (!mat.IsKeywordEnabled("_EMISSION"))
                    mat.EnableKeyword("_EMISSION");
            }
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (_active)
        {
            float t = (Time.time - _t0) * pulseSpeed;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
            float intensity = Mathf.Lerp(minIntensity, peakIntensity, pulse) * _currentMultiplier;
            ApplyEmission(baseEmission, intensity);

            if (bobAndRotate)
            {
                float y = Mathf.Sin((Time.time - _t0) * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
                transform.localPosition = _startPos + new Vector3(0, y, 0);
                transform.Rotate(0f, rotateSpeed * dt, 0f, Space.Self);
            }
        }
    }

    private void ApplyEmission(Color color, float intensity)
    {
        Color emi = color * Mathf.LinearToGammaSpace(intensity);
        foreach (var r in targetRenderers)
        {
            if (!r) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionColorId, emi);
            r.SetPropertyBlock(_mpb);
        }
    }

    public void StopGlow()
    {
        if (!_active) return;
        _active = false;
        StartCoroutine(FadeOutAndMaybeHide());
    }

    private System.Collections.IEnumerator FadeOutAndMaybeHide()
    {
        float t = 0f;
        float start = 1f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            _currentMultiplier = Mathf.Lerp(start, 0f, fadeOutTime <= 0f ? 1f : t / fadeOutTime);
            float intensity = Mathf.Lerp(minIntensity, peakIntensity, 0.5f) * _currentMultiplier;
            ApplyEmission(baseEmission, intensity);
            yield return null;
        }
        _currentMultiplier = 0f;
        ApplyEmission(baseEmission, 0f);

        if (hideMeshesAfter)
        {
            foreach (var r in targetRenderers)
                if (r) r.enabled = false;
        }
        enabled = false;
    }
}
