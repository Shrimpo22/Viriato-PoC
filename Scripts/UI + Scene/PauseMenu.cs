using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject codexRoot;
    [SerializeField] private GameObject mainPausePanel;

    [SerializeField] private List<GameObject> gameObjectsToDisable ;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button codexButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backFromCodexButton;
    [SerializeField] private Button firstPauseFocus;

    private bool isPaused;

    private void Awake()
    {
        if (pauseRoot) pauseRoot.SetActive(false);
        if (codexRoot) codexRoot.SetActive(false);
        if (mainPausePanel) mainPausePanel.SetActive(false);

        if (resumeButton) resumeButton.onClick.AddListener(Resume);
        if (codexButton) codexButton.onClick.AddListener(OpenCodex);
        if (quitButton)  quitButton.onClick.AddListener(QuitGame);
        if (backFromCodexButton) backFromCodexButton.onClick.AddListener(BackToPauseMenu);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isPaused) Pause();
            else if (codexRoot && codexRoot.activeSelf) BackToPauseMenu();
            else Resume();
        }
    }

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;
        gameObjectsToDisable.ForEach(obj => obj.SetActive(false));
        if (pauseRoot) pauseRoot.SetActive(true);
        if (codexRoot) codexRoot.SetActive(false);
        if (mainPausePanel) mainPausePanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (firstPauseFocus)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstPauseFocus.gameObject);
        }
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        gameObjectsToDisable.ForEach(obj => obj.SetActive(true));
        if (pauseRoot) pauseRoot.SetActive(false);
        if (codexRoot) codexRoot.SetActive(false);
        if (mainPausePanel) mainPausePanel.SetActive(false);
    }

    private void OpenCodex()
    {
        if (mainPausePanel) mainPausePanel.SetActive(false);
        if (codexRoot) codexRoot.SetActive(true);
    }

    private void BackToPauseMenu()
    {
        if (codexRoot) codexRoot.SetActive(false);
        if (mainPausePanel) mainPausePanel.SetActive(true);
    }

    private void OnDisable()
    {
        if (isPaused) Time.timeScale = 1f;
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
