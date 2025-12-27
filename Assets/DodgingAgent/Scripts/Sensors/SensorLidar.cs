using UnityEngine;
using Unity.MLAgents.Sensors;

namespace DodgyBall.Scripts.Sensors
{
    /// <summary>
    /// Lidar SensorComponent that creates a modular ISensorLidar implementation
    /// Provides spherical raycast coverage with configurable cardinal, edge, and corner directions
    /// </summary>
    public class SensorLidar : SensorComponent
    {
        [Header("Options")]
        [Tooltip("6 cardinal rays (±X, ±Y, ±Z)")]
        public bool cardinalSensors = true;
        [Tooltip("12 edge diagonal rays")]
        public bool edgeSensors = true;
        [Tooltip("8 corner diagonal rays")]
        public bool cornerSensors = true;
        public bool drawGizmos = false;

        [Header("Raycast Settings")]
        public float maxDistance = 50f;
        public LayerMask detectionLayers;

        [Header("References")]
        [Tooltip("(Optional)")]
        public Transform referenceTransform;

        private ISensorLidar _lidarSensor;

        private void Awake()
        {
            if (!referenceTransform) referenceTransform = transform;
        }

        public override ISensor[] CreateSensors()
        {
            _lidarSensor = new ISensorLidar( referenceTransform, maxDistance, detectionLayers,
                cardinalSensors, edgeSensors, cornerSensors
            );

            return new ISensor[] { _lidarSensor };
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (_lidarSensor == null) return;
            if (!referenceTransform) referenceTransform = transform;
        
            Gizmos.color = Color.magenta;
        
            foreach (var direction in _lidarSensor.GetRayDirections())
            {
                Vector3 worldDirection = referenceTransform.TransformDirection(direction);
                Gizmos.DrawLine(referenceTransform.position, referenceTransform.position + worldDirection * maxDistance);
            }
        }
    }
}