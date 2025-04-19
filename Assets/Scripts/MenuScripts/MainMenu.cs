using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public GameObject mainMenuCanvas;
    public GameObject settingsCanvas;
    public GameObject levelSelectionCanvas;
    public Button startButton;
    public Button settingsButton;
    public Button exitButton;

    void Start()
    {
        mainMenuCanvas.SetActive(true);
        settingsCanvas.SetActive(false);
        levelSelectionCanvas.SetActive(false);
        GameState.IsGameActive = false;
        
        startButton.onClick.AddListener(OpenLevelSelection);
        settingsButton.onClick.AddListener(OpenSettings);
        exitButton.onClick.AddListener(ExitGame);
    }

    public void OpenLevelSelection()
    {
        mainMenuCanvas.SetActive(false);
        settingsCanvas.SetActive(false);
        levelSelectionCanvas.SetActive(true);
    }

    public void OpenSettings()
    {
        mainMenuCanvas.SetActive(false);
        levelSelectionCanvas.SetActive(false);
        settingsCanvas.SetActive(true);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}