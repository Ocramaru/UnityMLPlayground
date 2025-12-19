using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts
{
    public class NeedsImplementationFlyingSword: MonoBehaviour
    {
        [Header("Stab")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
        
        private readonly Quaternion weaponAdjustment = Quaternion.Euler(0, 0, -90); // Sets point to be forward direction
        private Quaternion baseRotation = Quaternion.identity;

        public Transform target;
        private void Update()
        {
            // Reorient if R pressed
            if (Input.GetKeyDown(KeyCode.R))
            {
                Orient(target);
                Debug.Log($"Distance to target is {Vector3.Distance(transform.localPosition, target.localPosition)})");
            }
        }
        
        // To Stab travel distance plus 1 then move to waiting

        public void Orient(Transform target)
        {
            if (!target) return;
            
            Vector3 direction = target.localPosition - transform.localPosition;
            
            baseRotation = Quaternion.LookRotation(direction) * weaponAdjustment;
            transform.localRotation = baseRotation;
            Debug.Log($"Orient Set for {gameObject.name} with rotation: {baseRotation} | euler {baseRotation.eulerAngles}");
        }
        
        IEnumerator SwingArc(Quaternion start, Quaternion end, float duration, Action onComplete)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = ease.Evaluate(Mathf.Clamp01(t));
                transform.localRotation = Quaternion.Slerp(start, end, k);
                yield return null;
            }
            transform.localRotation = end;
            onComplete();
        }
        
        // public Coroutine Attack(float duration, Action onComplete)
        // {
        //     SwingKeyframe randomKeyframe = SwingKeyframeSet.GetRandomFromSingleton();
        //     
        //     Vector3 modifiedAxis = baseRotation * randomKeyframe.localSwingAxis;
        //     Quaternion swordPositioning = randomKeyframe.localSwingAxis.y < 0 ? Quaternion.Euler(0f, 180f, 90f) : Quaternion.Euler(0f, 0f, -90f);
        //
        //     Quaternion start = Quaternion.LookRotation(modifiedAxis, SwingKeyframeSet.Instance.planeNormal) 
        //                         * weaponAdjustment * swordPositioning;
        //     Quaternion end = Quaternion.AngleAxis(arcLength, modifiedAxis) * start;
        //     
        //     // SwordHelpers.DrawSwingPlane(transform, modifiedAxis,SwingKeyframeSet.Instance.planeNormal);
        //     // SwordHelpers.DebugStartEndSword(gameObject, transform, start, end);
        //     
        //     return StartCoroutine(SwingArc(start, end, duration, onComplete));
        // }
        //
        // public Coroutine Attack(float duration, Transform target, Action onComplete)
        // {
        //     Debug.Log("Called target based Attack");
        //     SwingKeyframe randomSwingKeyframe = SwingKeyframeSet.GetRandomFromSingleton();
        //         
        //     Vector3 normal = SwingKeyframeSet.Instance.planeNormal.sqrMagnitude > 0f ? SwingKeyframeSet.Instance.planeNormal.normalized : Vector3.up;
        //     Vector3 direction = target.localPosition - transform.localPosition;
        //     baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;
        //         
        //     Vector3 worldSwingAxis = baseRotation * randomSwingKeyframe.localSwingAxis;
        //     Quaternion swordOffset = randomSwingKeyframe.localSwingAxis.y < 0 ? SwordOffsetDown : SwordOffsetUp;
        //         
        //     Quaternion start = Quaternion.LookRotation(worldSwingAxis, normal) * weaponAdjustment * swordOffset;
        //     Quaternion end = Quaternion.AngleAxis(arcLength, worldSwingAxis) * start;
        //
        //     return StartCoroutine(SwingArc(start, end, duration, onComplete));
        // }
        //
        // public Vector3 GetRandomInRangePosition(Transform target)
        // {
        //     if (!target) return transform.localPosition;
        //
        //     float radius = attackRange;
        //     return target.localPosition + Random.insideUnitSphere * radius;
        // }
        //
        // Checks reach of weapon to determine if a need for repositioning
        // public bool CannotReach(Vector3 position)
        // {
        //     return Vector3.Distance(position, transform.localPosition) > attackRange;
        // }
        //
        // public void ApproachTarget(Vector3 targetPosition)
        // {
        //     
        // }
    }
}