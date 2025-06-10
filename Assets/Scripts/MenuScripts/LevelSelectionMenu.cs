using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class LevelSelectionMenu : MonoBehaviour
{
    public GameObject mainMenuCanvas;
    public GameObject levelSelectionCanvas;

    public Button backButton;
    public Button planeButton;
    public Button carButton;
    public Button manZenButton;
    public Button manInteractiveButton;

    private string currentLevelScene = "";
    void Start()
    {
        planeButton.onClick.AddListener(StartGamePlane);
        carButton.onClick.AddListener(StartGameCar);
        manZenButton.onClick.AddListener(StartGameManZen);
        manInteractiveButton.onClick.AddListener(StartGameManInteractive);
        backButton.onClick.AddListener(ReturnToMainMenu);
    }

    void StartGamePlane()
    {
        LoadLevel("PlaneCourse");
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
        
    }
    // loads each level individually by name of the scene
    void LoadLevel(string sceneName)
    {
        GameState.IsGameActive = true;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void ReturnToMainMenu()
    {
        levelSelectionCanvas.SetActive(false);
        mainMenuCanvas.SetActive(true);
    }
}
