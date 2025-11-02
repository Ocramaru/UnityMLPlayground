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
        
        private readonly Quaternion weaponAdjustment = Quaternion.Euler(-90, -90, 0);
        private Quaternion baseRotation = Quaternion.identity;
        
        public void Orient(Transform target)
        {
            Vector3 normal = SwingKeyframeSet.Instance.planeNormal.sqrMagnitude > 0f ? SwingKeyframeSet.Instance.planeNormal.normalized : Vector3.up;
            Vector3 direction = target.position - transform.position;
            
            baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;
            transform.rotation = baseRotation;
            Debug.Log($"Orient Set for {gameObject.name} with rotation: {baseRotation} | euler {baseRotation.eulerAngles}");
        }
        
        IEnumerator SwingArc(Quaternion start, Quaternion end, float duration, Action onComplete)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = ease.Evaluate(Mathf.Clamp01(t));
                transform.rotation = Quaternion.Slerp(start, end, k);
                yield return null;
            }
            transform.rotation = end;
            onComplete();
        }
        
        public Coroutine Attack(float duration, Action onComplete)
        {
            SwingKeyframe randomKeyframe = SwingKeyframeSet.GetRandomFromSingleton();
            
            Vector3 modifiedAxis = baseRotation * randomKeyframe.localSwingAxis;
            Quaternion swordPositioning = randomKeyframe.localSwingAxis.y < 0 ? Quaternion.Euler(0f, 180f, 90f) : Quaternion.Euler(0f, 0f, -90f);

            Quaternion start = Quaternion.LookRotation(modifiedAxis, SwingKeyframeSet.Instance.planeNormal) 
                                * weaponAdjustment * swordPositioning;
            Quaternion end = Quaternion.AngleAxis(arcLength, modifiedAxis) * start;
            
            // SwordHelpers.DrawSwingPlane(transform, modifiedAxis,SwingKeyframeSet.Instance.planeNormal);
            // SwordHelpers.DebugStartEndSword(gameObject, transform, start, end);
            
            return StartCoroutine(SwingArc(start, end, duration, onComplete));
        }
        
        public Vector3 GetRandomInRangePosition(Transform target)
        {
            if (!target) return transform.position;

            float radius = 1f; // 1 for now ig will test to figure out sword length later maybe make it dynamic from scaling if I feel like it
            return target.position + Random.insideUnitSphere * radius;
        }
    }
}