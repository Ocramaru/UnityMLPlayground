using System;
using UnityEngine;

namespace DodgingAgent.Scripts.Utilities
{
    [Serializable]
    public struct BezierCurve
    {
        public Vector3[] sampledPoints;
        public Vector3[] sampledTangents;
        public float[] cumulativeArcLengths; // Arc length between each sample point
        public Vector3 contactPoint;
        public float distanceToContact; // Direct vector distance
        public float totalArcLength;
        public float arcLengthToContact; // start -> contact
        public float contactTimeRatio; // (0-1) time till contact (this * duration)
    }

    [CreateAssetMenu(fileName = "BezierCurveLibrary", menuName = "Custom/Bezier Curve Library")]
    public class BezierCurveLibrary : ScriptableObject
    {
        private static BezierCurveLibrary _instance;
        public static BezierCurveLibrary Instance
        {
            get
            {
                if (_instance) return _instance;
                
                _instance = Resources.Load<BezierCurveLibrary>("BezierCurveLibrary");
                if (!_instance)
                {
                    Debug.LogError("BezierCurveLibrary not found! Place it in a 'Resources' folder at path: Resources/BezierCurveLibrary.asset");
                }
                return _instance;
            }
        }

        public BezierCurve[] curves;

        private void OnEnable()
        {
            if (!_instance) {
                _instance = this;
            } else if (_instance != this) {
                Debug.LogWarning($"Multiple BezierCurveLibrary instances detected. Using the first one: {_instance.name}");
            }

            if (curves == null || curves.Length == 0)
            {
                Debug.LogWarning($"BezierCurveLibrary '{name}' has no curves. Use the Bezier Curve Generator to create curves.");
            }
        }

        // Validity Checks | Looks a little prettier to do this imo
        private bool InvalidLibrary()
        {
            if (curves != null && curves.Length != 0) return false;

            Debug.LogError("No curves available in library");
            return true;
        }
        private bool InvalidIndex(int index)
        {
            if (index >= 0 && index < curves.Length) return false;

            Debug.LogError($"Invalid curve index {index}. Valid range: 0-{curves.Length - 1}");
            return true;
        }

        public BezierCurve GetRandomCurve()
        {
            if (InvalidLibrary()) return default;
            return curves[UnityEngine.Random.Range(0, curves.Length)];
        }

        public BezierCurve GetCurve(int index)
        {
            if (InvalidLibrary() || InvalidIndex(index)) return default;
            return curves[index];
        }

        public (Vector3[] points, Vector3[] tangents, float[] scaledArcLengths) FitCurve(BezierCurve curve, Vector3 start, Vector3 target)
        {
            Vector3 curveDirection = curve.contactPoint.normalized;

            // Scale
            float actualDistance = Vector3.Distance(start, target);
            float scale = actualDistance / curve.distanceToContact;

            // Rotation
            Vector3 targetDirection = (target - start).normalized;
            Quaternion rotation = Quaternion.FromToRotation(curveDirection, targetDirection);

            Vector3[] transformedPoints = new Vector3[curve.sampledPoints.Length];
            Vector3[] transformedTangents = new Vector3[curve.sampledTangents.Length];
            float[] scaledArcLengths = new float[curve.cumulativeArcLengths.Length];

            for (int i = 0; i < curve.sampledPoints.Length; i++)
            {
                Vector3 scaled = curve.sampledPoints[i] * scale;
                transformedPoints[i] = start + rotation * scaled;
            }

            for (int i = 0; i < curve.sampledTangents.Length; i++)
            {
                transformedTangents[i] = rotation * curve.sampledTangents[i];
            }

            for (int i = 0; i < curve.cumulativeArcLengths.Length; i++)
            {
                scaledArcLengths[i] = curve.cumulativeArcLengths[i] * scale;
            }

            return (transformedPoints, transformedTangents, scaledArcLengths);
        }

        public (Vector3[] points, Vector3[] tangents, float[] scaledArcLengths) GetFittedCurve(int index, Vector3 start, Vector3 target)
        {
            if (InvalidLibrary() || InvalidIndex(index)) return default;

            BezierCurve curve = curves[index];
            var (points, tangents, scaledArcLengths) = FitCurve(curve, start, target);
            return (points, tangents, scaledArcLengths);
        }

        public (Vector3[] points, Vector3[] tangents, float[] scaledArcLengths, int index) GetRandomFittedCurve(Vector3 start, Vector3 target)
        {
            int index = UnityEngine.Random.Range(0, curves.Length);
            var (points, tangents, scaledArcLengths) = GetFittedCurve(index, start, target);
            return (points, tangents, scaledArcLengths, index);
        }

        public float GetContactTimeRatioAtIndex(int index)
        {
            if (InvalidLibrary() || InvalidIndex(index)) return default;
            return curves[index].contactTimeRatio;
        }

        public static BezierCurve GenerateCurve(int numSamples, float contactAt, params Vector3[] controlPoints)
        {
            Vector3 contactPoint = Bezier.Evaluate(contactAt, controlPoints);
            float totalArcLength = Bezier.ArcLength(0f, 1f, 1e-5f, 30, controlPoints);
            float arcLengthToContact = Bezier.ArcLength(0f, contactAt, 1e-5f, 30, controlPoints);
            float contactTimeRatio = arcLengthToContact / totalArcLength;
            float distanceToContact = (contactPoint - controlPoints[0]).magnitude;

            Vector3[] sampledPoints = Bezier.SamplePoints(numSamples, controlPoints);
            Vector3[] sampledTangents = Bezier.SampleTangents(numSamples, controlPoints);

            // Calculate precise cumulative arc lengths for each sample using integration
            float[] cumulativeArcLengths = new float[sampledPoints.Length];
            cumulativeArcLengths[0] = 0f;
            for (int i = 1; i < sampledPoints.Length; i++)
            {
                float t = (float)i / (sampledPoints.Length - 1);
                cumulativeArcLengths[i] = Bezier.ArcLength(0f, t, 1e-5f, 30, controlPoints);
            }

            return new BezierCurve
            {
                sampledPoints = sampledPoints,
                sampledTangents = sampledTangents,
                cumulativeArcLengths = cumulativeArcLengths,
                contactPoint = contactPoint,
                distanceToContact = distanceToContact,
                totalArcLength = totalArcLength,
                arcLengthToContact = arcLengthToContact,
                contactTimeRatio = contactTimeRatio
            };
        }
    }
}