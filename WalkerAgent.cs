using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgentsExamples;
using Unity.MLAgents.Sensors;
using BodyPart = Unity.MLAgentsExamples.BodyPart;
using Random = UnityEngine.Random;

public class WalkerAgent : Agent
{
    [Header("Walk Speed")]
    [Range(0.1f, 10)]
    [SerializeField]
    private float m_TargetWalkingSpeed = 10;

    public float MTargetWalkingSpeed
    {
        get { return m_TargetWalkingSpeed; }
        set { m_TargetWalkingSpeed = Mathf.Clamp(value, .1f, m_maxWalkingSpeed); }
    }

    const float m_maxWalkingSpeed = 10;
    public bool randomizeWalkSpeedEachEpisode;
    private Vector3 m_WorldDirToWalk = Vector3.right;

    [Header("Target To Walk Towards")]
    public Transform target;

    [Header("Body Parts")]
    public Transform hips;
    public Transform chest;
    public Transform spine;
    public Transform head;
    public Transform thighL;
    public Transform shinL;
    public Transform footL;
    public Transform thighR;
    public Transform shinR;
    public Transform footR;
    public Transform armL;
    public Transform forearmL;
    public Transform handL;
    public Transform armR;
    public Transform forearmR;
    public Transform handR;

    OrientationCubeController m_OrientationCube;
    DirectionIndicator m_DirectionIndicator;
    JointDriveController m_JdController;
    EnvironmentParameters m_ResetParams;

    [Header("Jump & Pit Detection")]
    [SerializeField] private float heightCheckDistance = 2.0f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float jumpForce = 50f;
    private bool isOverPit = false;
    [SerializeField] private float prePitDistance = 3.0f;
    private bool prePitDetected = false;

    [SerializeField] private float farStairDistance = 2.5f;
    [SerializeField] private float veryFarStairDistance = 5.0f;
    private bool farStairsDetected = false;
    private bool veryFarStairsDetected = false;

    private bool stairsUpDetected = false;
    private bool isOnStairs = false;


    public override void Initialize()
    {
        m_OrientationCube = GetComponentInChildren<OrientationCubeController>();
        m_DirectionIndicator = GetComponentInChildren<DirectionIndicator>();
        m_JdController = GetComponent<JointDriveController>();
        m_JdController.SetupBodyPart(hips);
        m_JdController.SetupBodyPart(chest);
        m_JdController.SetupBodyPart(spine);
        m_JdController.SetupBodyPart(head);
        m_JdController.SetupBodyPart(thighL);
        m_JdController.SetupBodyPart(shinL);
        m_JdController.SetupBodyPart(footL);
        m_JdController.SetupBodyPart(thighR);
        m_JdController.SetupBodyPart(shinR);
        m_JdController.SetupBodyPart(footR);
        m_JdController.SetupBodyPart(armL);
        m_JdController.SetupBodyPart(forearmL);
        m_JdController.SetupBodyPart(handL);
        m_JdController.SetupBodyPart(armR);
        m_JdController.SetupBodyPart(forearmR);
        m_JdController.SetupBodyPart(handR);
        m_ResetParams = Academy.Instance.EnvironmentParameters;
    }

    private float spawnTimer = 0f;
    private readonly float spawnGracePeriod = 1.0f;
    private float currentY = 0f;
    private float _stairsBaseFootPosition = 0f;


    public override void OnEpisodeBegin()
    {
        foreach (var bodyPart in m_JdController.bodyPartsDict.Values)
        {
            bodyPart.Reset(bodyPart);
        }

        float randomX = Random.Range(-1f, 1f);
        float randomZ = Random.Range(-3f, 3f);
        transform.position = new Vector3(randomX, 1f, randomZ);
        hips.rotation = Quaternion.Euler(0, Random.Range(0.0f, 360.0f), 0);
        UpdateOrientationObjects();
        MTargetWalkingSpeed = randomizeWalkSpeedEachEpisode ? Random.Range(0.1f, m_maxWalkingSpeed) : MTargetWalkingSpeed;
        isOverPit = false;
        spawnTimer = spawnGracePeriod;

        float footLY = footL.position.y;
        float footRY = footR.position.y;
        float lowestFootY = Mathf.Min(footLY, footRY);

        _stairsBaseFootPosition = lowestFootY;
    }

