using UnityEngine;

public class UpDown : MonoBehaviour
{
    public float minY = -2.3f;
    public float maxY = 0f;
    public float speed = 1.5f;
    private bool movingUp = true;

    void Start()
    {
        InvokeRepeating(nameof(ToggleMovement), 0f, (maxY - minY) / speed * 2);
    }

    void ToggleMovement()
    {
        movingUp = !movingUp;
        StopAllCoroutines();
        StartCoroutine(MoveObstacle());
    }

    System.Collections.IEnumerator MoveObstacle()
    {
        float targetY = movingUp ? maxY : minY;
        Vector3 targetPos = new Vector3(transform.localPosition.x, targetY, transform.localPosition.z);

        while (Mathf.Abs(transform.localPosition.y - targetY) > 0.01f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPos, speed * Time.deltaTime);
            yield return null;
        }
    }
}