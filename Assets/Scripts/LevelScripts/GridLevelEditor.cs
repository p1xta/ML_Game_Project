using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Unity.MLAgents;

public class levelEditor : MonoBehaviour
{
    public Transform plane;
    public GameObject editorCanvas;
    public Button[] prefabButtons;
    public Button doneButton;
    private float gridSize = 1f;
    public GameObject trainedAgentPrefab;
    private GameObject agentInstance;

    private GameObject currentPrefab;
    private GameObject previewObject;
    private Vector3 planeOffset;
    private bool isPlacing;
    private bool editingDone = false;
    public bool EditingDone => editingDone;

    private float currentRotationY = 0f;

    void Start()
    {
        planeOffset = plane.position;
        doneButton.onClick.AddListener(FinishEditing);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        editorCanvas.SetActive(false);

        for (int i = 0; i < prefabButtons.Length; i++)
        {
            int index = i;
            prefabButtons[i].onClick.AddListener(() => SelectPrefab(index));
        }
    }

    void Update()
    {
        if (GameState.IsGameActive && !editingDone)
        {
            editorCanvas.SetActive(true);
        }
        if (isPlacing && previewObject != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane worldPlane = new Plane(Vector3.up, plane.position.y);

            if (worldPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                Vector3 localPoint = plane.InverseTransformPoint(hitPoint);

                float scaledGridSize = gridSize;

                float x = Mathf.Round(localPoint.x / scaledGridSize) * scaledGridSize;
                float z = Mathf.Round(localPoint.z / scaledGridSize) * scaledGridSize;

                Vector3 snappedLocalPos = new Vector3(x, 0f, z);

                Vector3 worldPos = plane.TransformPoint(snappedLocalPos);

                float yPos = plane.position.y + 0.5f;
                float xPos = worldPos.x;
                float zPos = worldPos.z;

                if (currentPrefab.CompareTag("obstacle") || currentPrefab.CompareTag("spawn") || currentPrefab.CompareTag("finish"))
                {
                    yPos += 5f;
                    xPos += 5f;
                    zPos += 5f;
                }
                if (currentPrefab.CompareTag("highObstacle"))
                {
                    yPos += 13f;
                    xPos += 5f;
                    zPos += 5f;
                }

                previewObject.transform.position = new Vector3(xPos, yPos, zPos);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                currentRotationY += 90f;
                if (currentRotationY >= 360f) currentRotationY = 0f;
                previewObject.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
            }
            if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                PlaceObject();
            }
        }

        if (Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject())
        {
            RemoveObject();
        }
    }

    void SelectPrefab(int buttonIndex)
    {
        if (buttonIndex < prefabButtons.Length)
        {
            var button = prefabButtons[buttonIndex];
            var prefab = button.GetComponent<PrefabButton>()?.prefab;
            if (prefab != null)
            {
                currentPrefab = prefab;
                isPlacing = true;

                if (previewObject != null)
                {
                    Destroy(previewObject);
                }
                previewObject = Instantiate(currentPrefab);
                SetPreviewMaterial(previewObject);
                currentRotationY = 0f;
                previewObject.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
            }
        }
        //Debug.Log("Selected prefab " + currentPrefab.name);
    }

    void PlaceObject()
    {
        if (currentPrefab != null && previewObject != null)
        {
            GameObject placedObject = Instantiate(currentPrefab, previewObject.transform.position, previewObject.transform.rotation);
            ResetMaterial(placedObject);
        }
    }

    void RemoveObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject != plane.gameObject && !hit.collider.gameObject.CompareTag("UI"))
            {
                Destroy(hit.collider.gameObject);
            }
        }
    }

    void SetPreviewMaterial(GameObject obj)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            Material mat = renderer.material;
            mat.color = new Color(mat.color.r, mat.color.g, mat.color.b, 0.5f);
        }
    }

    void ResetMaterial(GameObject obj)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            Material mat = renderer.material;
            mat.color = new Color(mat.color.r, mat.color.g, mat.color.b, 1f);
        }
    }

    void FinishEditing()
    {
        editingDone = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = true;
        editorCanvas.SetActive(false);
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
            isPlacing = false;
        }
        GameObject spawnPoint = GameObject.FindGameObjectWithTag("spawn");
        GameObject finishPoint = GameObject.FindGameObjectWithTag("finish");

        if (spawnPoint != null && finishPoint != null)
        {
            //Instantiate(trainedAgentPrefab, spawnPoint.transform.position, Quaternion.identity);
            GameObject agentInstance = Instantiate(trainedAgentPrefab);
            agentInstance.transform.position = spawnPoint.transform.position;
            agentInstance.transform.rotation = Quaternion.identity;


            //var controller = agentInstance.GetComponent<ObstacleAgentInteractive>();
        }
        else
        {
            Debug.LogError("Spawn or Finish point not found!");
        }
    }

}