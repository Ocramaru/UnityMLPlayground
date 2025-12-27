using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class editSword : MonoBehaviour
{
    public float AttackRange = .5f;
    public float arcLength = 150f;
    public float Duration = 1.5f;
    
    public GameObject target;

    private Rigidbody _rb;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (!_rb) Debug.LogWarning("No rigidbody found");
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!target) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Stop previous swing if one is running
            if (currentSwing != null)
            {
                StopCoroutine(currentSwing);
            }
            currentSwing = StartCoroutine(PeformVelocitySwing(Duration, target.transform.localPosition));
        }
    }
    
    // Position Based
    // private IEnumerator PerformNonRigidBodySwing(float duration, Vector3 targetPosition)
    // {
    //     float timeToMove = (HitAngle / ArcLength) * duration;
    //     float distance = Vector3.Distance(transform.localPosition, targetPosition) - AttackRange;
    //     Vector3 direction = (targetPosition - transform.localPosition).normalized;
    //     
    //     Vector3 positionalVelocity = direction * (distance / timeToMove);  // Handle local
    //     float offsetVelocity = (Random.value * 360) / timeToMove;  // Handle world
    //     float swingVelocity = ArcLength / duration;  // Handle local
    //     
    //     float elapsedTime = 0f;
    //     while (elapsedTime < duration)
    //     {
    //         if (elapsedTime < timeToMove)
    //         {
    //             transform.localPosition += positionalVelocity * Time.deltaTime;
    //             transform.Rotate(Vector3.right, offsetVelocity * Time.deltaTime, Space.Self);
    //         }
    //         transform.Rotate(Vector3.forward, swingVelocity * Time.deltaTime, Space.Self);
    //         
    //         elapsedTime += Time.deltaTime;
    //         yield return null;
    //     }
    // }
    
    // Quaternion based
    // private IEnumerator PerformRigidBodySwing(float duration, Vector3 targetPosition)
    // {
    //     // Time by which the weapon needs to be at hit point along arc
    //     Debug.Log($"duration is {duration}");
    //     float timeToContact = pointOfContact * duration;
    //     Debug.Log($"making contact at {timeToContact} seconds");
    //     float timeFromContact = duration - timeToContact;
    //     Debug.Log($"Following through for {timeFromContact} seconds");
    //     
    //     // Linear Position
    //     float distance = Vector3.Distance(transform.localPosition, targetPosition) - AttackRange;
    //     Vector3 direction = (targetPosition - transform.localPosition).normalized;
    //     Debug.DrawRay(transform.localPosition, direction * distance, Color.red, 5f);
    //     Vector3 linearVelocity = direction * (distance / timeToContact);
    //     
    //     // Rotations
    //     Quaternion startRot = _rb.rotation;
    //     Quaternion alignRot = Quaternion.FromToRotation(transform.right, -direction);
    //     // Debug.Log($"Dot {Mathf.Abs(Vector3.Dot(transform.forward, direction))} and {Mathf.Abs(Vector3.Dot(transform.forward, direction)) < 0.001f}");
    //     // if (Mathf.Abs(Vector3.Dot(transform.forward, direction)) < 0.001f) alignRot = Quaternion.AngleAxis(180f, -direction) * alignRot;
    //     
    //     Quaternion hitRot = alignRot * startRot;
    //     Quaternion followThroughRot = Quaternion.AngleAxis(arcContinuation, hitRot * Vector3.forward) * hitRot;
    //     Quaternion rollRot = Quaternion.AngleAxis(Random.value * 45f, transform.right);
    //     
    //     float elapsed = 0f;
    //     while (elapsed < duration)
    //     {
    //         Quaternion targetRotation;
    //         
    //         if (elapsed < timeToContact)
    //         {
    //             float t = Mathf.Clamp01(elapsed / timeToContact);
    //             targetRotation = Quaternion.Slerp(startRot, hitRot, t);
    //         }
    //         else
    //         {
    //             float t = Mathf.Clamp01((elapsed - timeToContact) / timeFromContact);
    //             targetRotation = Quaternion.Slerp(hitRot, followThroughRot, t);
    //         }
    //         // Build Quaternions
    //         // Quaternion qRoll =  Quaternion.Slerp(Quaternion.identity, rollRot, alignT);
    //         
    //         Quaternion delta = targetRotation * Quaternion.Inverse(_rb.rotation);
    //         delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
    //         if (angleDeg > 180f) angleDeg -= 360f;
    //         
    //         if (Mathf.Abs(angleDeg) < 0.001f) _rb.angularVelocity = Vector3.zero;
    //         else _rb.angularVelocity = axis.normalized * (angleDeg * Mathf.Deg2Rad / Time.fixedDeltaTime);
    //         
    //         // linear movement
    //         _rb.linearVelocity = elapsed < timeToContact ? linearVelocity : Vector3.zero;
    //
    //         elapsed += Time.fixedDeltaTime;
    //         yield return new WaitForFixedUpdate();
    //     }
    //     
    //     // Stop swing
    //     _rb.linearVelocity = Vector3.zero;
    //     _rb.angularVelocity = Vector3.zero;
    // }
    
    private float timer = 0f;
    private double expectedHitTime = 0f;
    private Coroutine currentSwing = null;
    

    private IEnumerator PeformVelocitySwing(float duration, Vector3 targetPosition)
    {
        timer = 0f;

        // Move to target distance and orient (25% of duration)
        float orientTime = duration * 0.25f;

        // Perform swing (75% of duration)
        float swingTime = duration - orientTime;

        float contactRatio = (0.037f / swingTime) + 0.488f; // Tested a bunch of swings to get this (probably a better way)
        float timeToContact = contactRatio * swingTime;
        expectedHitTime = orientTime + timeToContact;

        // Calculate linear movement
        float distance = Vector3.Distance(transform.localPosition, targetPosition) - AttackRange;
        Vector3 direction = (targetPosition - transform.localPosition).normalized;
        Vector3 linearVelocity = direction * (distance / orientTime);

        // Calculate orientation
        Quaternion startRot = _rb.rotation;
        Quaternion alignRot = Quaternion.FromToRotation(transform.right, -direction);
        Quaternion rollRot = Quaternion.AngleAxis(Random.value * 45f, Vector3.right);
    
        // Slerp would work but it goes backwards through the target so we can instead follow similar logic
        alignRot.ToAngleAxis(out float alignAngle, out Vector3 alignAxis);
        if (alignAngle < 180f && alignAngle > 0.1f) { alignAngle = -(360f - alignAngle);}

        Debug.Log($"Expected contact at {expectedHitTime}s");
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            Quaternion targetRotation;

            // Orient/Linear Movement Phase
            if (elapsed < orientTime)
            {
                float t = elapsed / orientTime;
                float currentAngle = alignAngle * t;
                Quaternion qAlign = Quaternion.AngleAxis(currentAngle, alignAxis);
                Quaternion qRoll = Quaternion.Slerp(Quaternion.identity, rollRot, t);
                targetRotation = qAlign * startRot * qRoll;
                _rb.linearVelocity = linearVelocity;
            }
            // Swing Phase
            else
            {
                float swingT = (elapsed - orientTime) / swingTime;
                Quaternion qSwing = Quaternion.AngleAxis(arcLength * swingT, Vector3.forward);
                Quaternion orientedRot = alignRot * startRot * rollRot;
                targetRotation = orientedRot * qSwing;
                _rb.linearVelocity = Vector3.zero;
            }

            // Calculate angular velocity with stability checks
            Quaternion delta = targetRotation * Quaternion.Inverse(_rb.rotation);
            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f) angleDeg -= 360f;
            if (Mathf.Abs(angleDeg) < 0.001f) { _rb.angularVelocity = Vector3.zero; }
        
            float angularSpeed = (angleDeg * Mathf.Deg2Rad) / Time.fixedDeltaTime;
            angularSpeed = Mathf.Clamp(angularSpeed, -500f, 500f);
            _rb.angularVelocity = axis.normalized * angularSpeed;

            elapsed += Time.fixedDeltaTime;
            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Stop swing
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        currentSwing = null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            // Calculate current rotation angle in the swing
            float orientTime = Duration * 0.25f;
            float timeIntoSwing = Mathf.Max(0, timer - orientTime);
            float swingTime = Duration - orientTime;
            float swingProgress = timeIntoSwing / swingTime;
            float currentSwingAngle = swingProgress * arcLength;

            if (Mathf.Abs((float)(timer - expectedHitTime)) < 0.02f)
            {
                Debug.Log($"Accurate Expected Hit Time");
            }
            Debug.Log($"Made Contact with Player, Timer and expectedHitTime: {timer} / {expectedHitTime}");
            Debug.Log($"Contact at swing angle: {currentSwingAngle:F2}° (progress: {swingProgress:F3}, duration: {Duration})");
        }
    }
}
