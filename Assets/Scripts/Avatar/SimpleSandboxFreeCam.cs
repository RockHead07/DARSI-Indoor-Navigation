using UnityEngine;

/// <summary>
/// Kontroler kamera bebas untuk pengujian di Editor Play Mode (Sandbox).
/// Menggunakan WASD untuk bergerak, Klik Kanan Mouse untuk memutar sudut pandang,
/// dan E/Q untuk naik/turun ketinggian kamera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SimpleSandboxFreeCam : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3.0f;
    [SerializeField] private float sprintMultiplier = 2.0f;
    [SerializeField] private float lookSensitivity = 3.0f;

    private float _rotationX = 0f;
    private float _rotationY = 0f;

    private void Start()
    {
        Vector3 rot = transform.localEulerAngles;
        _rotationX = rot.y;
        _rotationY = -rot.x;
    }

    private void Update()
    {
        // 1. Rotasi Kamera (Tahan Klik Kanan Mouse)
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

            _rotationX += mouseX;
            _rotationY += mouseY;
            _rotationY = Mathf.Clamp(_rotationY, -85f, 85f);

            transform.localRotation = Quaternion.Euler(-_rotationY, _rotationX, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 2. Pergerakan WASD + EQ
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1.0f);

        float horizontal = Input.GetAxis("Horizontal"); // A/D
        float vertical = Input.GetAxis("Vertical");     // W/S
        float upDown = 0f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) upDown += 1f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) upDown -= 1f;

        Vector3 moveDir = (transform.forward * vertical) + (transform.right * horizontal) + (Vector3.up * upDown);
        transform.position += moveDir * (speed * Time.deltaTime);
    }

    private void OnGUI()
    {
        GUI.color = new Color(1f, 1f, 1f, 0.75f);
        GUI.Label(new Rect(20, 20, 350, 60), 
            "<b>Sandbox Navigation:</b>\n" +
            "• <b>WASD:</b> Bergerak Maju/Mundur/Samping\n" +
            "• <b>Tahan Klik Kanan + Geser Mouse:</b> Putar Arah Pandang\n" +
            "• <b>E / Q:</b> Naik / Turun Ketinggian");
    }
}
