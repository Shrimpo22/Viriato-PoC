using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class SceneFader : MonoBehaviour
{
    CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    public IEnumerator FadeOut(float duration)
    {
        cg.blocksRaycasts = true;
        cg.interactable = true;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    public IEnumerator FadeIn(float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            cg.alpha = 1f - Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }
}
