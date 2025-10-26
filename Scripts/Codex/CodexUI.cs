using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CodexUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CodexDatabase database;
    [SerializeField] private List<string> unlockedEntryIds = new();

    [Header("Left Column (List)")]
    [SerializeField] private Transform listContent;
    [SerializeField] private Button listItemButtonPrefab;

    [Header("Right Column (Details)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Center (Preview)")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform previewStage;
    [SerializeField] private RenderTexture previewRT;

    [Header("Camera Fit Settings")]
    [Tooltip("0.90 = object fills 90% of view; higher = closer.")]
    [SerializeField, Range(0.60f, 0.99f)] private float viewFill = 0.92f;
    [Tooltip("Clamp the camera distance when framing (Perspective only).")]
    [SerializeField] private Vector2 distanceClamp = new Vector2(0.25f, 200f);
    [Tooltip("Safety gap from near plane, as a multiple of object radius.")]
    [SerializeField] private float nearPlaneSafety = 1.05f;
    [SerializeField] private bool forceCameraLookAtStage = true;
    [SerializeField] private PreviewOrbit previewOrbit;

    private readonly List<GameObject> spawnedListItems = new();
    private GameObject spawnedPreviewModel;

    private void OnEnable()
    {
        BuildList();
        var first = GetUnlockedEntries();
        if (first.Count > 0) SelectEntry(first[0]); else ClearDetail();
    }

    public void Unlock(string entryId)
    {
        if (!unlockedEntryIds.Contains(entryId)) unlockedEntryIds.Add(entryId);
    }

    private List<CodexEntry> GetUnlockedEntries()
    {
        var result = new List<CodexEntry>();
        if (!database) return result;
        foreach (var e in database.entries)
        {
            if (!e) continue;
            if (e.unlockedByDefault || unlockedEntryIds.Contains(e.entryId)) result.Add(e);
        }
        return result;
    }

    private void BuildList()
    {
        foreach (var go in spawnedListItems) Destroy(go);
        spawnedListItems.Clear();

        var entries = GetUnlockedEntries();
        entries.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal));

        foreach (var entry in entries)
        {
            var btn = Instantiate(listItemButtonPrefab, listContent);
            spawnedListItems.Add(btn.gameObject);

            var tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp) tmp.text = entry.displayName;
            else
            {
                var legacy = btn.GetComponentInChildren<Text>();
                if (legacy) legacy.text = entry.displayName;
            }

            btn.onClick.AddListener(() => SelectEntry(entry));
        }
    }

    private void SelectEntry(CodexEntry entry)
    {
        if (!entry) return;
        if (titleText) titleText.text = entry.displayName;
        if (descriptionText) descriptionText.text = entry.description;
        RefreshPreview(entry);
    }

    private void RefreshPreview(CodexEntry entry)
    {
        if (spawnedPreviewModel) Destroy(spawnedPreviewModel);
        if (previewImage && previewRT) previewImage.texture = previewRT;

        Vector3 frameCenter = previewStage ? previewStage.position : Vector3.zero;
        float framedDistance = 1f;

        if (entry.modelPrefab && previewStage)
        {
            spawnedPreviewModel = Instantiate(entry.modelPrefab);
            var t = spawnedPreviewModel.transform;
            t.localPosition = frameCenter;
        

            CenterByRendererBounds(t);
            t.localPosition += entry.previewOffset;

            FrameCameraToObject(previewCamera, t, viewFill, nearPlaneSafety, distanceClamp, out frameCenter, out framedDistance);
        }

        if (previewCamera)
        {
            if (previewOrbit)
            {
                var forwardHint = -previewCamera.transform.forward;
                previewOrbit.Init(previewCamera, frameCenter, framedDistance, forwardHint);
                var previewRect = previewImage ? (RectTransform)previewImage.transform : null;
                previewOrbit.SetInteractRect(previewRect);
            }

            previewCamera.targetTexture = previewRT;
            previewCamera.Render();
        }
    }

    private void ClearDetail()
    {
        if (titleText) titleText.text = "No Entries";
        if (descriptionText) descriptionText.text = "Unlock entries by exploring the world.";
        if (spawnedPreviewModel) Destroy(spawnedPreviewModel);
        if (previewImage) previewImage.texture = null;
    }

    // ---------- Centering & Framing ----------

    private static void CenterByRendererBounds(Transform root)
    {
        if (!root) return;
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return;

        Bounds wb = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends) { if (!r.enabled) continue; wb.Encapsulate(r.bounds); }
        var localCenter = root.InverseTransformPoint(wb.center);
        root.localPosition -= localCenter;
    }

    /// viewFill = fraction (0..1) of viewport the object should occupy; higher = closer
    private static void FrameCameraToObject(Camera cam, Transform target, float viewFill, float nearSafetyMul, Vector2 clamp,
                                            out Vector3 usedCenter, out float usedDistance)
    {
        usedCenter = target ? target.position : Vector3.zero;
        usedDistance = 1f;
        if (!cam || !target) return;
        if (!TryWorldBounds(target, out var wb)) return;

        usedCenter = wb.center;
        float padding = 1f / Mathf.Clamp(viewFill, 0.01f, 0.99f);

        if (cam.orthographic)
        {
            FitOrtho(cam, wb, padding);
            var radius = wb.extents.magnitude;
            var minDist = cam.nearClipPlane + radius * nearSafetyMul;
            var fwd = cam.transform.forward;
            cam.transform.position = usedCenter - fwd * Mathf.Max(minDist, 0.01f);
            cam.transform.LookAt(usedCenter, Vector3.up);
            usedDistance = Vector3.Distance(cam.transform.position, usedCenter);
        }
        else
        {
            float requiredDist = DistanceToFitPerspective(cam, wb, padding);
            var radius = wb.extents.magnitude;
            var minDist = cam.nearClipPlane + radius * nearSafetyMul;
            requiredDist = Mathf.Max(requiredDist, minDist);
            requiredDist = Mathf.Clamp(requiredDist, clamp.x, clamp.y);

            var fwd = cam.transform.forward;
            cam.transform.position = usedCenter - fwd * requiredDist;
            cam.transform.LookAt(usedCenter, Vector3.up);
            usedDistance = requiredDist;
        }
    }

    private static bool TryWorldBounds(Transform target, out Bounds worldBounds)
    {
        var rends = target.GetComponentsInChildren<Renderer>(true);
        worldBounds = default;
        bool hasAny = false;
        foreach (var r in rends)
        {
            if (!r.enabled) continue;
            if (!hasAny) { worldBounds = r.bounds; hasAny = true; }
            else worldBounds.Encapsulate(r.bounds);
        }
        return hasAny;
    }

    private static void FitOrtho(Camera cam, Bounds wb, float padding)
    {
        ToCameraHalfExtents(cam, wb, out var halfW, out var halfH);
        halfW *= padding; halfH *= padding;
        float sizeByHeight = halfH;
        float sizeByWidth  = halfW / Mathf.Max(0.0001f, cam.aspect);
        cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
    }

    private static float DistanceToFitPerspective(Camera cam, Bounds wb, float padding)
    {
        ToCameraHalfExtents(cam, wb, out var halfW, out var halfH);
        halfW *= padding; halfH *= padding;

        float halfVFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfHFov = Mathf.Atan(Mathf.Tan(halfVFov) * cam.aspect);

        float dByH = halfH / Mathf.Tan(halfVFov);
        float dByW = halfW / Mathf.Tan(halfHFov);
        return Mathf.Max(dByH, dByW);
    }

    private static void ToCameraHalfExtents(Camera cam, Bounds wb, out float halfWidth, out float halfHeight)
    {
        var m = cam.worldToCameraMatrix;
        Vector3 c = wb.center, e = wb.extents;

        Vector3[] corners =
        {
            new(c.x - e.x, c.y - e.y, c.z - e.z),
            new(c.x - e.x, c.y - e.y, c.z + e.z),
            new(c.x - e.x, c.y + e.y, c.z - e.z),
            new(c.x - e.x, c.y + e.y, c.z + e.z),
            new(c.x + e.x, c.y - e.y, c.z - e.z),
            new(c.x + e.x, c.y - e.y, c.z + e.z),
            new(c.x + e.x, c.y + e.y, c.z - e.z),
            new(c.x + e.x, c.y + e.y, c.z + e.z),
        };

        float maxAbsX = 0f, maxAbsY = 0f;
        for (int i = 0; i < 8; i++)
        {
            var v = m.MultiplyPoint3x4(corners[i]);
            maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(v.x));
            maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(v.y));
        }

        halfWidth  = Mathf.Max(0.0001f, maxAbsX);
        halfHeight = Mathf.Max(0.0001f, maxAbsY);
    }
}
