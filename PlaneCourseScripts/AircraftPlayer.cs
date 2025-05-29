using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aircraft
{
    public class AircraftPlayer : AircraftAgent
    {
        [Header("Input Bindings")]
        public InputAction pitchInput;
        public InputAction yawInput;
        public InputAction boostInput;
        public InputAction pauseInput;

        public override void Initialize()
        {
            base.Initialize();

            pitchInput.Enable();
            yawInput.Enable();
            boostInput.Enable();
            pauseInput.Enable();
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;

            float pitchValue = Mathf.Round(pitchInput.ReadValue<float>());
            discreteActions[0] = pitchValue == -1f ? 2 : (int)pitchValue;

            float yawValue = Mathf.Round(yawInput.ReadValue<float>());
            discreteActions[1] = yawValue == -1f ? 2 : (int)yawValue;

            float boostValue = Mathf.Round(boostInput.ReadValue<float>());
            discreteActions[2] = (int)boostValue;
        }


        private void OnDestroy()
        {
            pitchInput.Disable();
            yawInput.Disable();
            boostInput.Disable();
            pauseInput.Disable();
        }
    }
}