using UnityEngine;

public class LoopMovement : MonoBehaviour
{
    public Vector3 startPosition;     
    public Vector3 endPosition;       
    public float speed = 2.0f;        

    private void Start()
    {
        transform.localPosition = startPosition;
    }

    private void Update()
    {
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, endPosition, speed * Time.deltaTime);

        if (Vector3.Distance(transform.localPosition, endPosition) < 0.01f)
        {
            transform.localPosition = startPosition;
        }
    }
}
