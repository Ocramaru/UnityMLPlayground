// IMU Noise Model based on Kalibr: https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model

using UnityEngine;
using Unity.MLAgents.Sensors;

namespace DodgyBall.Scripts.Sensors
{
    /// <summary>
    /// IMU SensorComponent that creates modular ISensor implementations
    /// Composes gyroscope, accelerometer, barometer, compass, and gravity sensors
    /// </summary>
    public class SensorImu : SensorComponent
    {
        [Header("Sensor Options")]
        public bool includeNoise;
        public bool includeGyroscope = true;
        public bool includeAcceleration = true;
        public bool includeBarometer = true;
        public bool includeCompass = true;
        public bool includeGravity = true;

        [Header("Noise Parameters (Kalibr Model)")]
        [Tooltip("Gyroscope white noise density σ_g (rad/s/√Hz)")]
        public float gyroscopeNoiseDensity = 0.01f;
        [Tooltip("Gyroscope random walk σ_bg (rad/s²/√Hz)")]
        public float gyroscopeRandomWalk = 0.001f;
        [Tooltip("Accelerometer white noise density σ_a (m/s²/√Hz)")]
        public float accelerometerNoiseDensity = 0.02f;
        [Tooltip("Accelerometer random walk σ_ba (m/s³/√Hz)")]
        public float accelerometerRandomWalk = 0.002f;
        [Tooltip("Barometer white noise (m)")]
        public float barometerNoise = 0.5f;
        [Tooltip("Compass white noise (degrees)")]
        public float compassNoise = 2f;

        [Header("References")]
        [Tooltip("Transform for sensor frame (optional, defaults to this transform)")]
        public Transform referenceTransform;
        [Tooltip("Rigidbody to measure motion from (required for gyro/accelerometer)")]
        public Rigidbody rb;

        private ISensorAccelerometer _accelerometer;

        private void Awake()
        {
            if (!referenceTransform) referenceTransform = transform;
        }

        public override ISensor[] CreateSensors()
        {
            var sensors = new System.Collections.Generic.List<ISensor>();

            if (rb) {
                if (includeGyroscope)
                    sensors.Add(new ISensorGyro(referenceTransform, rb, includeNoise, gyroscopeNoiseDensity, gyroscopeRandomWalk));

                if (includeAcceleration)
                {
                    _accelerometer = new ISensorAccelerometer(referenceTransform, rb, includeNoise, accelerometerNoiseDensity, accelerometerRandomWalk);
                    sensors.Add(_accelerometer);
                }
            } else {
                if (includeGyroscope || includeAcceleration) Debug.LogWarning("No rigidbody assigned cannot collect gyro or accelerometer observations.");
            }

            if (includeBarometer)
                sensors.Add(new ISensorBarometer(referenceTransform, includeNoise, barometerNoise));

            if (includeCompass)
                sensors.Add(new ISensorCompass(referenceTransform, includeNoise, compassNoise));

            if (includeGravity)
                sensors.Add(new ISensorGravity(referenceTransform));

            return sensors.ToArray();
        }

        private void FixedUpdate()
        {
            _accelerometer?.FixedUpdate();  // Accelerometer needs to sync with physics updates
        }
    }
}
