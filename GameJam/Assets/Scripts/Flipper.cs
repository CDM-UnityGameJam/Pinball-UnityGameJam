using UnityEngine;
using UnityEngine.InputSystem;

// Kinematic flipper. Pivot is this transform; angles are Y rotations in board-local space.
[RequireComponent(typeof(Rigidbody))]
public class Flipper : MonoBehaviour
{
    public bool isLeft = true;
    public float restAngle = 25f;
    public float upAngle = -25f;
    public float speed = 1800f; // degrees per second

    Rigidbody rb;
    float angle;
    public bool IsPressed { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        angle = restAngle;
    }

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        bool pressed = kb != null && (isLeft
            ? kb.aKey.isPressed || kb.leftArrowKey.isPressed
            : kb.dKey.isPressed || kb.rightArrowKey.isPressed);
        IsPressed = pressed;
        angle = Mathf.MoveTowards(angle, pressed ? upAngle : restAngle, speed * Time.fixedDeltaTime);
        rb.MoveRotation(transform.parent.rotation * Quaternion.Euler(0f, angle, 0f));
    }
}
