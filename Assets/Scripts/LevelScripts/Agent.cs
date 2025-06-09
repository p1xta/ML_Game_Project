using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

public class ObstacleAgent : Agent
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask = 1;
    public float jumpCooldown = 0.2f;
    public float runAnimationSpeed = 0.7f;
    
    [Header("Goal System")]
    public Transform goalTarget;
    
    [Header("Bridge Reward System")]
    public float bridgeReward = 0.02f;
    public float bridgeRewardCooldown = 0.5f;
    
    [Header("Anti-Exploit Settings")]
    public float maxRewardPerEpisode = 50f;
    public float episodeTimeLimit = 120f;
    
    [Header("New Reward System Settings")]
    public float progressThreshold = 0.5f;
    public float progressRewardMultiplier = 0.2f;
    public float stuckPenalty = -0.2f;
    public float noProgressPenalty = -0.1f;
    
    private Rigidbody rb;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Animator animator;
    private bool isGrounded;
    private float lastJumpTime = 0f;
    private float lastDistanceToGoal = float.MaxValue;
    private float bestDistanceToGoal = float.MaxValue;
    private bool isJumping = false;
    private float episodeStartTime;
    private bool episodeLimitReached = false;
    private float lastGoalCollectionTime = 0f;
    private bool isOnBridge = false;
    private float lastBridgeRewardTime = 0f;
    
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private int stuckCounter = 0;
    private float lastSignificantProgress = 0f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        
        CameraFollower cameraFollower = transform.parent?.GetComponentInChildren<CameraFollower>();
        if (cameraFollower != null)
        {
            cameraFollower.SetTarget(transform);
        }
    }

    public override void OnEpisodeBegin()
    {
        transform.position = spawnPosition;
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
        isOnBridge = false;
        lastBridgeRewardTime = 0f;
        
        lastPosition = transform.position;
        stuckTimer = 0f;
        stuckCounter = 0;
        lastSignificantProgress = Time.time;

        ResetGoalState();
        FindNextGoal();
        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Позиция агента
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
            sensor.AddObservation(dot); // 1 = цель прямо впереди -1 = цель сзади
            
            float cross = Vector3.Cross(transform.forward, normalizedDirection).y;
            sensor.AddObservation(cross); // >0 = поворот вправо, <0 = поворот влево
            
            // Направление к цели в локальных координатах агента
            Vector3 localDirection = transform.InverseTransformDirection(normalizedDirection);
            sensor.AddObservation(localDirection.x); 
            sensor.AddObservation(localDirection.z);
        }
        else
        {
            // Заполняем нулями если цели нет (важно для стабильности наблюдений)
            for (int i = 0; i < 7; i++) sensor.AddObservation(0f);
        }
        
        // Состояние агента
        sensor.AddObservation(isGrounded ? 1f : 0f);
        sensor.AddObservation(rb.linearVelocity.x / 10f);
        sensor.AddObservation(rb.linearVelocity.z / 10f);
        sensor.AddObservation(rb.linearVelocity.y / 10f);
        sensor.AddObservation(isJumping ? 1f : 0f);
        
        // Кулдаун прыжка
        float jumpCooldownRemaining = Mathf.Max(0f, (lastJumpTime + jumpCooldown) - Time.time);
        float normalizedCooldown = jumpCooldownRemaining / jumpCooldown;
        sensor.AddObservation(normalizedCooldown); // 0 = можно прыгать, 1 = максимальный кулдаун
        
        bool canJumpNow = isGrounded && !isJumping && (Time.time - lastJumpTime > jumpCooldown);
        sensor.AddObservation(canJumpNow ? 1f : 0f);
        sensor.AddObservation(isOnBridge ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Обновляем состояние земли с улучшенной логикой сброса прыжка
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            
            if (isGrounded)
            {
                // Сбрасываем прыжок только когда скорость падения минимальна
                if (rb.linearVelocity.y <= 0.1f)
                {
                    isJumping = false;
                }
            }
            // Принудительный сброс при долгом зависании (защита от застревания в воздухе)
            else if (isJumping && Time.time - lastJumpTime > 1f && Mathf.Abs(rb.linearVelocity.y) < 0.5f)
            {
                isJumping = false;
            }
        }

        int forwardBackwardAction = actions.DiscreteActions[0];
        int leftRightAction = actions.DiscreteActions[1];
        int jumpAction = actions.DiscreteActions[2];

        float maxSpeed = moveSpeed;
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

        if (rb != null)
        {
            if (moveDirection != Vector3.zero)
            {
                // Сохраняем Y-компоненту скорости для правильной физики прыжка
                Vector3 movement = new Vector3(
                    moveDirection.x * maxSpeed,
                    rb.linearVelocity.y,
                    moveDirection.z * maxSpeed
                );
                rb.linearVelocity = movement;
            }
            else
            {
                // Останавливаем только горизонтальное движение, сохраняя гравитацию
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }

        bool canJump = Time.time - lastJumpTime > jumpCooldown;
        if (jumpAction == 1 && isGrounded && canJump && !isJumping)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            lastJumpTime = Time.time;
            isJumping = true;
        }
        // Наказание за попытку прыгнуть в неподходящий момент (обучение дисциплине)
        else if (jumpAction == 1 && (!canJump || isJumping))
        {
            AddReward(-0.01f);
        }

        // Обновляем анимации с корректной скоростью
        if (animator != null)
        {
            float speed = moveDirection.magnitude;
            bool isMoving = speed > 0;
            
            // Двойная проверка для корректного определения состояния на земле
            bool effectivelyGrounded = isGrounded && !isJumping;
            
            // Принудительный сброс состояния прыжка при застревании в анимации
            if (isGrounded && isJumping && Mathf.Abs(rb.linearVelocity.y) < 0.1f && Time.time - lastJumpTime > 0.5f)
            {
                isJumping = false;
                effectivelyGrounded = true;
            }
            
            // Замедляем анимацию бега только на земле
            if (effectivelyGrounded && isMoving)
            {
                animator.speed = runAnimationSpeed;
            }
            else
            {
                animator.speed = 1f; // Нормальная скорость для прыжков и падений
            }
            
            animator.SetFloat("Speed", speed);
            animator.SetBool("IsMoving", isMoving && effectivelyGrounded);
            animator.SetBool("IsGrounded", effectivelyGrounded);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

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

        discreteActions[2] = Keyboard.current.spaceKey.isPressed ? 1 : 0;
    }

    void Update()
    {
        RequestDecision();
        CheckEpisodeLimits();
        CheckProgressRewards();
        CheckStuckBehavior();
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
    
    // Награждает только за значительный прогресс, устраняет шум от микро-наград
    private void CheckProgressRewards()
    {
        if (goalTarget == null || episodeLimitReached) return;
        
        float currentDistance = Vector3.Distance(transform.position, goalTarget.position);
        float progressMade = lastDistanceToGoal - currentDistance;
        
        // Награда только за существенное приближение
        if (progressMade >= progressThreshold)
        {
            float progressReward = progressMade * progressRewardMultiplier;
            AddReward(progressReward);
            
            lastDistanceToGoal = currentDistance;
            lastSignificantProgress = Time.time;
        }
        
        // Бонус за новые рекорды приближения
        if (currentDistance < bestDistanceToGoal)
        {
            float recordBonus = (bestDistanceToGoal - currentDistance) * 0.3f;
            AddReward(recordBonus);
            bestDistanceToGoal = currentDistance;
        }
        
        // Штраф за долгое отсутствие прогресса
        if (Time.time - lastSignificantProgress > 10f)
        {
            AddReward(noProgressPenalty);
            lastSignificantProgress = Time.time; // Сбрасываем таймер чтобы избежать спама штрафов
        }
    }
    
    // Определяет и наказывает бессмысленные движения
    private void CheckStuckBehavior()
    {
        Vector3 currentPosition = transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        
        // Считаем агента застрявшим если он почти не двигается
        if (distanceMoved < 0.1f)
        {
            stuckTimer += Time.deltaTime;
            
            if (stuckTimer >= 3f)
            {
                AddReward(stuckPenalty);
                stuckCounter++;
                stuckTimer = 0f;
                
                // Прогрессивное наказание за повторные застревания
                if (stuckCounter >= 3)
                {
                    AddReward(stuckPenalty * 2f); // Удвоенный штраф за упорное застревание
                }
            }
        }
        else
        {
            // Агент движется - сбрасываем счетчики
            stuckTimer = 0f;
            lastPosition = currentPosition;
        }
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
    }

    public void FindNextGoal()
    {
        // Ищем цели только в пределах текущего уровня
        Goal[] goals = transform.parent.GetComponentsInChildren<Goal>();
        
        Goal closestGoal = null;
        float closestDistance = float.MaxValue;
        
        // Находим ближайшую активную цель
        foreach (Goal goal in goals)
        {
            bool isActive = goal.gameObject.activeInHierarchy;
            float distance = Vector3.Distance(transform.position, goal.transform.position);
            
            if (isActive && distance < closestDistance)
            {
                closestDistance = distance;
                closestGoal = goal;
            }
        }
        
        if (closestGoal != null)
        {
            goalTarget = closestGoal.transform;
            // Принудительно пересчитываем базовые расстояния для новой цели
            float actualDistance = Vector3.Distance(transform.position, goalTarget.position);
            lastDistanceToGoal = actualDistance;
            bestDistanceToGoal = actualDistance; // Сбрасываем рекорд для новой цели
        }
        else
        {
            goalTarget = null; // Все цели собраны
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("pit") || other.CompareTag("moving") || other.CompareTag("spit"))
        {
            HandleDeadlyCollision(other);
        }
        
        if (other.CompareTag("bridge"))
        {
            isOnBridge = true;
        }
    }
    
    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("bridge") && isOnBridge)
        {
            if (Time.time - lastBridgeRewardTime > bridgeRewardCooldown)
            {
                AddReward(bridgeReward);
                lastBridgeRewardTime = Time.time;
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("bridge"))
        {
            isOnBridge = false;
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("pit") || 
            collision.gameObject.CompareTag("moving") || 
            collision.gameObject.CompareTag("spit"))
        {
            HandleDeadlyCollision(collision.collider);
        }
        
        // Сброс вертикальной скорости при столкновении со стеной во время прыжка
        if (isJumping && collision.contacts.Length > 0)
        {
            Vector3 hitNormal = collision.contacts[0].normal;
            
            // Определяем вертикальную поверхность по малой Y-компоненте нормали
            if (Mathf.Abs(hitNormal.y) < 0.3f) // Почти вертикальная стена
            {
                // Обнуляем восходящую скорость чтобы персонаж упал вниз
                if (rb.linearVelocity.y > 0)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                }
            }
        }
    }

    private void HandleDeadlyCollision(Collider deadlyObject)
    {
        ResetGoalState();
        
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        isJumping = false;
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
        Goal[] goals = transform.parent.GetComponentsInChildren<Goal>();
        
        foreach (Goal goal in goals)
        {
            goal.ResetToInitialPosition();
        }
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
    }
}
