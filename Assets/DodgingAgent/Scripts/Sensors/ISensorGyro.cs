using DodgingAgent.Scripts.Utilities;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgingAgent.Scripts.Sensors
{
    /// <summary>
    /// Gyroscope sensor measuring angular velocity in local space
    /// Implements Kalibr noise model with white noise and random walk bias
    /// Kalibr: https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model
    /// </summary>
    public class ISensorGyro : ISensor
    {
        private readonly bool _includeNoise;
        private readonly float _noiseDensity;
        private readonly float _randomWalk;
        private Vector3 _bias;
        private readonly Transform _referenceTransform;
        private readonly Rigidbody _rb;

        public ISensorGyro(Transform transform, Rigidbody rigidbody, bool includeNoise, float noiseDensity = 0.01f, float randomWalk = 0.001f)
        {
            _referenceTransform = transform;
            _rb = rigidbody;
            _includeNoise = includeNoise;
            _noiseDensity = noiseDensity;
            _randomWalk = randomWalk;
            _bias = Vector3.zero;
        }

        public ObservationSpec GetObservationSpec()
        {
            return ObservationSpec.Vector(3);
        }

        public int Write(ObservationWriter writer)
        {
            // Gyroscope: Angular velocity in local space (3 observations)
            Vector3 localAngularVelocity = _referenceTransform.InverseTransformVector(_rb.angularVelocity);

            if (_includeNoise)
            {
                float sqrtDt = Mathf.Sqrt(Time.fixedDeltaTime);
                _bias += GaussianRandom.SampleVector(_randomWalk * sqrtDt); // update bias (random walk/brownian)
                localAngularVelocity += _bias + GaussianRandom.SampleVector(_noiseDensity / sqrtDt); // add bias + white noise
            }

            writer.Add(localAngularVelocity);

            return 3;
        }

        public byte[] GetCompressedObservation()
        {
            return null;
        }

        public void Update()
        {
            // No per-frame updates needed for gyroscope
        }

        public void Reset()
        {
            _bias = Vector3.zero;
        }

        public CompressionSpec GetCompressionSpec()
        {
            return CompressionSpec.Default();
        }

        public string GetName()
        {
            return "GyroscopeSensor";
        }
    }
}