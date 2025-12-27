// Uses Ziggurat algorithm for efficient Gaussian sampling
// Implementation adapted from Redzen library: https://github.com/colgreen/Redzen
// Copyright (c) Colin Green, MIT License

using UnityEngine;

namespace DodgyBall.Scripts.Utilities
{
    public static class GaussianRandom
    {
        /// <summary>
        /// Generate a standard normal (Gaussian) random value using Ziggurat algorithm
        /// Uses UnityEngine.Random as the underlying source for consistent seeding
        /// </summary>
        public static float Sample()
        {
            return (float)ZigguratGaussian.Sample();
        }

        /// <summary>
        /// Generate a Gaussian noise vector with specified sigma
        /// </summary>
        public static Vector3 SampleVector(float sigma)
        {
            return new Vector3(
                Sample() * sigma,
                Sample() * sigma,
                Sample() * sigma
            );
        }
    }
}