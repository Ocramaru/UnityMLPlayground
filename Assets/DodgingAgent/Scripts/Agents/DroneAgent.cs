using MBaske;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DodgingAgent.Scripts.Agents
{
    /// <summary>
    /// Drone agent for ML-Agents training
    /// </summary>
    public class DroneAgent : Agent
    {
        [SerializeField] private Multicopter multicopter;

        private Resetter resetter;
        private Vector3 initialPosition;

        public override void Initialize()
        {
            multicopter.Initialize();
            resetter = new Resetter(transform);
        }

        public override void OnEpisodeBegin()
        {
            resetter.Reset();
            initialPosition = multicopter.Frame.position;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Note: ImuSensor and LidarSensor observations are automatically collected
            // via SensorComponent - no manual collection needed!

            // Rotor thrust (4 observations)
            foreach (var rotor in multicopter.Rotors)
            {
                sensor.AddObservation(rotor.CurrentThrust);
            }
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            // Handle thrust inputs
            float[] actions = actionBuffers.ContinuousActions.Array;
            float[] mappedThrust = new float[actions.Length];

            for (int i = 0; i < actions.Length; i++)
            {
                mappedThrust[i] = Mathf.Lerp(-0.4f, 1f, (actions[i] + 1f) * 0.5f);
            }
            multicopter.UpdateThrust(mappedThrust);
            
            // Position holding reward
            float distanceFromInitial = Vector3.Distance(multicopter.Frame.position, initialPosition);
            float positionReward = Mathf.Exp(-distanceFromInitial * 0.5f);
            AddReward(positionReward * 0.1f);
            
            // Stay upright
            AddReward(Mathf.Clamp01(multicopter.Frame.up.y) * 0.15f);
            
            // Don't move to crazily
            float velocityMag = multicopter.Rigidbody.linearVelocity.magnitude;
            if (velocityMag > 0.5f) { AddReward(-(velocityMag - 0.5f) * 0.1f); }
            AddReward(multicopter.Rigidbody.angularVelocity.magnitude * -0.05f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Debug.Log($"HandleCollision hit {collision.gameObject.name}");
            if (collision.gameObject.CompareTag("Wall")) {
                AddReward(-1f); // Penalty for hitting wall, but continue episode
                resetter.Reset();
            }
        }
        
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var cont = actionsOut.ContinuousActions;

            // Throttle (Space/Shift for up/down)
            float throttle = 0f;
            if (Keyboard.current.spaceKey.isPressed) throttle = 1f;
            if (Keyboard.current.leftShiftKey.isPressed) throttle = -1f;

            // Pitch (W/S - forward/backward)
            float pitch = 0f;
            if (Keyboard.current.wKey.isPressed) pitch = 1f;
            if (Keyboard.current.sKey.isPressed) pitch = -1f;

            // Roll (A/D - left/right)
            float roll = 0f;
            if (Keyboard.current.aKey.isPressed) roll = -1f;
            if (Keyboard.current.dKey.isPressed) roll = 1f;

            // Yaw (Q/E - rotate left/right)
            float yaw = 0f;
            if (Keyboard.current.qKey.isPressed) yaw = -1f;
            if (Keyboard.current.eKey.isPressed) yaw = 1f;

            // Standard quadcopter mixing (assuming 4 rotors in X configuration)
            if (multicopter.Rotors.Length == 4)
            {
                cont[0] = throttle - pitch + roll - yaw;
                cont[1] = throttle - pitch - roll + yaw;
                cont[2] = throttle + pitch - roll - yaw;
                cont[3] = throttle + pitch + roll + yaw;

                for (int i = 0; i < 4; i++)
                {
                    cont[i] = Mathf.Clamp(cont[i], -1f, 1f);
                }
            }
            else
            {
                for (int i = 0; i < multicopter.Rotors.Length; i++)
                {
                    cont[i] = throttle;
                }
            }
        }
    }
}