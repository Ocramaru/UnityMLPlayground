using DodgingAgent.Scripts.Utilities;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgingAgent.Scripts.Sensors
{
    /// <summary>
    /// Barometer sensor measuring altitude (y position)
    /// Implements simple white noise model
    /// </summary>
    public class ISensorBarometer : ISensor
    {
        private readonly bool _includeNoise;
        private readonly float _noiseLevel;
        private readonly Transform _referenceTransform;

        public ISensorBarometer(Transform transform, bool includeNoise, float noiseLevel = 0.5f)
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
            // Barometer: Altitude (1 observation)
            float altitude = _referenceTransform.position.y;

            if (_includeNoise)
            {
                altitude += GaussianRandom.Sample() * _noiseLevel; // white noise
            }

            writer.AddList(new[] { altitude });

            return 1;
        }

        public byte[] GetCompressedObservation()
        {
            return null;
        }

        public void Update()
        {
            // No per-frame updates needed for barometer
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
            return "BarometerSensor";
        }
    }
}