    public void CollectObservationBodyPart(BodyPart bp, VectorSensor sensor)
    {
        sensor.AddObservation(bp.groundContact.touchingGround);
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.linearVelocity));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.angularVelocity));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.position - hips.position));
        if (bp.rb.transform != hips && bp.rb.transform != handL && bp.rb.transform != handR)
        {
            sensor.AddObservation(bp.rb.transform.localRotation);
            sensor.AddObservation(bp.currentStrength / m_JdController.maxJointForceLimit);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var cubeForward = m_OrientationCube.transform.forward;
        var velGoal = cubeForward * MTargetWalkingSpeed;
        var avgVel = GetAvgVelocity();
        sensor.AddObservation(Vector3.Distance(velGoal, avgVel));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(avgVel));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(velGoal));
        sensor.AddObservation(Quaternion.FromToRotation(hips.forward, cubeForward));
        sensor.AddObservation(Quaternion.FromToRotation(head.forward, cubeForward));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformPoint(target.transform.position));
        sensor.AddObservation(prePitDetected ? 1f : 0f);

        sensor.AddObservation(veryFarStairsDetected ? 1f : 0f);
        sensor.AddObservation(farStairsDetected ? 1f : 0f);
        sensor.AddObservation(stairsUpDetected ? 1f : 0f);
        sensor.AddObservation(isOnStairs ? 1f : 0f);

        foreach (var bodyPart in m_JdController.bodyPartsList)
        {
            CollectObservationBodyPart(bodyPart, sensor);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        bool shouldJump = actionBuffers.DiscreteActions[0] > 0;
        var bpDict = m_JdController.bodyPartsDict;
        var i = -1;
        var continuousActions = actionBuffers.ContinuousActions;
        bpDict[chest].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[spine].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[thighL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[thighR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[shinL].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[shinR].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[footR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[footL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], continuousActions[++i]);
        bpDict[armL].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[armR].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[forearmL].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[forearmR].SetJointTargetRotation(continuousActions[++i], 0, 0);
        bpDict[head].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[chest].SetJointStrength(continuousActions[++i]);
        bpDict[spine].SetJointStrength(continuousActions[++i]);
        bpDict[head].SetJointStrength(continuousActions[++i]);
        bpDict[thighL].SetJointStrength(continuousActions[++i]);
        bpDict[shinL].SetJointStrength(continuousActions[++i]);
        bpDict[footL].SetJointStrength(continuousActions[++i]);
        bpDict[thighR].SetJointStrength(continuousActions[++i]);
        bpDict[shinR].SetJointStrength(continuousActions[++i]);
        bpDict[footR].SetJointStrength(continuousActions[++i]);
        bpDict[armL].SetJointStrength(continuousActions[++i]);
        bpDict[forearmL].SetJointStrength(continuousActions[++i]);
        bpDict[armR].SetJointStrength(continuousActions[++i]);
        bpDict[forearmR].SetJointStrength(continuousActions[++i]);

        if (shouldJump && isOverPit && IsGrounded())
        {
            bpDict[hips].rb.AddForce(Vector3.up * jumpForce);
        }

        if (isOnStairs && IsGrounded())
        {
            float footLY = footL.position.y;
            float footRY = footR.position.y;
            float lowestFootY = Mathf.Min(footLY, footRY);
            currentY = lowestFootY;

            bpDict[thighL].SetJointStrength(0.8f + Mathf.Sin(Time.time * 3) * 0.2f);
            bpDict[thighR].SetJointStrength(0.8f + Mathf.Sin(Time.time * 3 + Mathf.PI) * 0.2f);

            float deltaY = currentY - _stairsBaseFootPosition;

            if (deltaY > 0.15f) 
            {
                AddReward(deltaY * 3.0f);
                _stairsBaseFootPosition = currentY;
            }
        }
    }

    private bool IsGrounded()
    {
        return m_JdController.bodyPartsDict[footL].groundContact.touchingGround ||
               m_JdController.bodyPartsDict[footR].groundContact.touchingGround;
    }

    void UpdateOrientationObjects()
    {
        m_WorldDirToWalk = target.position - hips.position;
        m_OrientationCube.UpdateOrientation(hips, target);
        if (m_DirectionIndicator)
        {
            m_DirectionIndicator.MatchOrientation(m_OrientationCube.transform);
        }
    }

    private bool _enteredStairs = false;
    private float _lastStairsY = 0f;
    private float _stairsProgressCounter = 0;

    private void CheckForPitAndStairs()
    {
        float additionalHeight = 0.5f;

        Vector3 rayOrigin = hips.position + m_OrientationCube.transform.forward * additionalHeight;
        RaycastHit nearHit;
        bool groundDetected = Physics.Raycast(rayOrigin, Vector3.down, out nearHit, heightCheckDistance, groundLayer);
        Debug.DrawRay(rayOrigin, Vector3.down * heightCheckDistance, groundDetected ? Color.green : Color.red);
        isOverPit = !groundDetected;

        Vector3 preRayOrigin = hips.position + m_OrientationCube.transform.forward * prePitDistance;
        RaycastHit preHit;
        bool preGroundDetected = Physics.Raycast(preRayOrigin, Vector3.down, out preHit, heightCheckDistance, groundLayer);
        Debug.DrawRay(preRayOrigin, Vector3.down * heightCheckDistance, preGroundDetected ? Color.green : Color.red);
        prePitDetected = !preGroundDetected;

        RaycastHit farHit;
        Vector3 farStairRayOrigin = hips.position + m_OrientationCube.transform.forward * farStairDistance;
        bool farStairGroundDetected = Physics.Raycast(farStairRayOrigin, Vector3.down, out farHit, heightCheckDistance, groundLayer);
        Debug.DrawRay(farStairRayOrigin, Vector3.down * heightCheckDistance, farStairGroundDetected ? Color.yellow : Color.magenta);

        farStairsDetected = false;
        if (farStairGroundDetected && farHit.collider.CompareTag("stairsup"))
        {
            farStairsDetected = true;
        }

        RaycastHit veryFarHit;
        Vector3 veryFarStairRayOrigin = hips.position + m_OrientationCube.transform.forward * veryFarStairDistance;
        bool veryFarStairGroundDetected = Physics.Raycast(veryFarStairRayOrigin, Vector3.down, out veryFarHit, heightCheckDistance, groundLayer);
        Debug.DrawRay(veryFarStairRayOrigin, Vector3.down * heightCheckDistance, veryFarStairGroundDetected ? Color.yellow : Color.magenta);
    
        veryFarStairsDetected = false;
        if (veryFarStairGroundDetected && veryFarHit.collider != null && veryFarHit.collider.CompareTag("stairsup"))
        {
            veryFarStairsDetected = true;
        }



        stairsUpDetected = false;
        if (groundDetected && nearHit.collider != null && nearHit.collider.CompareTag("stairsup"))
        {
            stairsUpDetected = true;
            _enteredStairs = true;
            _lastStairsY = currentY;
            _stairsProgressCounter = 0;
            isOnStairs = true;
        }
        else if (_enteredStairs)
        {
            isOnStairs = true;
        
            _stairsProgressCounter++;
        
            if ((currentY < _lastStairsY - 0.5f) || 
                (_stairsProgressCounter > 100 && currentY <= _lastStairsY) || 
                !IsGrounded())
            {
                _enteredStairs = false;
                isOnStairs = false;
            }
        
            if (currentY > _lastStairsY)
            {
                _lastStairsY = currentY;
                _stairsProgressCounter = 0;
            }
        }
        else
        {
            isOnStairs = false;
        }
    }

    private bool isJumping = false;
    private float landingTimer = 0f;
    private readonly float landingStableTime = 0.5f;
    private float airTime = 0f;
    private readonly float airTimeThreshold = 0.2f;

    void FixedUpdate()
    {
        if (spawnTimer > 0)
        {
            spawnTimer -= Time.fixedDeltaTime;
            isJumping = false;
            landingTimer = 0f;
        }

        UpdateOrientationObjects();
        CheckForPitAndStairs();

        bool grounded = IsGrounded();

        if (!grounded && spawnTimer <= 0)
        {
            airTime += Time.fixedDeltaTime;
            if (airTime >= airTimeThreshold)
            {
                isJumping = true;
                landingTimer = 0f;
            }
        }
        else if (isJumping && spawnTimer <= 0)
        {
            landingTimer += Time.fixedDeltaTime;
            if ((landingTimer >= landingStableTime) && !prePitDetected)
            {
                AddReward(0.9f);
                isJumping = false;
                landingTimer = 0f;
            }
        }
        else
        {
            airTime = 0f;
        }

        var cubeForward = m_OrientationCube.transform.forward;
        var matchSpeedReward = GetMatchingVelocityReward(cubeForward * MTargetWalkingSpeed, GetAvgVelocity());
        if (float.IsNaN(matchSpeedReward))
        {
            throw new ArgumentException(
                $"NaN in moveTowardsTargetReward.\n cubeForward: {cubeForward}\n hips.velocity: {m_JdController.bodyPartsDict[hips].rb.linearVelocity}\n maximumWalkingSpeed: {m_maxWalkingSpeed}");
        }
        var headForward = head.forward;
        headForward.y = 0;
        var lookAtTargetReward = (Vector3.Dot(cubeForward, headForward) + 1) * .5F;
        if (float.IsNaN(lookAtTargetReward))
        {
            throw new ArgumentException(
                $"NaN in lookAtTargetReward.\n cubeForward: {cubeForward}\n head.forward: {head.forward}");
        }
        AddReward(matchSpeedReward * lookAtTargetReward);
    }

    Vector3 GetAvgVelocity()
    {
        Vector3 velSum = Vector3.zero;
        int numOfRb = 0;
        foreach (var item in m_JdController.bodyPartsList)
        {
            numOfRb++;
            velSum += item.rb.linearVelocity;
        }
        var avgVel = velSum / numOfRb;
        return avgVel;
    }

    public float GetMatchingVelocityReward(Vector3 velocityGoal, Vector3 actualVelocity)
    {
        var velDeltaMagnitude = Mathf.Clamp(Vector3.Distance(actualVelocity, velocityGoal), 0, MTargetWalkingSpeed);
        return Mathf.Pow(1 - Mathf.Pow(velDeltaMagnitude / MTargetWalkingSpeed, 2), 2);
    }

    public void TouchedTarget()
    {
        AddReward(1f);
    }
}
