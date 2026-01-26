using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgingAgent.Scripts.Sensors
{
    /// <summary>
    /// Spatial Lidar SensorComponent that creates a modular ISensorSpatialLidar implementation
    /// Builds a point cloud map using Fibonacci sphere ray distribution
    /// </summary>
    public class SensorSpatialLidar : SensorComponent
    {
        [Header("Options")]
        public int numberOfRays = 360;
        public bool recordMap = false;
        public bool drawGizmos = false;

        [Header("Raycast Settings")]
        public float maxDistance = 50f;
        public LayerMask detectionLayers;

        [Header("References")]
        [Tooltip("(Optional)")]
        public Transform referenceTransform; 
        
        private Agent agent;
        private ISensorSpatialLidar _spatialLidarSensor;

        private void Awake()
        {
            if (!referenceTransform) referenceTransform = transform;
            agent = GetComponent<Agent>();
            if (!agent) Debug.Log("No agent found");
        }

        public override ISensor[] CreateSensors()
        {
            _spatialLidarSensor = new ISensorSpatialLidar(
                agent, referenceTransform, maxDistance, detectionLayers, numberOfRays, recordMap);
            
            return new ISensor[] { _spatialLidarSensor };
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (_spatialLidarSensor == null) return;
            if (!referenceTransform) referenceTransform = transform;

            Vector3 origin = referenceTransform.position;
            var rays = _spatialLidarSensor.GetRayDirections();

            for (int direction = 0; direction < rays.GetLength(0); direction++)
            {
                for (int i = 0; i < rays.GetLength(1); i++)
                {
                    Vector3 rayDirection = rays[direction, i];
                    if (rayDirection == Vector3.zero) continue;

                    Vector3 worldDirection = referenceTransform.TransformDirection(rayDirection);
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
                }
            }
        }
    }
}
