using UnityEngine;

/// <summary>
/// Hold Space to turn on the flashlight. The beam lights up the dark house in the direction the
/// player is facing, and if it hits the monster (with no wall in the way) the monster is scared off.
///
/// The flashlight runs on a battery: it drains while on and recharges while off. If it runs
/// completely dry, it must recharge to <see cref="minChargeToRestart"/> before it works again,
/// so the player can't keep it on the whole time.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class Flashlight : MonoBehaviour
{
    [Header("Battery")]
    [Tooltip("Battery used per second while on (1 = full battery).")]
    public float drainPerSecond = 0.35f;
    [Tooltip("Battery regained per second while off.")]
    public float rechargePerSecond = 0.2f;
    [Tooltip("After the battery hits zero, it must recharge to this level before turning back on.")]
    public float minChargeToRestart = 0.35f;

    [Header("Light")]
    [Tooltip("Radius (tiles) the player can always see around themselves.")]
    public float ambientRadius = 2.6f;
    [Tooltip("How far the beam reaches, in tiles.")]
    public float beamLength = 7f;
    [Tooltip("Half of the beam's cone angle, in degrees.")]
    public float beamHalfAngle = 26f;

    /// <summary>0 (empty) to 1 (full).</summary>
    public float Battery { get; private set; } = 1f;
    public bool IsOn { get; private set; }
    /// <summary>True while the battery is dead and recharging back to the restart level.</summary>
    public bool IsDepleted { get; private set; }

    PlayerController player;
    SpriteRenderer darknessRenderer;
    Sprite darkSprite;
    Sprite beamSprite;

    const float DarknessWorldSize = 48f; // big enough to cover the screen at any rotation

    void Awake()
    {
        player = GetComponent<PlayerController>();

        // The darkness overlay is a separate object (not a child) so it doesn't flip with the sprite.
        darkSprite = SpriteFactory.Darkness(DarknessWorldSize, ambientRadius, false, beamLength, beamHalfAngle);
        beamSprite = SpriteFactory.Darkness(DarknessWorldSize, ambientRadius, true, beamLength + 1f, beamHalfAngle + 4f);

        GameObject overlay = new GameObject("Darkness");
        overlay.transform.SetParent(transform.parent, false);
        darknessRenderer = overlay.AddComponent<SpriteRenderer>();
        darknessRenderer.sprite = darkSprite;
        darknessRenderer.sortingOrder = 50; // above everything in the world
        GameArt.ApplyMaterial(darknessRenderer);
    }

    void Update()
    {
        if (!GameManager.IsPlaying)
        {
            SetOn(false);
            return;
        }

        UpdateBattery();

        if (IsOn)
            TryScareMonster();
    }

    void LateUpdate()
    {
        // Keep the darkness centred on the player and turned toward where they face.
        darknessRenderer.transform.position = transform.position;
        float angle = Mathf.Atan2(player.Facing.y, player.Facing.x) * Mathf.Rad2Deg;
        darknessRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        darknessRenderer.sprite = IsOn ? beamSprite : darkSprite;
    }

    void UpdateBattery()
    {
        bool wantsOn = InputHelper.FlashlightHeld && !IsDepleted;

        if (wantsOn)
        {
            Battery -= drainPerSecond * Time.deltaTime;
            if (Battery <= 0f)
            {
                Battery = 0f;
                IsDepleted = true; // battery died: force it off until it recharges
                wantsOn = false;
            }
        }
        else
        {
            Battery = Mathf.Min(1f, Battery + rechargePerSecond * Time.deltaTime);
            if (IsDepleted && Battery >= minChargeToRestart)
                IsDepleted = false;
        }

        SetOn(wantsOn);
    }

    void SetOn(bool on)
    {
        if (on == IsOn) return;
        IsOn = on;
        if (GameManager.IsPlaying)
            SoundManager.Instance?.PlayFlashlightClick();
    }

    /// <summary>Scares the monster if it is inside the beam's cone with a clear line of sight.</summary>
    void TryScareMonster()
    {
        MonsterAI monster = GameManager.Instance.Monster;
        if (monster == null) return;

        Vector2 toMonster = (Vector2)monster.transform.position - (Vector2)transform.position;
        float distance = toMonster.magnitude;
        if (distance > beamLength) return;

        // Very close counts even if not perfectly aimed; otherwise the monster must be in the cone.
        bool inCone = distance < 1f || Vector2.Angle(player.Facing, toMonster) <= beamHalfAngle;
        if (inCone && GameManager.HasLineOfSight(transform.position, monster.transform.position))
            monster.Scare();
    }

    void OnDestroy()
    {
        if (darknessRenderer != null) Destroy(darknessRenderer.gameObject);
    }
}
