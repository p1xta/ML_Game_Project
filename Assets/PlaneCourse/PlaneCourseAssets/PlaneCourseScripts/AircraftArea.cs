using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace Aircraft
{
    public class AircraftArea : MonoBehaviour
    {
        [Tooltip("The path the race will take")]
        public SplineContainer racePath;  

        [Tooltip("The prefab to use for checkpoints")]
        public GameObject checkpointPrebab;

        [Tooltip("The prefab to use for the start/end checkpoint")]
        public GameObject finishCheckpointPrefab;

        [Tooltip("If true, enable training mode")]
        public bool trainingMode;

        public List<AircraftAgent> AircraftAgents { get; private set; }
        public List<GameObject> Checkpoints { get; private set; }

        private void FindAircraftAgents()
        {
            AircraftAgents = transform.GetComponentsInChildren<AircraftAgent>().ToList();
            Debug.Assert(AircraftAgents.Count > 0, "No agent found");
        }

        private void CreateCheckpoints()
        {
            Debug.Assert(racePath != null, "Race Path was not set");
            Checkpoints = new List<GameObject>();

            var spline = racePath.Spline; 
            int numKnots = spline.Count;

            for (int i = 0; i < numKnots; i++)
            {
                GameObject checkpoint = (i == numKnots - 1)
                    ? Instantiate(finishCheckpointPrefab)
                    : Instantiate(checkpointPrebab);

                checkpoint.transform.SetParent(racePath.transform, false);

                BezierKnot knot = spline[i];
                checkpoint.transform.localPosition = knot.Position;
                checkpoint.transform.localRotation = knot.Rotation;

                Checkpoints.Add(checkpoint);
            }
        }

        private void Awake()
        {
            if (AircraftAgents == null) FindAircraftAgents();
        }

        private void Start()
        {
            if (Checkpoints == null) CreateCheckpoints();
        }

        public void ResetAgentPosition(AircraftAgent agent, bool randomize = false)
        {
            if (AircraftAgents == null) FindAircraftAgents();
            if (Checkpoints == null) CreateCheckpoints();

            if (randomize)
            {
                agent.NextCheckpointIndex = UnityEngine.Random.Range(0, Checkpoints.Count);
            }

            int previousCheckpointIndex = agent.NextCheckpointIndex - 1;
            if (previousCheckpointIndex < 0) previousCheckpointIndex = Checkpoints.Count - 1;

            //Debug.Log($"ResetAgentPosition: spawning at checkpoint {previousCheckpointIndex}, heading to {agent.NextCheckpointIndex}");

            //var spline = racePath.Spline; 
            //int numKnots = spline.Count;

            //BezierKnot knot = spline[previousCheckpointIndex];
            //Vector3 basePosition = knot.Position;
            //Quaternion orientation = knot.Rotation;

            //Vector3 positionOffset = Vector3.right * (AircraftAgents.IndexOf(agent) - AircraftAgents.Count / 2f)
            //    * UnityEngine.Random.Range(9f, 10f);

            //agent.transform.localPosition = basePosition + orientation * positionOffset;
            //agent.transform.localRotation = orientation;

            GameObject checkpoint = Checkpoints[previousCheckpointIndex];
            Vector3 basePosition = checkpoint.transform.position;

            Vector3 nextCheckpointPos = Checkpoints[agent.NextCheckpointIndex].transform.position;
            Vector3 forwardDir = (nextCheckpointPos - basePosition).normalized;
            Vector3 upDir = Vector3.up;

            Quaternion orientation = Quaternion.LookRotation(forwardDir, upDir);

            Vector3 positionOffset = checkpoint.transform.right * (AircraftAgents.IndexOf(agent) - AircraftAgents.Count / 2f)
                * UnityEngine.Random.Range(9f, 10f); 

            agent.transform.position = basePosition + positionOffset;
            agent.transform.rotation = orientation;
        }
    }
}
