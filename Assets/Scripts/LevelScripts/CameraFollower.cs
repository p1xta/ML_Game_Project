using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -7);
    
    [Header("Rotation Settings")]
    public bool followRotation = true;
    public float rotationSmoothTime = 0.3f;

    [Header("Look Settings")]
    public float lookUpOffset = 3f;

    [Header("Movement Settings")]
    public float followSmoothTime = 0.3f;
    
    private Vector3 velocity = Vector3.zero;
    private float rotationVelocity = 0f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition;
        
        if (followRotation)
        {
            Vector3 rotatedOffset = target.rotation * offset;
            targetPosition = target.position + rotatedOffset;
        }
        else
        {
            targetPosition = target.position + offset;
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, followSmoothTime);

        Vector3 lookAtPoint = target.position + Vector3.up * lookUpOffset;
        transform.rotation = Quaternion.LookRotation(lookAtPoint - transform.position);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
