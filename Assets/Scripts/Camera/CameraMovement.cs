using UnityEngine;
using UnityEngine.InputSystem;


public class CameraMovement : MonoBehaviour
{
    [Header("跟随目标")]

    [SerializeField]
    [Tooltip("拖入场景中的 Player。")]
    private Transform target;

    [SerializeField]
    [Tooltip("选择输入资源中的 Player/Look 动作。")]
    private InputActionReference lookAction;

    [Header("相机位置")]

    [SerializeField]
    [Tooltip("相机与观察点之间的距离。")]
    private float distance = 5f;

    [SerializeField]
    [Tooltip("观察点相对于角色中心的高度。")]
    private float targetHeight = 0.5f;

    [SerializeField]
    [Tooltip("镜头沿自身右侧偏移的距离，单位为米。")]
    private float shoulderOffset;

    [Header("鼠标控制")]

    [SerializeField]
    [Tooltip("鼠标每移动一个像素，相机旋转的角度。")]
    private float mouseSensitivity = 0.15f;

    [SerializeField]
    [Tooltip("进入游戏时的俯角。")]
    private float initialPitch = 20f;

    [SerializeField]
    [Tooltip("向上观察时允许的最小俯仰角。")]
    private float minPitch = -10f;

    [SerializeField]
    [Tooltip("向下观察时允许的最大俯仰角。")]
    private float maxPitch = 60f;

    private float yaw;
    private float pitch;
    private float shakeAmplitude;
    private float shakeDuration;
    private float shakeStartTime;

    public void Shake(float amplitude, float duration)
    {
        shakeAmplitude = amplitude;
        shakeDuration = duration;
        shakeStartTime = Time.time;
    }

    private void Awake()
    {
        if (target == null ||
            lookAction == null ||
            lookAction.action == null)
        {
            Debug.LogError("请在 Inspector 中绑定 Player 和 Look 输入动作。", this);
            enabled = false;
            return;
        }

        yaw = transform.eulerAngles.y;
        pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
    }

    private void OnEnable()
    {
        lookAction.action.Enable();
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        lookAction?.action?.Disable();
        SetCursorLocked(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SetCursorLocked(false);
        }
    }

    private void LateUpdate()
    {
        Vector2 lookInput = Vector2.zero;

        if (Application.isFocused)
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState == CursorLockMode.Locked)
            {
                lookInput = lookAction.action.ReadValue<Vector2>();
            }
            else if (Mouse.current != null &&
                     Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }
        }

        // 水平鼠标移动控制左右环绕。
        yaw += lookInput.x * mouseSensitivity;

        // 竖直鼠标移动控制俯仰，并限制角度。
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 观察点位于角色中心上方。
        Vector3 focusPosition = target.position + Vector3.up * targetHeight;

        // 沿相机朝向的反方向退后，得到相机位置。
        Vector3 cameraPosition =
            focusPosition + rotation * new Vector3(shoulderOffset, 0f, -distance);

        float shakeElapsed = Time.time - shakeStartTime;
        if (shakeElapsed < shakeDuration)
        {
            float fade = 1f - shakeElapsed / shakeDuration;
            float angle = shakeElapsed * 2f * Mathf.PI * 24f;
            Vector3 offset = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle) * 0.5f, 0f);
            cameraPosition += rotation * (offset * (shakeAmplitude * fade));
        }

        transform.SetPositionAndRotation(cameraPosition, rotation);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }
}
