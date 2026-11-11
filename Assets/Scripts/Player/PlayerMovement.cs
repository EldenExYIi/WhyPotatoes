using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("输入与相机")]

    [SerializeField]
    [Tooltip("拖入场景中的 Main Camera，角色根据相机朝向移动。")]
    private Transform cameraTransform;

    [SerializeField]
    [Tooltip("选择 InputSystem_Actions 中的 Player/Move 输入动作。")]
    private InputActionReference moveAction;

    [Header("移动设置")]

    [SerializeField]
    [Tooltip("角色每秒移动的距离，单位为米。")]
    private float moveSpeed = 5f;

    [SerializeField]
    [Tooltip("角色每秒最多旋转的角度，数值越大转身越快。")]
    private float rotationSpeed = 720f;

    private Rigidbody playerRigidbody;
    private Vector2 moveInput;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();

        if (cameraTransform == null ||
            moveAction == null ||
            moveAction.action == null)
        {
            Debug.LogError("请在 Inspector 中绑定相机和 Move 输入动作。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action?.Disable();
        moveInput = Vector2.zero;
    }

    private void Update()
    {
        moveInput = moveAction.action.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // 获取相机在地面上的前方和右方。
        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 cameraRight = cameraTransform.right;
        cameraRight.y = 0f;
        cameraRight.Normalize();

        // 将键盘输入转换成相对于相机的移动方向。
        Vector3 moveDirection =
            cameraForward * moveInput.y +
            cameraRight * moveInput.x;

        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        // 设置水平速度，保留物理系统计算的竖直速度。
        Vector3 velocity = playerRigidbody.linearVelocity;
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;
        playerRigidbody.linearVelocity = velocity;

        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 平滑转向移动方向。
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

        Quaternion nextRotation = Quaternion.RotateTowards(
            playerRigidbody.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime
        );

        playerRigidbody.MoveRotation(nextRotation);
    }
}
