// IMU Noise Model based on Kalibr: https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model

using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DodgingAgent.Scripts.Sensors
{
    [System.Flags]
    public enum SensorTypes
    {
        None = 0,
        Accelerometer = 1 << 0,
        Gyroscope = 1 << 1,
        Barometer = 1 << 2,
        Compass = 1 << 3
    }

    [System.Serializable]
    public struct SensorNoiseConfig
    {
        [Tooltip("White noise density (units/√Hz)")]
        public float noiseDensity;
        [Tooltip("Random walk bias (units/s/√Hz)")]
        public float randomWalk;
    }

    /// <summary>
    /// IMU SensorComponent that creates modular ISensor implementations
    /// </summary>
    public class SensorImu : SensorComponent
    {
        [Header("Sensor Selection")]
        public SensorTypes enabledSensors = SensorTypes.Accelerometer | SensorTypes.Gyroscope;
        public bool includeNoise;

        [Header("Accelerometer Options")]
        public bool includeGravity = true;
        [Range(0f, 1f)]
        public float gravityAlpha = 0.95f;

        [Header("Noise Parameters (Kalibr Model)")]
        public SensorNoiseConfig accelerometerNoise = new() { noiseDensity = 0.02f, randomWalk = 0.002f };
        public SensorNoiseConfig gyroscopeNoise = new() { noiseDensity = 0.01f, randomWalk = 0.001f };
        public SensorNoiseConfig barometerNoise = new() { noiseDensity = 0.5f, randomWalk = 0.05f };
        public SensorNoiseConfig compassNoise = new() { noiseDensity = 2f, randomWalk = 0.1f };

        [Header("References")]
        public Transform referenceTransform;
        public Rigidbody _rigidbody;

        private ISensorImu imuSensor;

        private void Awake()
        {
            if (!referenceTransform) referenceTransform = transform;
        }

        public override ISensor[] CreateSensors()
        {
            var sensors = new List<ImuBaseSensor>();
            imuSensor = new ISensorImu(referenceTransform, _rigidbody, includeNoise, sensors);

            if (enabledSensors.HasFlag(SensorTypes.Accelerometer) && _rigidbody)
                sensors.Add(new Accelerometer(imuSensor, includeGravity, gravityAlpha,
                    accelerometerNoise.noiseDensity, accelerometerNoise.randomWalk));

            if (enabledSensors.HasFlag(SensorTypes.Gyroscope) && _rigidbody)
                sensors.Add(new Gyroscope(imuSensor,
                    gyroscopeNoise.noiseDensity, gyroscopeNoise.randomWalk));

            if (enabledSensors.HasFlag(SensorTypes.Barometer))
                sensors.Add(new Barometer(imuSensor,
                    barometerNoise.noiseDensity, barometerNoise.randomWalk));

            if (enabledSensors.HasFlag(SensorTypes.Compass))
                sensors.Add(new Compass(imuSensor,
                    compassNoise.noiseDensity, compassNoise.randomWalk));

            if (!_rigidbody && (enabledSensors.HasFlag(SensorTypes.Accelerometer) || enabledSensors.HasFlag(SensorTypes.Gyroscope)))
                Debug.LogWarning("SensorImu: No rigidbody assigned, cannot create Accelerometer or Gyroscope.");

            return new ISensor[] { imuSensor };
        }

        private void FixedUpdate()
        {
            imuSensor?.FixedUpdate();
        }
    }
}
