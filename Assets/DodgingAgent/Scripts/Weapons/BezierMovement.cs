using System;
using System.Collections;
using DodgyBall.Scripts.Core;
using DodgyBall.Scripts.Utilities;
using UnityEngine;

namespace DodgyBall.Scripts.Weapons
{
    public class BezierMovement: MonoBehaviour, IWeapon
    {
        [Header("Options")]
        public float contactOffset = 0f;
        public float maxAttackDistance = 10f;
        
        [Header("Debug")]
        [Tooltip("If you provide a debugTarget object it will allow you to test movement via 'Spacebar'")]
        public GameObject debugTarget;
        public float debugDuration = 1f;
        public bool drawDebugCurve = true;
        private float debugExpectedContactTime = 0f;
        private float debugTimer = 0f;
        
        
        private Rigidbody _rb;
        private BezierCurve curve;
        private bool curveConsumed = true;
        
        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (!_rb) Debug.LogWarning("No rigidbody found");
        }
        
        private void Update()
        {
            if (!debugTarget) return;

            if (Input.GetKeyDown(KeyCode.Space)) // Perform test fly function
            {
                debugExpectedContactTime = GetImpactTime(debugDuration, debugTarget.transform.localPosition);
                debugTimer = 0f;

                Attack(debugDuration, debugTarget.transform.localPosition);
            }

            if (Input.GetKeyDown(KeyCode.B)) // Just visualize the curve without movement
            {
                if (curveConsumed) SetNextCurve();

                var (scaledPoints, scaledTangents, scaledArcLengths) = BezierCurveLibrary.Instance.FitCurve(
                    curve,
                    transform.localPosition,
                    debugTarget.transform.localPosition);

                DebugDrawCurve(curve, scaledPoints, transform.localPosition, debugTarget.transform.localPosition, 10f);

                curveConsumed = true; // Mark as consumed after visualizing
            }
        }

        private void FixedUpdate()
        {
            if (debugTarget) debugTimer += Time.fixedDeltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!debugTarget || other.gameObject != debugTarget) return;
            
            float error = Mathf.Abs(debugTimer - debugExpectedContactTime);
            Debug.Log($"Contact at {debugTimer:F3}s | Expected: {debugExpectedContactTime:F3}s | Error: {error:F3}s");
            
