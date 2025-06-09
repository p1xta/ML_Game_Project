using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

public class ObstacleAgentInteractive : Agent
{
    [Header("Agent Control")]
    public bool isControllableAgent = false;
    
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float runAnimationSpeed = 0.7f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask = 1;
    
    [Header("Goal System")]
    public Transform goalTarget;
    public float progressThreshold = 0.5f;
    public float progressRewardMultiplier = 0.2f;
    public float stuckPenalty = -0.2f;
    public float noProgressPenalty = -0.1f;
    
    [Header("Finish Goals Collection")]
    public int goalsToCollect = 3;
    public LayerMask finishLayer = -1;
    
    [Header("Anti-Exploit Settings")]
    public float maxRewardPerEpisode = 50f;
    public float episodeTimeLimit = 120f;
    
    [Header("Random Spawn Settings")]
    public bool enableRandomSpawn = false;
    public Vector3 spawnAreaSize = new Vector3(5f, 0f, 5f);
    public Vector3 spawnAreaOffset = Vector3.zero;
    
    [Header("Fall Detection")]
    public bool enableFallDetection = true;
    public float fallThreshold = -10f;
    public bool autoSetFallThreshold = true;
    
    private Rigidbody rb;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Animator animator;
    private bool isGrounded;
    private float lastDistanceToGoal = float.MaxValue;
    private float bestDistanceToGoal = float.MaxValue;
    private float episodeStartTime;
    private bool episodeLimitReached = false;
    private float lastGoalCollectionTime = 0f;
    private int collectedGoals = 0;
    
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private int stuckCounter = 0;
    private float lastRewardTime = 0f;
    
