using UnityEngine;

public class PersistentObject : MonoBehaviour
{
    public static PersistentObject Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
