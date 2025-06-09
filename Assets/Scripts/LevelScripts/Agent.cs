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
    public Transform groundCheck; // Пустой объект под ногами персонажа
    public float groundDistance = 0.4f;
    public LayerMask groundMask = 1; // Слой земли и мостов (включает ground и bridge)
    public float jumpCooldown = 0.2f; // Кулдаун прыжка в секундах
    public float runAnimationSpeed = 0.7f; // Скорость анимации бега (0.5 = в 2 раза медленнее)
      [Header("Goal System")]
    public Transform goalTarget; // Цель для отслеживания
    public float proximityRewardDistance = 10f; // Увеличиваем радиус для покрытия всего уровня
    public float proximityReward = 0.01f; // Награда за приближение к цели
    public float globalDirectionReward = 0.005f; // Постоянная награда за движение в сторону цели
    
    [Header("Bridge Reward System")]
    public float bridgeReward = 0.02f; // Награда за нахождение на мосту
    public float bridgeRewardCooldown = 0.5f; // Кулдаун между наградами за мост
      [Header("Anti-Exploit Settings")]
    public float maxRewardPerEpisode = 50f; // Максимальная награда за эпизод для предотвращения эксплойтов
    public float episodeTimeLimit = 120f; // Максимальное время эпизода в секундах
    
    [Header("New Reward System Settings")]
    public float progressThreshold = 0.5f; // Минимальный прогресс для получения награды
    public float progressRewardMultiplier = 0.2f; // Множитель для наград за прогресс
    public float stuckPenalty = -0.2f; // Штраф за застревание
    public float noProgressPenalty = -0.1f; // Штраф за отсутствие прогресса
      private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 spawnPosition; // Истинная позиция спавна
    private Quaternion spawnRotation; // Истинный поворот спавна
    private Animator animator;
    private bool isGrounded;
    private bool jumpedWithInertia = false; 
    private float lastJumpTime = 0f;
    private float lastDistanceToGoal = float.MaxValue; // Последнее расстояние до цели
    private float bestDistanceToGoal = float.MaxValue; // Лучшее (минимальное) расстояние до текущей цели
    private bool isJumping = false; // Флаг для отслеживания состояния прыжка
    private float episodeStartTime; // Время начала эпизода
    private bool episodeLimitReached = false; // Флаг достижения лимита награды
    private float lastGoalCollectionTime = 0f; // Время последнего сбора цели    
    private bool isOnBridge = false; // Находится ли агент на мосту
    private float lastBridgeRewardTime = 0f; // Время последней награды за мост
    
    // Новые переменные для улучшенной системы наград
    private Vector3 lastPosition; // Последняя позиция для отслеживания движения
    private float stuckTimer = 0f; // Таймер для определения застревания
    private int stuckCounter = 0; // Счетчик застреваний
    private float lastRewardTime = 0f; // Время последней награды за прогресс
    private float lastSignificantProgress = 0f; // Время последнего значительного прогресса

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        
        // Сохраняем истинную позицию спавна только один раз
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
          initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        // Ищем CameraFollower только в родительском уровне
        CameraFollower cameraFollower = transform.parent?.GetComponentInChildren<CameraFollower>();
        if (cameraFollower != null)
        {
            cameraFollower.SetTarget(transform);
        }
    }    public override void OnEpisodeBegin()
    {
        // Телепортируемся на истинный спавн
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        
        // Обновляем текущую начальную позицию
        initialPosition = spawnPosition;
        initialRotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }        
        
        jumpedWithInertia = false;
        episodeStartTime = Time.time; // Запоминаем время начала эпизода
        episodeLimitReached = false; // Сбрасываем флаг лимита
        bestDistanceToGoal = float.MaxValue; // Сбрасываем лучшее расстояние        lastGoalCollectionTime = 0f; // Сбрасываем время сбора цели
        isOnBridge = false; // Сбрасываем состояние мостов
        lastBridgeRewardTime = 0f; // Сбрасываем время награды за мост
        
        // Сбрасываем новые переменные системы наград
        lastPosition = transform.position;
        stuckTimer = 0f;
        stuckCounter = 0;
        lastRewardTime = 0f;
        lastSignificantProgress = Time.time;

        // Сбрасываем состояние всех целей в начале эпизода
        ResetGoalState();
        
        // Автоматически находим ближайшую цель при старте эпизода
        FindNextGoal();        
        
        RequestDecision();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Позиция агента
        sensor.AddObservation(transform.position.x / 10f);
        sensor.AddObservation(transform.position.z / 10f);
        
        // Позиция цели (если есть)
        if (goalTarget != null)
        {
            Vector3 directionToGoal = goalTarget.position - transform.position;
            sensor.AddObservation(directionToGoal.x / 10f);
            sensor.AddObservation(directionToGoal.z / 10f);
            sensor.AddObservation(directionToGoal.magnitude / 10f); // Расстояние до цели
            
            // Дополнительные наблюдения для лучшей ориентации
            Vector3 normalizedDirection = directionToGoal.normalized;
            
            // Угол между направлением агента и направлением к цели
            float dot = Vector3.Dot(transform.forward, normalizedDirection);
            sensor.AddObservation(dot); // От -1 до 1
            
            // Определяем, нужно ли поворачивать влево или вправо
            float cross = Vector3.Cross(transform.forward, normalizedDirection).y;
            sensor.AddObservation(cross); // Положительное - поворот вправо, отрицательное - влево
            
            // Относительное направление к цели в локальных координатах агента
            Vector3 localDirection = transform.InverseTransformDirection(normalizedDirection);
            sensor.AddObservation(localDirection.x); // Влево/вправо относительно агента
            sensor.AddObservation(localDirection.z); // Вперед/назад относительно агента
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
        }        // Состояние агента
        sensor.AddObservation(isGrounded ? 1f : 0f);
        sensor.AddObservation(rb.linearVelocity.x / 10f);
        sensor.AddObservation(rb.linearVelocity.z / 10f);
        sensor.AddObservation(rb.linearVelocity.y / 10f); // Вертикальная скорость для понимания динамики прыжка
        
        // Информация о прыжке и кулдауне
        sensor.AddObservation(isJumping ? 1f : 0f); // Находится ли агент в состоянии прыжка
        
        // Кулдаун прыжка (0 = можно прыгать, 1 = максимальный кулдаун)
        float jumpCooldownRemaining = Mathf.Max(0f, (lastJumpTime + jumpCooldown) - Time.time);
        float normalizedCooldown = jumpCooldownRemaining / jumpCooldown; // От 0 до 1
        sensor.AddObservation(normalizedCooldown);
        
        // Может ли агент прыгнуть прямо сейчас (комбинированная информация)
        bool canJumpNow = isGrounded && !isJumping && (Time.time - lastJumpTime > jumpCooldown);
        sensor.AddObservation(canJumpNow ? 1f : 0f);
        
        // Информация о нахождении на мосту
        sensor.AddObservation(isOnBridge ? 1f : 0f);
    }public override void OnActionReceived(ActionBuffers actions)
    {          // Обновляем состояние земли перед обработкой действий
        if (groundCheck != null)
        {
            bool wasGrounded = isGrounded;
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            
            // Улучшенная логика сброса isJumping
            if (isGrounded)
            {
                // Если на земле и вертикальная скорость близка к нулю или отрицательная (падаем/стоим)
                if (rb.linearVelocity.y <= 0.1f)
                {
                    isJumping = false;
                }
            }
            // Дополнительная проверка: если долго в воздухе без значительной вертикальной скорости
            else if (isJumping && Time.time - lastJumpTime > 1f && Mathf.Abs(rb.linearVelocity.y) < 0.5f)
            {
                isJumping = false;
            }
        }

        int forwardBackwardAction = actions.DiscreteActions[0]; // Движение вперёд/назад
        int leftRightAction = actions.DiscreteActions[1];       // Движение влево/вправо
        int jumpAction = actions.DiscreteActions[2];            // Прыжок

        float maxSpeed = moveSpeed;
        float rotationSpeed = 220f;

        // Устанавливаем движение вперёд/назад
        Vector3 moveDirection = Vector3.zero;
        if (forwardBackwardAction == 1) // Вперёд (W)
        {
            moveDirection += transform.forward;
        }
        else if (forwardBackwardAction == 2) // Назад (S)
        {
            moveDirection -= transform.forward;
        }

        // Устанавливаем движение влево/вправо
        if (leftRightAction == 1) // Влево (A)
        {
            transform.Rotate(0, -rotationSpeed * Time.fixedDeltaTime, 0);
        }
        else if (leftRightAction == 2) // Вправо (D)
        {
            transform.Rotate(0, rotationSpeed * Time.fixedDeltaTime, 0);
        }

        // Применяем движение
        if (rb != null)
        {
            if (moveDirection != Vector3.zero)
            {
                Vector3 movement = new Vector3(
                    moveDirection.x * maxSpeed,
                    rb.linearVelocity.y,
                    moveDirection.z * maxSpeed
                );
                rb.linearVelocity = movement;
            }
            else
            {
                // Сбрасываем горизонтальную скорость, если нет движения
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }        // Обрабатываем прыжок
        bool canJump = Time.time - lastJumpTime > jumpCooldown;
        if (jumpAction == 1 && isGrounded && canJump && !isJumping)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            lastJumpTime = Time.time;
            isJumping = true; // Устанавливаем флаг прыжка
        }
        else if (jumpAction == 1 && (!canJump || isJumping))
        {
            // Небольшое наказание за попытку прыгнуть во время кулдауна или в воздухе
            AddReward(-0.01f);
        }

        // Логирование действий агента ПОСЛЕ выполнения
        string actionLog = "Agent actions: ";
        
        // Движение вперёд/назад
        switch (forwardBackwardAction)
        {
            case 1: actionLog += "Forward "; break;
            case 2: actionLog += "Backward "; break;
            default: actionLog += "NoMove "; break;
        }
        
        // Поворот
        switch (leftRightAction)
        {
            case 1: actionLog += "TurnLeft "; break;
            case 2: actionLog += "TurnRight "; break;
            default: actionLog += "NoTurn "; break;
        }
        
        // Прыжок
        if (jumpAction == 1)
        {
            actionLog +="Jump";
        }
        else
        {
            actionLog += "NoJump";
        }          // Выводим лог каждые 30 кадров (примерно 2 раза в секунду)
        // Логирование отключено для производительности        // Обновляем анимации
        if (animator != null)
        {
            float speed = moveDirection.magnitude; // Скорость движения
            bool isMoving = speed > 0; // Если есть движение
            
            // Дополнительная проверка для корректности состояния
            bool effectivelyGrounded = isGrounded && !isJumping;
            
            // Если на земле но застряли в состоянии прыжка, принудительно сбрасываем
            if (isGrounded && isJumping && Mathf.Abs(rb.linearVelocity.y) < 0.1f && Time.time - lastJumpTime > 0.5f)
            {
                isJumping = false;
                effectivelyGrounded = true;
            }
            
            // Устанавливаем скорость анимации только для движения на земле
            if (effectivelyGrounded && isMoving)
            {
                animator.speed = runAnimationSpeed;
            }
            else
            {
                animator.speed = 1f; // Нормальная скорость для прыжка и других анимаций
            }
            
            animator.SetFloat("Speed", speed);
            
            // IsMoving только для движения на земле, не мешает прыжку
            animator.SetBool("IsMoving", isMoving && effectivelyGrounded);

            // Обновляем IsGrounded в аниматоре
            animator.SetBool("IsGrounded", effectivelyGrounded);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        // Движение вперёд/назад
        if (Keyboard.current.wKey.isPressed)
            discreteActions[0] = 1; // Вперёд
        else if (Keyboard.current.sKey.isPressed)
            discreteActions[0] = 2; // Назад
        else
            discreteActions[0] = 0; // Нет движения

        // Движение влево/вправо
        if (Keyboard.current.aKey.isPressed)
            discreteActions[1] = 1; // Влево
        else if (Keyboard.current.dKey.isPressed)
            discreteActions[1] = 2; // Вправо
        else
            discreteActions[1] = 0; // Нет движения

        // Прыжок
        discreteActions[2] = Keyboard.current.spaceKey.isPressed ? 1 : 0;
    }    void Update()
    {
        RequestDecision();
        
        // Проверяем лимиты эпизода
        CheckEpisodeLimits();
        
        // Новая улучшенная система наград
        CheckProgressRewards();
        CheckStuckBehavior();
    }    private void CheckEpisodeLimits()
    {          // Проверяем лимит времени
        float episodeTime = Time.time - episodeStartTime;
        if (episodeTime > episodeTimeLimit)
        {
            EndEpisode();
            return;
        }
            // Проверяем лимит награды для предотвращения эксплойтов
        float currentReward = GetCumulativeReward();
        if (currentReward > maxRewardPerEpisode)
        {
            episodeLimitReached = true;
            EndEpisode();
        }
    }
    
    /// <summary>
    /// Новая система наград: награждает только за значительный прогресс к цели
    /// Устраняет шум от постоянных микро-наград
    /// </summary>
    private void CheckProgressRewards()
    {
        if (goalTarget == null || episodeLimitReached) return;
        
        float currentDistance = Vector3.Distance(transform.position, goalTarget.position);
        
        // Проверяем значительное приближение к цели
        float progressMade = lastDistanceToGoal - currentDistance;
        
        if (progressMade >= progressThreshold)
        {
            float progressReward = progressMade * progressRewardMultiplier;
            AddReward(progressReward);
            
            lastDistanceToGoal = currentDistance;
            lastSignificantProgress = Time.time;
            
            Debug.Log($"[PROGRESS REWARD] +{progressReward:F3} for {progressMade:F2} units progress toward goal");
        }
        
        // Обновляем лучшее расстояние для новых рекордов
        if (currentDistance < bestDistanceToGoal)
        {
            float recordBonus = (bestDistanceToGoal - currentDistance) * 0.3f;
            AddReward(recordBonus);
            bestDistanceToGoal = currentDistance;
            
            Debug.Log($"[RECORD BONUS] +{recordBonus:F3} for new best distance: {currentDistance:F2}");
        }
        
        // Штраф за долгое отсутствие прогресса (10 секунд)
        if (Time.time - lastSignificantProgress > 10f)
        {
            AddReward(noProgressPenalty);
            lastSignificantProgress = Time.time; // Предотвращаем спам штрафов
            
            Debug.Log($"[NO PROGRESS PENALTY] {noProgressPenalty} for 10+ seconds without progress");
        }
    }
    
    /// <summary>
    /// Система анти-застревания: определяет и наказывает бессмысленные движения
    /// </summary>
    private void CheckStuckBehavior()
    {
        float currentTime = Time.time;
        Vector3 currentPosition = transform.position;
        
        // Проверяем, находится ли агент примерно в том же месте
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        
        if (distanceMoved < 0.1f) // Практически не двигается
        {
            stuckTimer += Time.deltaTime;
            
            // Если застрял на 3+ секунды
            if (stuckTimer >= 3f)
            {
                AddReward(stuckPenalty);
                stuckCounter++;
                stuckTimer = 0f; // Сбрасываем таймер
                
                Debug.Log($"[STUCK PENALTY] {stuckPenalty} for being stuck (count: {stuckCounter})");
                
                // Прогрессивное наказание за повторные застревания
                if (stuckCounter >= 3)
                {
                    AddReward(stuckPenalty * 2f); // Двойной штраф
                    Debug.Log($"[MULTIPLE STUCK PENALTY] {stuckPenalty * 2f} for repeated stuck behavior");
                }
            }
        }
        else
        {
            // Агент движется - сбрасываем таймер застревания
            stuckTimer = 0f;
            lastPosition = currentPosition;
        }
    }public void OnGoalCollected(float reward)
    {
        
        // Дополнительная защита от множественных вызовов
        if (Time.time - lastGoalCollectionTime < 0.5f)
        {
            return;
        }
        
        lastGoalCollectionTime = Time.time;
        AddReward(reward);
        // НЕ вызываем FindNextGoal здесь - это будет сделано в Goal.cs после телепортации
    }    public void FindNextGoal()
    {
        
        // Ищем следующую активную цель только в родительском уровне
        Goal[] goals = transform.parent.GetComponentsInChildren<Goal>();
        
        Goal closestGoal = null;
        float closestDistance = float.MaxValue;
        
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
              
            // Принудительно пересчитываем расстояние
            float actualDistance = Vector3.Distance(transform.position, goalTarget.position);
            lastDistanceToGoal = actualDistance;
            bestDistanceToGoal = actualDistance; // Сбрасываем лучшее расстояние для новой цели
        }
        else
        {
            goalTarget = null;
        }
    }    void OnTriggerEnter(Collider other)
    {        
        // Проверка на попадание в смертельные препятствия по тегам
        if (other.CompareTag("pit") || other.CompareTag("moving") || other.CompareTag("spit"))
        {
            HandleDeadlyCollision(other);
        }
        
        // Проверка на вход на мост
        if (other.CompareTag("bridge"))
        {
            isOnBridge = true;
            Debug.Log($"[BRIDGE] Agent entered bridge: {other.name}");
        }
    }
    
    void OnTriggerStay(Collider other)
    {
        // Награда за нахождение на мосту
        if (other.CompareTag("bridge") && isOnBridge)
        {
            // Проверяем кулдаун для предотвращения спама наград
            if (Time.time - lastBridgeRewardTime > bridgeRewardCooldown)
            {
                AddReward(bridgeReward);
                lastBridgeRewardTime = Time.time;
                Debug.Log($"[BRIDGE REWARD] +{bridgeReward} for staying on bridge: {other.name} (Total: {GetCumulativeReward():F3})");
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        // Проверка на выход с моста
        if (other.CompareTag("bridge"))
        {
            isOnBridge = false;
            Debug.Log($"[BRIDGE] Agent left bridge: {other.name}");
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Проверяем если это объект с смертельными тегами
        if (collision.gameObject.CompareTag("pit") || 
            collision.gameObject.CompareTag("moving") || 
            collision.gameObject.CompareTag("spit"))
        {
            HandleDeadlyCollision(collision.collider);
        }
        
        // Проверяем столкновение со стеной во время прыжка
        if (isJumping && collision.contacts.Length > 0)
        {
            // Получаем нормаль поверхности столкновения
            Vector3 hitNormal = collision.contacts[0].normal;
            
            // Если столкнулись с вертикальной поверхностью (стеной)
            if (Mathf.Abs(hitNormal.y) < 0.3f) // y компонента мала означает вертикальную поверхность
            {
                // Сбрасываем вертикальную скорость чтобы персонаж упал
                if (rb.linearVelocity.y > 0)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                }
            }
        }
    }    private void HandleDeadlyCollision(Collider deadlyObject)
    {
        // Определяем тип смертельного препятствия для отладки
        string obstacleType = deadlyObject.tag;
        
        // Сбрасываем состояние цели при смерти
        ResetGoalState();
        
        // Телепортируем на истинную стартовую позицию
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }        
        jumpedWithInertia = false;
        isJumping = false;
        
        // Завершаем эпизод
        EndEpisode();
    }      // Убираем OnTriggerStay чтобы избежать множественных срабатываний
    // void OnTriggerStay(Collider other)
    // {
    //     // Дублируем проверку в OnTriggerStay для надежности при высокой скорости
    //     if (other.CompareTag("pit"))
    //     {
    //         HandlePitCollision(other);
    //     }
    // }    
    public void CompleteLevel()
    {
        float levelReward = 50f; // Увеличиваем награду за завершение уровня
        AddReward(levelReward);
        Debug.Log($"[LEVEL COMPLETE] Agent completed level! Reward: {levelReward}");
        EndEpisode();
    }void ResetGoalState()
    {
        
        // Сбрасываем только цели в текущем уровне
        Goal[] goals = transform.parent.GetComponentsInChildren<Goal>();
        
        foreach (Goal goal in goals)
        {
            goal.ResetToInitialPosition();
        }
        
    }

    void OnDrawGizmosSelected()
    {
        // Визуализируем область проверки земли в редакторе
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
        
        // Дополнительная визуализация raycast проверки
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, Vector3.down * 1.1f);
        
        // Визуализируем направление к цели
        if (goalTarget != null)
        {
            // КРАСНАЯ линия показывает к какой цели движется агент
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, goalTarget.position);
            
            // Красная сфера на позиции цели
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(goalTarget.position, 1f);
            
            // Показываем расстояние
            Vector3 midPoint = (transform.position + goalTarget.position) / 2f;
            float distance = Vector3.Distance(transform.position, goalTarget.position);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(midPoint, $"Distance: {distance:F1}");
            #endif
        }
    }
}