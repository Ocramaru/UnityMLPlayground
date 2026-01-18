using Unity.MLAgents.Sensors;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DodgingAgent.Scripts.Sensors
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

            Vector3 origin = referenceTransform.position;

            foreach (var direction in _lidarSensor.GetRayDirections())
            {
                Vector3 worldDirection = referenceTransform.TransformDirection(direction);
                bool hit = Physics.Raycast(origin, worldDirection, out RaycastHit hitInfo, maxDistance, detectionLayers);
                float distance; Vector3 endPoint;
                float color_t = 1f;
                if (hit) {
                    distance = hitInfo.distance;
                    endPoint = hitInfo.point;
                    color_t = distance / maxDistance;
                } else {
                    distance = maxDistance;
                    endPoint = origin + worldDirection * maxDistance;
                }
                float hue = Mathf.Lerp(120f, 0f, 1f - color_t) / 360f;
                Color color = Color.HSVToRGB(hue, 1f, 1f);
                color.a = 0.3f;
                Gizmos.color = color;
                
                Gizmos.DrawLine(origin, endPoint);

#if UNITY_EDITOR
                Vector3 labelPos = origin + worldDirection * (distance * 0.5f) + Vector3.up * 0.2f;
                Handles.Label(labelPos, $"{distance:F1}m");
#endif
            }
        }
    }
}