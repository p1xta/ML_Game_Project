using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class LevelSelectionMenu : MonoBehaviour
{
    public GameObject mainMenuCanvas;
    public GameObject levelSelectionCanvas;

    public Button backButton;
    public Button boatButton;
    public Button carButton;
    public Button manZenButton;
    public Button manInteractiveButton;

    private string currentLevelScene = "";
    void Start()
    {
        boatButton.onClick.AddListener(StartGameBoat);
        carButton.onClick.AddListener(StartGameCar);
        manZenButton.onClick.AddListener(StartGameManZen);
        manInteractiveButton.onClick.AddListener(StartGameManInteractive);
        backButton.onClick.AddListener(ReturnToMainMenu);
    }

    void StartGameBoat()
    {
        LoadLevel("LevelBoat");
    }

    void StartGameCar()
    {
        LoadLevel("LevelCar");
    }

    void StartGameManZen()
    {
        LoadLevel("LevelManZen");
    }

    void StartGameManInteractive()
    {
        LoadLevel("LevelManInteractive");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LoadLevel(string levelSceneName)
    {
        GameState.IsGameActive = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!string.IsNullOrEmpty(currentLevelScene) && SceneManager.GetSceneByName(currentLevelScene).isLoaded)
        {
            SceneManager.UnloadSceneAsync(currentLevelScene);
        }

        if (!SceneManager.GetSceneByName(levelSceneName).isLoaded)
        {
            SceneManager.LoadScene(levelSceneName, LoadSceneMode.Additive);
            currentLevelScene = levelSceneName;
        }

        mainMenuCanvas.SetActive(false);
        levelSelectionCanvas.SetActive(false);
    }
    public void ReturnToMainMenu()
    {
        levelSelectionCanvas.SetActive(false);
        mainMenuCanvas.SetActive(true);
    }
}
