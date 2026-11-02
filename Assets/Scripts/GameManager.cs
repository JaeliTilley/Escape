using UnityEngine;

public enum GameState { Title, Playing, Paused, GameOver, Won }

/// <summary>
/// Runs the whole game. Put this on ONE empty GameObject in an otherwise empty scene and press Play.
///
/// It builds the haunted house, spawns the player and monster, follows the player with the camera,
/// tracks keys and score, handles Enter (start / pause / restart), and draws the HUD and menus.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Shortcut every gameplay script checks before doing anything.</summary>
    public static bool IsPlaying => Instance != null && Instance.State == GameState.Playing;

    [Header("Art Overrides (optional: drag downloaded sprites here)")]
    public GameArt art = new GameArt();

    [Tooltip("Draw the monster's red eyes on top of the darkness so you can spot it in the dark.")]
    public bool showMonsterEyesInDark = true;

    [Header("Camera")]
    public float cameraSize = 6.5f;
    public float cameraFollowSpeed = 8f;

    [Header("Scoring")]
    public int pointsPerKey = 500;
    [Tooltip("Time bonus starts here and drops by timeBonusLossPerSecond every second.")]
    public int maxTimeBonus = 2000;
    public int timeBonusLossPerSecond = 10;
    [Tooltip("Bonus for escaping with a charged flashlight (scaled by battery level).")]
    public int maxBatteryBonus = 200;

    public GameState State { get; private set; } = GameState.Title;
    public PlayerController Player { get; private set; }
    public MonsterAI Monster { get; private set; }
    public Flashlight PlayerFlashlight { get; private set; }
    public int KeysCollected { get; private set; }
    public int KeysNeeded { get; private set; }
    public int Score { get; private set; }
    public float ElapsedTime { get; private set; }

    Level level;
    Transform houseRoot;
    Camera cam;
    Color[] collectedKeyColors = new Color[0];

    // End-of-game breakdown shown on the win screen.
    int finalBaseScore, finalTimeBonus, finalBatteryBonus, highScore;
    bool newHighScore;

    // On-screen message (e.g. "Key found!").
    string message = "";
    float messageTimer;

    float shakeTimer;
    const string HighScoreKey = "Escape_HighScore";

    // ---------- Setup ----------

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        art.FillMissing();
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);

        if (GetComponent<SoundManager>() == null) gameObject.AddComponent<SoundManager>();
        SetUpCamera();
        BuildWorld();
        State = GameState.Title;
    }

    void SetUpCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
        }
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

        cam.orthographic = true;
        cam.orthographicSize = cameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    /// <summary>(Re)creates the house, player and monster from scratch. Used for the first load and every restart.</summary>
    void BuildWorld()
    {
        if (houseRoot != null) Destroy(houseRoot.gameObject);
        houseRoot = new GameObject("House").transform;

        level = Level.Build(Level.HouseMap, houseRoot, art);
        KeysNeeded = level.KeyCount;
        KeysCollected = 0;
        collectedKeyColors = new Color[0];
        Score = 0;
        ElapsedTime = 0f;
        message = "";

        Player = SpawnPlayer(level.PlayerStart);
        PlayerFlashlight = Player.GetComponent<Flashlight>();
        Monster = SpawnMonster(level.MonsterStart);

        cam.transform.position = new Vector3(level.PlayerStart.x, level.PlayerStart.y, -10f);
    }

    PlayerController SpawnPlayer(Vector2 position)
    {
        GameObject go = new GameObject("Player");
        go.transform.SetParent(houseRoot, false);
        go.transform.position = position;
        AddSprite(go.transform, art.player, 0.85f, 10);

        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Slightly smaller than a tile so the player slides through doorways easily.
        go.AddComponent<CircleCollider2D>().radius = 0.36f;

        PlayerController controller = go.AddComponent<PlayerController>();
        go.AddComponent<Flashlight>();
        return controller;
    }

    MonsterAI SpawnMonster(Vector2 position)
    {
        GameObject go = new GameObject("Monster");
        go.transform.SetParent(houseRoot, false);
        go.transform.position = position;
        AddSprite(go.transform, art.monster, 1f, 11);
        if (showMonsterEyesInDark)
            AddSprite(go.transform, SpriteFactory.MonsterEyes(), 1f, 60); // above the darkness (50)

        // Kinematic: the monster moves along its own path and only needs to *detect* the player.
        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.38f;

        MonsterAI ai = go.AddComponent<MonsterAI>();
        ai.Init(level, Player);
        return ai;
    }

    /// <summary>Puts the sprite on a child object so scaling the art never changes the physics collider.</summary>
    static void AddSprite(Transform parent, Sprite sprite, float worldSize, int order)
    {
        GameObject visual = new GameObject("Sprite");
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = Vector3.one * GameArt.FitScale(sprite, worldSize);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        GameArt.ApplyMaterial(sr);
    }

    // ---------- Main loop ----------

    void Update()
    {
        if (InputHelper.EnterPressed) HandleEnter();

        if (State == GameState.Playing)
            ElapsedTime += Time.deltaTime;

        if (messageTimer > 0f) messageTimer -= Time.unscaledDeltaTime;
        if (shakeTimer > 0f) shakeTimer -= Time.unscaledDeltaTime;
    }

    /// <summary>Enter starts the game, pauses/unpauses it, and restarts after a win or loss.</summary>
    void HandleEnter()
    {
        switch (State)
        {
            case GameState.Title:
                State = GameState.Playing;
                ShowMessage("Find the 3 keys. Don't let it catch you.");
                break;
            case GameState.Playing:
                State = GameState.Paused;
                Time.timeScale = 0f;
                break;
            case GameState.Paused:
                State = GameState.Playing;
                Time.timeScale = 1f;
                break;
            case GameState.GameOver:
            case GameState.Won:
                Time.timeScale = 1f;
                BuildWorld();
                State = GameState.Playing;
                ShowMessage("Find the 3 keys. Don't let it catch you.");
                break;
        }
    }

    void LateUpdate()
    {
        if (Player == null || cam == null) return;

        // Smoothly follow the player.
        Vector3 target = new Vector3(Player.transform.position.x, Player.transform.position.y, -10f);
        Vector3 pos = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-cameraFollowSpeed * Time.unscaledDeltaTime));

        // Screen shake when caught.
        if (shakeTimer > 0f) pos += (Vector3)(Random.insideUnitCircle * 0.25f * shakeTimer);

        cam.transform.position = pos;
    }

    // ---------- Gameplay events (called by other scripts) ----------

    public void CollectKey(Color keyColor)
    {
        KeysCollected++;
        System.Array.Resize(ref collectedKeyColors, KeysCollected);
        collectedKeyColors[KeysCollected - 1] = keyColor;

        AddScore(pointsPerKey, null);
        SoundManager.Instance?.PlayKey();
        Monster.OnKeyCollected(KeysCollected);

        if (KeysCollected >= KeysNeeded)
        {
            level.Exit.Unlock();
            ShowMessage("All keys found! The exit door is open. Get out!");
        }
        else
        {
            ShowMessage("Key found! (" + KeysCollected + "/" + KeysNeeded + ")  It's getting faster...");
        }
    }

    public void AddScore(int points, string popup)
    {
        Score += points;
        if (!string.IsNullOrEmpty(popup)) ShowMessage(popup);
    }

    public void PlayerCaught()
    {
        if (State != GameState.Playing) return;
        State = GameState.GameOver;
        shakeTimer = 0.6f;
        SoundManager.Instance?.PlayGameOver();
    }

    public void PlayerEscaped()
    {
        if (State != GameState.Playing) return;
        State = GameState.Won;

        finalBaseScore = Score; // keys + flashlight scares
        finalTimeBonus = Mathf.Max(0, maxTimeBonus - Mathf.FloorToInt(ElapsedTime) * timeBonusLossPerSecond);
        finalBatteryBonus = Mathf.RoundToInt(PlayerFlashlight.Battery * maxBatteryBonus);
        Score += finalTimeBonus + finalBatteryBonus;

        newHighScore = Score > highScore;
        if (newHighScore)
        {
            highScore = Score;
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
        }
        SoundManager.Instance?.PlayVictory();
    }

    public void ShowMessage(string text)
    {
        message = text;
        messageTimer = 3f;
    }

    /// <summary>
    /// True if nothing solid (wall or furniture) is between two points.
    /// Used by the monster to see the player and by the flashlight to hit the monster.
    /// </summary>
    public static bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && hit.collider.GetComponent<Obstacle>() != null)
                return false;
        }
        return true;
    }

    // ---------- HUD and menus (IMGUI: no Canvas or UI package setup needed) ----------

    GUIStyle titleStyle, headingStyle, bodyStyle, hudStyle, messageStyle, smallStyle;
    Texture2D whiteTex;
    const float RefHeight = 1080f;

    void OnGUI()
    {
        if (titleStyle == null) CreateStyles();

        // Scale everything from a 1080p layout so it reads the same at any resolution.
        float scale = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        float w = Screen.width / scale;
        float h = RefHeight;

        switch (State)
        {
            case GameState.Title: DrawTitle(w, h); break;
            case GameState.Playing: DrawHud(w, h); break;
            case GameState.Paused: DrawHud(w, h); DrawPaused(w, h); break;
            case GameState.GameOver: DrawGameOver(w, h); break;
            case GameState.Won: DrawWon(w, h); break;
        }
    }

    void CreateStyles()
    {
        whiteTex = SpriteFactory.WhitePixel();

        titleStyle = MakeStyle(110, FontStyle.Bold, new Color(0.85f, 0.1f, 0.1f), TextAnchor.MiddleCenter);
        headingStyle = MakeStyle(54, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        bodyStyle = MakeStyle(30, FontStyle.Normal, new Color(0.88f, 0.88f, 0.9f), TextAnchor.MiddleCenter);
        hudStyle = MakeStyle(30, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        messageStyle = MakeStyle(34, FontStyle.Bold, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter);
        smallStyle = MakeStyle(24, FontStyle.Normal, new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleCenter);
    }

    static GUIStyle MakeStyle(int size, FontStyle fontStyle, Color color, TextAnchor anchor)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = fontStyle,
            alignment = anchor,
            wordWrap = true,
            richText = true
        };
        style.normal.textColor = color;
        return style;
    }

    void DrawHud(float w, float h)
    {
        // Top-left panel: keys, score, time.
        DrawRect(new Rect(20, 20, 380, 170), new Color(0f, 0f, 0f, 0.55f));
        GUI.Label(new Rect(40, 30, 200, 44), "Keys", hudStyle);
        for (int i = 0; i < KeysNeeded; i++)
        {
            Rect slot = new Rect(140 + i * 54, 36, 40, 32);
            DrawRect(slot, new Color(1f, 1f, 1f, 0.15f));
            if (i < collectedKeyColors.Length)
                DrawRect(new Rect(slot.x + 4, slot.y + 4, slot.width - 8, slot.height - 8), collectedKeyColors[i]);
        }
        GUI.Label(new Rect(40, 80, 340, 44), "Score  " + Score, hudStyle);
        GUI.Label(new Rect(40, 128, 340, 44), "Time   " + FormatTime(ElapsedTime), hudStyle);

        // Bottom-left: flashlight battery bar.
        if (PlayerFlashlight != null)
        {
            float battery = PlayerFlashlight.Battery;
            Rect back = new Rect(20, h - 100, 420, 80);
            DrawRect(back, new Color(0f, 0f, 0f, 0.55f));
            string label = PlayerFlashlight.IsDepleted ? "Flashlight  <color=#ff6655>RECHARGING</color>" : "Flashlight  [SPACE]";
            GUI.Label(new Rect(40, h - 98, 380, 36), label, hudStyle);
            Rect bar = new Rect(40, h - 56, 380, 22);
            DrawRect(bar, new Color(1f, 1f, 1f, 0.15f));
            Color barColor = battery > 0.5f ? new Color(0.4f, 0.9f, 0.4f)
                           : battery > 0.2f ? new Color(0.95f, 0.8f, 0.3f)
                           : new Color(0.95f, 0.3f, 0.25f);
            DrawRect(new Rect(bar.x, bar.y, bar.width * battery, bar.height), barColor);
        }

        // Top-right: warning while being chased.
        if (Monster != null && Monster.IsChasing)
        {
            float flash = 0.6f + 0.4f * Mathf.Sin(Time.time * 10f);
            GUIStyle warn = new GUIStyle(headingStyle);
            warn.normal.textColor = new Color(1f, 0.15f, 0.1f, flash);
            warn.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(w - 520, 20, 500, 70), "IT SEES YOU", warn);
        }

        // Centre-top message that fades out.
        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
        {
            Color c = messageStyle.normal.textColor;
            GUIStyle faded = new GUIStyle(messageStyle);
            faded.normal.textColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(messageTimer));
            DrawRect(new Rect(w / 2 - 520, 110, 1040, 60), new Color(0f, 0f, 0f, 0.5f * Mathf.Clamp01(messageTimer)));
            GUI.Label(new Rect(w / 2 - 520, 110, 1040, 60), message, faded);
        }
    }

    void DrawTitle(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.82f));
        float cx = w / 2f;
        GUI.Label(new Rect(cx - 600, 110, 1200, 150), "ESCAPE", titleStyle);
        GUI.Label(new Rect(cx - 600, 250, 1200, 60),
            "You're trapped in a haunted house. Find all 3 keys and reach the exit, but don't get caught.", bodyStyle);

        DrawRect(new Rect(cx - 330, 350, 660, 380), new Color(1f, 1f, 1f, 0.06f));
        GUI.Label(new Rect(cx - 300, 365, 600, 60), "CONTROLS", headingStyle);
        string controls =
            "<b>W A S D</b>   Move\n" +
            "<b>SPACE</b> (hold)   Flashlight: scares the monster away.\n" +
            "The battery drains, so use it wisely.\n" +
            "<b>ENTER</b>   Start / Pause / Restart\n\n" +
            "Hide behind walls and furniture to break its line of sight.\n" +
            "Listen for your heartbeat: it speeds up when the monster is near.";
        GUI.Label(new Rect(cx - 310, 425, 620, 300), controls, bodyStyle);

        float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        GUIStyle prompt = new GUIStyle(headingStyle);
        prompt.normal.textColor = new Color(1f, 1f, 1f, 0.4f + 0.6f * blink);
        GUI.Label(new Rect(cx - 400, 780, 800, 70), "Press ENTER to start", prompt);

        if (highScore > 0)
            GUI.Label(new Rect(cx - 300, 860, 600, 40), "Best score: " + highScore, smallStyle);
    }

    void DrawPaused(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.6f));
        GUI.Label(new Rect(w / 2 - 400, h / 2 - 80, 800, 80), "PAUSED", headingStyle);
        GUI.Label(new Rect(w / 2 - 400, h / 2 + 10, 800, 50), "Press ENTER to resume", bodyStyle);
    }

    void DrawGameOver(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0.25f, 0f, 0f, 0.8f));
        float cx = w / 2f;
        GUI.Label(new Rect(cx - 600, h / 2 - 220, 1200, 150), "YOU WERE CAUGHT", titleStyle);
        GUI.Label(new Rect(cx - 400, h / 2 - 50, 800, 50),
            "Keys found: " + KeysCollected + "/" + KeysNeeded + "     Score: " + Score, bodyStyle);
        GUI.Label(new Rect(cx - 400, h / 2 + 60, 800, 70), "Press ENTER to try again", headingStyle);
    }

    void DrawWon(float w, float h)
    {
        DrawRect(new Rect(0, 0, w, h), new Color(0f, 0.08f, 0.04f, 0.85f));
        float cx = w / 2f;
        GUIStyle winTitle = new GUIStyle(titleStyle);
        winTitle.normal.textColor = new Color(0.45f, 1f, 0.55f);
        GUI.Label(new Rect(cx - 600, 140, 1200, 150), "YOU ESCAPED!", winTitle);

        string breakdown =
            "Keys and scares:  " + finalBaseScore + "\n" +
            "Time bonus (" + FormatTime(ElapsedTime) + "):  +" + finalTimeBonus + "\n" +
            "Battery bonus:  +" + finalBatteryBonus;
        GUI.Label(new Rect(cx - 400, 320, 800, 160), breakdown, bodyStyle);
        GUI.Label(new Rect(cx - 400, 500, 800, 80), "FINAL SCORE: " + Score, headingStyle);
        GUI.Label(new Rect(cx - 400, 580, 800, 50),
            newHighScore ? "<color=#ffd84d>NEW BEST SCORE!</color>" : "Best: " + highScore, bodyStyle);
        GUI.Label(new Rect(cx - 400, 700, 800, 70), "Press ENTER to play again", headingStyle);
    }

    void DrawRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = old;
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}
