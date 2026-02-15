using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;
using DodgingAgent.Scripts.Core;

namespace DodgingAgent.Scripts.Sensors
{
    /// <summary>
    /// Spatial LiDAR sensor that builds a point cloud map of the environment.
    /// Collects world-space points with step-based timestamps.
    /// </summary>
    public class ISensorSpatialLidar : ISensor
    {
        private readonly Transform _referenceTransform;
        private readonly float _maxDistance;
        private readonly LayerMask _detectionLayers;
        private readonly Vector3[,] _rayDirections;
        private int maxVisualHeight = 0;
        
        private readonly Agent _agent; // needed for step count

        private readonly GeodesicMeshMap _geodesicMeshMap;
        private bool _firstStep = true;
        private int _episode;

        // private const float AngleThreshold = 5f; // degrees - refine mesh if hit deviates more than this

        public ISensorSpatialLidar(Agent agent, Transform referenceTransform, float maxDistance, LayerMask detectionLayers, int numberOfRays = 360, GeodesicMeshMap geodesicMeshMap = null)
        {
            _agent = agent;
            _referenceTransform = referenceTransform;
            _maxDistance = maxDistance;
            _detectionLayers = detectionLayers;
            _rayDirections = SetupUnitDirections(numberOfRays);

            _geodesicMeshMap = geodesicMeshMap;
        }

        private Vector3[,] SetupUnitDirections(int n)
        {
            var buckets = new List<Vector3>[6] { new(), new(), new(), new(), new(), new() };
            
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f)); // roughly 2.399963
            
            for (int i = 0; i < n; i++)
            {
                float y = 1f - 2f * ((i + 0.5f) / n); // -1 to 1
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = goldenAngle * i;

                float x = Mathf.Cos(theta) * r;
                float z = Mathf.Sin(theta) * r;

                float ax = Mathf.Abs(x), ay = Mathf.Abs(y), az = Mathf.Abs(z);

                int axis = (ax >= ay && ax >= az) ? 0 : (ay >= az) ? 1 : 2;
                int bucket = axis * 2 + ((axis == 0 ? x : axis == 1 ? y : z) >= 0 ? 0 : 1);
                
                buckets[bucket].Add(new Vector3(x, y, z));
            }
            // Calculate max height observation length
            var maxLength = 0;
            for (int i = 0; i < 6; i++) 
                if (buckets[i].Count > maxLength) maxLength = buckets[i].Count;

            maxVisualHeight = maxLength;
            var directions = new Vector3[6, maxLength];
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < buckets[i].Count; j++)
                    directions[i, j] = buckets[i][j];
            }
//            Debug.Log($"Max: {maxVisualHeight}, Buckets: [{string.Join(", ", buckets.Select(b => b.Count))}]");
            return directions;
        }

        public ObservationSpec GetObservationSpec() => ObservationSpec.Visual(6, maxVisualHeight, 1);  // squeeze in python

        public int Write(ObservationWriter writer)
        {
            Vector3 origin = _referenceTransform.position;
            
            var vectorList = (_geodesicMeshMap) ? new List<Vector3>() : null;

            for (int direction = 0; direction < 6; direction++)
            {
                for (int i = 0; i < maxVisualHeight; i++)
                {
                    Vector3 rayDirection = _rayDirections[direction, i];

                    if (rayDirection == Vector3.zero)
                    {
                        writer[direction, i, 0] = 0f;
                        continue;
                    }

                    Vector3 worldDirection = _referenceTransform.TransformDirection(rayDirection);

                    if (Physics.Raycast(origin, worldDirection, out RaycastHit hit, _maxDistance, _detectionLayers))
                    {
                        writer[direction, i, 0] = 1f - (hit.distance / _maxDistance);
                        vectorList?.Add(hit.point);  // maybe do something different and not perfect
                    } else {
                        writer[direction, i, 0] = 0f;
                    }
                }
            }
            
            if (vectorList is { Count: >= 4 })
            {
                if (_firstStep)
                {
                    _geodesicMeshMap.InitializeMesh(vectorList.ToArray());
                    _firstStep = false;
                } else {
                    _geodesicMeshMap.ProcessObservation(_agent.transform.position,vectorList.ToArray());
                }
            }

            return 6 * maxVisualHeight;
        }

        public byte[] GetCompressedObservation() => null;
        public void Update() { }

        public void Reset()
        {
            _episode++;
        }
        public CompressionSpec GetCompressionSpec() => CompressionSpec.Default();
        public string GetName() => "SpatialLidarSensor";

        public Vector3[,] GetRayDirections() => _rayDirections;
    }
}