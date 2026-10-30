using UnityEngine;

namespace Unity3DInspection
{
    /// <summary>相机平移与俯仰，左键留给射线拾取，右键拖动转视角。</summary>
    public class CameraRigController : MonoBehaviour
    {
        public float moveSpeed = 4f;
        public float lookSpeed = 2.2f;
        public float minX = -18f;
        public float maxX = 18f;
        public float minZ = -18f;
        public float maxZ = 18f;
        public float minHeight = 1.2f;
        public float maxHeight = 3f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private void Update()
        {
            Look();
            Move();
        }

        private void Look()
        {
            if (!Input.GetMouseButton(1))
            {
                return;
            }

            _yaw += Input.GetAxis("Mouse X") * lookSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void Move()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            Vector3 direction = transform.forward * vertical + transform.right * horizontal;
            direction.y = 0f;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            Vector3 next = transform.position + direction * moveSpeed * Time.deltaTime;
            next.x = Mathf.Clamp(next.x, minX, maxX);
            next.z = Mathf.Clamp(next.z, minZ, maxZ);
            next.y = Mathf.Clamp(next.y, minHeight, maxHeight);
            transform.position = next;
        }
    }
}
