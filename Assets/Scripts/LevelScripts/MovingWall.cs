using UnityEngine;

public class MovingWall : MonoBehaviour
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public Axis moveAxis = Axis.X;
    public float minX = -2f;
    public float maxX = 2f;
    public float minY = -2f;
    public float maxY = 2f;
    public float minZ = -2f;
    public float maxZ = 2f;
    public float speed = 1.5f;

    private bool movingPositive = true;

    void Start()
    {
        if (moveAxis == Axis.X)
        {
            InvokeRepeating(nameof(ToggleMovementX), 0f, Mathf.Abs(maxX - minX) / speed * 2);
        }
        else if (moveAxis == Axis.Y)
        {
            InvokeRepeating(nameof(ToggleMovementY), 0f, Mathf.Abs(maxY - minY) / speed * 2);
        }
        else if (moveAxis == Axis.Z)
        {
            InvokeRepeating(nameof(ToggleMovementZ), 0f, Mathf.Abs(maxZ - minZ) / speed * 2);
        }
    }

    void ToggleMovementX()
    {
        movingPositive = !movingPositive;
        StopAllCoroutines();
        StartCoroutine(MoveObstacleX());
    }

    void ToggleMovementY()
    {
        movingPositive = !movingPositive;
        StopAllCoroutines();
        StartCoroutine(MoveObstacleY());
    }

    void ToggleMovementZ()
    {
        movingPositive = !movingPositive;
        StopAllCoroutines();
        StartCoroutine(MoveObstacleZ());
    }

    System.Collections.IEnumerator MoveObstacleX()
    {
        float targetX = movingPositive ? maxX : minX;
        Vector3 targetPos = new Vector3(targetX, transform.localPosition.y, transform.localPosition.z);

        while (Mathf.Abs(transform.localPosition.x - targetX) > 0.01f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPos, speed * Time.deltaTime);
            yield return null;
        }
    }

    System.Collections.IEnumerator MoveObstacleY()
    {
        float targetY = movingPositive ? maxY : minY;
        Vector3 targetPos = new Vector3(transform.localPosition.x, targetY, transform.localPosition.z);

        while (Mathf.Abs(transform.localPosition.y - targetY) > 0.01f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPos, speed * Time.deltaTime);
            yield return null;
        }
    }

    System.Collections.IEnumerator MoveObstacleZ()
    {
        float targetZ = movingPositive ? maxZ : minZ;
        Vector3 targetPos = new Vector3(transform.localPosition.x, transform.localPosition.y, targetZ);

        while (Mathf.Abs(transform.localPosition.z - targetZ) > 0.01f)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPos, speed * Time.deltaTime);
            yield return null;
        }
    }
}
