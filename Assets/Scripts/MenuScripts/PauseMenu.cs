using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuCanvas;
    public GameObject mainMenuCanvas;

    public Button resumeButton;
    public Button returnToMenuButton;
    public Button quitButton;
    void Start()
    {
        resumeButton.onClick.AddListener(Resume);
        quitButton.onClick.AddListener(QuitGame);
        returnToMenuButton.onClick.AddListener(LoadMainMenu);
        pauseMenuCanvas.SetActive(false);
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Pause();
        }
    }
    public void Pause()
    {
        GameState.IsGameActive = false;
        pauseMenuCanvas.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Resume()
    {
        pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameState.IsGameActive = true;
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        GameState.IsGameActive = false;
        pauseMenuCanvas.SetActive(false);
        mainMenuCanvas.SetActive(true);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name != "MenuScene")
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
