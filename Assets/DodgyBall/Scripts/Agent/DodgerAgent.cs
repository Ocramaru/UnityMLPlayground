using System;
using DodgyBall.Scripts.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

namespace DodgyBall.Scripts
{
    public class DodgerAgent : Agent
    {
        [Header("Movement")]
        public float forceMultiplier = 10;

        [Header("Weapon Tracking")]
        public int maxObservedWeapons = 3;
        public Orchestrator orchestrator;

        private Rigidbody _rb;
        private Transform[] _weaponRefs;
        private BufferSensorComponent _bufferSensor;
        
        public float[] aliveMilestones = { 1f, 3f, 5f, 10f, 15f};
        private int _currentAliveMilestone = 0;
        private float _timeAlive = 0f;
        public float Reward = 0f;
        
        void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _bufferSensor = GetComponent<BufferSensorComponent>();
            InitializeWeaponRefs();
        }

        private void InitializeWeaponRefs()
        {
            if (orchestrator)
            {
                _weaponRefs = orchestrator.GetWeaponTransforms();
            }
        }
        
        public override void OnEpisodeBegin()
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.localPosition = new Vector3( 0, 0.5f, 0);
            _timeAlive = 0f;
            _currentAliveMilestone = 0;
            Reward = 0f;

            orchestrator.RedrawRandomWeaponCount();
        }
        
        public override void CollectObservations(VectorSensor sensor)
        {
            // (agent) - 6 observations
            sensor.AddObservation(transform.localPosition);
            sensor.AddObservation(_rb.linearVelocity);

            // (weapons) - 6 observations per weapon * maxObservedWeapons
            if (_weaponRefs != null)
            {
                for (int i = 0; i < _weaponRefs.Length; i++)
                {
                    var p = _weaponRefs[i].localPosition;
                    var r = _weaponRefs[i].localRotation.eulerAngles;
                    
                    _bufferSensor.AppendObservation(new float[]
                    {
                        p.x, p.y, p.z,
                        r.x, r.y, r.z
                    });
                }
            }
        }
        
        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            // 3d movement Actions
            Vector3 controlSignal = Vector3.zero;
            controlSignal.x = actionBuffers.ContinuousActions[0];
            controlSignal.y = actionBuffers.ContinuousActions[1];
            controlSignal.z = actionBuffers.ContinuousActions[2];
            _rb.AddForce(controlSignal * forceMultiplier, ForceMode.Acceleration);
            
            // Rewards for time
            if (_timeAlive >= aliveMilestones[_currentAliveMilestone])
            {
                Reward += 1f;
                SetReward(Reward);
                if (_currentAliveMilestone == aliveMilestones.Length - 1) {
                    Debug.Log($"Reached final milestone: {_currentAliveMilestone} / {aliveMilestones.Length - 1}");
                    EndEpisode(); // End if reached final milestone
                }
                _currentAliveMilestone++;
            }
        }

        void FixedUpdate()
        {
            _timeAlive += Time.fixedDeltaTime;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.CompareTag("Wall")) {
                Vector3 normal = other.contacts[0].normal;
                _rb.linearVelocity = Vector3.Reflect(_rb.linearVelocity, normal);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Weapon")) {
                EndEpisode();
            }
        }

        // Use this to manual test inputs
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var cont = actionsOut.ContinuousActions;

            // WASD for XZ plane
            float x = 0f, z = 0f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;

            // J/K for vertical (Y)
            float y = 0f;
            if (Keyboard.current.jKey.isPressed) y += 1f;
            if (Keyboard.current.kKey.isPressed) y -= 1f;

            // Normalize so diagonals aren’t faster
            Vector3 dir = new Vector3(x, y, z);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            cont[0] = dir.x;
            cont[1] = dir.y;
            cont[2] = dir.z;
        }
    }
}
