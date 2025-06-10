using UnityEngine;

public class Move : MonoBehaviour
{
    public Vector3 direction = Vector3.left;
    public float amplitude = 1f;
    public float frequency = 1f;

    private levelEditor editor;
    private Vector3 startPos;

    void Start()
    {
        editor = FindFirstObjectByType<levelEditor>();
        startPos = transform.position;
    }
    // script for the blade obstacle to move
    void Update()
    {
        if (editor != null && editor.EditingDone)
        {
            float offset = Mathf.Sin(Time.time * frequency) * amplitude;
            Vector3 localOffset = transform.TransformDirection(direction.normalized) * offset;
            transform.position = startPos + localOffset;
        }
    }
}