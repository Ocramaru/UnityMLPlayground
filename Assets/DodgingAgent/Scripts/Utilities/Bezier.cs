using UnityEngine;
using System;

namespace DodgyBall.Scripts.Utilities
{
    public static class Bezier
    {
        // General Splitters to handle different numbers of control points
        public static Vector3 Evaluate(float t, params Vector3[] points)
        {
            switch (points.Length) // control points
            {
                case 5: // quartic bezier
                    return Evaluate(t, points[0], points[1], points[2], points[3], points[4]);
                case 4: // cubic bezier
                    return Evaluate(t, points[0], points[1], points[2], points[3]);
                case 3: // quadratic bezier
                    return Evaluate(t, points[0], points[1], points[2]);
            }
            
            Debug.LogError($"Bezier Evaluate was called with an unhandled number of control points {points.Length}");
            return Vector3.zero; // on exception
        }
        
        public static Vector3 Derivative(float t, params Vector3[] points)
        {
            switch (points.Length) // control points
            {
                case 5: // quartic bezier
                    return Derivative(t, points[0], points[1], points[2], points[3], points[4]);
                case 4: // cubic bezier
                    return Derivative(t, points[0], points[1], points[2], points[3]);
                case 3: // quadratic bezier
                    return Derivative(t, points[0], points[1], points[2]);
            }
            
            Debug.LogError($"Bezier Derivative was called with an unhandled number of control points {points.Length}");
            return Vector3.zero; // on exception
        }
        
