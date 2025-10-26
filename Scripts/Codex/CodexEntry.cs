using UnityEngine;

[CreateAssetMenu(fileName = "NewCodexEntry", menuName = "Codex/Entry")]
public class CodexEntry : ScriptableObject
{
    [Header("Identity")]
    public string entryId;
    public string displayName;
    [TextArea(4, 12)] public string description;

    [Header("Preview")]
    public GameObject modelPrefab;
    public Vector3 previewOffset = Vector3.zero;           
    public Vector3 previewRotationEuler = new Vector3(0,180,0);

    [Header("Unlocking")]
    public bool unlockedByDefault = false;
}
