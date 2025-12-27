using DodgingAgent.Scripts.Utilities;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgingAgent.Scripts.Sensors
{
    /// <summary>
    /// Compass/Magnetometer sensor measuring heading angle (yaw)
    /// Implements simple white noise model
    /// </summary>
    public class ISensorCompass : ISensor
    {
        private readonly bool _includeNoise;
        private readonly float _noiseLevel;
        private readonly Transform _referenceTransform;

        public ISensorCompass(Transform transform, bool includeNoise, float noiseLevel = 2f)
        {
            _referenceTransform = transform;
            _includeNoise = includeNoise;
            _noiseLevel = noiseLevel;
        }

        public ObservationSpec GetObservationSpec()
        {
            return ObservationSpec.Vector(1);
        }

        public int Write(ObservationWriter writer)
        {
            // Magnetometer/Compass: Heading angle in degrees (1 observation)
            float heading = _referenceTransform.eulerAngles.y;

            if (_includeNoise)
            {
                heading += GaussianRandom.Sample() * _noiseLevel; // white noise
            }

            // Normalize to [-180, 180]
            if (heading > 180f) heading -= 360f;

            // Normalize to [-1, 1] for ML-Agents
            writer.AddList(new[] { heading / 180f });

            return 1;
        }

        public byte[] GetCompressedObservation()
        {
            return null;
        }

        public void Update()
        {
            // No per-frame updates needed for compass
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
            return "CompassSensor";
        }
    }
}