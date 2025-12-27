using DodgingAgent.Scripts.Utilities;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgingAgent.Scripts.Sensors
{
    /// <summary>
    /// Accelerometer sensor measuring linear acceleration in local space
    /// Implements Kalibr noise model with white noise and random walk bias
    /// Kalibr: https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model
    /// </summary>
    public class ISensorAccelerometer : ISensor
    {
        private readonly bool _includeNoise;
        private readonly float _noiseDensity;
        private readonly float _randomWalk;
        private Vector3 _bias;
        private readonly Transform _referenceTransform;
        private readonly Rigidbody _rb;
        private Vector3 _previousVelocity;
        private Vector3 _currentAcceleration;

        public ISensorAccelerometer(Transform refTransform, Rigidbody rigidbody, bool includeNoise, float noiseDensity = 0.02f, float randomWalk = 0.002f)
        {
            _referenceTransform = refTransform;
            _rb = rigidbody;
            _includeNoise = includeNoise;
            _noiseDensity = noiseDensity;
            _randomWalk = randomWalk;
            _bias = Vector3.zero;
            _previousVelocity = rigidbody.linearVelocity;
            _currentAcceleration = Vector3.zero;
        }

        public ObservationSpec GetObservationSpec()
        {
            return ObservationSpec.Vector(3);
        }

        public int Write(ObservationWriter writer)
        {
            // Accelerometer: Linear acceleration in local space (3 observations)
            Vector3 localAcceleration = _referenceTransform.InverseTransformVector(_currentAcceleration);

            if (_includeNoise)
            {
                float sqrtDt = Mathf.Sqrt(Time.fixedDeltaTime);
                _bias += GaussianRandom.SampleVector(_randomWalk * sqrtDt); // update bias (random walk/brownian)
                localAcceleration += _bias + GaussianRandom.SampleVector(_noiseDensity / sqrtDt); // add bias + white noise
            }

            writer.Add(localAcceleration);

            return 3;
        }

        public byte[] GetCompressedObservation()
        {
            return null;
        }

        public void Update()
        {
            // Called once per step but since acceleration is a Phyiscs step we are doing it in FixedUpdate
        }

        public void FixedUpdate()
        {
            _currentAcceleration = (_rb.linearVelocity - _previousVelocity) / Time.fixedDeltaTime;
            _previousVelocity = _rb.linearVelocity;
        }

        public void Reset()
        {
            _previousVelocity = _rb.linearVelocity;
            _bias = Vector3.zero;
        }

        public CompressionSpec GetCompressionSpec()
        {
            return CompressionSpec.Default();
        }

        public string GetName()
        {
            return "AccelerometerSensor";
        }
    }
}
