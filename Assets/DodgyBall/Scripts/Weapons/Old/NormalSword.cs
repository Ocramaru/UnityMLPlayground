using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DodgyBall.Scripts
{
    [ExecuteAlways] // temp
    public class NormalSword : MonoBehaviour
    {
        [Header("Editor Preview")]
        public bool autoAimInEditor = true;
    
        [Header("Refs")]
        public Transform target;
    
        [Header("Swing")]
        public Vector3 planeNormal = Vector3.up;
        [Range(0f, 360f)] public float arcLength = 150f;
        public float duration = .35f;
        public float MIN_DURATION = .001f;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
        public float variationOffset = 45f;
        private float swingVariation = 0f; // Between -variationOffset and +variationOffset

        [Header("Weapon Type")] public string weaponType = "katana";

        public readonly Quaternion weaponAdjustment = Quaternion.Euler(-90, -90, 0);
        private Quaternion baseRotation = Quaternion.identity;
        
        private void Update()
        {
            if (autoAimInEditor)
            {
                Orient();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                Swing();
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                BakeSwingKeyframes();
            }
        }
    
        private (Vector3 normal, Vector3 targetDir) Orient()
        {
            if (!target) return (Vector3.up, Vector3.forward);

            Vector3 normal = planeNormal.sqrMagnitude > 0f ? planeNormal.normalized : Vector3.up;
            Vector3 direction = target.position - transform.position;
            
            baseRotation = PlanarRotation(direction, normal);
            transform.rotation = baseRotation;
        
            return (normal, direction);
        }

        private Quaternion PlanarRotation(Vector3 planarDirection, Vector3 normal, bool useCorrection = true)
        {
            planarDirection.Normalize();
            if (useCorrection) return Quaternion.LookRotation(planarDirection, normal) * weaponAdjustment;
            return Quaternion.LookRotation(planarDirection, normal);
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
            if (!target) return;
    
            var (normal, targetDirection) = Orient(); // Orient and get both values
    
            // Use the actual target direction, not transform.forward
            Vector3 baseSwingAxis = Vector3.Cross(normal, targetDirection).normalized;
    
            // Add variation using the target direction
            swingVariation = Random.Range(-variationOffset, variationOffset);
            Quaternion offsetRotation = Quaternion.AngleAxis(swingVariation, targetDirection);
            Vector3 variedSwingAxis = offsetRotation * baseSwingAxis;
        
            // Debug
            DrawSwingPlane(variedSwingAxis, targetDirection);
        
            // Swing from current rotation
            bool isUpwardSwing = Vector3.Dot(variedSwingAxis, Vector3.up) > 0f;  // ← Use variedSwingAxis!
            Quaternion swordPositioning = isUpwardSwing 
                ? Quaternion.Euler(0f, 0f, -90f)      // Normal swing
                : Quaternion.Euler(0f, 180f, -90f);   // Flipped swing
            
            // Quaternion swordPositioning = (variedSwingAxis.y < 0f) ? Quaternion.Euler(0f, 180f, -90f) : Quaternion.Euler(0f, 0f, -90f);
            Quaternion start = PlanarRotation(variedSwingAxis, normal) * swordPositioning;
            
            float arcDirection = isUpwardSwing ? arcLength : -arcLength;
            Quaternion end = Quaternion.AngleAxis(arcDirection, variedSwingAxis) * start;

            StopAllCoroutines();
            StartCoroutine(SwingArc(start, end));
        }

        public void Swing(float duration)
        {
            this.duration = Mathf.Max(MIN_DURATION, duration);
            Swing();
        }
        
        private GameObject debugPlane;
        private void DrawSwingPlane(Vector3 swingAxis, Vector3 targetDir)
        {
            // Clean up old plane
            if (debugPlane) Destroy(debugPlane);

            debugPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debugPlane.name = "SwingPlane";

            debugPlane.transform.localScale = new Vector3(2.5f, 2.5f, 0.001f);
            debugPlane.transform.position = transform.position;

            // Orient the plane - swing axis is the plane's normal
            // Vector3 planeRight = targetDir;
            Vector3 planeUp = Vector3.Cross(swingAxis, targetDir).normalized;
            debugPlane.transform.rotation = Quaternion.LookRotation(swingAxis, planeUp);

            // Make semi-transparent
            var renderer = debugPlane.GetComponent<Renderer>();
            renderer.material.color = new Color(0, 1, 0, 0.15f); // Green, semi-transparent
        }
        
        public void BakeSwingKeyframes(float step = 1f)
        {
            if (!target)
            {
                Debug.LogWarning("No target assigned.");
                return;
            }

            var (normal, targetDirection) = Orient();
            Quaternion baseOrientation = transform.rotation; // Store the orientation we aimed at
            
            Vector3 baseSwingAxis = Vector3.Cross(normal, targetDirection).normalized;

            var keyframeList = new System.Collections.Generic.List<SwingKeyframe>();

            for (float angle = 0f; angle < 360f; angle += step)
            {
                Quaternion offsetRotation = Quaternion.AngleAxis(angle, targetDirection);
                Vector3 variedSwingAxis = offsetRotation * baseSwingAxis;
                
                bool isUpwardSwing = Vector3.Dot(variedSwingAxis, Vector3.up) > 0f;
                Quaternion swordPositioning = isUpwardSwing ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.Euler(0f, 180f, -90f);
                
                Quaternion start = PlanarRotation(variedSwingAxis, normal) * swordPositioning;
                float arcDirection = isUpwardSwing ? arcLength : -arcLength;
                Quaternion end = Quaternion.AngleAxis(arcDirection, variedSwingAxis) * start;
                
                // Store as relative to base orientation (make them local/reusable)
                Quaternion relativeStart = Quaternion.Inverse(baseOrientation) * start;
                Quaternion relativeEnd = Quaternion.Inverse(baseOrientation) * end;
                Vector3 localAxis = Quaternion.Inverse(baseOrientation) * variedSwingAxis;
                
                keyframeList.Add(new SwingKeyframe
                {
                    angle = angle,
                    relativeStart = relativeStart,
                    relativeEnd = relativeEnd,
                    localSwingAxis = localAxis,
                    isUpwardSwing = isUpwardSwing
                });
            }

            var keyframeSet = new SwingKeyframeSet
            {
                keyframes = keyframeList.ToArray(),
                planeNormal = normal
            };
    
            string path = "Assets/DodgyBall/data/swing_keyframes.bin";
            SwingKeyframeSet.Save(path, keyframeSet);

            Debug.Log($"Saved {keyframeList.Count} swing keyframes to {path}");
        }
    }
}
