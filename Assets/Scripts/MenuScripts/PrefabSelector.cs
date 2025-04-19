using UnityEngine;
using UnityEngine.UI;

public class PrefabButton : MonoBehaviour
{
    public GameObject prefabToPlace;
    public LevelEditor levelEditor;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(SelectPrefab);
    }
    void SelectPrefab()
    {
        if (levelEditor != null && prefabToPlace != null)
        {
            levelEditor.SelectPrefab(prefabToPlace);
        }
    }
}
