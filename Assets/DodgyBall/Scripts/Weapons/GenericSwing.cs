using System;
using System.Collections;
using DodgyBall.Scripts.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts.Weapons
{
    public class GenericSwing: MonoBehaviour, IWeapon
    {
        [Header("Swing")]
        [Range(0f, 360f)] public float arcLength = 150f;
        public float AttackRange = 0.5f;
        
        private Rigidbody _rb;
        
        void Awake()
        {
            // Get the Rigidbody from this weapon GameObject
            _rb = GetComponent<Rigidbody>();
            if (!_rb) Debug.LogWarning("No rigidbody found");
        }

        private IEnumerator Swing(float duration, Vector3 targetPosition, Action callback=null)
        {
            float orientTime = duration * 0.25f;  // Time to Move
            float swingTime = duration - orientTime;  // Time for Swing
            // float contactRatio = (0.037f / swingTime) + 0.488f; // Tested a bunch of swings to get this (probably a better way)
            
            // Calculate linear movement
            float distance = Vector3.Distance(transform.localPosition, targetPosition) - AttackRange;
            Vector3 direction = (targetPosition - transform.localPosition).normalized;
            Vector3 linearVelocity = direction * (distance / orientTime);
            
            // Calculate orientation
            Quaternion startRot = _rb.rotation;
            Quaternion alignRot = Quaternion.FromToRotation(transform.right, -direction);
            Quaternion rollRot = Quaternion.AngleAxis(Random.value * 45f, Vector3.right);
            
            alignRot.ToAngleAxis(out float alignAngle, out Vector3 alignAxis);  // get axis needed to travel in order to flip it
            if (alignAngle < 180f && alignAngle > 0.1f) { alignAngle = -(360f - alignAngle);}
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                Quaternion targetRotation;
                
                // Orient/Linear Movement Phase
                if (elapsed < orientTime) {
                    float t = elapsed / orientTime;
                    float currentAngle = alignAngle * t;
                    Quaternion qAlign = Quaternion.AngleAxis(currentAngle, alignAxis);
                    Quaternion qRoll = Quaternion.Slerp(Quaternion.identity, rollRot, t);
                    targetRotation = qAlign * startRot * qRoll;
                    // Convert local velocity to world space
                    _rb.linearVelocity = linearVelocity;
                } else {  // Swing Phase
                    float swingT = (elapsed - orientTime) / swingTime;
                    Quaternion qSwing = Quaternion.AngleAxis(arcLength * swingT, Vector3.forward);
                    Quaternion orientedRot = alignRot * startRot * rollRot;
                    targetRotation = orientedRot * qSwing;
                    _rb.linearVelocity = Vector3.zero;
                }

                // Calc Angular Vel
                Quaternion delta = targetRotation * Quaternion.Inverse(_rb.rotation);
                delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
                if (angleDeg > 180f) angleDeg -= 360f;
                if (Mathf.Abs(angleDeg) < 0.001f) { _rb.angularVelocity = Vector3.zero; }  // Remove instability
                float angularSpeed = (angleDeg * Mathf.Deg2Rad) / Time.fixedDeltaTime;
                angularSpeed = Mathf.Clamp(angularSpeed, -500f, 500f);  // clamp limits
                _rb.angularVelocity = axis.normalized * angularSpeed;

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            // Stop swing
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            
            callback?.Invoke();
        }

        public Coroutine Attack(float duration, Vector3 targetPosition, Action callback)
        {
            return StartCoroutine(Swing(duration, targetPosition, callback));
        }

        public float GetImpactTime(float duration)
        {
            return (float) (0.037f / (duration * .75)) + 0.488f;
        }
    }
}