            if (error < 0.1f) {
                Debug.Log("✓ Contact time is accurate!");
            } else {
                Debug.LogWarning($"✗ Contact time off by {error:F3}s");
            }
        }

        private IEnumerator HandleBezierMovement(float duration, params Vector3[] controlPoints)
        {
            int numSamples = 50;
            Vector3[] sampledPoints = Utilities.Bezier.SamplePoints(numSamples, controlPoints);
            Vector3[] sampledTangents = Utilities.Bezier.SampleTangents(numSamples, controlPoints);

            yield return HandleBezierMovementWithSamples(duration, sampledPoints, sampledTangents);
        }

        private IEnumerator HandleBezierMovementWithSamples(float duration, Vector3[] sampledPoints, Vector3[] sampledTangents, Action callback=null)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float sampleIndex = progress * (sampledPoints.Length - 1);
                int i0 = Mathf.FloorToInt(sampleIndex);
                int i1 = Mathf.Min(i0 + 1, sampledPoints.Length - 1);
                float t = sampleIndex - i0;

                transform.localPosition = Vector3.Lerp(sampledPoints[i0], sampledPoints[i1], t);

                Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, sampledTangents[i0]);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, 0.5f);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            _rb.angularVelocity = Vector3.zero;
            curveConsumed = true;
            callback?.Invoke();
        }

        private IEnumerator HandleBezierMovementWithCurve(float duration, BezierCurve curveData, Vector3 start, Vector3 target, Action callback=null)
        {
            Debug.Log("Called BezierMovement with curve");
            var (scaledPoints, scaledTangents, cumulativeArcLengths) = BezierCurveLibrary.Instance.FitCurve(curveData, start, target);

            float totalLength = cumulativeArcLengths[^1];
            Debug.Log($"Original arc length: {curveData.cumulativeArcLengths[^1]:F3}, Scaled arc length: {totalLength:F3}");
            DebugDrawCurve(curveData, scaledPoints, start, target, duration);

            // Allows the attack to stop early if too far so it doesn't get exponentially farther away haha
            float overshoot = Vector3.Distance(scaledPoints[^1], target) - maxAttackDistance;
            bool stopShort = overshoot > 0f;
            float maxArcLength = stopShort ? totalLength - overshoot : totalLength;

            // Move through curve
            int segment = 0;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float targetArcLength = progress * totalLength;

                while (segment < cumulativeArcLengths.Length - 1 && targetArcLength > cumulativeArcLengths[segment + 1])
                {
                    segment++;
                }

                if (stopShort && targetArcLength >= maxArcLength)
                {
                    _rb.angularVelocity = Vector3.zero;
                    curveConsumed = true;
                    callback?.Invoke();
                    yield break;
                }

                // Interpolate within the segment
                int nextSegment = Mathf.Min(segment + 1, scaledPoints.Length - 1);
                float segmentStart = cumulativeArcLengths[segment];
                float segmentEnd = cumulativeArcLengths[nextSegment];
                float segmentLength = segmentEnd - segmentStart;
                float t = segmentLength > 0 ? (targetArcLength - segmentStart) / segmentLength : 0f;

                transform.localPosition = Vector3.Lerp(scaledPoints[segment], scaledPoints[nextSegment], t);

                Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, scaledTangents[segment]);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, 0.35f);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            transform.localPosition = scaledPoints[^1];
            _rb.angularVelocity = Vector3.zero;
            curveConsumed = true;
            callback?.Invoke();
        }

        private void SetNextCurve()
        {
            curve = BezierCurveLibrary.Instance.GetRandomCurve();
            curveConsumed = false;
        }

        private void DebugDrawCurve(BezierCurve curveData, Vector3[] scaledPoints, Vector3 start, Vector3 target, float duration)
        {
            if (!drawDebugCurve) return;

            // Draw the curve path
            for (int i = 0; i < scaledPoints.Length - 1; i++)
            {
                Debug.DrawLine(scaledPoints[i], scaledPoints[i + 1], Color.cyan, duration);
            }

            // Calculate scaled contact point
            Vector3 curveDirection = curveData.contactPoint.normalized;
            float actualDistance = Vector3.Distance(start, target);
            float scale = actualDistance / curveData.distanceToContact;
            Vector3 targetDirection = (target - start).normalized;
            Quaternion rotation = Quaternion.FromToRotation(curveDirection, targetDirection);
            Vector3 scaledContactPoint = start + rotation * (curveData.contactPoint * scale);

            // Draw markers
            float markerSize = 0.2f;

            // Start point (green)
            Debug.DrawLine(start + Vector3.up * markerSize, start - Vector3.up * markerSize, Color.green, duration);
            Debug.DrawLine(start + Vector3.right * markerSize, start - Vector3.right * markerSize, Color.green, duration);
            Debug.DrawLine(start + Vector3.forward * markerSize, start - Vector3.forward * markerSize, Color.green, duration);

            // Contact point (yellow)
            Debug.DrawLine(scaledContactPoint + Vector3.up * markerSize, scaledContactPoint - Vector3.up * markerSize, Color.yellow, duration);
            Debug.DrawLine(scaledContactPoint + Vector3.right * markerSize, scaledContactPoint - Vector3.right * markerSize, Color.yellow, duration);
            Debug.DrawLine(scaledContactPoint + Vector3.forward * markerSize, scaledContactPoint - Vector3.forward * markerSize, Color.yellow, duration);

            // Target point (red)
            Debug.DrawLine(target + Vector3.up * markerSize, target - Vector3.up * markerSize, Color.red, duration);
            Debug.DrawLine(target + Vector3.right * markerSize, target - Vector3.right * markerSize, Color.red, duration);
            Debug.DrawLine(target + Vector3.forward * markerSize, target - Vector3.forward * markerSize, Color.red, duration);

            Debug.Log($"Debug: Start={start}, ScaledContact={scaledContactPoint}, Target={target}");
            Debug.Log($"Debug: Original contact={curveData.contactPoint}, Scale={scale}, ContactTimeRatio={curveData.contactTimeRatio:F3}");
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public Coroutine Attack(float duration, Vector3 targetPosition, Action callback=null)
        {
            if (curveConsumed) SetNextCurve();

            BezierCurve attackCurve = curve;
            curveConsumed = true;

            return StartCoroutine(HandleBezierMovementWithCurve(duration, attackCurve, transform.localPosition, targetPosition, callback));
        }

        public float GetImpactTime(float duration, Vector3 targetPosition)
        {
            if (curveConsumed) SetNextCurve();

            if (Mathf.Abs(contactOffset) > 0.001f)
            {
                float actualDistance = Vector3.Distance(transform.localPosition, targetPosition);
                var scale = actualDistance / curve.distanceToContact;

                float unscaledOffset = contactOffset / scale;

                return duration * ((curve.arcLengthToContact - unscaledOffset) / curve.totalArcLength);
            }
            return duration * curve.contactTimeRatio;
        }
    }
}