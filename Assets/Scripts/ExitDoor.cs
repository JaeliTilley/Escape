using UnityEngine;

/// <summary>
/// The way out. While locked it is solid and tells the player how many keys are still missing.
/// Once all keys are collected it unlocks (becomes a trigger) and walking into it wins the game.
/// </summary>
public class ExitDoor : MonoBehaviour
{
    public bool IsUnlocked { get; private set; }

    BoxCollider2D box;
    SpriteRenderer spriteRenderer;

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
        if (box == null) box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        SoundManager.Instance?.PlayDoor();
    }

    void Update()
    {
        // Unlocked door glows green and pulses so the player can find it.
        if (IsUnlocked && spriteRenderer != null)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
            spriteRenderer.color = new Color(0.5f * pulse, 1f * pulse, 0.55f * pulse);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsUnlocked && GameManager.IsPlaying &&
            collision.collider.GetComponent<PlayerController>() != null)
        {
            int missing = GameManager.Instance.KeysNeeded - GameManager.Instance.KeysCollected;
            GameManager.Instance.ShowMessage("Locked. Find " + missing + " more key" + (missing == 1 ? "." : "s."));
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsUnlocked && GameManager.IsPlaying && other.GetComponent<PlayerController>() != null)
            GameManager.Instance.PlayerEscaped();
    }
}
