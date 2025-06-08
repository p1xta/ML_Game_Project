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
    public bool showSpawnAreaGizmos = true;
    
    [Header("Fall Detection")]
    public bool enableFallDetection = true;
    public float fallThreshold = -10f;
    public bool autoSetFallThreshold = true;
    
    private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Animator animator;
    private bool isGrounded;    private float lastDistanceToGoal = float.MaxValue;
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
            Debug.LogError("[SPAWN INIT] Spawn point with tag 'spawn' not found! Using current transform.");
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        //spawnPosition = transform.position;
        //spawnRotation = transform.rotation;

        Debug.Log($"[SPAWN INIT] Agent {name} spawn position set to: {spawnPosition}");
        Debug.Log($"[SPAWN INIT] Agent {name} current transform.position: {transform.position}");
        Debug.Log($"[SPAWN INIT] Agent {name} spawnAreaOffset: {spawnAreaOffset}");
        Debug.Log($"[SPAWN INIT] Agent {name} spawnAreaSize: {spawnAreaSize}");
        Debug.Log($"[SPAWN INIT] Agent {name} spawn center will be: {spawnPosition + spawnAreaOffset}");
        
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        
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
    }    public override void OnEpisodeBegin()
    {
        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning($"[SPAWN WARNING] Agent {name} spawnPosition is zero! Re-initializing...");
            Debug.LogWarning($"[SPAWN WARNING] Current transform.position: {transform.position}");
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }
        
        Debug.Log($"[EPISODE BEGIN] Agent {name} using spawn position: {spawnPosition}");
        Debug.Log($"[EPISODE BEGIN] Agent {name} current transform.position: {transform.position}");
        Debug.Log($"[EPISODE BEGIN] Agent {name} spawnAreaOffset: {spawnAreaOffset}");
        Debug.Log($"[EPISODE BEGIN] Agent {name} calculated spawn center: {spawnPosition + spawnAreaOffset}");
        
        Vector3 finalSpawnPosition = GetRandomSpawnPosition();
        transform.position = finalSpawnPosition;
        transform.rotation = spawnRotation;
        initialPosition = finalSpawnPosition;
        initialRotation = spawnRotation;
        
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
        sensor.AddObservation(transform.position.x / 10f);
        sensor.AddObservation(transform.position.z / 10f);
        
        if (goalTarget != null)
        {
            Vector3 directionToGoal = goalTarget.position - transform.position;
            sensor.AddObservation(directionToGoal.x / 10f);
            sensor.AddObservation(directionToGoal.z / 10f);
            sensor.AddObservation(directionToGoal.magnitude / 10f);
            
            Vector3 normalizedDirection = directionToGoal.normalized;
            
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
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f); // dot
            sensor.AddObservation(0f); // cross
            sensor.AddObservation(0f); // local x
            sensor.AddObservation(0f); // local z        
        }
        
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

        float maxSpeed = moveSpeed;
        float rotationSpeed = 220f;

        Vector3 moveDirection = Vector3.zero;
        if (forwardBackwardAction == 1) // Вперёд (W)
        {
            moveDirection += transform.forward;
        }
        else if (forwardBackwardAction == 2) // Назад (S)
        {
            moveDirection -= transform.forward;
        }

        if (leftRightAction == 1) // Влево (A)
        {
            transform.Rotate(0, -rotationSpeed * Time.fixedDeltaTime, 0);
        }
        else if (leftRightAction == 2) // Вправо (D)
        {
            transform.Rotate(0, rotationSpeed * Time.fixedDeltaTime, 0);
        }

        if (rb != null)
        {
            if (moveDirection != Vector3.zero)
            {
                Vector3 movement = new Vector3(
                    moveDirection.x * maxSpeed,
                    rb.linearVelocity.y,                    moveDirection.z * maxSpeed
                );
                rb.linearVelocity = movement;
            }
            else
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }

        string actionLog = "Agent actions: ";
        
        switch (forwardBackwardAction)
        {
            case 1: actionLog += "Forward "; break;
            case 2: actionLog += "Backward "; break;
            default: actionLog += "NoMove "; break;
        }
        
        switch (leftRightAction)
        {
            case 1: actionLog += "TurnLeft "; break;            case 2: actionLog += "TurnRight "; break;
            default: actionLog += "NoTurn "; break;
        }
        
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
            discreteActions[0] = 1; // Вперёд
        else if (Keyboard.current.sKey.isPressed)
            discreteActions[0] = 2; // Назад
        else
            discreteActions[0] = 0; // Нет движения

        if (Keyboard.current.aKey.isPressed)
            discreteActions[1] = 1; // Влево
        else if (Keyboard.current.dKey.isPressed)
            discreteActions[1] = 2; // Вправо
        else
            discreteActions[1] = 0; // Нет движения
    }

    public void SetAsControllable(bool controllable = true)
    {
        isControllableAgent = controllable;
        
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
            
            Debug.Log($"[AGENT CONTROL] Agent '{name}' теперь управляется игроком");
        }
        else
        {
            Debug.Log($"[AGENT CONTROL] Agent '{name}' больше не управляется игроком");
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
        
        if (spawnPosition == Vector3.zero && Application.isPlaying)
        {
            Debug.LogError($"[SPAWN ERROR] Agent {name} spawnPosition was reset to zero during runtime! Restoring...");
            spawnPosition = initialPosition != Vector3.zero ? initialPosition : transform.position;
            Debug.Log($"[SPAWN RESTORE] Agent {name} spawnPosition restored to: {spawnPosition}");
        }
        
        CheckFallDetection(); 
        CheckEpisodeLimits();
        
        CheckProgressRewards();
    }
      private void CheckProgressRewards()
    {
        if (goalTarget == null || episodeLimitReached) return;
        
        float currentDistance = Vector3.Distance(transform.position, goalTarget.position);
        
        CheckStuckBehavior();
          // Награда только за ЗНАЧИТЕЛЬНОЕ приближение к цели
        if (currentDistance < bestDistanceToGoal - progressThreshold) // Приблизились минимум на progressThreshold единиц
        {
            float progressReward = (bestDistanceToGoal - currentDistance) * progressRewardMultiplier;
            AddReward(progressReward);
            bestDistanceToGoal = currentDistance;
            lastRewardTime = Time.time;
            
            Debug.Log($"[PROGRESS] Agent made progress! Distance: {currentDistance:F2}, Reward: {progressReward:F3}");
        }
        
        // Штраф за долгое отсутствие прогресса
        if (Time.time - lastRewardTime > 10f) // 10 секунд без прогресса
        {
            AddReward(noProgressPenalty);
            lastRewardTime = Time.time;
            Debug.Log("[NO PROGRESS] Agent hasn't made progress for 10 seconds!");
        }
        
        lastDistanceToGoal = currentDistance;
    }
    
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
                
                Debug.Log($"[STUCK] Agent is stuck! Counter: {stuckCounter}, Penalty applied");
                
                if (stuckCounter > 3)
                {
                    Debug.Log("[STUCK] Agent stuck too many times, ending episode");
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
        float episodeTime = Time.time - episodeStartTime;        if (episodeTime > episodeTimeLimit)
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
            Debug.Log($"[FALL DETECTED] Агент {name} упал на Y={transform.position.y:F2}, порог={fallThreshold:F2}");
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
        
        if (Time.time - lastGoalCollectionTime < 0.5f)
        {
            return;
        }
        
        lastGoalCollectionTime = Time.time;
        AddReward(reward);
        
        collectedGoals++;
        Debug.Log($"[GOAL COLLECTED] Агент собрал {collectedGoals}/{goalsToCollect} целей");
        
        if (collectedGoals >= goalsToCollect)
        {
            Debug.Log($"[LEVEL COMPLETE] Агент собрал все {goalsToCollect} целей! Уровень завершен.");            CompleteLevel();
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
                    }                }
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
                Debug.Log($"[FINISH COLLECTED] Агент собрал объект: {other.name}");
                
                other.gameObject.SetActive(false);
                OnGoalCollected(10f);
                
                return;
            }
        }
        
        // if (other.CompareTag("pit") || other.CompareTag("moving") || other.CompareTag("spit"))
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
            collision.gameObject.CompareTag("spit"))        {
            HandleDeadlyCollision(collision.collider);
        }
    }

    private void HandleDeadlyCollision(Collider deadlyObject)
    {
        string obstacleType = deadlyObject.tag;
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
    
    // void OnTriggerStay(Collider other)
    // {
    //     if (other.CompareTag("pit"))
    //     {
        
    public void CompleteLevel()
    {        
        float levelReward = 50f; 
        AddReward(levelReward);
        Debug.Log($"[LEVEL COMPLETE] Agent completed level! Reward: {levelReward}");
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
        else
        {
            Debug.LogWarning($"[GOAL RESET] Агент {name} не имеет родителя — Goal-объекты не сброшены");
        }
    }

    private void InitializeFinishObjects()
    {
        GameObject[] allFinishObjects = GameObject.FindGameObjectsWithTag("finish");

        if (transform.parent == null)
        {
            finishObjects = allFinishObjects;
            Debug.Log($"[AGENT INIT] Агент {name} не имеет родителя, использует все {finishObjects.Length} finish-объектов");
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
        Debug.Log($"[AGENT INIT] Найдено {finishObjects.Length} finish-объектов для агента {name} в уровне {transform.parent?.name}");
    }

    /// <summary>
    /// диагностический метод для проверки состояния системы спавна
    /// </summary>
    [ContextMenu("Debug Spawn System")]
    public void DebugSpawnSystem()
    {
        Debug.Log($"=== SPAWN SYSTEM DEBUG for Agent {name} ===");
        Debug.Log($"Current Transform Position: {transform.position}");
        Debug.Log($"Stored Spawn Position: {spawnPosition}");
        Debug.Log($"Initial Position: {initialPosition}");
        Debug.Log($"Spawn Area Offset: {spawnAreaOffset}");
        Debug.Log($"Spawn Area Size: {spawnAreaSize}");
        Debug.Log($"Enable Random Spawn: {enableRandomSpawn}");
        Debug.Log($"Show Spawn Area Gizmos: {showSpawnAreaGizmos}");
        
        Vector3 calculatedCenter = spawnPosition + spawnAreaOffset;
        Debug.Log($"Calculated Spawn Center: {calculatedCenter}");
        
        if (spawnPosition == Vector3.zero)
        {
            Debug.LogError("PROBLEM: spawnPosition is Vector3.zero!");
        }
          if (transform.position != spawnPosition && !Application.isPlaying)
        {
            Debug.LogWarning($"WARNING: Current position ({transform.position}) differs from stored spawn position ({spawnPosition}) in edit mode!");
        }
        
        Debug.Log($"=== END SPAWN SYSTEM DEBUG ===");
    }
    
    /// <summary>
    /// Принудительно синхронизирует spawnPosition с текущей позицией
    /// Используется для исправления проблем с областью спавна
    /// </summary>
    [ContextMenu("Force Sync Spawn Position")]
    public void ForceSyncSpawnPosition()
    {
        Vector3 oldSpawnPosition = spawnPosition;
        spawnPosition = transform.position;
        
        Debug.Log($"[FORCE SYNC] Agent {name} spawn position changed:");
        Debug.Log($"[FORCE SYNC] Old: {oldSpawnPosition}");
        Debug.Log($"[FORCE SYNC] New: {spawnPosition}");
        Debug.Log($"[FORCE SYNC] Spawn center will now be: {spawnPosition + spawnAreaOffset}");
        
        if (autoSetFallThreshold)
        {
            float oldThreshold = fallThreshold;
            fallThreshold = spawnPosition.y - 15f;
            Debug.Log($"[FORCE SYNC] Fall threshold updated from {oldThreshold:F2} to {fallThreshold:F2}");
        }
    }
    
    /// <summary>
    /// сбрасывает область спавна к стандартным настройкам
    /// </summary>
    [ContextMenu("Reset Spawn Area Settings")]
    public void ResetSpawnAreaSettings()
    {        spawnAreaSize = new Vector3(5f, 0f, 5f);
        spawnAreaOffset = Vector3.zero;
        enableRandomSpawn = true;
        showSpawnAreaGizmos = true;
        
        Debug.Log($"[RESET SPAWN] Agent {name} spawn area settings reset to defaults");
        Debug.Log($"[RESET SPAWN] Size: {spawnAreaSize}, Offset: {spawnAreaOffset}");
    }
    
    /// <summary>
    /// устанавливает текущую позицию агента как новую точку спавна
    /// </summary>
    [ContextMenu("Set Current Position as Spawn")]
    public void SetCurrentPositionAsSpawn()
    {
        Vector3 oldSpawnPosition = spawnPosition;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        
        spawnAreaOffset = Vector3.zero;
        
        Debug.Log($"[SET SPAWN] Agent {name} spawn position updated:");
        Debug.Log($"[SET SPAWN] Old spawn: {oldSpawnPosition}");
        Debug.Log($"[SET SPAWN] New spawn: {spawnPosition}");
        Debug.Log($"[SET SPAWN] Offset reset to: {spawnAreaOffset}");
        
        if (autoSetFallThreshold)
        {
            float oldThreshold = fallThreshold;            fallThreshold = spawnPosition.y - 15f;
            Debug.Log($"[SET SPAWN] Fall threshold updated from {oldThreshold:F2} to {fallThreshold:F2}");
        }
    }
    
    /// <summary>
    /// получает случайную позицию в области спавна или возвращает исходную позицию
    /// </summary>
    /// <returns>Финальная позиция для спавна агента</returns>    
    private Vector3 GetRandomSpawnPosition()
    {
        Debug.Log($"[RANDOM SPAWN] === Agent {name} GetRandomSpawnPosition() called ===");
        Debug.Log($"[RANDOM SPAWN] enableRandomSpawn: {enableRandomSpawn}");
        Debug.Log($"[RANDOM SPAWN] spawnPosition: {spawnPosition}");
        Debug.Log($"[RANDOM SPAWN] current transform.position: {transform.position}");
        Debug.Log($"[RANDOM SPAWN] spawnAreaOffset: {spawnAreaOffset}");
        Debug.Log($"[RANDOM SPAWN] spawnAreaSize: {spawnAreaSize}");
        
        if (!enableRandomSpawn)
        {
            Debug.Log($"[RANDOM SPAWN] Agent {name} using fixed spawn: {spawnPosition}");
            return spawnPosition;
        }
        
        Vector3 basePosition = Application.isPlaying ? spawnPosition : transform.position;
        Vector3 spawnCenter = basePosition + spawnAreaOffset;
        
        Debug.Log($"[RANDOM SPAWN] Agent {name} - Base position: {basePosition}");
        Debug.Log($"[RANDOM SPAWN] Agent {name} - Calculated center: {spawnCenter}");
        Debug.Log($"[RANDOM SPAWN] Agent {name} - Offset: {spawnAreaOffset}");
        
        float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        float randomZ = Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f);
        float randomY = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
        
        Vector3 randomOffset = new Vector3(randomX, randomY, randomZ);
        Vector3 finalPosition = spawnCenter + randomOffset;
        
        Debug.Log($"[RANDOM SPAWN] Agent {name} - Random offset: {randomOffset}");
        Debug.Log($"[RANDOM SPAWN] Agent {name} - Final position: {finalPosition}");
        Debug.Log($"[RANDOM SPAWN] === End GetRandomSpawnPosition() ===");
          return finalPosition;
    }
    
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
        
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, Vector3.down * 1.1f);
        
        if (goalTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, goalTarget.position);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(goalTarget.position, 1f);
            
            Vector3 midPoint = (transform.position + goalTarget.position) / 2f;
            float distance = Vector3.Distance(transform.position, goalTarget.position);
            
            #if UNITY_EDITOR 
            UnityEditor.Handles.Label(midPoint, $"Distance: {distance:F1}");
            #endif
        }
        if (enableRandomSpawn && showSpawnAreaGizmos)
        {
            Vector3 basePosition = Application.isPlaying ? spawnPosition : transform.position;
            Vector3 spawnCenter = basePosition + spawnAreaOffset;
            
            #if UNITY_EDITOR
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(transform.position, 0.3f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, $"Current Pos: {transform.position:F1}");
            
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(spawnPosition, 0.4f);
                UnityEditor.Handles.Label(spawnPosition + Vector3.up * 3.5f, $"Spawn Pos: {spawnPosition:F1}");
            }
            else
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 3.5f, $"Spawn Area Base: {basePosition:F1}");
            }
            #endif
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnCenter, spawnAreaSize);
            
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawCube(spawnCenter, spawnAreaSize);
            
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(spawnCenter, 0.5f);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(spawnCenter + Vector3.up * 2f, $"Spawn Area: {spawnAreaSize}");
            UnityEditor.Handles.Label(spawnCenter + Vector3.up * 1.5f, $"Offset: {spawnAreaOffset}");
            UnityEditor.Handles.Label(spawnCenter + Vector3.up * 1f, $"Center: {spawnCenter:F1}");
            #endif
        }
        
        if (enableFallDetection)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Vector3 fallPlaneCenter = new Vector3(transform.position.x, fallThreshold, transform.position.z);
            Vector3 fallPlaneSize = new Vector3(20f, 0.1f, 20f);
            Gizmos.DrawCube(fallPlaneCenter, fallPlaneSize);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(fallPlaneCenter, fallPlaneSize);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(fallPlaneCenter + Vector3.up * 1f, $"Fall Threshold: {fallThreshold:F1}");
            #endif
        }
    }
}