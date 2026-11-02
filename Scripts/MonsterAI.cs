using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shadow monster. It is a small state machine:
///
///   Patrol -> wanders between waypoints in different rooms.
///   Chase  -> the player is close AND visible (no wall/furniture in between), or close enough
///             to be heard walking. Paths straight to the player's tile.
///   Search -> lost sight of the player for a few seconds; walks to where it last saw them.
///   Flee   -> hit by the flashlight beam; runs to the spot farthest from the player.
///
/// Pathfinding is breadth-first search on the house grid (see Level.FindPath), so it goes
/// around walls and through doorways instead of getting stuck against them.
/// It gets a little faster each time the player picks up a key.
/// </summary>
public class MonsterAI : MonoBehaviour
{
    public enum MonsterState { Patrol, Chase, Search, Flee }

    [Header("Speed (tiles per second)")]
    public float patrolSpeed = 1.8f;
    public float chaseSpeed = 3.0f;
    public float fleeSpeed = 4.2f;
    [Tooltip("Extra chase speed added for every key the player collects.")]
    public float speedBonusPerKey = 0.25f;

    [Header("Senses")]
    [Tooltip("How far (tiles) the monster can see the player, if nothing blocks its view.")]
    public float sightRange = 6.5f;
    [Tooltip("How close (tiles) the player must be for the monster to hear them walking through walls.")]
    public float hearingRange = 2.2f;
    [Tooltip("Seconds without seeing the player before the monster gives up the chase.")]
    public float loseSightTime = 2.5f;

    [Header("Flashlight reaction")]
    [Tooltip("How long the monster runs away after being hit by the beam.")]
    public float fleeDuration = 3f;

    public MonsterState State { get; private set; } = MonsterState.Patrol;
    public bool IsChasing => State == MonsterState.Chase;
    public bool IsFleeing => State == MonsterState.Flee;

    Level level;
    Transform player;
    PlayerController playerController;
    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    SpriteRenderer[] allRenderers;

    List<Vector2Int> path = new List<Vector2Int>();
    Vector2 lastKnownPlayerPos;
    float timeSinceSeen;
    float repathTimer;
    float fleeTimer;
    float waitTimer;
    float growlCooldown;
    float ambientGrowlTimer;
    int keysCollected;

    /// <summary>Called by GameManager right after the monster is spawned.</summary>
    public void Init(Level houseLevel, PlayerController target)
    {
        level = houseLevel;
        playerController = target;
        player = target.transform;
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        allRenderers = GetComponentsInChildren<SpriteRenderer>();
        ambientGrowlTimer = Random.Range(6f, 10f);
        PickPatrolGoal();
    }

    /// <summary>GameManager tells the monster about each key so it can get more aggressive.</summary>
    public void OnKeyCollected(int totalKeys) => keysCollected = totalKeys;

    void Update()
    {
        if (!GameManager.IsPlaying || level == null) return;

        growlCooldown -= Time.deltaTime;
        UpdateSenses();
        UpdateState();
        UpdateLooks();
        AmbientGrowl();
    }

    void FixedUpdate()
    {
        if (!GameManager.IsPlaying || level == null) return;
        FollowPath();
    }

    // ---------- Senses ----------

    void UpdateSenses()
    {
        if (State == MonsterState.Flee) return; // too scared to notice anything

        float distance = Vector2.Distance(transform.position, player.position);
        bool canSee = distance <= sightRange &&
                      GameManager.HasLineOfSight(transform.position, player.position);
        bool canHear = distance <= hearingRange && playerController.IsMoving;

        if (canSee || canHear)
        {
            lastKnownPlayerPos = player.position;
            timeSinceSeen = 0f;
            if (State != MonsterState.Chase) StartChase(distance);
        }
        else
        {
            timeSinceSeen += Time.deltaTime;
        }
    }

    void StartChase(float distance)
    {
        State = MonsterState.Chase;
        repathTimer = 0f;
        if (growlCooldown <= 0f)
        {
            SoundManager.Instance?.PlayGrowl(distance);
            growlCooldown = 4f;
        }
    }

    // ---------- State machine ----------

    void UpdateState()
    {
        switch (State)
        {
            case MonsterState.Chase:
                if (timeSinceSeen > loseSightTime)
                {
                    // Lost them: go check the last place they were seen.
                    State = MonsterState.Search;
                    SetGoal(Level.WorldToCell(lastKnownPlayerPos));
                    break;
                }
                // Re-plan a few times per second because the player keeps moving.
                repathTimer -= Time.deltaTime;
                if (repathTimer <= 0f)
                {
                    SetGoal(Level.WorldToCell(player.position));
                    repathTimer = 0.2f;
                }
                break;

            case MonsterState.Search:
                if (path.Count == 0)
                {
                    State = MonsterState.Patrol;
                    waitTimer = 1f; // pause and "look around" before patrolling again
                    PickPatrolGoal();
                }
                break;

            case MonsterState.Patrol:
                if (path.Count == 0)
                {
                    waitTimer -= Time.deltaTime;
                    if (waitTimer <= 0f)
                    {
                        PickPatrolGoal();
                        waitTimer = Random.Range(0.4f, 1.2f);
                    }
                }
                break;

            case MonsterState.Flee:
                fleeTimer -= Time.deltaTime;
                if (fleeTimer <= 0f)
                {
                    State = MonsterState.Patrol;
                    timeSinceSeen = loseSightTime; // don't instantly re-chase
                    PickPatrolGoal();
                }
                break;
        }
    }

