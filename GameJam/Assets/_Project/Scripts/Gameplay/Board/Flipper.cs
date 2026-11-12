using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pinball.Gameplay
{
    // Kinematic flipper. This transform is the pivot; angles are Y rotations relative to the board.
    // Port of the old Flipper: MoveTowards at a fixed angular speed, applied with MoveRotation in FixedUpdate.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Flipper : MonoBehaviour
    {
        [Required] public InputActionReference input;
        [SuffixLabel("deg")] public float restAngle = 25f;
        [SuffixLabel("deg")] public float upAngle = -25f;
        [MinValue(0), SuffixLabel("deg/s")] public float speed = 1800f;

        [Title("Hooks")]
        public UltEvent onFlipUp;
        public UltEvent onFlipDown;

        Rigidbody body;
        float angle;
        bool? pressedOverride;

        public bool IsPressed { get; private set; }
        public float Angle => angle;

        // Drive the flipper from code (tests, touch zones, AI) instead of the input action. Null = input.
        public void SetPressedOverride(bool? pressed) => pressedOverride = pressed;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            angle = restAngle;
        }

        void OnEnable()
        {
            if (input && input.action != null) input.action.Enable();
        }

        void FixedUpdate()
        {
            bool pressed = pressedOverride ?? (input && input.action != null && input.action.IsPressed());
            if (pressed != IsPressed)
            {
                IsPressed = pressed;
                if (pressed) onFlipUp?.Invoke();
                else onFlipDown?.Invoke();
            }
            angle = Mathf.MoveTowards(angle, pressed ? upAngle : restAngle, speed * Time.fixedDeltaTime);
            body.MoveRotation(transform.parent.rotation * Quaternion.Euler(0f, angle, 0f));
        }
    }
}