    private GameObject[] finishObjects;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        
        GameObject spawnPoint = GameObject.FindGameObjectWithTag("spawn");
        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.transform.position;
            spawnRotation = spawnPoint.transform.rotation;
        }
        else
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }
        
        if (autoSetFallThreshold)
        {
            fallThreshold = spawnPosition.y - 15f;
        }
        
        CameraFollower cameraFollower = transform.parent?.GetComponentInChildren<CameraFollower>();
        if (cameraFollower != null)
        {
            cameraFollower.SetTarget(transform);
        }
        
        InitializeFinishObjects();
    }

    public override void OnEpisodeBegin()
    {
        Vector3 finalSpawnPosition = GetRandomSpawnPosition();
        transform.position = finalSpawnPosition;
        transform.rotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        episodeStartTime = Time.time;
        episodeLimitReached = false;
        bestDistanceToGoal = float.MaxValue;
        lastGoalCollectionTime = 0f;
        collectedGoals = 0;
        
        lastPosition = transform.position;
        stuckTimer = 0f;
        stuckCounter = 0;
        lastRewardTime = 0f;
        
        ResetGoalState();
        FindNextGoal();
        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Позиция агента (нормализованная)
        sensor.AddObservation(transform.position.x / 10f);
        sensor.AddObservation(transform.position.z / 10f);
        
        if (goalTarget != null)
        {
            Vector3 directionToGoal = goalTarget.position - transform.position;
            sensor.AddObservation(directionToGoal.x / 10f);
            sensor.AddObservation(directionToGoal.z / 10f);
            sensor.AddObservation(directionToGoal.magnitude / 10f);
            
            Vector3 normalizedDirection = directionToGoal.normalized;
            
            // Навигационные данные для ориентации к цели
            float dot = Vector3.Dot(transform.forward, normalizedDirection);
            sensor.AddObservation(dot);
            
            float cross = Vector3.Cross(transform.forward, normalizedDirection).y;
            sensor.AddObservation(cross);
            
            Vector3 localDirection = transform.InverseTransformDirection(normalizedDirection);
            sensor.AddObservation(localDirection.x);
            sensor.AddObservation(localDirection.z);
        }
        else
        {
            // Заполняем нулями если цели нет
            for (int i = 0; i < 7; i++) sensor.AddObservation(0f);
        }
        
        // Состояние агента
        sensor.AddObservation(isGrounded ? 1f : 0f);
        sensor.AddObservation(rb.linearVelocity.x / 10f);
        sensor.AddObservation(rb.linearVelocity.z / 10f);
        sensor.AddObservation(rb.linearVelocity.y / 10f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            
            if (!isGrounded)
            {
                Collider[] bridgeColliders = Physics.OverlapSphere(groundCheck.position, groundDistance);
                foreach (Collider col in bridgeColliders)
                {
                    if (col.CompareTag("bridge"))
                    {
                        isGrounded = true;
                        break;
                    }
                }
            }
        }

        int forwardBackwardAction = actions.DiscreteActions[0];
        int leftRightAction = actions.DiscreteActions[1];

        float rotationSpeed = 220f;
        Vector3 moveDirection = Vector3.zero;
        
        if (forwardBackwardAction == 1)
        {
            moveDirection += transform.forward;
        }
        else if (forwardBackwardAction == 2)
        {
            moveDirection -= transform.forward;
        }

        if (leftRightAction == 1)
        {
            transform.Rotate(0, -rotationSpeed * Time.fixedDeltaTime, 0);
        }
        else if (leftRightAction == 2)
        {
            transform.Rotate(0, rotationSpeed * Time.fixedDeltaTime, 0);
        }

        // Применяем движение, сохраняя Y-компоненту для гравитации
        if (rb != null)
        {
            if (moveDirection != Vector3.zero)
            {
                Vector3 movement = new Vector3(
                    moveDirection.x * moveSpeed,
                    rb.linearVelocity.y,
                    moveDirection.z * moveSpeed
                );
                rb.linearVelocity = movement;
            }
            else
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }

        // Обновляем анимации
        if (animator != null)
        {
            float speed = moveDirection.magnitude;
            bool isMoving = speed > 0;

            if (isGrounded && isMoving)
            {
                animator.speed = runAnimationSpeed;
            }
            else
            {
                animator.speed = 1f;
            }
            
            animator.SetFloat("Speed", speed);
            animator.SetBool("IsMoving", isMoving && isGrounded);
            animator.SetBool("IsGrounded", isGrounded);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        
        if (!isControllableAgent)
        {
            discreteActions[0] = 0;
            discreteActions[1] = 0;
            return;
        }

        if (Keyboard.current.wKey.isPressed)
            discreteActions[0] = 1;
        else if (Keyboard.current.sKey.isPressed)
            discreteActions[0] = 2;
        else
            discreteActions[0] = 0;

        if (Keyboard.current.aKey.isPressed)
            discreteActions[1] = 1;
        else if (Keyboard.current.dKey.isPressed)
            discreteActions[1] = 2;
        else
            discreteActions[1] = 0;
    }

    public void SetAsControllable(bool controllable = true)
    {
        isControllableAgent = controllable;
        
        // Убираем контроль у других агентов
        if (controllable)
        {
            ObstacleAgentInteractive[] allAgents = FindObjectsOfType<ObstacleAgentInteractive>();
            foreach (ObstacleAgentInteractive agent in allAgents)
            {
                if (agent != this)
                {
                    agent.isControllableAgent = false;
                }
            }
        }
    }

    private void SwitchToAgent(int agentIndex)
    {
        ObstacleAgentInteractive[] allAgents = FindObjectsOfType<ObstacleAgentInteractive>();
        if (agentIndex >= 0 && agentIndex < allAgents.Length)
        {
            allAgents[agentIndex].SetAsControllable(true);
        }
    }

    void Update()
    {
        // Переключение между агентами по клавишам 1-9
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchToAgent(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchToAgent(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) SwitchToAgent(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) SwitchToAgent(3);
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) SwitchToAgent(4);
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) SwitchToAgent(5);
        else if (Keyboard.current.digit7Key.wasPressedThisFrame) SwitchToAgent(6);
        else if (Keyboard.current.digit8Key.wasPressedThisFrame) SwitchToAgent(7);
        else if (Keyboard.current.digit9Key.wasPressedThisFrame) SwitchToAgent(8);
        
        RequestDecision();
        CheckFallDetection();
        CheckEpisodeLimits();
        CheckProgressRewards();
    }

    // Награждает только за значительный прогресс, устраняет шум от микро-наград
    private void CheckProgressRewards()
    {
        if (goalTarget == null || episodeLimitReached) return;
        
        float currentDistance = Vector3.Distance(transform.position, goalTarget.position);
        CheckStuckBehavior();
        
        // Награда только за существенное приближение
        if (currentDistance < bestDistanceToGoal - progressThreshold)
        {
            float progressReward = (bestDistanceToGoal - currentDistance) * progressRewardMultiplier;
            AddReward(progressReward);
            bestDistanceToGoal = currentDistance;
            lastRewardTime = Time.time;
        }
        
        // Штраф за долгое отсутствие прогресса
        if (Time.time - lastRewardTime > 10f)
        {
            AddReward(noProgressPenalty);
            lastRewardTime = Time.time;
        }
        
        lastDistanceToGoal = currentDistance;
    }
    
    // Определяет и наказывает бессмысленные движения
    private void CheckStuckBehavior()
    {
        float movementThreshold = 0.3f;
        float timeThreshold = 3f;
        
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < movementThreshold)
        {
            stuckTimer += Time.deltaTime;
            
            if (stuckTimer > timeThreshold)
            {
                stuckCounter++;
                AddReward(stuckPenalty);
                
                // Завершаем эпизод при слишком частых застреваниях
                if (stuckCounter > 3)
                {
                    EndEpisode();
                    return;
                }
                
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
    }
    
    private void CheckEpisodeLimits()
    {
        float episodeTime = Time.time - episodeStartTime;
        if (episodeTime > episodeTimeLimit)
        {
            EndEpisode();
            return;
        }
        
        float currentReward = GetCumulativeReward();
        if (currentReward > maxRewardPerEpisode)
        {
            episodeLimitReached = true;
            EndEpisode();
        }
    }
    
    private void CheckFallDetection()
    {
        if (!enableFallDetection) return;
        
        if (transform.position.y < fallThreshold)
        {
            HandleFall();
        }
    }
    
    private void HandleFall()
    {
        ResetGoalState();
        collectedGoals = 0;
        
        Vector3 finalSpawnPosition = GetRandomSpawnPosition();
        transform.position = finalSpawnPosition;
        transform.rotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        EndEpisode();
    }

    public void OnGoalCollected(float reward)
    {
        // Защита от множественных вызовов
        if (Time.time - lastGoalCollectionTime < 0.5f)
        {
            return;
        }
        
        lastGoalCollectionTime = Time.time;
        AddReward(reward);
        
        collectedGoals++;
        
        if (collectedGoals >= goalsToCollect)
        {
            CompleteLevel();
            return;
        }
        
        FindNextGoal();
    }

    public void FindNextGoal()
    {
        Transform closestFinish = null;
        float closestDistance = float.MaxValue;
        
        if (finishObjects != null)
        {
            foreach (GameObject finishObj in finishObjects)
            {
                if (finishObj != null && finishObj.activeInHierarchy)
                {
                    float distance = Vector3.Distance(transform.position, finishObj.transform.position);
                    
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestFinish = finishObj.transform;
                    }
                }
            }
        }
        
        if (closestFinish != null)
        {
            goalTarget = closestFinish;
            float actualDistance = Vector3.Distance(transform.position, goalTarget.position);
            lastDistanceToGoal = actualDistance;
            bestDistanceToGoal = actualDistance;
        }
        else
        {
            goalTarget = null;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("finish"))
        {
            bool layerMatches = finishLayer == -1 || ((1 << other.gameObject.layer) & finishLayer) != 0;
            
            if (layerMatches)
            {
                other.gameObject.SetActive(false);
                OnGoalCollected(10f);
                return;
            }
        }
        
        if (other.CompareTag("pit") || other.CompareTag("moving") || other.CompareTag("spit"))
        {
            HandleDeadlyCollision(other);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("pit") || 
            collision.gameObject.CompareTag("moving") ||
            collision.gameObject.CompareTag("obstacle") ||
            collision.gameObject.CompareTag("highObstacle") ||
            collision.gameObject.CompareTag("spit"))
        {
            HandleDeadlyCollision(collision.collider);
        }
    }

    private void HandleDeadlyCollision(Collider deadlyObject)
    {
        ResetGoalState();
        collectedGoals = 0;
        
        Vector3 finalSpawnPosition = GetRandomSpawnPosition();
        transform.position = finalSpawnPosition;
        transform.rotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        EndEpisode();
    }
    
    public void CompleteLevel()
    {
        float levelReward = 50f;
        AddReward(levelReward);
        EndEpisode();
    }

    void ResetGoalState()
    {
        if (finishObjects != null)
        {
            foreach (GameObject finishObj in finishObjects)
            {
                if (finishObj != null)
                {
                    finishObj.SetActive(true);
                }
            }
        }

        if (transform.parent != null)
        {
            Goal[] goals = transform.parent.GetComponentsInChildren<Goal>();
            foreach (Goal goal in goals)
            {
                goal.ResetToInitialPosition();
            }
        }
    }

    private void InitializeFinishObjects()
    {
        GameObject[] allFinishObjects = GameObject.FindGameObjectsWithTag("finish");

        if (transform.parent == null)
        {
            finishObjects = allFinishObjects;
            return;
        }

        var levelFinishObjects = new System.Collections.Generic.List<GameObject>();

        foreach (GameObject finishObj in allFinishObjects)
        {
            bool isInSameLevel = finishObj.transform.IsChildOf(transform.parent) || finishObj.transform.parent == transform.parent;
            bool layerMatches = finishLayer == -1 || ((1 << finishObj.layer) & finishLayer) != 0;

            if (isInSameLevel && layerMatches)
            {
                levelFinishObjects.Add(finishObj);
            }
        }

        finishObjects = levelFinishObjects.ToArray();
    }
    
    // Получает случайную позицию в области спавна или возвращает исходную позицию
    private Vector3 GetRandomSpawnPosition()
    {
        if (!enableRandomSpawn)
        {
            return spawnPosition;
        }
        
        Vector3 spawnCenter = spawnPosition + spawnAreaOffset;
        
        float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        float randomZ = Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f);
        float randomY = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
        
        Vector3 randomOffset = new Vector3(randomX, randomY, randomZ);
        Vector3 finalPosition = spawnCenter + randomOffset;
        
        return finalPosition;
    }
    
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
        
        if (goalTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, goalTarget.position);
            Gizmos.DrawWireSphere(goalTarget.position, 1f);
            
            #if UNITY_EDITOR
            Vector3 midPoint = (transform.position + goalTarget.position) / 2f;
            float distance = Vector3.Distance(transform.position, goalTarget.position);
            UnityEditor.Handles.Label(midPoint, $"Distance: {distance:F1}");
            #endif
        }
        
        // Визуализация области случайного спавна
        if (enableRandomSpawn)
        {
            Vector3 spawnCenter = spawnPosition + spawnAreaOffset;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnCenter, spawnAreaSize);
            
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawCube(spawnCenter, spawnAreaSize);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(spawnCenter + Vector3.up * 2f, $"Spawn Area: {spawnAreaSize}");
            #endif
        }
        
        // Визуализация порога падения
        if (enableFallDetection)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Vector3 fallPlaneCenter = new Vector3(transform.position.x, fallThreshold, transform.position.z);
            Vector3 fallPlaneSize = new Vector3(20f, 0.1f, 20f);
            Gizmos.DrawCube(fallPlaneCenter, fallPlaneSize);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(fallPlaneCenter + Vector3.up * 1f, $"Fall Threshold: {fallThreshold:F1}");
            #endif
        }
    }
}
