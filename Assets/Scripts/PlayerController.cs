using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float acceleration = 13f;
    public float deceleration = 16f;
    public float frictionAmount = 0.2f;
    [Range(0f, 1f)] public float airControl = 0.8f;

    [Header("Jump")]
    public float jumpForce = 15f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 3f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.1f;

    [Header("Wall Interaction")]
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpForce = new Vector2(10f, 15f);

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.2f;

    [Header("Collision")]
    public LayerMask groundLayer;
    public Vector2 bottomOffset = new Vector2(0, -0.5f);
    public Vector2 rightOffset = new Vector2(0.5f, 0);
    public Vector2 leftOffset = new Vector2(-0.5f, 0);
    public float collisionRadius = 0.25f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float lastGroundedTime = -100f;
    private float lastJumpTime = -100f;
    private float lastDashTime = -100f;
    
    private bool isGrounded;
    private bool isOnWall;
    private bool isFacingRight = true;
    private bool isDashing;
    private bool canDash = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
    }

    void Update()
    {
        if (isDashing) return;

        CheckCollisions();
        HandleInput();
        HandleJump();
        HandleWallSlide();
    }

    void FixedUpdate()
    {
        if (isDashing) return;

        Move();
        ApplyGravity();
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        moveInput.x = 0;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveInput.x += 1;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveInput.x -= 1;

        moveInput.y = 0;
        if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) moveInput.y += 1;
        if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) moveInput.y -= 1;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            lastJumpTime = Time.time;
        }

        if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame)
        {
            if (canDash && Time.time - lastDashTime > dashCooldown)
            {
                StartCoroutine(DashRoutine());
            }
        }

        if (moveInput.x > 0 && !isFacingRight)
            Flip();
        else if (moveInput.x < 0 && isFacingRight)
            Flip();
    }

    private void CheckCollisions()
    {
        isGrounded = Physics2D.OverlapCircle((Vector2)transform.position + bottomOffset, collisionRadius, groundLayer);
        isOnWall = Physics2D.OverlapCircle((Vector2)transform.position + rightOffset, collisionRadius, groundLayer) 
            || Physics2D.OverlapCircle((Vector2)transform.position + leftOffset, collisionRadius, groundLayer);

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            canDash = true;
        }
    }

    private void Move()
    {
        float targetSpeed = moveInput.x * moveSpeed;
        float speedDif = targetSpeed - rb.linearVelocity.x;

        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        
        if (!isGrounded) accelRate *= airControl;

        float movement = speedDif * accelRate;
        rb.AddForce(movement * Vector2.right, ForceMode2D.Force);

        if (isGrounded && Mathf.Abs(moveInput.x) < 0.01f)
        {
            float amount = Mathf.Min(Mathf.Abs(rb.linearVelocity.x), Mathf.Abs(frictionAmount));
            amount *= Mathf.Sign(rb.linearVelocity.x);
            rb.AddForce(Vector2.right * -amount, ForceMode2D.Impulse);
        }
    }

    private void HandleJump()
    {
        if (Time.time - lastGroundedTime <= coyoteTime && Time.time - lastJumpTime <= jumpBufferTime)
        {
            Jump(Vector2.up, jumpForce);
        }
        else if (isOnWall && !isGrounded && Time.time - lastJumpTime <= jumpBufferTime)
        {
            int wallDir = Physics2D.OverlapCircle((Vector2)transform.position + rightOffset, collisionRadius, groundLayer) ? -1 : 1;
            Vector2 jumpDir = new Vector2(wallJumpForce.x * wallDir, wallJumpForce.y);
            Jump(jumpDir.normalized, jumpDir.magnitude);
        }
    }

    private void Jump(Vector2 direction, float force)
    {
        lastJumpTime = -100f;
        lastGroundedTime = -100f;
        
        float currentYVel = rb.linearVelocity.y;
        if (currentYVel < 0) currentYVel = 0;
        
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentYVel);
        rb.AddForce(direction * force, ForceMode2D.Impulse);
    }

    private void ApplyGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && Keyboard.current != null && !Keyboard.current.spaceKey.isPressed)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private void HandleWallSlide()
    {
        if (isOnWall && !isGrounded && rb.linearVelocity.y < 0 && moveInput.x != 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
        }
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        canDash = false;
        lastDashTime = Time.time;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        Vector2 dashDir = moveInput.normalized;
        if (dashDir == Vector2.zero) dashDir = new Vector2((isFacingRight ? 1 : -1), 0);

        rb.linearVelocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        rb.linearVelocity = rb.linearVelocity * 0.3f; // decelerate
        isDashing = false;
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + bottomOffset, collisionRadius);
        Gizmos.DrawWireSphere((Vector2)transform.position + rightOffset, collisionRadius);
        Gizmos.DrawWireSphere((Vector2)transform.position + leftOffset, collisionRadius);
    }
}
