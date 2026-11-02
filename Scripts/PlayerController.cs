using UnityEngine;

/// <summary>
/// Top-down WASD movement. Walls and furniture stop the player because the player has a
/// dynamic Rigidbody2D and we move it by setting its velocity, so the physics engine
/// resolves every collision (no walking through walls).
/// Also tracks which way the player is facing (the flashlight points that way) and plays footsteps.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("Movement speed in tiles per second.")]
    public float moveSpeed = 4f;
    [Tooltip("Seconds between footstep sounds while walking.")]
    public float footstepInterval = 0.3f;

    /// <summary>Unit vector of the last direction the player moved in. Starts facing right.</summary>
    public Vector2 Facing { get; private set; } = Vector2.right;
    public bool IsMoving { get; private set; }

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Vector2 moveInput;
    float footstepTimer;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (!GameManager.IsPlaying)
        {
            moveInput = Vector2.zero;
            IsMoving = false;
            return;
        }

        moveInput = InputHelper.GetMoveInput();
        IsMoving = moveInput.sqrMagnitude > 0.01f;

        if (IsMoving)
        {
            Facing = moveInput.normalized;
            // Mirror the sprite so the character looks where it's walking.
            if (Mathf.Abs(moveInput.x) > 0.01f && spriteRenderer != null)
                spriteRenderer.flipX = moveInput.x < 0f;

            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                SoundManager.Instance?.PlayFootstep();
                footstepTimer = footstepInterval;
            }
        }
        else
        {
            footstepTimer = 0f; // first step sounds immediately when walking resumes
        }
    }

    void FixedUpdate()
    {
        Vector2 velocity = moveInput * moveSpeed;
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = velocity;   // Unity 6 renamed "velocity"
#else
        body.velocity = velocity;
#endif
    }
}
