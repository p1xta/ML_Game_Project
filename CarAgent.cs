using System.Collections;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CarAgent : Agent
{
    
    [Header("Rewards")]
    [SerializeField] private float penaltyCollision = -10f; 
    [SerializeField] private float lateralLimit = 15f;

    [Header("Advanced Rewards")]
    [SerializeField] private float nearMissReward = 1f;
    [SerializeField] private float nearMissThreshold = 0.10f;
    [SerializeField] private float idleTimePenalty = -0.001f;

    private int stepsSinceLastReward = 0;
    
    private Rigidbody carRigidbody;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private HashSet<int> rewardedCars = new HashSet<int>();
    private Dictionary<int, float> potentialRewardCars = new Dictionary<int, float>();

    private void FixedUpdate()
    {
        List<int> carsToReward = new List<int>();

        foreach (var carPair in potentialRewardCars)
        {
            int carID = carPair.Key;
            float rewardAmount = carPair.Value;
        
            GameObject carObj = FindObjectByInstanceID(carID);
            if (carObj == null || Vector3.Distance(carObj.transform.position, transform.position) > 10f)
            {
                AddReward(rewardAmount);
                carsToReward.Add(carID);
            }
        }
    
        foreach (var carID in carsToReward)
        {
            potentialRewardCars.Remove(carID);
        }
    
        var sensorComponent = GetComponentInChildren<RayPerceptionSensorComponent3D>();
        if (sensorComponent != null)
        {
            RayPerceptionInput rayInput = sensorComponent.GetRayPerceptionInput();
            RayPerceptionOutput output = RayPerceptionSensor.Perceive(rayInput, false);
        
            for (int i = 0; i < output.RayOutputs.Length; i++)
            {
                if (output.RayOutputs[i].HasHit)
                {
                    GameObject hitObject = output.RayOutputs[i].HitGameObject;
                    if (hitObject != null && hitObject.CompareTag("car"))
                    {
                        int carInstanceID = hitObject.GetInstanceID();
                    
                        float hitFraction = output.RayOutputs[i].HitFraction;
                    
                        if (hitFraction < nearMissThreshold && hitFraction > 0.05f && !rewardedCars.Contains(carInstanceID))
                        {
                            float proximityFactor = 1.0f - (hitFraction / nearMissThreshold);
                            float reward = nearMissReward * proximityFactor;
            
                            if (!potentialRewardCars.ContainsKey(carInstanceID))
                            {
                                potentialRewardCars.Add(carInstanceID, reward);
                            }
            
                            rewardedCars.Add(carInstanceID);
                        }
                    }
                }
            }
        }
    }
    private GameObject FindObjectByInstanceID(int instanceID)
    {
        foreach (var obj in FindObjectsOfType<GameObject>())
        {
            if (obj.GetInstanceID() == instanceID)
                return obj;
        }
        return null;
    }
    private IEnumerator RemoveCarFromTracking(int carID, float delay)
    {
        yield return new WaitForSeconds(delay);
        rewardedCars.Remove(carID);
    }
    public override void Initialize()
    {
        carRigidbody = GetComponent<Rigidbody>();
        
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    public override void OnEpisodeBegin()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        if (carRigidbody)
        {
            carRigidbody.linearVelocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        rewardedCars.Clear();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float leftEdge = initialPosition.x - lateralLimit;
        float rightEdge = initialPosition.x + lateralLimit;
    
        float normalizedPosition = (2 * (transform.position.x - leftEdge) / (rightEdge - leftEdge)) - 1;
    
        normalizedPosition = Mathf.Clamp(normalizedPosition, -1f, 1f);
    
        sensor.AddObservation(normalizedPosition);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float lateralMovement = actions.ContinuousActions[0];
        float maxLateralSpeed = 10f;
    
        Vector3 movement = new Vector3(lateralMovement * maxLateralSpeed * Time.fixedDeltaTime, 0, 0);
        transform.position += movement;
    
        Vector3 currentPos = transform.position;
        float initialX = initialPosition.x;
        currentPos.x = Mathf.Clamp(currentPos.x, initialX - lateralLimit, initialX + lateralLimit);
        transform.position = currentPos;
    
        transform.rotation = initialRotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("car"))
        {
            potentialRewardCars.Clear();

            AddReward(penaltyCollision);
            EndEpisode();
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
    
        Vector2 move = Keyboard.current != null 
            ? new Vector2(
                Keyboard.current.aKey.isPressed ? -1f : (Keyboard.current.dKey.isPressed ? 1f : 0f),
                Keyboard.current.wKey.isPressed ? 1f : (Keyboard.current.sKey.isPressed ? -1f : 0f))
            : Vector2.zero;
    
        continuousActions[0] = move.x;
    }
}
