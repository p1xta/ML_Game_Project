using UnityEngine;

public class Swing : MonoBehaviour
{
    public float amplitude = 15f;
    public float frequency = 1f;

    private levelEditor editor;
    private Quaternion startRotation;

    void Start()
    {
        editor = FindFirstObjectByType<levelEditor>();
        startRotation = transform.localRotation;
    }

    void Update()
    {
        if (editor != null && editor.EditingDone)
        {
            float angle = Mathf.Sin(Time.time * frequency) * amplitude;
            Quaternion swayRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.localRotation = startRotation * swayRotation;
        }
    }

}
