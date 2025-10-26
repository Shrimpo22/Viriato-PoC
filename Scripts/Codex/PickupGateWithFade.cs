using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class PickupGateWithFade : MonoBehaviour
{
    [Header("Required item IDs")]
    public List<string> requiredIds = new List<string> { "item_a", "item_b" };

    [Header("Load")]
    public string nextSceneName = "NextScene";
    public LoadSceneMode loadMode = LoadSceneMode.Single;

    [Header("Fade → Load")]
    public float fadeDuration = 0.75f;
    public float holdBlack = 0.1f;
    public SceneFader fader;

    readonly HashSet<string> _collected = new HashSet<string>();
    bool _loading;

    public void OnPickedId(string id)
    {
        if (_loading || string.IsNullOrEmpty(id)) return;
        if (requiredIds.Contains(id)) _collected.Add(id);
        TryGo();
    }

    public void OnPickedEntry(CodexEntry entry)
    {
        if (!entry) return;
        OnPickedId(entry.entryId);
    }

    void TryGo()
    {
        if (_loading) return;
        if (requiredIds.Count > 0 && _collected.Count >= requiredIds.Count)
            StartCoroutine(FadeAndLoad());
    }

    IEnumerator FadeAndLoad()
    {
        _loading = true;

        if (!fader) fader = EnsureRuntimeFader();
        var oldScale = Time.timeScale; Time.timeScale = 1f;

        yield return fader.FadeOut(fadeDuration);
        if (holdBlack > 0f) yield return new WaitForSecondsRealtime(holdBlack);

        var op = SceneManager.LoadSceneAsync(nextSceneName, loadMode);
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
