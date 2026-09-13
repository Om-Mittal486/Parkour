using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform groundCheck;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 7f;
    [SerializeField] private float sprintSpeed = 11f;
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float deceleration = 30f;
    [SerializeField] private float airControl = 0.5f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 2.2f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundMask;

    [Header("Slide")]
    [SerializeField] private float slideSpeed = 14f;
    [SerializeField] private float slideDuration = 1.0f;
    [SerializeField] private float slideDeceleration = 12f;
    [SerializeField] private float slideSteer = 5f;

    [Header("Player Height")]
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float slidingHeight = 1.0f;
    [SerializeField] private float heightTransitionSpeed = 8f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference slideAction;

    private CharacterController controller;

    private Vector3 horizontalVelocity;
    private Vector3 slideDirection;

    private float verticalVelocity;

    private float coyoteTimer;
    private float jumpBufferTimer;
    private float slideTimer;

    private bool isGrounded;
    private bool wasGrounded;
    private bool isSliding;
    private bool sprintToggled;

    public bool IsGrounded => isGrounded;
    public bool IsSprinting { get; private set; }
    public bool IsSliding => isSliding;

    public float CurrentSpeed
    {
        get
        {
            return new Vector3(
                horizontalVelocity.x,
                0f,
                horizontalVelocity.z
            ).magnitude;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        controller.height = standingHeight;

        Vector3 center = controller.center;
        center.y = standingHeight / 2f;
        controller.center = center;
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        lookAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();
        slideAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        lookAction.action.Disable();
        jumpAction.action.Disable();
        sprintAction.action.Disable();
        slideAction.action.Disable();
    }

    private void Update()
    {
        CheckGround();

        HandleTimers();
        HandleJumpInput();

        if (isSliding)
        {
            HandleSlide();
        }
        else
        {
            HandleMovement();
            HandleSlideInput();
            if (!isSliding && CanStand())
                SmoothControllerHeight(standingHeight);
        }

        ApplyGravity();
        MovePlayer();
    }

    // =========================================================
    // GROUND DETECTION
    // =========================================================

    private void CheckGround()
    {
        wasGrounded = isGrounded;

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;

            if (verticalVelocity < 0f)
                verticalVelocity = -2f;
        }
    }

    // =========================================================
    // TIMERS
    // =========================================================

    private void HandleTimers()
    {
        if (!isGrounded)
            coyoteTimer -= Time.deltaTime;

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        if (isSliding)
            slideTimer -= Time.deltaTime;
    }

    // =========================================================
    // JUMP
    // =========================================================

    private void HandleJumpInput()
    {
        if (jumpAction.action.WasPressedThisFrame())
        {
            jumpBufferTimer = jumpBufferTime;
        }

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            Jump();
        }
    }

    private void Jump()
    {
        if (isSliding)
            return;

        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
    }

    // =========================================================
    // NORMAL MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        Vector2 input =
            moveAction.action.ReadValue<Vector2>();

        Vector3 forward =
            cameraTransform.forward;

        Vector3 right =
            cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 direction =
            forward * input.y +
            right * input.x;

        if (direction.magnitude > 1f)
            direction.Normalize();

        // Toggle sprint.
        if (sprintAction.action.WasPressedThisFrame())
        {
            sprintToggled = !sprintToggled;
        }

        IsSprinting =
            sprintToggled &&
            direction.magnitude > 0.1f;

        // Stop sprinting when the player completely stops.
        if (direction.magnitude <= 0.1f)
        {
            IsSprinting = false;
            sprintToggled = false;
        }

        float targetSpeed =
            IsSprinting
                ? sprintSpeed
                : walkSpeed;

        Vector3 targetVelocity =
            direction * targetSpeed;

        float control =
            isGrounded
                ? 1f
                : airControl;

        float rate =
            direction.magnitude > 0.01f
                ? acceleration
                : deceleration;

        horizontalVelocity =
            Vector3.MoveTowards(
                horizontalVelocity,
                targetVelocity,
                rate * control * Time.deltaTime
            );
    }

    // =========================================================
    // SLIDE INPUT
    // =========================================================

    private void HandleSlideInput()
    {
        if (!slideAction.action.WasPressedThisFrame())
            return;

        if (!isGrounded)
            return;

        if (!IsSprinting)
            return;

        StartSlide();
    }

    // =========================================================
    // START SLIDE
    // =========================================================

    private void StartSlide()
    {
        isSliding = true;

        slideTimer = slideDuration;

        Vector3 direction =
            horizontalVelocity;

        direction.y = 0f;

        if (direction.magnitude < 0.1f)
        {
            direction = transform.forward;
        }

        slideDirection =
            direction.normalized;

        float startingSpeed =
            Mathf.Max(
                horizontalVelocity.magnitude,
                slideSpeed
            );

        horizontalVelocity =
            slideDirection *
            startingSpeed;

        // Immediately enter the slide height.
        // The camera will handle its own smooth transition.
        SetControllerHeight(slidingHeight);
    }

    // =========================================================
    // SLIDE
    // =========================================================

    private void HandleSlide()
    {
        IsSprinting = false;

        Vector2 input =
            moveAction.action.ReadValue<Vector2>();

        Vector3 forward =
            cameraTransform.forward;

        Vector3 right =
            cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection =
            forward * input.y +
            right * input.x;

        if (desiredDirection.magnitude > 1f)
            desiredDirection.Normalize();

        // Smooth steering.
        if (desiredDirection.magnitude > 0.1f)
        {
            slideDirection =
                Vector3.Slerp(
                    slideDirection,
                    desiredDirection,
                    slideSteer * Time.deltaTime
                ).normalized;
        }

        float currentSpeed =
            horizontalVelocity.magnitude;

        // Smoothly slow down.
        currentSpeed =
            Mathf.MoveTowards(
                currentSpeed,
                0f,
                slideDeceleration *
                Time.deltaTime
            );

        horizontalVelocity =
            slideDirection *
            currentSpeed;

        // Smoothly raise the player as the slide
        // comes to an end.
        float targetHeight =
            slidingHeight;

        bool wantsToStand = slideTimer <= 0f || currentSpeed < 2f;
        bool canStand = CanStand();
        if (wantsToStand && canStand)
        {
            targetHeight =
                standingHeight;
        }

        SmoothControllerHeight(targetHeight);

        if (wantsToStand && canStand)
        {
            EndSlide();
        }
        else if (wantsToStand)
        {
            // Keep moving out of a low opening instead of standing into its roof.
            horizontalVelocity = slideDirection * Mathf.Max(currentSpeed, walkSpeed * 0.55f);
        }
    }

    // =========================================================
    // END SLIDE
    // =========================================================

    private void EndSlide()
    {
        isSliding = false;

        // Don't kill momentum.
        horizontalVelocity *= 0.85f;

        // Make sure the controller returns smoothly
        // to standing height.
        SmoothControllerHeight(standingHeight);
    }

    private bool CanStand()
    {
        float radius = Mathf.Max(0.05f, controller.radius - controller.skinWidth);
        Vector3 feet = transform.position;
        return !Physics.CheckCapsule(
            feet + Vector3.up * (slidingHeight + radius),
            feet + Vector3.up * (standingHeight - radius),
            radius, groundMask, QueryTriggerInteraction.Ignore);
    }

    public void ResetMotion()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        coyoteTimer = jumpBufferTimer = slideTimer = 0f;
        isSliding = sprintToggled = IsSprinting = false;
        SetControllerHeight(standingHeight);
    }

    // =========================================================
    // CONTROLLER HEIGHT
    // =========================================================

    private void SmoothControllerHeight(float targetHeight)
    {
        float currentHeight =
            controller.height;

        float newHeight =
            Mathf.Lerp(
                currentHeight,
                targetHeight,
                heightTransitionSpeed *
                Time.deltaTime
            );

        SetControllerHeight(newHeight);
    }

    private void SetControllerHeight(float height)
    {
        float previousHeight =
            controller.height;

        controller.height =
            height;

        float heightDifference =
            height - previousHeight;

        Vector3 center =
            controller.center;

        // Keep the feet in approximately the same position.
        center.y +=
            heightDifference * 0.5f;

        controller.center =
            center;
    }

    // =========================================================
    // GRAVITY
    // =========================================================

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            verticalVelocity +=
                gravity *
                Time.deltaTime;
        }
    }

    // =========================================================
    // MOVE PLAYER
    // =========================================================

    private void MovePlayer()
    {
        Vector3 movement =
            horizontalVelocity +
            Vector3.up *
            verticalVelocity;

        controller.Move(
            movement *
            Time.deltaTime
        );
    }

    // =========================================================
    // DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}
