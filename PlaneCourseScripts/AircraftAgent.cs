using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;

namespace Aircraft
{
    public class AircraftAgent : Agent
    {
        [Header("Movement Parameters")]
        public float thrust = 100000f;
        public float pitchSpeed = 100f;
        public float yawSpeed = 100f; 
        public float rollSpeed = 100f;
        public float boostMultiplier = 2f;

        [Header("Explosion Stuff")]
        [Tooltip("The aircraft mesh that will disappear on explosion")]
        public GameObject meshObject;

        [Tooltip("The game object of the explosion particle effect")]
        public GameObject explosionEffect;

        [Header("Training")]
        [Tooltip("Number of steps to time out after in training")]
        public int stepTimeout = 300;

        public int NextCheckpointIndex { get; set; }

        private AircraftArea area;
        new private Rigidbody rigidbody;
        private TrailRenderer trail;

        // When the next step timeout will be during training
        private float nextStepTimeout;

        // Остановлен ли самолет
        private bool frozen = false;

        // Управление
        private float pitchChange = 0f;
        private float smoothPitchChange = 0f;
        private float maxPitchAngle = 45f;
        private float yawChange = 0f;
        private float smoothYawChange = 0f;
        private float rollChange = 0f;
        private float smoothRollChange = 0f;
        private float maxRollAngle = 45f;
        private bool boost;


        /// <summary>
        /// Инициализация агента
        /// </summary>
        public override void Initialize()
        {
            area = GetComponentInParent<AircraftArea>();
            rigidbody = GetComponent<Rigidbody>();
            trail = GetComponent<TrailRenderer>();

            // Override the max step set in the inspector
            // Max 5000 steps if training, infinite steps if racing
            MaxStep = area.trainingMode ? 5000 : 0;
        }


        /// <summary>
        /// Что происходит, когда агент принимает решение(либо если игрок управляет самолетом, то какие решения принимает он)
        /// </summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            if (frozen) return;

            var discreteActions = actions.DiscreteActions;

            pitchChange = discreteActions[0]; // =0: не меняется, =1: вверх, 2: вниз(тогда далее ставит -1)
            if (pitchChange == 2) pitchChange = -1f;

            yawChange = discreteActions[1]; // =0: не меняется, 1: вправо, 2: влево
            if (yawChange == 2) yawChange = -1f;

            boost = discreteActions[2] == 1;
            if (boost && !trail.emitting) trail.Clear(); 
            trail.emitting = boost;

            ProcessMovement();

            if (area.trainingMode)
            {
                AddReward(-1f / MaxStep);

                if (StepCount > nextStepTimeout)
                {
                    AddReward(-.5f);
                    EndEpisode();
                }

                Vector3 localCheckpointDir = VectorToNextCheckpoint();
                if (localCheckpointDir.magnitude < Academy.Instance.EnvironmentParameters.GetWithDefault("checkpoint_radius", 0f))
                {
                    GotCheckpoint();
                }
            }
        }


        /// <summary>
        /// Вызывается, когда начинается новый тренировочный эпизод
        /// </summary>
        public override void OnEpisodeBegin()
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            trail.emitting = false;

            NextCheckpointIndex = 0;
            area.ResetAgentPosition(agent: this, randomize: area.trainingMode);

            if (area.trainingMode) nextStepTimeout = StepCount + stepTimeout;
        }


        /// <summary>
        /// Возвращаем вектор до следующего чекпойнта
        /// </summary>
        private Vector3 VectorToNextCheckpoint()
        {
            Vector3 nextCheckpointDir = area.Checkpoints[NextCheckpointIndex].transform.position - transform.position;
            Vector3 localCheckpointDir = transform.InverseTransformDirection(nextCheckpointDir);
            return localCheckpointDir;
        }


        /// <summary>
        /// Вызывается, когда агент пролетает через нужный чекпойнт
        /// </summary>
        private void GotCheckpoint()
        {
            NextCheckpointIndex = (NextCheckpointIndex + 1) % area.Checkpoints.Count;

            if (area.trainingMode)
            {
                AddReward(.5f);
                nextStepTimeout = StepCount + stepTimeout;
            }
        }


        /// <summary>
        /// Обработка управления
        /// </summary>
        private void ProcessMovement()
        {
            float boostModifier = boost ? boostMultiplier : 1f;

            rigidbody.AddForce(transform.forward * thrust * boostModifier, ForceMode.Force);

            Vector3 curRot = transform.rotation.eulerAngles;

            float rollAngle = curRot.z > 180f ? curRot.z - 360f : curRot.z;
            if (yawChange == 0f)
            {
                rollChange = -rollAngle / maxRollAngle;
            }
            else
            {
                rollChange = -yawChange;
            }

            smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
            smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);
            smoothRollChange = Mathf.MoveTowards(smoothRollChange, rollChange, 2f * Time.fixedDeltaTime);

            float pitch = curRot.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

            float yaw = curRot.y + smoothYawChange * Time.fixedDeltaTime * yawSpeed;

            float roll = curRot.z + smoothRollChange * Time.fixedDeltaTime * rollSpeed;
            if (roll > 180f) roll -= 360f;
            roll = Mathf.Clamp(roll, -maxRollAngle, maxRollAngle);

            transform.rotation = Quaternion.Euler(pitch, yaw, roll);
        }


        /// <summary>
        /// Собираем информацию, на основе которой агент принимает решение
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(transform.InverseTransformDirection(rigidbody.linearVelocity));

            sensor.AddObservation(VectorToNextCheckpoint());

            Vector3 nextCheckpointForward = area.Checkpoints[NextCheckpointIndex].transform.forward;
            sensor.AddObservation(transform.InverseTransformDirection(nextCheckpointForward));

        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            Debug.LogError("Heuristic() was called on " + gameObject.name +
                " Make sure only the AircraftPlayer is set to Behavior Type: Heuristic Only.");
        }

        public void FreezeAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in training");
            frozen = true;
            rigidbody.Sleep();
            trail.emitting = false;
        }

        public void ResumeAgent()
        {
            Debug.Assert(area.trainingMode == false, "Freeze/Thaw not supported in training");
            frozen = false;
            rigidbody.WakeUp();
        }


        /// <summary>
        /// Cрабатывание триггера чекпойнта
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            //Debug.Log($"Triggered: {other.gameObject.name}");

            if (other.transform.CompareTag("checkpoint") &&
                other.gameObject == area.Checkpoints[NextCheckpointIndex])
            {
                //Debug.Log($"Correct checkpoint reached!");
                GotCheckpoint();
            }
        }


        /// <summary>
        /// RОбработка коллизий
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.transform.CompareTag("agent"))
            {
                if (area.trainingMode)
                {
                    AddReward(-1f);
                    EndEpisode();
                }
                else
                {
                    StartCoroutine(ExplosionReset());
                }
            }
        }


        /// <summary>
        /// Сброс позиции после взрыва
        /// </summary>
        private IEnumerator ExplosionReset()
        {
            FreezeAgent();

            meshObject.SetActive(false);
            explosionEffect.SetActive(true);
            yield return new WaitForSeconds(2f);

            meshObject.SetActive(true);
            explosionEffect.SetActive(false);

            area.ResetAgentPosition(agent: this);
            yield return new WaitForSeconds(1f);

            ResumeAgent();
        }
    }
}