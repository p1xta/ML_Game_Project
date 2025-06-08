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
    public float proximityRewardDistance = 10f;
    public float proximityReward = 0.01f;
    public float globalDirectionReward = 0.005f; 
    public float maxRewardPerEpisode = 50f;
    public float episodeTimeLimit = 120f;
      private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Animator animator;
    private bool isGrounded;
    private bool jumpedWithInertia = false; 
    private float lastJumpTime = 0f;
    private float lastDistanceToGoal = float.MaxValue;
    private float bestDistanceToGoal = float.MaxValue;
    private bool isJumping = false;
    private float episodeStartTime;
    private bool episodeLimitReached = false;
    private float lastGoalCollectionTime = 0f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
          initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        CameraFollower cameraFollower = transform.parent?.GetComponentInChildren<CameraFollower>();
        if (cameraFollower != null)
        {
            cameraFollower.SetTarget(transform);
        }
    }    public override void OnEpisodeBegin()
    {
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        
        initialPosition = spawnPosition;
        initialRotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }        
        
        jumpedWithInertia = false;
        episodeStartTime = Time.time;
        episodeLimitReached = false;
        bestDistanceToGoal = float.MaxValue;
        lastGoalCollectionTime = 0f;
        
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
        
        sensor.AddObservation(isJumping ? 1f : 0f);
        
        float jumpCooldownRemaining = Mathf.Max(0f, (lastJumpTime + jumpCooldown) - Time.time);
        float normalizedCooldown = jumpCooldownRemaining / jumpCooldown; // От 0 до 1
        sensor.AddObservation(normalizedCooldown);
        
        bool canJumpNow = isGrounded && !isJumping && (Time.time - lastJumpTime > jumpCooldown);
        sensor.AddObservation(canJumpNow ? 1f : 0f);
    }public override void OnActionReceived(ActionBuffers actions)
    {
        if (groundCheck != null)
        {
            bool wasGrounded = isGrounded;
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
            
            if (isGrounded)
            {
                if (rb.linearVelocity.y <= 0.1f)
                {
                    isJumping = false;
                }
            }
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
                Vector3 movement = new Vector3(
                    moveDirection.x * maxSpeed,
                    rb.linearVelocity.y,
                    moveDirection.z * maxSpeed
                );
                rb.linearVelocity = movement;
            }
            else
            {
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
        else if (jumpAction == 1 && (!canJump || isJumping))
        {
            AddReward(-0.01f);
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
            case 1: actionLog += "TurnLeft "; break;
            case 2: actionLog += "TurnRight "; break;
            default: actionLog += "NoTurn "; break;
        }
        
        if (jumpAction == 1)
        {
            actionLog +="Jump";
        }
        else
        {
            actionLog += "NoJump";
        }
        if (animator != null)
        {
            float speed = moveDirection.magnitude;
            bool isMoving = speed > 0;
            
            bool effectivelyGrounded = isGrounded && !isJumping;
            
            if (isGrounded && isJumping && Mathf.Abs(rb.linearVelocity.y) < 0.1f && Time.time - lastJumpTime > 0.5f)
            {
                isJumping = false;
                effectivelyGrounded = true;
            }
            
            if (effectivelyGrounded && isMoving)
            {
                animator.speed = runAnimationSpeed;
            }
            else
            {
                animator.speed = 1f;
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

        discreteActions[2] = Keyboard.current.spaceKey.isPressed ? 1 : 0;
    }    void Update()
    {
        RequestDecision();
        
        CheckEpisodeLimits();
        if (goalTarget != null && !episodeLimitReached)
        {
            float currentDistance = Vector3.Distance(transform.position, goalTarget.position);
            Vector3 directionToGoal = (goalTarget.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, directionToGoal);
              // ОСНОВНАЯ НАГРАДА: пропорциональная реальному приближению к цели
            float distanceChange = lastDistanceToGoal - currentDistance;
              // Дополнительная награда за достижение нового рекорда близости к цели
            if (currentDistance < bestDistanceToGoal)
            {
                float recordBonus = (bestDistanceToGoal - currentDistance) * 0.2f;
                AddReward(recordBonus);
                bestDistanceToGoal = currentDistance;
            }
            
            if (distanceChange > 0.01f)
            {
                float proximityReward = distanceChange * 0.5f; // Базовая награда
                
                // Бонус за правильное направление взгляда
                if (dot > 0.7f)
                {
                    proximityReward *= 1.5f;
                }
                else if (dot > 0.3f)
                {
                    proximityReward *= 1.2f;
                }
                  AddReward(proximityReward);
                
            }
            else if (distanceChange < -0.01f) 
            {
                float distancePenalty = Mathf.Abs(distanceChange) * 0.3f;
                AddReward(-distancePenalty);
            }
            
            // Небольшая постоянная награда за движение в правильном направлении
            if (rb.linearVelocity.magnitude > 0.5f && dot > 0.5f)
            {
                AddReward(0.0005f);
            }
            
            AddReward(-0.0002f);
            
            lastDistanceToGoal = currentDistance;
        }
        else if (!episodeLimitReached)
        {
            AddReward(-0.002f);
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
    }    public void OnGoalCollected(float reward)
    {
        
        if (Time.time - lastGoalCollectionTime < 0.5f)
        {
            return;
        }
        
        lastGoalCollectionTime = Time.time;
        AddReward(reward);
    }    public void FindNextGoal()
    {
        
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
              
            float actualDistance = Vector3.Distance(transform.position, goalTarget.position);
            lastDistanceToGoal = actualDistance;
            bestDistanceToGoal = actualDistance;
        }
        else
        {
            goalTarget = null;
        }
    }    void OnTriggerEnter(Collider other)
    {        
        if (other.CompareTag("pit") || other.CompareTag("moving") || other.CompareTag("spit"))
        {
            HandleDeadlyCollision(other);
        }
    }    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("pit") || 
            collision.gameObject.CompareTag("moving") || 
            collision.gameObject.CompareTag("spit"))
        {
            HandleDeadlyCollision(collision.collider);
        }
        
        if (isJumping && collision.contacts.Length > 0)
        {
            Vector3 hitNormal = collision.contacts[0].normal;
            
            if (Mathf.Abs(hitNormal.y) < 0.3f)
            {
                if (rb.linearVelocity.y > 0)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                }
            }
        }
    }    private void HandleDeadlyCollision(Collider deadlyObject)
    {
        string obstacleType = deadlyObject.tag;
        
        ResetGoalState();
        
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }        
        jumpedWithInertia = false;
        isJumping = false;
        
        EndEpisode();
    }
    // void OnTriggerStay(Collider other)
    // {
    //     if (other.CompareTag("pit"))
    //     {
    //         HandlePitCollision(other);
    //     }
    // }
    public void CompleteLevel()
    {
        float levelReward = 20f;
        AddReward(levelReward);
        EndEpisode();
    }    void ResetGoalState()
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
    }
}