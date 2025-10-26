using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CodexDatabase", menuName = "Codex/Database")]
public class CodexDatabase : ScriptableObject
{
    public List<CodexEntry> entries = new();
}