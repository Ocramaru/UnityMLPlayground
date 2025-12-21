using System;
using UnityEngine;

namespace DodgyBall.Scripts.Utilities
{
    [Serializable]
    public struct BezierCurve
    {
        public Vector3[] sampledPoints;
        public Vector3[] sampledTangents;
        public Vector3 contactPoint;
        public float distanceToContact;
    }

    [CreateAssetMenu(fileName = "BezierCurveLibrary", menuName = "Custom/Bezier Curve Library")]
    public class BezierCurveLibrary : ScriptableObject
    {
        public static BezierCurveLibrary Instance { get; private set; }

        public BezierCurve[] curves;

        private void OnEnable()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning($"Multiple BezierCurveLibrary instances detected. Using the first one: {Instance.name}");
            }

            if (curves == null || curves.Length == 0)
            {
                Debug.LogWarning($"BezierCurveLibrary '{name}' has no curves. Use the Bezier Curve Generator to create curves.");
            }
        }

        public (Vector3[] points, Vector3[] tangents) GetTransformedCurve(int curveIndex, Vector3 start, Vector3 target)
        {
            if (curveIndex < 0 || curveIndex >= curves.Length)
            {
                Debug.LogError($"Invalid curve index {curveIndex}");
                return (new Vector3[] { start, target }, new Vector3[] { Vector3.up, Vector3.up });
            }

            BezierCurve curve = curves[curveIndex];

            Vector3 curveDirection = curve.contactPoint.normalized;

            // Scale
            float actualDistance = Vector3.Distance(start, target);
            float scale = actualDistance / curve.distanceToContact;

            // Rotation
            Vector3 targetDirection = (target - start).normalized;
            Quaternion rotation = Quaternion.FromToRotation(curveDirection, targetDirection);

            Vector3[] transformedPoints = new Vector3[curve.sampledPoints.Length];
            Vector3[] transformedTangents = new Vector3[curve.sampledTangents.Length];

            for (int i = 0; i < curve.sampledPoints.Length; i++)
            {
                Vector3 scaled = curve.sampledPoints[i] * scale;
                transformedPoints[i] = start + rotation * scaled;
            }

            for (int i = 0; i < curve.sampledTangents.Length; i++)
            {
                transformedTangents[i] = rotation * curve.sampledTangents[i];
            }

            return (transformedPoints, transformedTangents);
        }

        public (Vector3[] points, Vector3[] tangents) GetRandomCurve(Vector3 start, Vector3 target)
        {
            return GetTransformedCurve(UnityEngine.Random.Range(0, curves.Length), start, target);
        }

        // Come fix this after
        public static BezierCurve GenerateCurve(int numSamples, Vector3 contactPoint, params Vector3[] controlPoints)
        {
            Vector3[] sampledPoints = Bezier.SamplePoints(numSamples, controlPoints);
            Vector3[] sampledTangents = Bezier.SampleTangents(numSamples, controlPoints);
            float distanceToContact = Bezier.ArcLength(0f, 0.5f, 1e-4f, 16, controlPoints);

            return new BezierCurve
            {
                sampledPoints = sampledPoints,
                sampledTangents = sampledTangents,
                contactPoint = contactPoint,
                distanceToContact = distanceToContact
            };
        }
    }
}