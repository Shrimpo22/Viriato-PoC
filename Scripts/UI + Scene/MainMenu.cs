using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] string gameplaySceneName = "Gameplay";

    [Header("Toggle Groups")]
    [SerializeField] GameObject[] showOnPlay;
    [SerializeField] GameObject[] hideOnPlay;

    [Header("Loading UI (optional)")]
    [SerializeField] Slider progressBar;
    [SerializeField] TMP_Text percentLabel;
    [SerializeField] RectTransform spinner;
    [SerializeField] CanvasGroup fadeOverlay;

    [Header("Shader Warmup")]
    [SerializeField] ShaderVariantCollection[] variantCollections;
    [SerializeField] bool warmupShaders = true;

    [Header("Timings")]
    [SerializeField] float minShowTime = 0.5f;
    [SerializeField] float fadeTime = 0.2f;
    [SerializeField] float readyTimeout = 20f;

    AsyncOperation loadOp;
    bool isLoading;

    public void Play()
    {
        if (isLoading) return;
        isLoading = true;
        Time.timeScale = 1f;

        foreach (var go in showOnPlay) if (go) go.SetActive(true);
        foreach (var go in hideOnPlay) if (go) go.SetActive(false);

        if (fadeOverlay) { fadeOverlay.gameObject.SetActive(true); fadeOverlay.alpha = 0f; }
        StartCoroutine(LoadFlow());
    }

    void Update()
    {
        if (isLoading && spinner) spinner.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
    }

    IEnumerator LoadFlow()
    {
        if (fadeOverlay) yield return Fade(1f, fadeTime);

        float start = Time.unscaledTime;

        loadOp = SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
        {
            float p = Mathf.Clamp01(loadOp.progress / 0.9f);
            if (progressBar) progressBar.value = p;
            if (percentLabel) percentLabel.text = $"Loading… {Mathf.RoundToInt(p * 100f)}%";
            yield return null;
        }

        if (warmupShaders && variantCollections != null)
        {
            for (int i = 0; i < variantCollections.Length; i++)
            {
                var svc = variantCollections[i];
                if (svc != null && !svc.isWarmedUp) svc.WarmUp();
                yield return null;
            }
        }

        if (progressBar) progressBar.value = 1f;
        if (percentLabel) percentLabel.text = "Finalizing…";

        loadOp.allowSceneActivation = true;
        while (!loadOp.isDone) yield return null;

        var gameplay = SceneManager.GetSceneByName(gameplaySceneName);
        if (gameplay.IsValid()) SceneManager.SetActiveScene(gameplay);

        float t0 = Time.realtimeSinceStartup;
        while (GameObject.FindWithTag("Player") == null) yield return null;

        float elapsed = Time.unscaledTime - start;
        if (elapsed < minShowTime) yield return new WaitForSecondsRealtime(minShowTime - elapsed);

        if (fadeOverlay) yield return Fade(0f, fadeTime);

        var menu = SceneManager.GetSceneByName("MainMenu");
        if (menu.IsValid()) SceneManager.UnloadSceneAsync(menu);
    }

    IEnumerator Fade(float target, float duration)
    {
        if (!fadeOverlay) yield break;
        float start = fadeOverlay.alpha, t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        fadeOverlay.alpha = target;
    }
}
