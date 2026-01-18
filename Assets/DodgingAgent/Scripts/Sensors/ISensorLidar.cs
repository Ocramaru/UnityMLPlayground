using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgingAgent.Scripts.Sensors
{
    /// <summary>
    /// Lidar sensor using spherical raycast coverage
    /// Supports cardinal (6), edge (12), and corner (8) directions for attempted 360° coverage
    /// </summary>
    public class ISensorLidar : ISensor
    {
        private readonly Transform _referenceTransform;
        private readonly float _maxDistance;
        private readonly LayerMask _detectionLayers;
        private readonly Vector3[] _rayDirections;

        public ISensorLidar(Transform referenceTransform, float maxDistance, LayerMask detectionLayers,
            bool cardinalSensors, bool edgeSensors, bool cornerSensors)
        {
            _referenceTransform = referenceTransform;
            _maxDistance = maxDistance;
            _detectionLayers = detectionLayers;
            _rayDirections = SetupRayDirections(cardinalSensors, edgeSensors, cornerSensors);
        }

        private static Vector3[] SetupRayDirections(bool cardinalSensors, bool edgeSensors, bool cornerSensors)
        {
            List<Vector3> directions = new List<Vector3>();

            // 6 Cardinal directions
            if (cardinalSensors)
            {
                directions.Add(Vector3.right);      // +X
                directions.Add(Vector3.left);       // -X
                directions.Add(Vector3.up);         // +Y
                directions.Add(Vector3.down);       // -Y
                directions.Add(Vector3.forward);    // +Z
                directions.Add(Vector3.back);       // -Z
            }

            // 12 Edge diagonals
            if (edgeSensors)
            {
                // XY plane
                directions.Add(new Vector3(1, 1, 0).normalized);
                directions.Add(new Vector3(1, -1, 0).normalized);
                directions.Add(new Vector3(-1, 1, 0).normalized);
                directions.Add(new Vector3(-1, -1, 0).normalized);

                // XZ plane
                directions.Add(new Vector3(1, 0, 1).normalized);
                directions.Add(new Vector3(1, 0, -1).normalized);
                directions.Add(new Vector3(-1, 0, 1).normalized);
                directions.Add(new Vector3(-1, 0, -1).normalized);

                // YZ plane
                directions.Add(new Vector3(0, 1, 1).normalized);
                directions.Add(new Vector3(0, 1, -1).normalized);
                directions.Add(new Vector3(0, -1, 1).normalized);
                directions.Add(new Vector3(0, -1, -1).normalized);
            }

            // 8 Corner diagonals
            if (cornerSensors)
            {
                directions.Add(new Vector3(1, 1, 1).normalized);
                directions.Add(new Vector3(1, 1, -1).normalized);
                directions.Add(new Vector3(1, -1, 1).normalized);
                directions.Add(new Vector3(1, -1, -1).normalized);
                directions.Add(new Vector3(-1, 1, 1).normalized);
                directions.Add(new Vector3(-1, 1, -1).normalized);
                directions.Add(new Vector3(-1, -1, 1).normalized);
                directions.Add(new Vector3(-1, -1, -1).normalized);
            }

            return directions.ToArray();
        }

        public ObservationSpec GetObservationSpec()
        {
            return ObservationSpec.Vector(_rayDirections.Length);
        }

        public int Write(ObservationWriter writer)
        {
            float[] observations = new float[_rayDirections.Length];
            int index = 0;
            
            foreach (var direction in _rayDirections)
            {
                Vector3 worldDirection = _referenceTransform.TransformDirection(direction);

                if (Physics.Raycast(_referenceTransform.position, worldDirection, out RaycastHit hit, _maxDistance,
                        _detectionLayers))
                {
                    // Normalized distance: 1.0 = very close, 0.0 = at max distance
                    observations[index] = 1f - (hit.distance / _maxDistance);
                }
                else
                { observations[index] = 0f; } // No hit = 0.0
                index++;
            }
            writer.AddList(observations);
            // Debug.Log($"Lidar: [{string.Join(", ", observations)}]");
            return _rayDirections.Length;
        }

        public byte[] GetCompressedObservation()
        {
            return null;
        }

        public void Update()
        {
            // No per-frame updates needed for lidar
        }

        public void Reset()
        {
            // No state to reset
        }

        public CompressionSpec GetCompressionSpec()
        {
            return CompressionSpec.Default();
        }

        public string GetName()
        {
            return "LidarSensor";
        }

        public Vector3[] GetRayDirections()
        {
            return _rayDirections;
        }
    }
}