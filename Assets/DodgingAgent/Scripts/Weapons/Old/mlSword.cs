using System;
using System.Collections;
using DodgyBall.Scripts.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts
{
    public class mlSword: MonoBehaviour//, IWeapon // removed this because major changes to IWeapon
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
        
        public void Orient(Vector3 targetPosition)
        {
            Vector3 normal = SwingKeyframeSet.Instance.planeNormal.sqrMagnitude > 0f ? SwingKeyframeSet.Instance.planeNormal.normalized : Vector3.up;
            Vector3 direction = targetPosition - transform.localPosition;
            
            baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;
            transform.localRotation = baseRotation;
            // Debug.Log($"Orient Set for {gameObject.name} with rotation: {baseRotation} | euler {baseRotation.eulerAngles}");
        }
        
        IEnumerator SwingArc(Quaternion start, float duration, float arcDegrees, Vector3 rotationAxis, Action onComplete)
        {
            Quaternion InitialRotation = transform.localRotation;
            // slerp to attack start position for 5th of duration
            float initialMove = 0f;
            while (initialMove < 1f)
            {
                initialMove += Time.deltaTime / (duration / 5);
                float k0 = Mathf.Clamp01(initialMove);
                transform.localRotation = Quaternion.Slerp(InitialRotation, start, k0);
                yield return null;
            }
            
            // finish attack with remaining 4/5ths of duration
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / (4*duration/5);
                float k = ease.Evaluate(Mathf.Clamp01(t));

                // Directly interpolate the rotation angle for full control
                float currentAngle = k * arcDegrees;
                transform.localRotation = Quaternion.AngleAxis(currentAngle, rotationAxis) * start;
                
                yield return null;
            }
            transform.localRotation = Quaternion.AngleAxis(arcDegrees, rotationAxis) * start;

            // Ensure velocity is zero
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            onComplete?.Invoke();
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
            // Quaternion end = Quaternion.AngleAxis(arcLength, modifiedAxis) * start;

            // SwordHelpers.DrawSwingPlane(transform, modifiedAxis,SwingKeyframeSet.Instance.planeNormal);
            // SwordHelpers.DebugStartEndSword(gameObject, transform, start, end);

            return StartCoroutine(SwingArc(start, duration, arcLength, modifiedAxis, onComplete));
        }
        
        public Coroutine Attack(float duration, Vector3 targetPosition, Action onComplete)
        {
            // Set zero
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            
            // Sample Random Swing Plane
            SwingKeyframe randomSwingKeyframe = SwingKeyframeSet.GetRandomFromSingleton();

            // Calculate direction and rotation to target
            Vector3 normal = SwingKeyframeSet.Instance.planeNormal.sqrMagnitude > 0f ? SwingKeyframeSet.Instance.planeNormal.normalized : Vector3.up;
            Vector3 direction = targetPosition - transform.localPosition;
            baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;
            
            // Orient Swing Axis and position approach
            Vector3 swingAxis = baseRotation * randomSwingKeyframe.localSwingAxis;
            Quaternion swordOffset = randomSwingKeyframe.localSwingAxis.y < 0 ? SwordOffsetDown : SwordOffsetUp;

            // Project direction and orient blade to be on swingAxis
            Vector3 projected = Vector3.ProjectOnPlane(direction, swingAxis).normalized;
            Quaternion neutral = Quaternion.LookRotation(swingAxis, normal) * weaponAdjustment * swordOffset;
            
            // Calculate offset needed to split arc
            float offset =  Vector3.SignedAngle(neutral * Vector3.forward, projected, swingAxis) - arcLength / 2f;
            // Debug.Log($"Adjusting by: {offset}");
            
            Quaternion start = Quaternion.AngleAxis(offset, swingAxis) * neutral;
            
            // Debug Step
            // float finalOffset = Vector3.SignedAngle(start * Vector3.forward, projected, swingAxis);
            // Debug.Log($"Final Offset from mid: {finalOffset} (expected ±{arcLength/2f})");
            
            return StartCoroutine(SwingArc(start, duration, arcLength, swingAxis, onComplete));
        }
        
        public Vector3 GetRandomInRangePosition(Vector3 targetPosition)
        {
            float radius = attackRange + 1;
            return targetPosition + Random.insideUnitSphere * radius;
        }
        
        // Checks reach of weapon to determine if a need for repositioning
        public bool CannotReach(Vector3 targetPosition)
        {
            return Vector3.Distance(targetPosition, transform.localPosition) > attackRange;
        }

        public void ApproachTarget(Vector3 targetPosition)
        {
            if (!_rb) return;

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
            if (!_rb) return;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        public float GetImpactTimeRatio()
        {
            // There is a lead up of 2/10ths and the arc hits at half of 8/10ths so 0.6f.
            return 0.6f;
        }
    }
}