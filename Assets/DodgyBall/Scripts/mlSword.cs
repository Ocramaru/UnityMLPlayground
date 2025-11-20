using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts
{
    public class mlSword: MonoBehaviour, IWeapon
    {
        [Header("Swing")]
        [Range(0f, 360f)] public float arcLength = 150f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
        public float attackRange = 1f; // 1 for now ig will test to figure out sword length later maybe make it dynamic from scaling if I feel like it

        [Header("Movement")]
        public float approachSpeed = 5f;
        public float stoppingDistance = 0.1f; // Distance threshold to stop approaching
        public float decelerationDistance = 1f; // Distance to start slowing down
        public float dampingFactor = 0.9f; // How much to dampen velocity when close (0-1)

        private Rigidbody _rb;

        private readonly Quaternion weaponAdjustment = Quaternion.Euler(-90, -90, 0);
        private Quaternion baseRotation = Quaternion.identity;

        void Awake()
        {
            // Get the Rigidbody from this weapon GameObject
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                Debug.LogWarning($"mlSword on {gameObject.name}: No Rigidbody found. ApproachTarget will not work.");
            }
        }
        
        private static readonly Quaternion SwordOffsetDown = Quaternion.Euler(0f, 180f, 90f);
        private static readonly Quaternion SwordOffsetUp = Quaternion.Euler(0f, 0f, -90f);
        
        public void Orient(Transform target)
        {
            Vector3 normal = SwingKeyframeSet.Instance.planeNormal.sqrMagnitude > 0f ? SwingKeyframeSet.Instance.planeNormal.normalized : Vector3.up;
            Vector3 direction = target.localPosition - transform.localPosition;
            
            baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;
            transform.localRotation = baseRotation;
            // Debug.Log($"Orient Set for {gameObject.name} with rotation: {baseRotation} | euler {baseRotation.eulerAngles}");
        }
        
        IEnumerator SwingArc(Quaternion start, Quaternion end, float duration, float arcDegrees, Vector3 rotationAxis, Action onComplete)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = ease.Evaluate(Mathf.Clamp01(t));

                // Directly interpolate the rotation angle for full control
                float currentAngle = k * arcDegrees;
                transform.localRotation = Quaternion.AngleAxis(currentAngle, rotationAxis) * start;

                yield return null;
            }
            transform.localRotation = Quaternion.AngleAxis(arcDegrees, rotationAxis) * start;
            onComplete();
        }
        
        public Coroutine Attack(float duration, Action onComplete)
        {
            // Set zero
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            SwingKeyframe randomKeyframe = SwingKeyframeSet.GetRandomFromSingleton();

            Vector3 modifiedAxis = baseRotation * randomKeyframe.localSwingAxis;
            Quaternion swordPositioning = randomKeyframe.localSwingAxis.y < 0 ? Quaternion.Euler(0f, 180f, 90f) : Quaternion.Euler(0f, 0f, -90f);

            Quaternion start = Quaternion.LookRotation(modifiedAxis, SwingKeyframeSet.Instance.planeNormal)
                                * weaponAdjustment * swordPositioning;
            Quaternion end = Quaternion.AngleAxis(arcLength, modifiedAxis) * start;

            // SwordHelpers.DrawSwingPlane(transform, modifiedAxis,SwingKeyframeSet.Instance.planeNormal);
            // SwordHelpers.DebugStartEndSword(gameObject, transform, start, end);

            return StartCoroutine(SwingArc(start, end, duration, arcLength, modifiedAxis, onComplete));
        }
        
        public Coroutine Attack(float duration, Transform target, Action onComplete)
        {
            // Set zero
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            SwingKeyframe randomSwingKeyframe = SwingKeyframeSet.GetRandomFromSingleton();

            Vector3 normal = SwingKeyframeSet.Instance.planeNormal.sqrMagnitude > 0f ? SwingKeyframeSet.Instance.planeNormal.normalized : Vector3.up;
            Vector3 direction = target.localPosition - transform.localPosition;
            baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;

            Vector3 worldSwingAxis = baseRotation * randomSwingKeyframe.localSwingAxis;
            Quaternion swordOffset = randomSwingKeyframe.localSwingAxis.y < 0 ? SwordOffsetDown : SwordOffsetUp;

            Quaternion start = Quaternion.LookRotation(worldSwingAxis, normal) * weaponAdjustment * swordOffset;
            Quaternion end = Quaternion.AngleAxis(arcLength, worldSwingAxis) * start;

            return StartCoroutine(SwingArc(start, end, duration, arcLength, worldSwingAxis, onComplete));
        }
        
        public Vector3 GetRandomInRangePosition(Transform target)
        {
            if (!target) return transform.localPosition;

            float radius = attackRange;
            return target.localPosition + Random.insideUnitSphere * radius;
        }
        
        // Checks reach of weapon to determine if a need for repositioning
        public bool CannotReach(Vector3 position)
        {
            return Vector3.Distance(position, transform.localPosition) > attackRange;
        }

        public void ApproachTarget(Vector3 targetPosition)
        {
            if (_rb == null) return;

            Vector3 currentPos = transform.localPosition;
            Vector3 direction = (targetPosition - currentPos).normalized;
            float distance = Vector3.Distance(currentPos, targetPosition);

            // If within stopping distance, smoothly dampen all velocity
            if (distance <= stoppingDistance)
            {
                _rb.linearVelocity *= dampingFactor;
            }
            // If within deceleration distance, gradually reduce speed
            else if (distance <= decelerationDistance)
            {
                // Calculate speed multiplier based on distance (1.0 at decelerationDistance, 0 at stoppingDistance)
                float speedMultiplier = Mathf.InverseLerp(stoppingDistance, decelerationDistance, distance);
                float targetSpeed = approachSpeed * speedMultiplier;

                // Apply force with reduced speed
                _rb.AddForce(direction * targetSpeed, ForceMode.VelocityChange);

                // Apply light damping to smooth out movement
                _rb.linearVelocity *= Mathf.Lerp(dampingFactor, 1f, speedMultiplier);
            }
            else
            {
                // Full speed when far from target
                _rb.AddForce(direction * approachSpeed, ForceMode.VelocityChange);
            }
        }

        public void StopApproaching()
        {
            if (_rb) return;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}