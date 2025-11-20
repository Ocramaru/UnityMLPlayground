using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts
{
    public class VelocitySword: MonoBehaviour, IWeapon
    {
        [Header("Swing")]
        [Range(0f, 360f)] public float arcLength = 150f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
        public float attackRange = 1f; // 1 for now ig will test to figure out sword length later maybe make it dynamic from scaling if I feel like it
        public float swingAngularSpeed = 360f; // Degrees per second for angular velocity swing

        [Header("Movement")]
        public float approachSpeed = 5f;
        public float stoppingDistance = 0.1f; // Distance threshold to stop approaching

        private Rigidbody _rb;

        private readonly Quaternion weaponAdjustment = Quaternion.Euler(-90, -90, 0);
        private Quaternion baseRotation = Quaternion.identity;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                Debug.LogWarning($"mlSword on {gameObject.name}: No Rigidbody found. ApproachTarget will not work.");
            }
        }
        
        private static readonly Quaternion SwordOffsetDown = Quaternion.Euler(0f, 0f, 90f);
        private static readonly Quaternion SwordOffsetUp = Quaternion.Euler(0f, 180f, -90f);
        
        public void Orient(Transform target)
        {
            Vector3 direction = target.localPosition - transform.localPosition;
            transform.localRotation = Quaternion.LookRotation(direction) * weaponAdjustment;
        }
        
        void SwingTowards(Quaternion targetLocalRotation)
        {
            if (_rb == null) return;

            // Calculate rotation delta in local space
            Quaternion deltaRotation = targetLocalRotation * Quaternion.Inverse(transform.localRotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 localAxis);

            // Convert to local angular velocity
            if (angle > 180f) angle -= 360f;
            Vector3 localAngularVelocity = localAxis * (angle * Mathf.Deg2Rad * swingAngularSpeed);

            // Transform to world space for rigidbody
            _rb.angularVelocity = transform.rotation * localAngularVelocity;
        }
        
        public Coroutine Attack(float duration, Action onComplete)
        {
            SwingKeyframe randomKeyframe = SwingKeyframeSet.GetRandomFromSingleton();

            Vector3 modifiedAxis = baseRotation * randomKeyframe.localSwingAxis;
            Quaternion swordPositioning = randomKeyframe.localSwingAxis.y < 0 ? Quaternion.Euler(0f, 180f, 90f) : Quaternion.Euler(0f, 0f, -90f);

            Quaternion targetRotation = Quaternion.LookRotation(modifiedAxis, SwingKeyframeSet.Instance.planeNormal)
                                * weaponAdjustment * swordPositioning;
            Quaternion end = Quaternion.AngleAxis(arcLength, modifiedAxis) * targetRotation;

            SwingTowards(end);
            onComplete?.Invoke();
            return null;
        }

        public Coroutine Attack(float duration, Transform target, Action onComplete)
        {
            SwingKeyframe randomSwingKeyframe = SwingKeyframeSet.GetRandomFromSingleton();

            Vector3 normal = SwingKeyframeSet.Instance.planeNormal.sqrMagnitude > 0f ? SwingKeyframeSet.Instance.planeNormal.normalized : Vector3.up;
            Vector3 direction = target.localPosition - transform.localPosition;
            baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;

            Vector3 worldSwingAxis = baseRotation * randomSwingKeyframe.localSwingAxis;
            Quaternion swordOffset = randomSwingKeyframe.localSwingAxis.y < 0 ? SwordOffsetDown : SwordOffsetUp;

            Quaternion targetRotation = Quaternion.LookRotation(worldSwingAxis, normal) * weaponAdjustment * swordOffset;
            Quaternion end = Quaternion.AngleAxis(arcLength, worldSwingAxis) * targetRotation;

            SwingTowards(end);
            onComplete?.Invoke();
            return null;
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

            // If within stopping distance, dampen velocity in the direction of movement
            if (distance <= stoppingDistance)
            {
                // Remove velocity component in direction of target
                Vector3 velocityInDirection = Vector3.Project(_rb.linearVelocity, direction);
                _rb.linearVelocity -= velocityInDirection;
            }
            else
            {
                // Add velocity towards target
                _rb.AddForce(direction * approachSpeed, ForceMode.VelocityChange);
            }
        }

        public void StopApproaching()
        {
            if (_rb == null) return;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}