        // Evaluate
        public static Vector3 Evaluate(float t, Vector3 p0, Vector3 p1, Vector3 p2)                                                                                                                             
        {   // C(t) = (1-t)^2 * p0 + 2(1-t) * t * p1 + t^2 * p2                                                                                                                                                  
            float oneMinusT = 1f - t;
            
            return Mathf.Pow(oneMinusT, 2) * p0 +                                                                                                                                                                
                    2f * oneMinusT * t * p1 +                                                                                                                                                                    
                    Mathf.Pow(t, 2) * p2;                                                                                                                                                                        
        }
        public static Vector3 Evaluate(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {  // C(t) = (1-t)^3 * p0 + 3(1-t)^2 * t * p1 + 3(1-t)^2 * t^2 * p2 + t^3 * p3
            float oneMinusT = 1f - t;
                
            return Mathf.Pow(oneMinusT, 3) * p0 + 
                    3f * Mathf.Pow(oneMinusT, 2) * t * p1 +
                    3f * oneMinusT * Mathf.Pow(t, 2) * p2 +
                    Mathf.Pow(t, 3) * p3;
        }
        public static Vector3 Evaluate(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {   // C(t) = (1-t)^4 * p0 + 4(1-t)^3 * t * p1 + 6(1-t)^2 * t^2 * p2 + 4(1-t) * t^3 * p3 + t^4 * p4
            float oneMinusT = 1f - t;

            return Mathf.Pow(oneMinusT, 4) * p0 +
                    4f * Mathf.Pow(oneMinusT, 3) * t * p1 +
                    6f * Mathf.Pow(oneMinusT, 2) * Mathf.Pow(t, 2) * p2 +
                    4f * oneMinusT * Mathf.Pow(t, 3) * p3 +
                    Mathf.Pow(t, 4) * p4;
        }
        
        // Derivative
        public static Vector3 Derivative(float t, Vector3 p0, Vector3 p1, Vector3 p2)                                                                                                                  
        {   // C'(t) = 2[(1-t) * (p1-p0) + t * (p2-p1)]                                                                                                                                                          
            float oneMinusT = 1f - t;                                                                                                                                                                            
            return 2f * (                                                                                                                                                                                        
                oneMinusT * (p1-p0) +                                                                                                                                                                            
                t * (p2-p1));                                                                                                                                                                                    
        } 
        
        public static Vector3 Derivative(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {   // C'(t) = 3[(1-t)^2 * (p1-p0) + 2(1-t) * t * (p2-p1) + t^2 * (p3-p2)
            float oneMinusT = 1f - t;
            return 3f * (
                Mathf.Pow(oneMinusT, 2) * (p1-p0) + 
                2 * oneMinusT * t * (p2-p1) + 
                MathF.Pow(t, 2) * (p3-p2));
        }
        
        public static Vector3 Derivative(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {   // C'(t) = 4[(1-t)^3 * (p1-p0) + 3(1-t)^2 * t * (p2-p1) + 3(1-t) * t^2 * (p3-p2) + t^3 * (p4-p3)
            float oneMinusT = 1f - t;
            return 4f * (
                Mathf.Pow(oneMinusT, 3) * (p1-p0) + 
                3 * Mathf.Pow(oneMinusT, 2) * t * (p2-p1) + 
                3 * oneMinusT * MathF.Pow(t, 2) * (p3-p2) + 
                MathF.Pow(t, 3) * (p4-p3));
        }
        
        // derivativeLength = |C'(t)| = speed
        public static float Speed(float t, params Vector3[] points)
        {
            return Derivative(t, points).magnitude;
        }
        
        // Adaptive Simpson integrator
        public static float ArcLength(float startT = 0f, float endT = 1f, float tolerance = 1e-4f, int maxDepth = 16, params Vector3[] points)
        {
            float startD = Speed(startT, points);
            float endD = Speed(endT, points);
            float middleT = 0.5f * (startT + endT);
            float middleD = Speed(middleT, points);

            float full = Simpson(startT, endT, startD, endD, middleD);

            // recursive Adaptive Simpson
            return AdaptiveSimpson(startT, endT, startD, endD, middleD, full, tolerance, maxDepth, points);
        }
        
        public static (float totalLength, float[] cumulativeLengths) ArcLengthWithInterLengths(float tolerance = 1e-4f, int maxDepth = 16, params Vector3[] points)
        {
            int segments = points.Length - 1;
            float[] cumulativeLengths = new float[points.Length];
            cumulativeLengths[0] = 0f;

            for (int i = 1; i <= segments; i++)
            {
                float startT = (float)(i - 1) / segments;
                float endT = (float)i / segments;
                float segmentLength = ArcLength(startT, endT, tolerance, maxDepth, points);
                cumulativeLengths[i] = cumulativeLengths[i - 1] + segmentLength;
            }

            float totalLength = cumulativeLengths[segments];
            return (totalLength, cumulativeLengths);
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        public static float AdaptiveSimpson(float startT, float endT, float startD, float endD, float middleD,
            float full, float tolerance, int depth, params Vector3[] points)
        {
            float middleT = 0.5f * (startT + endT);
            float leftT = 0.5f * (startT + middleT);
            float rightT = 0.5f * (middleT + endT);

            float leftD = Speed(leftT, points);
            float rightD = Speed(rightT, points);

            float left = Simpson(startT, middleT, startD, middleD, leftD);
            float right = Simpson(middleT, endT, middleD, endD, rightD);

            float delta = left + right - full;
            if (depth <= 0 || Mathf.Abs(delta) < 15f * tolerance) // return early
            {
                return left + right + delta / 15f; // Richardson Correction
            }

            float leftInt = AdaptiveSimpson(startT, middleT, startD, middleD, leftD, left, tolerance * 0.5f, depth - 1, points);
            float rightInt = AdaptiveSimpson(middleT, endT, middleD, endD, rightD, right, tolerance * 0.5f, depth - 1, points);

            return leftInt + rightInt;
        }

        public static float Simpson(float startT, float endT, float startD, float endD, float middleD)
        {
            return (endT - startT) * (startD + 4f * middleD + endD) / 6f;
        }

        // These allow for calculating a midpoint
        public static Vector3 SolveP1(Vector3 p0, Vector3 p2, Vector3 hump)
        {
            float b0 = 1f / 4f;
            float b1 = 1f / 2f;
            float b2 = 1f / 4f;

            Vector3 others = b0 * p0 + b2 * p2;
            return (hump - others) / b1;
        }
        public static Vector3 SolveP2(Vector3 p0, Vector3 p1, Vector3 p3, Vector3 p4, Vector3 hump)
        {
            float b0 = 1f / 16f;
            float b1 = 1f / 4f;
            float b2 = 3f / 8f;
            float b3 = 1f / 4f;
            float b4 = 1f / 16f;

            Vector3 others = b0 * p0 + b1 * p1 + b3 * p3 + b4 * p4;
            return (hump - others) / b2;
        }

        // Sample points along the Bézier curve at evenly spaced t values
        public static Vector3[] SamplePoints(int numSamples, params Vector3[] points)
        {
            if (numSamples < 2)
            {
                Debug.LogError($"SamplePoints requires at least 2 samples, got {numSamples}");
                return new Vector3[] { points[0], points[^1] };
            }

            Vector3[] samples = new Vector3[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / (numSamples - 1);
                samples[i] = Evaluate(t, points);
            }

            return samples;
        }

        // Sample tangent directions along the Bézier curve at evenly spaced t values
        public static Vector3[] SampleTangents(int numSamples, params Vector3[] points)
        {
            if (numSamples < 2)
            {
                Debug.LogError($"SampleTangents requires at least 2 samples, got {numSamples}");
                return new Vector3[] { Derivative(0f, points).normalized, Derivative(1f, points).normalized };
            }

            Vector3[] tangents = new Vector3[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / (numSamples - 1);
                tangents[i] = Derivative(t, points).normalized;
            }

            return tangents;
        }
    }
}