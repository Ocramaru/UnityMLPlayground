using System;
using UnityEngine;

namespace DodgingAgent.Scripts.Utilities
{
    public class InferenceCamera:MonoBehaviour
    {
        public Transform target;
        [SerializeField] private float cameraGap = 2f;  // aka radius
        [SerializeField][Tooltip("smoothing used in lerp, allows for smoothed delayed movement")][Range(0f, 1f)] private float cameraSmoothing = 0.25f;
        [SerializeField] private float verticalTheta = 1f;
        [SerializeField] private float horizontalTheta = 0f;
        [SerializeField][Tooltip("degrees/sec")][Range(0f, 45f)] private float rotationSpeed = 10f;
        [SerializeField][Tooltip("Zoom speed - controls how much cameraGap changes per scroll")] private float scrollSensitivity = 50f;
        [SerializeField] private float minCameraGap = 0.5f;
        [SerializeField] private float maxCameraGap = 20f;
        [SerializeField] private bool invertX = false;
        [SerializeField] private bool invertY = false;
        [SerializeField][Tooltip("Distance threshold to stop lerping and snap to target")] private float snapThreshold = 0.1f;

        public void LateUpdate()
        {
            // Handle input
            float horizontalInput = Input.GetAxis("Horizontal") * (invertX ? -1f : 1f);
            float verticalInput = Input.GetAxis("Vertical") * (invertY ? 1f : -1f);

            horizontalTheta += rotationSpeed * horizontalInput * Time.deltaTime;
            verticalTheta += rotationSpeed * verticalInput * Time.deltaTime;

            cameraGap -= Input.mouseScrollDelta.y * scrollSensitivity;
            cameraGap = Mathf.Clamp(cameraGap, minCameraGap, maxCameraGap);

            // Update camera position
            float x = cameraGap * Mathf.Sin(verticalTheta) * Mathf.Cos(horizontalTheta);
            float y = cameraGap * Mathf.Cos(verticalTheta);
            float z = cameraGap * Mathf.Sin(verticalTheta) * Mathf.Sin(horizontalTheta);

            Vector3 offset = new Vector3(x, y, z);
            Vector3 nextPosition = target.position + offset;

            float distance = Vector3.Distance(transform.position, nextPosition);
            if (distance < snapThreshold) {
                transform.position = nextPosition;
            } else {
                transform.position = Vector3.Lerp(transform.position, nextPosition, cameraSmoothing);
            }

            Quaternion nextRotation = Quaternion.LookRotation(target.position - nextPosition, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, nextRotation, cameraSmoothing);
        }
    }
}