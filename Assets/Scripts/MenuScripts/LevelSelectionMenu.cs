using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

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
        LoadLevel("LevelPlane");
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

    void LoadLevel(string sceneName)
    {
        GameState.IsGameActive = true;
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
