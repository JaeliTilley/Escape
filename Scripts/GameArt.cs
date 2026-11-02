using UnityEngine;

/// <summary>
/// Every sprite the game uses, shown on the GameManager in the Inspector.
/// Drag a downloaded sprite (e.g. from Kenney.nl or itch.io) into any slot to replace
/// the generated placeholder. Empty slots fall back to SpriteFactory art.
/// Sprites of any size are scaled automatically to fit one tile.
/// </summary>
[System.Serializable]
public class GameArt
{
    public Sprite floor;
    public Sprite wall;
    public Sprite bookshelf;
    public Sprite table;
    public Sprite player;
    public Sprite monster;
    public Sprite key;
    public Sprite exitDoor;

    /// <summary>Fills every empty slot with a generated placeholder sprite.</summary>
    public void FillMissing()
    {
        if (floor == null) floor = SpriteFactory.Floor();
        if (wall == null) wall = SpriteFactory.Wall();
        if (bookshelf == null) bookshelf = SpriteFactory.Bookshelf();
        if (table == null) table = SpriteFactory.Table();
        if (player == null) player = SpriteFactory.Player();
        if (monster == null) monster = SpriteFactory.Monster();
        if (key == null) key = SpriteFactory.Key();
        if (exitDoor == null) exitDoor = SpriteFactory.ExitDoor();
    }

    static Material unlitSpriteMaterial;
    static bool materialSearched;

    /// <summary>
    /// Gives a SpriteRenderer an unlit sprite material. In URP 2D projects new sprites default to a
    /// *lit* material that renders pitch black without a 2D Light in the scene; the game does its
    /// own darkness, so unlit sprites are what we want. Falls back to the project default if neither
    /// shader is found.
    /// </summary>
    public static void ApplyMaterial(SpriteRenderer renderer)
    {
        if (!materialSearched)
        {
            materialSearched = true;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) unlitSpriteMaterial = new Material(shader);
        }
        if (unlitSpriteMaterial != null) renderer.sharedMaterial = unlitSpriteMaterial;
    }

    /// <summary>Uniform scale that makes <paramref name="sprite"/>'s larger side equal <paramref name="worldSize"/> units.</summary>
    public static float FitScale(Sprite sprite, float worldSize)
    {
        if (sprite == null) return 1f;
        Vector3 size = sprite.bounds.size;
        float largest = Mathf.Max(size.x, size.y);
        return largest > 0.0001f ? worldSize / largest : 1f;
    }
}
