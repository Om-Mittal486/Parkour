using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement movement;

    [Header("Look")]
    [SerializeField] private float sensitivity = 0.12f;
    [SerializeField] private float maxLookAngle = 89f;

    [Header("Head Bob")]
    [SerializeField] private float walkBobSpeed = 8f;
    [SerializeField] private float walkBobAmount = 0.035f;

    [SerializeField] private float sprintBobSpeed = 12f;
    [SerializeField] private float sprintBobAmount = 0.06f;

    [Header("Camera Tilt")]
    [SerializeField] private float strafeTilt = 3f;
    [SerializeField] private float turnTilt = 2f;
    [SerializeField] private float tiltSmoothSpeed = 8f;

    [Header("Slide Camera")]
    [SerializeField] private float slideCameraDrop = 0.35f;
    [SerializeField] private float slideCameraTilt = 4f;
    [SerializeField] private float slideTransitionSpeed = 10f;

    [Header("Landing")]
    [SerializeField] private float landingAmount = 0.12f;
    [SerializeField] private float landingRecoverySpeed = 8f;

    [Header("Slide Shake")]
    [SerializeField] private float slideShakeAmount = 0.025f;
    [SerializeField] private float slideShakeSpeed = 18f;

    [Header("Input")]
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference moveAction;

    private float pitch;
    private float yaw;

    private float bobTimer;
    private float slideShakeTimer;

    private float landingOffset;

    private Vector3 originalCameraPosition;

    private bool wasGrounded;

    private void Awake()
    {
        originalCameraPosition =
            playerCamera.transform.localPosition;

        yaw = player.eulerAngles.y;
    }

    private void OnEnable()
    {
        lookAction.action.Enable();
        moveAction.action.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        lookAction.action.Disable();
        moveAction.action.Disable();
    }

    private void Update()
    {
        HandleLook();
        HandleHeadBob();
        HandleTilt();
        HandleSlideCamera();
        HandleLanding();

        ApplyCameraPosition();
    }

    // =========================================================
    // LOOK
    // =========================================================

    private void HandleLook()
    {
        Vector2 look =
            lookAction.action.ReadValue<Vector2>();

        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;

        pitch =
            Mathf.Clamp(
                pitch,
                -maxLookAngle,
                maxLookAngle
            );

        player.rotation =
            Quaternion.Euler(
                0f,
                yaw,
                0f
            );

        // Pitch is handled separately from the player's rotation.
        transform.localRotation =
            Quaternion.Euler(
                pitch,
                0f,
                0f
            );
    }

    // =========================================================
    // HEAD BOB
    // =========================================================

    private void HandleHeadBob()
    {
        if (movement.IsSliding)
            return;

        if (!movement.IsGrounded ||
            movement.CurrentSpeed < 0.1f)
        {
            bobTimer = 0f;
            return;
        }

        bool sprinting =
            movement.IsSprinting;

        float bobSpeed =
            sprinting
                ? sprintBobSpeed
                : walkBobSpeed;

        float bobAmount =
            sprinting
                ? sprintBobAmount
                : walkBobAmount;

        bobTimer +=
            Time.deltaTime * bobSpeed;

        float bobX =
            Mathf.Cos(bobTimer * 0.5f)
            * bobAmount;

        float bobY =
            Mathf.Sin(bobTimer)
            * bobAmount;

        Vector3 targetPosition =
            originalCameraPosition +
            new Vector3(
                bobX,
                bobY,
                0f
            );

        playerCamera.transform.localPosition =
            Vector3.Lerp(
                playerCamera.transform.localPosition,
                targetPosition,
                12f * Time.deltaTime
            );
    }

    // =========================================================
    // CAMERA TILT
    // =========================================================

    private void HandleTilt()
    {
        Vector2 input =
            moveAction.action.ReadValue<Vector2>();

        float targetTilt =
            -input.x * strafeTilt;

        float turnVelocity =
            Mathf.DeltaAngle(
                player.eulerAngles.y,
                yaw
            );

        targetTilt +=
            Mathf.Clamp(
                turnVelocity * turnTilt,
                -5f,
                5f
            );

        if (movement.IsSliding)
        {
            targetTilt =
                -input.x * slideCameraTilt;
        }

        Quaternion targetRotation =
            Quaternion.Euler(
                pitch,
                0f,
                targetTilt
            );

        transform.localRotation =
            Quaternion.Lerp(
                transform.localRotation,
                targetRotation,
                tiltSmoothSpeed *
                Time.deltaTime
            );
    }

    // =========================================================
    // SLIDE CAMERA
    // =========================================================

    private void HandleSlideCamera()
    {
        float targetDrop =
            movement.IsSliding
                ? -slideCameraDrop
                : 0f;

        Vector3 targetPosition =
            originalCameraPosition;

        targetPosition.y +=
            targetDrop;

        if (movement.IsSliding)
        {
            slideShakeTimer +=
                Time.deltaTime *
                slideShakeSpeed;

            float shakeX =
                Mathf.Sin(slideShakeTimer)
                * slideShakeAmount;

            float shakeY =
                Mathf.Cos(slideShakeTimer * 1.3f)
                * slideShakeAmount;

            targetPosition.x += shakeX;
            targetPosition.y += shakeY;
        }

        playerCamera.transform.localPosition =
            Vector3.Lerp(
                playerCamera.transform.localPosition,
                targetPosition,
                slideTransitionSpeed *
                Time.deltaTime
            );
    }

    // =========================================================
    // LANDING
    // =========================================================

    private void HandleLanding()
    {
        if (!wasGrounded &&
            movement.IsGrounded)
        {
            float impact =
                Mathf.Clamp01(
                    movement.CurrentSpeed / 12f
                );

            landingOffset =
                -landingAmount *
                impact;
        }

        landingOffset =
            Mathf.Lerp(
                landingOffset,
                0f,
                landingRecoverySpeed *
                Time.deltaTime
            );

        wasGrounded =
            movement.IsGrounded;
    }

    // =========================================================
    // CAMERA POSITION
    // =========================================================

    private void ApplyCameraPosition()
    {
        Vector3 position =
            playerCamera.transform.localPosition;

        position.y += landingOffset;

        playerCamera.transform.localPosition =
            Vector3.Lerp(
                playerCamera.transform.localPosition,
                position,
                12f * Time.deltaTime
            );
    }
}