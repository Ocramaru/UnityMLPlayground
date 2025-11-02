using System.Collections;
using UnityEngine;

namespace DodgyBall.Scripts
{
    public class EfficientSword : MonoBehaviour
    {
        [Header("Refs")]
        public Transform target;
        public string binPath = "Assets/DodgyBall/data/swing_keyframes.bin";
        private Vector3 planeNormal;
        private Vector3[] planes;
    
        [Header("Swing")]
        [Range(0f, 360f)] public float arcLength = 150f;
        public float duration = .35f;
        public float MIN_DURATION = .001f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
    
        public readonly Quaternion weaponAdjustment = Quaternion.Euler(-90, -90, 0);
        private Quaternion baseRotation = Quaternion.identity;
        private SwingKeyframeSet loadedKeyframes;
        
        void Start()
        {
            // Precomp orientation
            Orient();
        
            // Load swordKeyframes
            loadedKeyframes = SwingKeyframeSet.Load(binPath);
            var randomSwing = loadedKeyframes.GetRandomSwing();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Swing();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Orient();
            }
        }
    
        private void Orient()
        {
            if (!target) return;

            Vector3 normal = planeNormal.sqrMagnitude > 0f ? planeNormal.normalized : Vector3.up;
            Vector3 direction = target.position - transform.position;
            
            Debug.DrawLine(transform.position, transform.forward * 100f, Color.blue, 10f); // Where sword thinks it's pointing
            Debug.DrawLine(transform.position, direction.normalized * 100f, Color.red, 10f); // Where target actually is

            baseRotation = Quaternion.LookRotation(direction, normal) * weaponAdjustment;
            transform.rotation = baseRotation;
            Debug.Log($"Orient rotation {baseRotation}");
        }
    
        IEnumerator SwingArc(Quaternion start, Quaternion end)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(MIN_DURATION, duration);
                float k = ease.Evaluate(Mathf.Clamp01(t));
                transform.rotation = Quaternion.Slerp(start, end, k);
                yield return null;
            }
            transform.rotation = end;
        }
        
        public void Swing()
        {
            SwingKeyframe randomSwingKeyframe = loadedKeyframes.GetRandomSwing();
            // SwordHelpers.DebugSwingKeyframe(randomSwingKeyframe);
            
            Vector3 modifiedAxis = baseRotation * randomSwingKeyframe.localSwingAxis;
            Quaternion swordPositioning = randomSwingKeyframe.localSwingAxis.y < 0 ? Quaternion.Euler(0f, 180f, 90f) : Quaternion.Euler(0f, 0f, -90f);
    
            Quaternion start = Quaternion.LookRotation(modifiedAxis, loadedKeyframes.planeNormal) * weaponAdjustment * swordPositioning;
            Quaternion end = Quaternion.AngleAxis(arcLength, modifiedAxis) * start;
            
            // SwordHelpers.DrawSwingPlane(transform, modifiedAxis,loadedKeyframes.planeNormal);
            // SwordHelpers.DebugStartEndSword(gameObject, transform, start, end);
            StopAllCoroutines();
            StartCoroutine(SwingArc(start, end));
        }
    
        public void Swing(float duration)
        {
            this.duration = Mathf.Max(MIN_DURATION, duration);
            Swing();
        }
    }
}
