using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kontroler kamera bebas untuk pengujian di Editor Play Mode (Sandbox).
/// Menggunakan New Input System:
/// - W/A/S/D: Bergerak maju, mundur, kiri, kanan
/// - Tahan Klik Kanan Mouse: Putar sudut pandang
/// - E / Q (atau Space / Ctrl): Naik / Turun
/// - Left Shift: Lari cepat
/// </summary>
[RequireComponent(typeof(Camera))]
public class SimpleSandboxFreeCam : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float sprintMultiplier = 2.0f;
    [SerializeField] private float lookSensitivity = 0.15f;

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
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        // 1. Rotasi Kamera (Tahan Klik Kanan Mouse)
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            _rotationX += delta.x;
            _rotationY += delta.y;
            _rotationY = Mathf.Clamp(_rotationY, -85f, 85f);

            transform.localRotation = Quaternion.Euler(-_rotationY, _rotationX, 0f);
        }

        // 2. Pergerakan WASD + EQ via Keyboard
        if (keyboard != null)
        {
            float speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? sprintMultiplier : 1.0f);
            Vector3 moveDir = Vector3.zero;

            if (keyboard.wKey.isPressed) moveDir += transform.forward;
            if (keyboard.sKey.isPressed) moveDir -= transform.forward;
            if (keyboard.dKey.isPressed) moveDir += transform.right;
            if (keyboard.aKey.isPressed) moveDir -= transform.right;
            if (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed) moveDir += Vector3.up;
            if (keyboard.qKey.isPressed || keyboard.leftCtrlKey.isPressed) moveDir -= Vector3.up;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                transform.position += moveDir.normalized * (speed * Time.deltaTime);
            }
        }
    }

    private void OnGUI()
    {
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(20, 20, 400, 75), 
            "<b><size=14>🎮 Sandbox Navigation (New Input System):</size></b>\n" +
            "• <b>WASD:</b> Bergerak Maju / Mundur / Samping\n" +
            "• <b>Tahan Klik Kanan + Geser Mouse:</b> Putar Arah Pandang\n" +
            "• <b>E / Q / Spasi:</b> Naik / Turun Ketinggian\n" +
            "• <b>Left Shift:</b> Lari Cepat");
    }
}
