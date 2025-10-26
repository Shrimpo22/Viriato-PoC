using UnityEngine;

public class CircleReplicator : MonoBehaviour
{
    [Header("What to place")]
    public GameObject prefab;
    [Min(1)] public int count = 8;

    [Header("Circle")]
    public float radius = 5f;
    [Range(0f, 360f)] public float startAngle = 0f;
    public bool useXZPlane = true;

    [Header("Parenting")]
    public bool parentInstances = true;

    [Header("Grounding (3D only)")]
    public bool snapToGround = false;
    public LayerMask groundLayers = ~0;
    public float raycastHeight = 20f;
    public float extraRayLen = 100f;
    public float surfaceOffset = 0.02f;
    public bool alignUpToGround = true;

    public void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    public void BuildCircle(bool clearExisting = true)
    {
        if (prefab == null || count <= 0) return;
        if (clearExisting) ClearChildren();

        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float ang = (startAngle + step * i) * Mathf.Deg2Rad;

            Vector3 localPos = useXZPlane
                ? new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius)
                : new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);

            Vector3 worldPos = transform.TransformPoint(localPos);

            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            else
                go = Instantiate(prefab);
#else
            go = Instantiate(prefab);
#endif
            if (parentInstances) go.transform.SetParent(transform, true);
            go.transform.position = worldPos;

            Vector3 center = transform.position;
            Vector3 inward = (center - worldPos);

            if (useXZPlane)
            {
                if (snapToGround)
                {
                    Vector3 rayStart = worldPos + Vector3.up * Mathf.Abs(raycastHeight);
                    float rayLen = Mathf.Abs(raycastHeight) + Mathf.Abs(extraRayLen);

                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayLen, groundLayers, QueryTriggerInteraction.Ignore))
                    {
                        go.transform.position = hit.point + hit.normal * surfaceOffset;

                        if (alignUpToGround)
                        {
                            Vector3 flatInward = Vector3.ProjectOnPlane(center - hit.point, hit.normal).normalized;
                            if (flatInward.sqrMagnitude < 1e-6f)
                                flatInward = Vector3.ProjectOnPlane(-transform.forward, hit.normal).normalized;

                            go.transform.rotation = Quaternion.LookRotation(flatInward, hit.normal);
                            continue;
                        }
                    }
                }

                inward.y = 0f;
                if (inward.sqrMagnitude > 1e-6f)
                    go.transform.rotation = Quaternion.LookRotation(inward.normalized, Vector3.up);
            }
            else
            {
                float zRot = Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg;
                go.transform.rotation = Quaternion.Euler(0f, 0f, zRot);
            }
        }
    }
}
