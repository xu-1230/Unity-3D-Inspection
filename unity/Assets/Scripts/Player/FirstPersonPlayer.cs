using UnityEngine;

namespace VirtualInspection
{
    /// <summary>第一人称巡检视角：WASD 移动、鼠标转向、Shift 奔跑、空格跳跃。</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonPlayer : MonoBehaviour
    {
        public float walkSpeed = 3.3f;
        public float runSpeed = 5.8f;
        public float jumpHeight = 0.9f;
        public float gravity = -20f;
        public float lookSpeed = 2.4f;

        private CharacterController controller;
        private Transform head;
        private Camera playerCamera;
        private float yaw;
        private float pitch;
        private Vector3 verticalVelocity;
        private bool controlsEnabled = true;

        public Camera ViewCamera { get { return playerCamera; } }
        public float Yaw { get { return yaw; } }

        public void Initialize()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.04f;

            yaw = transform.eulerAngles.y;

            GameObject headGo = new GameObject("Head");
            headGo.transform.SetParent(transform, false);
            headGo.transform.localPosition = new Vector3(0f, SiteLayout.EyeHeight, 0f);
            head = headGo.transform;

            GameObject cameraGo = new GameObject("PlayerCamera");
            cameraGo.transform.SetParent(head, false);
            playerCamera = cameraGo.AddComponent<Camera>();
            playerCamera.tag = "MainCamera";
            playerCamera.fieldOfView = 68f;
            playerCamera.nearClipPlane = 0.05f;
            playerCamera.farClipPlane = 200f;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
            cameraGo.AddComponent<AudioListener>();

            LockCursor(true);
        }

        public void SetControls(bool enabled)
        {
            controlsEnabled = enabled;
            LockCursor(enabled);
        }

        public void Teleport(Vector3 position)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalVelocity = Vector3.zero;
        }

        public void ResetView(float yawDegrees)
        {
            yaw = yawDegrees;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (head != null) head.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (controller == null || head == null) return;

            HandleCursor();

            if (Cursor.lockState == CursorLockMode.Locked && controlsEnabled)
            {
                yaw += Input.GetAxisRaw("Mouse X") * lookSpeed;
                pitch -= Input.GetAxisRaw("Mouse Y") * lookSpeed;
                pitch = Mathf.Clamp(pitch, -85f, 85f);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            Vector3 wishMove = Vector3.zero;
            if (controlsEnabled)
            {
                float horizontal = Input.GetAxisRaw("Horizontal");
                float vertical = Input.GetAxisRaw("Vertical");
                Vector3 direction = transform.right * horizontal + transform.forward * vertical;
                if (direction.sqrMagnitude > 1f) direction.Normalize();

                float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
                wishMove = direction * speed;

                if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
                {
                    verticalVelocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
                }
            }

            if (controller.isGrounded && verticalVelocity.y <= 0f) verticalVelocity.y = -2f;
            verticalVelocity.y += gravity * Time.deltaTime;

            controller.Move((wishMove + verticalVelocity) * Time.deltaTime);
        }

        private void HandleCursor()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && Cursor.lockState == CursorLockMode.Locked)
            {
                LockCursor(false);
                return;
            }

            if (controlsEnabled && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                LockCursor(true);
            }
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
