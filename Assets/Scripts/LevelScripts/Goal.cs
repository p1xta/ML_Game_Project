using UnityEngine;

public class Goal : MonoBehaviour
{
    [Header("Goal Settings")]
    public float collectReward = 5f; 
    public Vector3 offsetPosition = new Vector3(5f, 0f, 0f);
    
    private Vector3 initialPosition;
    private bool isAtOffset = false;
    private bool isBeingCollected = false;
    private float lastCollectionTime = 0f;
      void Start()
    {
        initialPosition = transform.position;
    }void OnTriggerEnter(Collider other)
    {
        
        if (other.CompareTag("Player") && !isBeingCollected && (Time.time - lastCollectionTime > 0.5f))
        {   
            Debug.Log($"[GOAL {name}] COLLECTING GOAL! Position before: {transform.position}, isAtOffset={isAtOffset}");
            
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
    }    void ResetCollectionFlag()
    {
        isBeingCollected = false;
    }    void TogglePosition()
    {        
        
        if (!isAtOffset)
        {
            Vector3 newPosition = initialPosition + offsetPosition;
            transform.position = newPosition;
            isAtOffset = true;
        }        else
        {   
            transform.position = initialPosition;
            isAtOffset = false;
            Debug.Log($"[GOAL {name}] RETURNED TO INITIAL POSITION: {initialPosition} - COMPLETING LEVEL!");
            
            ObstacleAgent agent = transform.parent.GetComponentInChildren<ObstacleAgent>();
            if (agent != null)
            {
                Debug.Log($"[GOAL {name}] Calling agent.CompleteLevel() for agent '{agent.name}' in level '{transform.parent.name}'");
                agent.CompleteLevel();
            }
            else
            {
                Debug.LogError($"[GOAL {name}] Agent not found in level '{transform.parent.name}' for CompleteLevel!");
            }
        }
        
        Debug.Log($"[GOAL {name}] TogglePosition finished - new state: isAtOffset={isAtOffset}, position={transform.position}");
    }    public void ResetToInitialPosition()
    {
        Debug.Log($"[GOAL {name}] ResetToInitialPosition called - current: pos={transform.position}, isAtOffset={isAtOffset}");
        
        if (isAtOffset)
        {
            transform.position = initialPosition;
            isAtOffset = false;
            Debug.Log($"[GOAL {name}] RESET TO INITIAL: {initialPosition}");
        }
        else
        {
            Debug.Log($"[GOAL {name}] Already at initial position, no reset needed");
        }
        
        isBeingCollected = false;
        lastCollectionTime = 0f;
        Debug.Log($"[GOAL {name}] Reset completed - flags cleared");
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