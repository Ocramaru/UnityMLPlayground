using System;
using System.Collections;
using DodgyBall.Scripts.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts.Weapons
{
    public class BezierMovement: MonoBehaviour, IWeapon
    {
        [Header("Movement")]
        public float AttackRange = 0.5f;
        public float PlacementOffset = 0.2f;

        public GameObject debugTarget;

        private Rigidbody _rb;
        private GameObject[] debugPoints;

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
                StartCoroutine(Fly(1f, debugTarget.transform.localPosition));
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                Debug.Log("Called Movement: PathThroughTarget");
                StartCoroutine(PathThroughTarget(2f, debugTarget.transform.localPosition));
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("Called Movement: PathThroughTargetWithLibrary");
                StartCoroutine(PathThroughTargetWithLibrary(2f, debugTarget.transform.localPosition));
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator PathThroughTarget5(float duration, Vector3 targetPosition)
        {
            Vector3 r1 = Random.insideUnitSphere; Vector3 r2 = Random.insideUnitSphere;
            r1.z = MathF.Abs(r1.z); r2.z = -MathF.Abs(r2.z);

            Vector3 direction = transform.localPosition - targetPosition;
            float placementDistance = direction.magnitude - PlacementOffset;
            Vector3 p0 = transform.localPosition;
            Vector3 p4 = targetPosition - direction;
            Vector3 p1 = p0 + r1 * placementDistance;
            Vector3 p3 = p4 + r2 * placementDistance;
            Vector3 p2 = Utilities.Bezier.SolveP2(p0, p1, p3, p4, targetPosition);

            // Create debug visualizations for control points
            if (debugPoints != null)
            {
                for (int i = 0; i < debugPoints.Length; i++)
                {
                    if (debugPoints[i]) Destroy(debugPoints[i]);
                }
            }

            debugPoints = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                debugPoints[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                debugPoints[i].transform.localScale = Vector3.one * 0.1f;
                debugPoints[i].name = $"DebugPoint_P{i}";
            }

            debugPoints[0].transform.position = p0;
            debugPoints[1].transform.position = p1;
            debugPoints[2].transform.position = p2;
            debugPoints[3].transform.position = p3;
            debugPoints[4].transform.position = p4;

            // Follow single quartic Bezier curve through target
            yield return HandleBezierMovement(duration, p0, p1, p2, p3, p4);
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator PathThroughTarget(float duration, Vector3 targetPosition)
        {
            Vector3 r1 = Random.insideUnitSphere; Vector3 r2 = Random.insideUnitSphere;
            r1.z = MathF.Abs(r1.z); r2.z = -MathF.Abs(r2.z);
            
            Vector3 direction = transform.localPosition - targetPosition;
            float placementDistance = direction.magnitude - PlacementOffset;
            Vector3 p0 = transform.localPosition;
            Vector3 p4 = targetPosition - direction;
            Vector3 p1 = p0 + r1 * placementDistance;
            Vector3 p3 = p4 + r2 * placementDistance;

            // Create debug visualizations for control points
                if (debugPoints != null)
                {
                    for (int i = 0; i < debugPoints.Length; i++)
                    {
                        if (debugPoints[i]) Destroy(debugPoints[i]);
                    }
                }

                debugPoints = new GameObject[5];
                for (int i = 0; i < 5; i++)
                {
                    debugPoints[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    debugPoints[i].transform.localScale = Vector3.one * 0.1f;
                    debugPoints[i].name = $"DebugPoint_P{i}";
                }

                debugPoints[0].transform.position = p0;
                debugPoints[1].transform.position = p1;
                debugPoints[2].transform.position = targetPosition;
                debugPoints[3].transform.position = p3;
                debugPoints[4].transform.position = p4;

            // Follow two quadratic Bézier curves in sequence
            yield return HandleBezierMovement(duration / 2f, p0, p1, targetPosition);
            yield return HandleBezierMovement(duration / 2f, targetPosition, p3, p4);
        }

        private IEnumerator HandleBezierMovement(float duration, params Vector3[] points)
        {
            int numSamples = 50;
            Vector3[] sampledPoints = Utilities.Bezier.SamplePoints(numSamples, points);
            Vector3[] sampledTangents = Utilities.Bezier.SampleTangents(numSamples, points);

            yield return HandleBezierMovementWithSamples(duration, sampledPoints, sampledTangents);
        }

        private IEnumerator HandleBezierMovementWithSamples(float duration, Vector3[] sampledPoints, Vector3[] sampledTangents)
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
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, 0.35f);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            _rb.angularVelocity = Vector3.zero;
        }

        private IEnumerator PathThroughTargetWithLibrary(float duration, Vector3 targetPosition)
        {
            var (sampledPoints, sampledTangents) = Utilities.BezierCurveLibrary.Instance.GetRandomCurve(transform.localPosition, targetPosition);

            yield return HandleBezierMovementWithSamples(duration, sampledPoints, sampledTangents);
        }

        private IEnumerator Fly(float duration, Vector3 targetPosition, Action callback=null)
        {
            // Calculate direction to target
            Vector3 direction = (targetPosition - transform.localPosition).normalized;

            // Orient the tip (Vector3.up) to point at target
            Quaternion startRot = _rb.rotation;
            Quaternion targetRot = Quaternion.FromToRotation(transform.up, direction) * startRot;
            
            float distance = Vector3.Distance(transform.localPosition, targetPosition);
            
            Vector3 linearVelocity = direction * (distance / duration);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Smoothly rotate to face target
                float t = Mathf.Clamp01(elapsed / (duration * 0.3f)); // Orient in first 30% of duration
                Quaternion currentRot = Quaternion.Slerp(startRot, targetRot, t);

                // Calculate angular velocity
                Quaternion delta = currentRot * Quaternion.Inverse(_rb.rotation);
                delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
                if (angleDeg > 180f) angleDeg -= 360f;
                if (Mathf.Abs(angleDeg) < 0.001f) { _rb.angularVelocity = Vector3.zero; }
                else
                {
                    float angularSpeed = (angleDeg * Mathf.Deg2Rad) / Time.fixedDeltaTime;
                    angularSpeed = Mathf.Clamp(angularSpeed, -500f, 500f);
                    _rb.angularVelocity = axis.normalized * angularSpeed;
                }

                // Set linear velocity to fly at target
                _rb.linearVelocity = linearVelocity;

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // Stop
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            callback?.Invoke();
        }

        public Coroutine Attack(float duration, Vector3 targetPosition, Action callback)
        {
            return StartCoroutine(Fly(duration, targetPosition, callback));
        }

        public float GetImpactTime(float duration)
        {
            // Contact happens around midpoint since it's flying straight through
            return duration * 0.5f;
        }
    }
}