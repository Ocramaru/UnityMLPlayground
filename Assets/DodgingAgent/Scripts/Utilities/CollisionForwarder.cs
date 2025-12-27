using UnityEngine;

namespace DodgyBall.Scripts.Utilities
{
    /// <summary>
    /// Forwards collision events to a target GameObject
    /// Attach this to any GameObject with a Rigidbody to forward its collisions
    /// </summary>
    public class CollisionForwarder : MonoBehaviour
    {
        [SerializeField] private GameObject target;

        private void OnCollisionEnter(Collision collision)
        {
            if (target != null)
            {
                target.SendMessage("OnCollisionEnter", collision, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
