using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

public class SequenceSpawner : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] string enemyTag = "Enemy";
    [SerializeField] Transform[] spawnPoints;          
    [SerializeField] Transform fallbackSpawnPoint;      
    [SerializeField] int totalToSpawn = 3;

    [Header("Polling")]
    [SerializeField] float checkInterval = 0.5f;     
    [SerializeField] float firstSpawnDelay = 0.25f;   

    [Header("Codex (unlock on completion)")]
    [SerializeField] CodexEntry codexEntryToUnlock;
    [SerializeField] CodexUI codexUI;                

    [Header("Fade → Load")]
    [SerializeField] string nextSceneName = "YourNextScene";
    [SerializeField] float fadeDuration = 0.75f;
    [SerializeField] float holdBlack = 0.1f;
    [SerializeField] SceneFader fader;                

    [Header("Events")]
    public UnityEvent onEncounterStart;
    public UnityEvent onEncounterComplete;

    int _spawned;
    bool _completed;
    Transform _lastSpawn;

    void OnEnable()
    {
        if (!codexUI) codexUI = FindObjectOfType<CodexUI>(true);
        _spawned = 0;
        _completed = false;
        onEncounterStart?.Invoke();
        StartCoroutine(RunLoop());
    }

    IEnumerator RunLoop()
    {
        if (firstSpawnDelay > 0) yield return new WaitForSeconds(firstSpawnDelay);

        while (true)
        {
            int alive = 0;
            var arr = GameObject.FindGameObjectsWithTag(enemyTag);
            if (arr != null) alive = arr.Length;

            if (alive == 0)
            {
                if (_spawned < totalToSpawn)
                {
                    SpawnOne();
                }
                else
                {
                    if (!_completed)
                    {
                        _completed = true;

                        if (codexUI && codexEntryToUnlock && !string.IsNullOrEmpty(codexEntryToUnlock.entryId))
                            codexUI.Unlock(codexEntryToUnlock.entryId);

                        onEncounterComplete?.Invoke();
                        StartCoroutine(FadeAndLoad());
                    }
                    yield break;
                }
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    void SpawnOne()
    {
        if (!enemyPrefab)
        {
            Debug.LogError($"{name}: enemyPrefab not assigned.", this);
            return;
        }

        var p = GetRandomSpawnPoint();
        if (!p)
        {
            Debug.LogError($"{name}: No spawn point found (fill Spawn Points or Fallback).", this);
            return;
        }

        Instantiate(enemyPrefab, p.position, p.rotation);
        _spawned++;
    }

    Transform GetRandomSpawnPoint()
    {
        var list = new List<Transform>();
        if (spawnPoints != null) foreach (var t in spawnPoints) if (t) list.Add(t);
        if (list.Count == 0 && fallbackSpawnPoint) list.Add(fallbackSpawnPoint);
        if (list.Count == 0) return null;

        Transform choice;
        if (list.Count == 1) choice = list[0];
        else
        {
            int tries = 3;
            do { choice = list[Random.Range(0, list.Count)]; }
            while (choice == _lastSpawn && --tries > 0);
        }
        _lastSpawn = choice;
        return choice;
    }

    IEnumerator FadeAndLoad()
    {
        if (!fader) fader = EnsureRuntimeFader();
        var oldScale = Time.timeScale; Time.timeScale = 1f;

        yield return fader.FadeOut(fadeDuration);
        if (holdBlack > 0f) yield return new WaitForSecondsRealtime(holdBlack);

        var op = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        yield return op;

        Time.timeScale = oldScale;
    }

    SceneFader EnsureRuntimeFader()
    {
        var root = new GameObject("SceneFader_RuntimeCanvas", typeof(Canvas), typeof(CanvasGroup));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        var imgGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(root.transform, false);
        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        imgGO.GetComponent<Image>().color = Color.black;

        var sf = root.AddComponent<SceneFader>();
        root.GetComponent<CanvasGroup>().alpha = 0f;
        return sf;
    }
}
