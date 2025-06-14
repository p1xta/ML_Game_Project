using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aircraft
{
    public enum GameState
    {
        Default,
        Preparing,
        Playing,
        Gameover
    }

    public delegate void OnStateChangeHandler();

    public class GameManager : MonoBehaviour
    {
        /// <summary>
        /// Event is called when the game state changes
        /// </summary>
        public event OnStateChangeHandler OnStateChange;

        private GameState gameState;

        /// <summary>
        /// The current game state
        /// </summary>
        public GameState GameState
        {
            get
            {
                return gameState;
            }

            set
            {
                gameState = value;
                if (OnStateChange != null) OnStateChange();
            }
        }

        /// <summary>
        /// The singleton GameManager instance
        /// </summary>
        public static GameManager Instance
        {
            get; private set;
        }

        /// <summary>
        /// Manager the singleton and set fullscreen resolution
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void OnApplicationQuit()
        {
            Instance = null;
        }

        /// <summary>
        /// Loads a new level and sets the game state
        /// </summary>
        /// <param name="levelName">The level to load</param>
        /// <param name="newState">The new game state</param>
        public void LoadLevel(string levelName)
        {
            StartCoroutine(LoadLevelAsync(levelName));
        }

        private IEnumerator LoadLevelAsync(string levelName)
        {
            // Load the new level
            AsyncOperation operation = SceneManager.LoadSceneAsync(levelName);
            while (operation.isDone == false)
            {
                yield return null;
            }

            // Set the resolution
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
        }
    }
}
