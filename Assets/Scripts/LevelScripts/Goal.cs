using UnityEngine;

public class Goal : MonoBehaviour
{
    [Header("Goal Settings")]
    public float collectReward = 10f;
    
    [Header("Teleport Settings")]
    public Vector3 offsetPosition = new Vector3(5f, 0f, 0f);
    
    private Vector3 initialPosition;
    private bool isAtOffset = false;
    private bool isBeingCollected = false;
    private float lastCollectionTime = 0f;

    void Start()
    {
        initialPosition = transform.position;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null || transform == null) return;
        
        // Защита от множественных срабатываний
        if (other.CompareTag("Player") && !isBeingCollected && (Time.time - lastCollectionTime > 0.5f))
        {
            isBeingCollected = true;
            lastCollectionTime = Time.time;
            
            ObstacleAgent agent = other.GetComponent<ObstacleAgent>();
            if (agent != null)
            {
                agent.OnGoalCollected(collectReward);
                TogglePosition();
                agent.FindNextGoal();
            }
            
            Invoke(nameof(ResetCollectionFlag), 0.5f);
        }
    }

    void ResetCollectionFlag()
    {
        isBeingCollected = false;
    }

    void TogglePosition()
    {
        if (!isAtOffset)
        {
            Vector3 newPosition = initialPosition + offsetPosition;
            transform.position = newPosition;
            isAtOffset = true;
        }
        else
        {
            // Второе взятие цели - завершаем уровень
            transform.position = initialPosition;
            isAtOffset = false;
            
            ObstacleAgent agent = transform.parent.GetComponentInChildren<ObstacleAgent>();
            if (agent != null)
            {
                agent.CompleteLevel();
                return;
            }
        }
    }

    public void ResetToInitialPosition()
    {
        if (isAtOffset)
        {
            transform.position = initialPosition;
            isAtOffset = false;
        }
        
        isBeingCollected = false;
        lastCollectionTime = 0f;
    }
    
    void OnDrawGizmosSelected()
    {
        Vector3 basePos = Application.isPlaying ? initialPosition : transform.position;
        
        Gizmos.color = !isAtOffset ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(basePos, 0.5f);
        
        Gizmos.color = isAtOffset ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(basePos + offsetPosition, 0.5f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(basePos, basePos + offsetPosition);
    }
}
