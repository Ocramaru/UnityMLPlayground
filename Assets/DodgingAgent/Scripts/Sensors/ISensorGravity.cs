using UnityEngine;
using Unity.MLAgents.Sensors;

namespace DodgyBall.Scripts.Sensors
{
    /// <summary>
    /// Gravity sensor measuring the direction to ground in local space
    /// </summary>
    public class ISensorGravity : ISensor
    {
        private readonly Transform _referenceTransform;

        public ISensorGravity(Transform transform)
        {
            _referenceTransform = transform;
        }

        public ObservationSpec GetObservationSpec()
        {
            return ObservationSpec.Vector(3);
        }

        public int Write(ObservationWriter writer)
        {
            // Gravity sensor: Direction to ground in local space (3 observations)
            Vector3 gravityDirection = _referenceTransform.InverseTransformDirection(Vector3.down);
            writer.Add(gravityDirection);

            return 3;
        }

        public byte[] GetCompressedObservation()
        {
            return null;
        }

        public void Update()
        {
            // No per-frame updates needed for gravity sensor
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
            return "GravitySensor";
        }
    }
}