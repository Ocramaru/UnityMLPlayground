using System;
using MBaske;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DodgingAgent.Scripts.Agents
{
    public enum TrainingObjective
    {
        HoldPosition,
        Explore
    }
    
    /// <summary>
    /// Drone agent for ML-Agents training
    /// </summary>
    public class DroneAgent : Agent
    {
        [SerializeField] private Multicopter multicopter;

        [Header("Training Configuration")]
        [SerializeField] private TrainingObjective objective = TrainingObjective.HoldPosition;
        [SerializeField] private float resetDistance = 50f;
        [SerializeField, Range(0f, 1f)] private float objectiveRewardWeight = 0.3f;

        [Tooltip("Target distance per step (m). Gets max reward at this speed in Explore mode.")]
        [SerializeField] private float optimalStepDistance = 1f;

        [Header("Success Condition")]
        [Tooltip("HoldPosition: steps to hold near spawn")]
        [SerializeField] private float successHoldSteps = 500f;
        [Tooltip("Explore: total meters to travel")]
        [SerializeField] private float successExploreDistance = 100f;
        [Tooltip("HoldPosition: max distance from spawn (m) to count as holding")]
        [SerializeField] private float holdThreshold = 1f;
        [Tooltip("Bonus reward for completing goal (faster = better via less penalty time)")]
        [SerializeField] private float successBonus = 50f;

        private Resetter resetter;
        private Vector3 initialPosition;
        private Vector3 lastPosition;
        private float goalProgress;
        private float successGoal;
        
        public override void Initialize()
        {
            multicopter.Initialize();
            resetter = new Resetter(transform);
        }

        public override void OnEpisodeBegin()
        {
            resetter.Reset();
            initialPosition = multicopter.Frame.position;
            lastPosition = multicopter.Frame.position;
            goalProgress = 0f;

            // Set success goal based on objective
            successGoal = objective == TrainingObjective.HoldPosition ? successHoldSteps : successExploreDistance; // TODO: Change if I add more Objectives
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

            switch (objective)
            {
                case TrainingObjective.HoldPosition: // Position holding reward (exponential decaying reward)
                    float distanceFromInitial = Vector3.Distance(multicopter.Frame.position, initialPosition);
                    float positionReward = Mathf.Exp(-distanceFromInitial * 0.5f);
                    AddReward(positionReward * objectiveRewardWeight);

                    // Calculate Goal Progress
                    if (distanceFromInitial <= holdThreshold)
                    {
                        goalProgress++;
                        if (goalProgress >= successGoal)
                        {
                            AddReward(successBonus);
                            // Debug.Log($"{gameObject.name} SUCCESS! Held position for {goalProgress} steps. Final reward: {GetCumulativeReward():F2}");
                            EndEpisode();
                        }
                    } else { goalProgress = 0; } // Reset on drift
                    break;

                case TrainingObjective.Explore: // distance traveled this step (exponential decaying reward)
                    float distanceTraveled = Vector3.Distance(multicopter.Frame.position, lastPosition);
                    float explorationReward = Mathf.Exp(-Mathf.Abs(distanceTraveled - optimalStepDistance) * 0.5f);
                    AddReward(explorationReward * objectiveRewardWeight);
                    lastPosition = multicopter.Frame.position;

                    // Calculate Goal Progress
                    goalProgress += distanceTraveled;
                    if (goalProgress >= successGoal)
                    {
                        AddReward(successBonus);
                        // Debug.Log($"{gameObject.name} SUCCESS! Explored {goalProgress:F2}m in {StepCount} steps. Final reward: {GetCumulativeReward():F2}");
                        EndEpisode();
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            // Debug.Log($"{gameObject.name} - Goal Progress: {goalProgress:F2} / {successGoal:F2} ({(goalProgress/successGoal*100f):F1}%)");
            // TODO: Make a ui bar to view progress on inference

            // Stay upright
            AddReward(Mathf.Clamp01(multicopter.Frame.up.y) * 0.75f);
            
            // Don't move to crazily
            float velocityMag = multicopter.Rigidbody.linearVelocity.magnitude;
            if (velocityMag > 0.5f) { AddReward(-(velocityMag - 0.5f) * 0.1f); }
            AddReward(multicopter.Rigidbody.angularVelocity.magnitude * -0.05f);
            
            // Check for distance (Could punish for going to far. Think about it.)
            if ((initialPosition - multicopter.Frame.position).magnitude > resetDistance)
            {
                EndEpisode();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Debug.Log($"HandleCollision hit {collision.gameObject.name}: Tag={collision.gameObject.tag}");
            if (collision.gameObject.CompareTag("Wall"))
            {
                var currentReward = GetCumulativeReward();
                float crashPenalty = -(currentReward * 1.1f); // Lose all (reward + 10%)
                AddReward(crashPenalty);
                // Debug.Log($"{gameObject.name} crashed after {StepCount} steps with reward {currentReward:F2}, penalty: {crashPenalty:F2}, final: {GetCumulativeReward():F2}");
                EndEpisode();
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