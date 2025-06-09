using UnityEngine;

public class Goal : MonoBehaviour
{    [Header("Goal Settings")]
    public float collectReward = 10f; // Увеличена награда за сбор цели для более значимого обучения[Header("Teleport Settings")]
    public Vector3 offsetPosition = new Vector3(5f, 0f, 0f); // Смещение от начальной позиции
    
    private Vector3 initialPosition;
    private bool isAtOffset = false; // Находится ли цель в смещенной позиции
    private bool isBeingCollected = false; // Флаг для предотвращения двойного срабатывания
    private float lastCollectionTime = 0f; // Время последнего сбора
      void Start()
    {
        initialPosition = transform.position;
    }    void OnTriggerEnter(Collider other)
    {
        // Проверка на валидность объектов перед обработкой
        if (other == null || transform == null) return;
        
        // Улучшенная защита от множественных срабатываний
        if (other.CompareTag("Player") && !isBeingCollected && (Time.time - lastCollectionTime > 0.5f))
        {               
            isBeingCollected = true; // Блокируем повторные срабатывания
            lastCollectionTime = Time.time; // Записываем время сбора
            
            ObstacleAgent agent = other.GetComponent<ObstacleAgent>();
            if (agent != null)
            {
                agent.OnGoalCollected(collectReward);
                
                TogglePosition();
                
                
                agent.FindNextGoal();
                
            }
            // Сбрасываем флаг через более длительное время
            Invoke(nameof(ResetCollectionFlag), 0.5f);
        }
    }void ResetCollectionFlag()
    {
        isBeingCollected = false;
    }    void TogglePosition()
    {        
        
        if (!isAtOffset)
        {
            // Смещаемся в сторону
            Vector3 newPosition = initialPosition + offsetPosition;
            transform.position = newPosition;
            isAtOffset = true;
        }        else
        {   
            // Возвращаемся обратно - это второе взятие цели, завершаем уровень
            transform.position = initialPosition;
            isAtOffset = false;
            
            // Находим агента в родительском уровне
            ObstacleAgent agent = transform.parent.GetComponentInChildren<ObstacleAgent>();
            if (agent != null)
            {
                agent.CompleteLevel();
                return; // Немедленно прекращаем выполнение после завершения уровня
            }
        }
        
    }    public void ResetToInitialPosition()
    {
        
        if (isAtOffset)
        {
            transform.position = initialPosition;
            isAtOffset = false;
        }
        else
        {
        }
        
        isBeingCollected = false; // Также сбрасываем флаг
        lastCollectionTime = 0f; // Сбрасываем время
    }
    
    void OnDrawGizmosSelected()
    {
        // Визуализируем обе позиции
        Vector3 basePos = Application.isPlaying ? initialPosition : transform.position;
        
        // Начальная позиция
        Gizmos.color = !isAtOffset ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(basePos, 0.5f);
        
        // Смещенная позиция
        Gizmos.color = isAtOffset ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(basePos + offsetPosition, 0.5f);
        
        // Линия между позициями
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(basePos, basePos + offsetPosition);
    }
}