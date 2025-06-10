using UnityEngine;

public class Rotate : MonoBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);
    private levelEditor editor;

    void Start()
    {
        editor = FindFirstObjectByType<levelEditor>();
    }

    // script for the obstacles to rotate
    void Update()
    {
        if (editor != null && editor.EditingDone)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
}