    /// <summary>Called by the Flashlight when the beam hits the monster.</summary>
    public void Scare()
    {
        if (State == MonsterState.Flee)
        {
            fleeTimer = fleeDuration; // keep it running while the beam stays on it
            return;
        }

        State = MonsterState.Flee;
        fleeTimer = fleeDuration;
        SoundManager.Instance?.PlayShriek();
        GameManager.Instance.AddScore(100, "Scared it off! +100");
        SetGoal(FarthestCellFromPlayer());
    }

    void PickPatrolGoal()
    {
        // Mostly walk between hand-placed waypoints; sometimes wander to a random spot
        // so the patrol route isn't perfectly predictable.
        Vector2Int goal;
        if (level.Waypoints.Count > 0 && Random.value < 0.75f)
            goal = level.Waypoints[Random.Range(0, level.Waypoints.Count)];
        else
            goal = level.FloorCells[Random.Range(0, level.FloorCells.Count)];

        if (goal == Level.WorldToCell(transform.position) && level.Waypoints.Count > 1)
            goal = level.Waypoints[(level.Waypoints.IndexOf(goal) + 1) % level.Waypoints.Count];

        SetGoal(goal);
    }

    /// <summary>Picks a reachable spot that is far from the player (checks a sample of tiles to stay cheap).</summary>
    Vector2Int FarthestCellFromPlayer()
    {
        Vector2Int best = Level.WorldToCell(transform.position);
        float bestDist = -1f;
        List<Vector2Int> candidates = new List<Vector2Int>(level.Waypoints);
        for (int i = 0; i < 12; i++)
            candidates.Add(level.FloorCells[Random.Range(0, level.FloorCells.Count)]);

        foreach (Vector2Int cell in candidates)
        {
            float d = Vector2.Distance(Level.CellToWorld(cell), player.position);
            if (d > bestDist)
            {
                bestDist = d;
                best = cell;
            }
        }
        return best;
    }

    void SetGoal(Vector2Int goal)
    {
        // If we're between two tiles, finish stepping onto the tile we're heading for first.
        // Planning from there means the monster never cuts diagonally through a wall corner
        // and never jitters backwards when it re-plans mid-step.
        if (path.Count > 0)
        {
            Vector2Int stepTarget = path[0];
            path = level.FindPath(stepTarget, goal);
            path.Insert(0, stepTarget);
        }
        else
        {
            path = level.FindPath(Level.WorldToCell(transform.position), goal);
        }
    }

    // ---------- Movement ----------

    float CurrentSpeed()
    {
        switch (State)
        {
            case MonsterState.Chase: return chaseSpeed + speedBonusPerKey * keysCollected;
            case MonsterState.Search: return (patrolSpeed + chaseSpeed) * 0.5f;
            case MonsterState.Flee: return fleeSpeed;
            default: return patrolSpeed;
        }
    }

    void FollowPath()
    {
        Vector2 position = body.position;
        float step = CurrentSpeed() * Time.fixedDeltaTime;

        Vector2 target;
        if (path.Count > 0)
        {
            target = Level.CellToWorld(path[0]);
        }
        else if (State == MonsterState.Chase)
        {
            // Same tile as the player: lunge straight at them.
            target = player.position;
        }
        else
        {
            return; // nowhere to go right now
        }

        Vector2 next = Vector2.MoveTowards(position, target, step);
        body.MovePosition(next);

        if (path.Count > 0 && Vector2.Distance(next, target) < 0.02f)
            path.RemoveAt(0);

        // Face the direction of travel.
        if (allRenderers != null && Mathf.Abs(target.x - position.x) > 0.01f)
            foreach (SpriteRenderer r in allRenderers) r.flipX = target.x < position.x;
    }

    // ---------- Feedback ----------

    void UpdateLooks()
    {
        if (spriteRenderer == null) return;

        // Glowing eyes (if present) dim while the monster is scared.
        for (int i = 1; i < allRenderers.Length; i++)
            allRenderers[i].color = new Color(1f, 1f, 1f, State == MonsterState.Flee ? 0.3f : 1f);

        if (State == MonsterState.Flee)
        {
            // Flicker and fade while scared.
            float a = 0.35f + 0.25f * Mathf.Sin(Time.time * 30f);
            spriteRenderer.color = new Color(0.8f, 0.8f, 1f, a);
        }
        else if (State == MonsterState.Chase)
        {
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 12f);
            spriteRenderer.color = new Color(1f, pulse * 0.6f, pulse * 0.6f, 1f);
        }
        else
        {
            spriteRenderer.color = Color.white;
        }
    }

    /// <summary>Occasional distant growl so the player has an audio hint of where it is.</summary>
    void AmbientGrowl()
    {
        if (State != MonsterState.Patrol) return;
        ambientGrowlTimer -= Time.deltaTime;
        if (ambientGrowlTimer <= 0f)
        {
            SoundManager.Instance?.PlayGrowl(Vector2.Distance(transform.position, player.position));
            ambientGrowlTimer = Random.Range(8f, 14f);
        }
    }

    // ---------- Catching the player ----------

    void OnTriggerEnter2D(Collider2D other) => TryCatch(other);
    void OnTriggerStay2D(Collider2D other) => TryCatch(other);

    void TryCatch(Collider2D other)
    {
        if (!GameManager.IsPlaying || State == MonsterState.Flee) return;
        if (other.GetComponent<PlayerController>() != null)
            GameManager.Instance.PlayerCaught();
    }
}
