using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the haunted house from a text map and answers grid questions for the monster AI
/// (which tiles are walkable, and the shortest path between two tiles).
///
/// Map legend:
///   #  wall                 .  floor
///   +  doorway (floor)      P  player start
///   M  monster start        K  key
///   E  exit door            W  monster patrol waypoint
///   B  bookshelf            F  bookshelf (alt. spot)   T  table
///
/// Row 0 is the top of the house. Tile (x, y) sits at world position (x, -y).
/// </summary>
public class Level
{
    public static readonly string[] HouseMap =
    {
        "################################",
        "#......#..........#............#",
        "#..P...#..F....F..#.....K......#",
        "#......+..........+............#",
        "#..B...#....T.....#..B......B..#",
        "#......#..........#............#",
        "###+#######+#######+####+#######",
        "#......#..........#............#",
        "#..K...#..W....W..#..W.........#",
        "#......+..........+.......T....#",
        "#...T..#....M.....#............#",
        "#......#..........######+#######",
        "#####+##..........#............#",
        "#......####+#######..W.........#",
        "#..W...+..........+.......B....#",
        "#......#....T.....#....K.......E",
        "#..B...#..W.......#............#",
        "################################",
    };

    public int Width { get; private set; }
    public int Height { get; private set; }
    public Vector2 PlayerStart { get; private set; }
    public Vector2 MonsterStart { get; private set; }
    public List<Vector2Int> Waypoints { get; } = new List<Vector2Int>();
    public List<Vector2Int> FloorCells { get; } = new List<Vector2Int>();
    public ExitDoor Exit { get; private set; }
    public int KeyCount { get; private set; }

    bool[,] walkable;

    // Three key colours so the player can tell them apart on the HUD.
    static readonly Color[] KeyColors =
    {
        new Color(1f, 0.85f, 0.25f),  // gold
        new Color(0.75f, 0.8f, 0.9f), // silver
        new Color(0.85f, 0.5f, 0.3f)  // copper
    };

    /// <summary>Creates every tile, piece of furniture, key and door as children of <paramref name="root"/>.</summary>
    public static Level Build(string[] map, Transform root, GameArt art)
    {
        Level level = new Level();
        level.Height = map.Length;
        level.Width = map[0].Length;
        level.walkable = new bool[level.Width, level.Height];

        Transform floorParent = NewChild(root, "Floor");
        Transform wallParent = NewChild(root, "Walls");
        Transform propParent = NewChild(root, "Furniture");
        Transform itemParent = NewChild(root, "Items");

        for (int y = 0; y < level.Height; y++)
        {
            for (int x = 0; x < level.Width; x++)
            {
                char c = map[y][x];
                Vector2 pos = CellToWorld(new Vector2Int(x, y));

                // Every non-wall tile gets a floor sprite underneath it.
                if (c != '#')
                    MakeSprite(floorParent, "Floor", pos, art.floor, 0);

                switch (c)
                {
                    case '#':
                        MakeSolid(wallParent, "Wall", pos, art.wall, 1);
                        break;
                    case 'B':
                    case 'F':
                        MakeSolid(propParent, "Bookshelf", pos, art.bookshelf, 2);
                        break;
                    case 'T':
                        MakeSolid(propParent, "Table", pos, art.table, 2);
                        break;
                    case 'K':
                        Color tint = KeyColors[level.KeyCount % KeyColors.Length];
                        GameObject key = MakeSprite(itemParent, "Key " + (level.KeyCount + 1), pos, art.key, 3);
                        key.GetComponent<SpriteRenderer>().color = tint;
                        CircleCollider2D keyCol = key.AddComponent<CircleCollider2D>();
                        keyCol.isTrigger = true;
                        keyCol.radius = 0.4f / key.transform.localScale.x;
                        key.AddComponent<KeyPickup>().keyColor = tint;
                        level.KeyCount++;
                        break;
                    case 'E':
                        GameObject door = MakeSprite(itemParent, "Exit Door", pos, art.exitDoor, 3);
                        door.AddComponent<BoxCollider2D>().size = Vector2.one / door.transform.localScale.x;
                        level.Exit = door.AddComponent<ExitDoor>();
                        break;
                    case 'P':
                        level.PlayerStart = pos;
                        break;
                    case 'M':
                        level.MonsterStart = pos;
                        break;
                    case 'W':
                        level.Waypoints.Add(new Vector2Int(x, y));
                        break;
                }

                // The monster can walk on floor, doorways and spawn/waypoint/key tiles.
                bool isWalkable = c == '.' || c == '+' || c == 'P' || c == 'M' || c == 'W' || c == 'K';
                level.walkable[x, y] = isWalkable;
                if (isWalkable) level.FloorCells.Add(new Vector2Int(x, y));
            }
        }
        return level;
    }

    // ---------- Grid helpers ----------

    public static Vector2 CellToWorld(Vector2Int cell) => new Vector2(cell.x, -cell.y);

    public static Vector2Int WorldToCell(Vector2 world) =>
        new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(-world.y));

    public bool IsWalkable(Vector2Int cell) =>
        cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height && walkable[cell.x, cell.y];

    static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>
    /// Breadth-first search for the shortest tile path from start to goal (4-directional).
    /// The returned list excludes the start tile. Returns an empty list if no path exists.
    /// The house is small (32x18), so running this a few times per second is cheap.
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        if (!IsWalkable(goal)) goal = NearestWalkable(goal);
        if (!IsWalkable(start)) start = NearestWalkable(start);
        if (start == goal) return path;

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        frontier.Enqueue(start);
        cameFrom[start] = start;

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            if (current == goal) break;
            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = current + dir;
                if (IsWalkable(next) && !cameFrom.ContainsKey(next))
                {
                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }
        }

        if (!cameFrom.ContainsKey(goal)) return path;

        // Walk backwards from the goal to rebuild the path.
        for (Vector2Int step = goal; step != start; step = cameFrom[step])
            path.Add(step);
        path.Reverse();
        return path;
    }

    /// <summary>Closest walkable tile to <paramref name="cell"/> (used when the player stands in a doorway edge or on the exit).</summary>
    public Vector2Int NearestWalkable(Vector2Int cell)
    {
        Vector2Int best = cell;
        float bestDist = float.MaxValue;
        foreach (Vector2Int floor in FloorCells)
        {
            float d = (floor - cell).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = floor;
            }
        }
        return best;
    }

    // ---------- Object helpers ----------

    static Transform NewChild(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject MakeSprite(Transform parent, string name, Vector2 pos, Sprite sprite, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        GameArt.ApplyMaterial(sr);
        // Downloaded sprites come in all sizes; scale each one to fill exactly one tile.
        go.transform.localScale = Vector3.one * GameArt.FitScale(sprite, 1f);
        return go;
    }

    /// <summary>A sprite with a 1x1 box collider that blocks movement and sight.</summary>
    static void MakeSolid(Transform parent, string name, Vector2 pos, Sprite sprite, int order)
    {
        GameObject go = MakeSprite(parent, name, pos, sprite, order);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = Vector2.one / go.transform.localScale.x; // exactly one tile in world space
        go.AddComponent<Obstacle>();
    }
}
