using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuCanvas;

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

        if (!SceneManager.GetSceneByName("MenuScene").isLoaded)
        {
            SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
        }
        else
        {
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("MenuScene"));
        }

    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
