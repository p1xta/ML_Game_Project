using UnityEngine;

public class LevelEditor : MonoBehaviour
{
    public float gridSize = 1f;

    private GameObject selectedPrefab;
    private GameObject previewInstance;
    public GameObject levelEditorCanvas;

    public Material previewMaterial;

    private void Start()
    {
        //levelEditorCanvas.SetActive(false);
    }
    public void SelectPrefab(GameObject prefab)
    {
        selectedPrefab = prefab;

        if (previewInstance != null)
            Destroy(previewInstance);

        if (selectedPrefab != null)
        {
            previewInstance = Instantiate(selectedPrefab);
            SetPreviewMode(previewInstance);
        }
    }

    void Update()
    {
        UpdatePreviewPosition();

        if (Input.GetMouseButtonDown(0) && selectedPrefab != null)
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
            {
                Vector3 gridPos = RoundToGrid(hit.point);
                Instantiate(selectedPrefab, gridPos, Quaternion.identity);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
            {
                if (hit.collider != null && hit.collider.gameObject != previewInstance)
                {
                    Destroy(hit.collider.gameObject);
                }
            }
        }
    }

    void UpdatePreviewPosition()
    {
        if (previewInstance == null) return;

        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
        {
            Vector3 gridPos = RoundToGrid(hit.point);
            previewInstance.transform.position = gridPos;
        }
    }

    Vector3 RoundToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            Mathf.Round(position.y / gridSize) * gridSize,
            Mathf.Round(position.z / gridSize) * gridSize
        );
    }

    void SetPreviewMode(GameObject obj)
    {
        foreach (Collider col in obj.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
        {
            if (previewMaterial != null)
                renderer.material = previewMaterial;
        }
    }
}
