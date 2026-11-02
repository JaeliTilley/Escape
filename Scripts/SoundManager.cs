using UnityEngine;

/// <summary>
/// Plays every sound in the game.
///
/// There is intentionally no looping background music. The only "music" is a heartbeat
/// that is tied to gameplay: it gets faster and louder the closer the monster is to the
/// player, and goes quiet when the monster is far away.
///
/// Any clip slot left empty in the Inspector is filled with a synthesized placeholder
/// from SoundFactory, so you can swap in downloaded sounds one at a time.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Optional clip overrides (leave empty to use generated sounds)")]
    public AudioClip footstep;
    public AudioClip keyPickup;
    public AudioClip doorUnlock;
    public AudioClip monsterGrowl;
    public AudioClip monsterShriek;
    public AudioClip flashlightClick;
    public AudioClip heartbeat;
    public AudioClip gameOver;
    public AudioClip victory;

    [Header("Heartbeat (gameplay-driven tension)")]
    [Tooltip("Monster distance (in tiles) at which the heartbeat is fastest and loudest.")]
    public float heartbeatNearDistance = 2f;
    [Tooltip("Monster distance (in tiles) beyond which the heartbeat is silent.")]
    public float heartbeatFarDistance = 13f;

    AudioSource sfxSource;
    AudioSource heartbeatSource;
    AudioSource footstepSource;
    float heartbeatTimer;

    void Awake()
    {
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        heartbeatSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource.playOnAwake = false;

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;

        if (footstep == null) footstep = SoundFactory.Footstep();
        if (keyPickup == null) keyPickup = SoundFactory.KeyPickup();
        if (doorUnlock == null) doorUnlock = SoundFactory.DoorUnlock();
        if (monsterGrowl == null) monsterGrowl = SoundFactory.MonsterGrowl();
        if (monsterShriek == null) monsterShriek = SoundFactory.MonsterShriek();
        if (flashlightClick == null) flashlightClick = SoundFactory.Click();
        if (heartbeat == null) heartbeat = SoundFactory.Heartbeat();
        if (gameOver == null) gameOver = SoundFactory.GameOver();
        if (victory == null) victory = SoundFactory.Victory();
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing || gm.Player == null || gm.Monster == null)
            return;

        // 0 when the monster is far away, 1 when it is right on top of the player.
        float distance = Vector2.Distance(gm.Player.transform.position, gm.Monster.transform.position);
        float danger = 1f - Mathf.InverseLerp(heartbeatNearDistance, heartbeatFarDistance, distance);
        if (gm.Monster.IsChasing) danger = Mathf.Max(danger, 0.6f);
        if (danger <= 0.02f) return;

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer <= 0f)
        {
            heartbeatSource.pitch = Mathf.Lerp(0.9f, 1.15f, danger);
            heartbeatSource.PlayOneShot(heartbeat, Mathf.Lerp(0.15f, 1f, danger));
            heartbeatTimer = Mathf.Lerp(1.4f, 0.38f, danger); // seconds between beats
        }
    }

    public void PlayFootstep()
    {
        // Separate source so the random pitch doesn't affect other sounds.
        footstepSource.pitch = Random.Range(0.85f, 1.15f);
        footstepSource.PlayOneShot(footstep, 0.35f);
    }

    public void PlayKey() => Play(keyPickup, 0.8f);
    public void PlayDoor() => Play(doorUnlock, 0.9f);
    public void PlayFlashlightClick() => Play(flashlightClick, 0.5f);
    public void PlayShriek() => Play(monsterShriek, 0.8f);
    public void PlayGameOver() => Play(gameOver, 1f);
    public void PlayVictory() => Play(victory, 1f);

    /// <summary>Growl volume falls off with distance so the player can tell how close it is.</summary>
    public void PlayGrowl(float distanceToPlayer)
    {
        float volume = Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(2f, 12f, distanceToPlayer));
        Play(monsterGrowl, volume);
    }

    void Play(AudioClip clip, float volume)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume);
    }
}
