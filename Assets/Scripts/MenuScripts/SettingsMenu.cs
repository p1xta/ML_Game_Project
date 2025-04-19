using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    public GameObject mainMenuCanvas;
    public GameObject settingsCanvas; 
    public GameObject levelSelectionCanvas;
    public Button backButton; 
    void Start()
    {
        settingsCanvas.SetActive(false);
        levelSelectionCanvas.SetActive(false);
        backButton.onClick.AddListener(ReturnToMainMenu);
    }

    public void ReturnToMainMenu()
    {
        settingsCanvas.SetActive(false);
        levelSelectionCanvas.SetActive(false);
        mainMenuCanvas.SetActive(true);
    }
}
