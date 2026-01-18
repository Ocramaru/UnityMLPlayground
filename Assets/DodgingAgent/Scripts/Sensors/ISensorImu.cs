using System.Collections.Generic;
using DodgingAgent.Scripts.Utilities;
using Unity.MLAgents.Sensors;
using UnityEngine;
using System.Linq;

namespace DodgingAgent.Scripts.Sensors
{
    public abstract class ImuBaseSensor
    {
        protected readonly ISensorImu Imu;
        protected readonly float noiseDensity;
        protected readonly float randomWalk;
        public readonly int vectorSize;

        protected ImuBaseSensor(ISensorImu imu, float noiseDensity, float randomWalk, int vectorSize)
        {
            Imu = imu;
            this.noiseDensity = noiseDensity;
            this.randomWalk = randomWalk;
            this.vectorSize = vectorSize;
        }

        public abstract int Write(ObservationWriter writer);
        public abstract void Reset();
        public virtual void FixedUpdate() { }
    }

    /// <summary>
    /// Accelerometer sensor measuring linear acceleration in local space
    /// Optionally includes gravity direction estimated via low-pass filter
    /// </summary>
    public class Accelerometer : ImuBaseSensor
    {
        private readonly bool includeGravity;
        private readonly float gravityAlpha;
        private Vector3 bias;
        private Vector3 previousVelocity;
        private Vector3 currentAcceleration;
        private Vector3 gravityEstimate;

        public Accelerometer(ISensorImu imu, bool includeGravity = true, float gravityAlpha = 0.95f,
            float noiseDensity = 0.02f, float randomWalk = 0.002f)
            : base(imu, noiseDensity, randomWalk, includeGravity ? 6 : 3)
        {
            this.includeGravity = includeGravity;
            this.gravityAlpha = gravityAlpha;
            gravityEstimate = Vector3.down;
        }

        public override int Write(ObservationWriter writer)
        {
            Vector3 localAcceleration = Imu.transform.InverseTransformVector(currentAcceleration);

            if (Imu.includeNoise)
            {
                float sqrtDt = Mathf.Sqrt(Time.fixedDeltaTime);
                bias += GaussianRandom.SampleVector(randomWalk * sqrtDt);
                localAcceleration += bias + GaussianRandom.SampleVector(noiseDensity / sqrtDt);
            }

            writer.Add(localAcceleration);

            if (includeGravity)
            {
                Vector3 gravityDirection = Imu.transform.InverseTransformDirection(gravityEstimate.normalized);
                writer.Add(gravityDirection);
                return 6;
            }

            return 3;
        }

        public override void FixedUpdate()
        {
            currentAcceleration = (Imu.rigidbody.linearVelocity - previousVelocity) / Time.fixedDeltaTime;
            previousVelocity = Imu.rigidbody.linearVelocity;

            if (!includeGravity) return;
            Vector3 rawAccelWithGravity = currentAcceleration + Physics.gravity;
            gravityEstimate = gravityAlpha * gravityEstimate + (1f - gravityAlpha) * rawAccelWithGravity;
        }

        public override void Reset()
        {
            previousVelocity = Imu.rigidbody.linearVelocity;
            bias = Vector3.zero;
            gravityEstimate = Vector3.down;
        }
    }

    /// <summary>
    /// Barometer sensor measuring altitude (y position)
    /// </summary>
    public class Barometer : ImuBaseSensor
    {
        private float bias;

        public Barometer(ISensorImu imu, float noiseDensity = 0.5f, float randomWalk = 0.05f)
            : base(imu, noiseDensity, randomWalk, 1) {}

        public override int Write(ObservationWriter writer)
        {
            float altitude = Imu.transform.position.y;

            if (Imu.includeNoise)
            {
                float sqrtDt = Mathf.Sqrt(Time.fixedDeltaTime);
                bias += GaussianRandom.Sample(randomWalk * sqrtDt);
                altitude += bias + GaussianRandom.Sample(noiseDensity / sqrtDt);
            }
            writer.AddList(new[] { altitude });

            return 1;
        }

        public override void Reset()
        {
            bias = 0f;
        }
    }

    /// <summary>
    /// Compass/Magnetometer sensor measuring heading angle (yaw)
    /// </summary>
    public class Compass : ImuBaseSensor
    {
        private float bias;

        public Compass(ISensorImu imu, float noiseDensity = 2f, float randomWalk = 0.1f)
            : base(imu, noiseDensity, randomWalk, 1) {}

        public override int Write(ObservationWriter writer)
        {
            float heading = Imu.transform.eulerAngles.y;

            if (Imu.includeNoise)
            {
                float sqrtDt = Mathf.Sqrt(Time.fixedDeltaTime);
                bias += GaussianRandom.Sample(randomWalk * sqrtDt);
                heading += bias + GaussianRandom.Sample(noiseDensity / sqrtDt);
            }

            // Normalize to [-180, 180]
            if (heading > 180f) heading -= 360f;

            // Normalize to [-1, 1]
            writer.AddList(new[] { heading / 180f });

            return 1;
        }

        public override void Reset()
        {
            bias = 0f;
        }
    }

    /// <summary>
    /// Gyroscope sensor measuring angular velocity in local space
    /// </summary>
    public class Gyroscope : ImuBaseSensor
    {
        private Vector3 bias;

        public Gyroscope(ISensorImu imu, float noiseDensity = 0.01f, float randomWalk = 0.001f)
            : base(imu, noiseDensity, randomWalk, 3) {}

        public override int Write(ObservationWriter writer)
        {
            Vector3 localAngularVelocity = Imu.transform.InverseTransformVector(Imu.rigidbody.angularVelocity);

            if (Imu.includeNoise)
            {
                float sqrtDt = Mathf.Sqrt(Time.fixedDeltaTime);
                bias += GaussianRandom.SampleVector(randomWalk * sqrtDt);
                localAngularVelocity += bias + GaussianRandom.SampleVector(noiseDensity / sqrtDt);
            }

            writer.Add(localAngularVelocity);
            return 3;
        }

        public override void Reset()
        {
            bias = Vector3.zero;
        }
    }

    /// <summary>
    /// Implements Kalibr noise model with white noise and random walk bias
    /// Kalibr: https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model
    /// </summary>
    public class ISensorImu : ISensor
    {
        public readonly bool includeNoise;
        public readonly Transform transform;
        public readonly Rigidbody rigidbody;

        private readonly List<ImuBaseSensor> sensors;

        public ISensorImu(Transform transform, Rigidbody rigidbody, bool includeNoise, List<ImuBaseSensor> sensors)
        {
            this.transform = transform;
            this.rigidbody = rigidbody;
            this.includeNoise = includeNoise;
            this.sensors = sensors;
        }

        public ObservationSpec GetObservationSpec()
        {
            return ObservationSpec.Vector(sensors.Sum(s => s.vectorSize));
        }

        public int Write(ObservationWriter writer)
        {
            return sensors.Sum(s => s.Write(writer)); 
        }

        public byte[] GetCompressedObservation()
        {
            return null;
        }

        public void Update() { }
        public void FixedUpdate()
        {
            sensors.ForEach(s => s.FixedUpdate());
        }

        public void Reset()
        {
            sensors.ForEach(s => s.Reset());
        }

        public CompressionSpec GetCompressionSpec()
        {
            return CompressionSpec.Default();
        }

        public string GetName()
        {
            return "ImuSensor";
        }
    }
}