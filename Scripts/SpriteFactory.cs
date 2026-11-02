using UnityEngine;

/// <summary>
/// Draws the game's placeholder pixel art in code, so the project runs with zero imported assets.
/// Every sprite here can be replaced by dragging a real sprite into the matching slot
/// on the GameManager in the Inspector (see the "Art Overrides" section there).
/// </summary>
public static class SpriteFactory
{
    const int PixelsPerUnit = 16;

    // ---------- Tiles ----------

    /// <summary>Dark wooden floorboards with a little random grain.</summary>
    public static Sprite Floor()
    {
        Texture2D tex = NewTexture(16, 16);
        Color baseColor = new Color(0.23f, 0.16f, 0.11f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float grain = Random.Range(-0.025f, 0.025f);
                Color c = baseColor * (1f + grain * 4f);
                // Plank seams every 4 pixels, with a staggered end joint.
                if (y % 4 == 0) c *= 0.7f;
                if ((y / 4) % 2 == 0 && x == 11) c *= 0.75f;
                if ((y / 4) % 2 == 1 && x == 4) c *= 0.75f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return Finish(tex);
    }

    /// <summary>Grey stone bricks.</summary>
    public static Sprite Wall()
    {
        Texture2D tex = NewTexture(16, 16);
        Color stone = new Color(0.32f, 0.31f, 0.36f);
        Color mortar = new Color(0.14f, 0.13f, 0.16f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int row = y / 4;
                int offset = (row % 2 == 0) ? 0 : 4;
                bool isMortar = (y % 4 == 3) || ((x + offset) % 8 == 7);
                Color c = isMortar ? mortar : stone * Random.Range(0.85f, 1.1f);
                // Darker top edge gives the walls a bit of depth.
                if (y >= 14 && !isMortar) c *= 1.15f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }
        return Finish(tex);
    }

    /// <summary>Tall bookshelf seen from above.</summary>
    public static Sprite Bookshelf()
    {
        Texture2D tex = NewTexture(16, 16, Color.clear);
        Color wood = new Color(0.36f, 0.22f, 0.12f);
        Color[] books =
        {
            new Color(0.55f, 0.15f, 0.15f), new Color(0.2f, 0.3f, 0.5f),
            new Color(0.25f, 0.45f, 0.25f), new Color(0.6f, 0.5f, 0.2f)
        };
        FillRect(tex, 1, 1, 14, 14, wood);
        for (int shelf = 0; shelf < 3; shelf++)
        {
            int y0 = 3 + shelf * 4;
            for (int x = 2; x < 14; x++)
            {
                Color book = books[(x + shelf) % books.Length] * Random.Range(0.8f, 1.1f);
                book.a = 1f;
                for (int y = y0; y < y0 + 3; y++) tex.SetPixel(x, y, book);
            }
        }
        Outline(tex, new Color(0.12f, 0.07f, 0.04f));
        return Finish(tex);
    }

    /// <summary>Small round table with a dusty cloth.</summary>
    public static Sprite Table()
    {
        Texture2D tex = NewTexture(16, 16, Color.clear);
        FillCircle(tex, 8f, 8f, 6.5f, new Color(0.42f, 0.27f, 0.15f));
        FillCircle(tex, 8f, 8f, 4.5f, new Color(0.55f, 0.52f, 0.47f));
        FillCircle(tex, 9.5f, 9.5f, 1.2f, new Color(0.85f, 0.75f, 0.4f)); // candle
        Outline(tex, new Color(0.12f, 0.07f, 0.04f));
        return Finish(tex);
    }

    // ---------- Characters ----------

    /// <summary>The player: a pale-blue figure with a dark outline and eyes.</summary>
    public static Sprite Player()
    {
        Texture2D tex = NewTexture(16, 16, Color.clear);
        FillCircle(tex, 8f, 7.5f, 6f, new Color(0.55f, 0.75f, 0.95f));
        FillCircle(tex, 8f, 9.5f, 3.5f, new Color(0.95f, 0.85f, 0.75f)); // face
        tex.SetPixel(7, 10, Color.black);
        tex.SetPixel(9, 10, Color.black);
        Outline(tex, new Color(0.08f, 0.12f, 0.2f));
        return Finish(tex);
    }

    /// <summary>The monster: a ragged shadow with glowing red eyes.</summary>
    public static Sprite Monster()
    {
        Texture2D tex = NewTexture(16, 16, Color.clear);
        Color body = new Color(0.16f, 0.05f, 0.2f);
        FillCircle(tex, 8f, 9f, 6.5f, body);
        // Ragged "tendril" bottom edge.
        for (int x = 2; x < 14; x += 2)
            for (int y = 1; y < 4 + (x % 4); y++) tex.SetPixel(x, y, body);
        Color eye = new Color(1f, 0.15f, 0.1f);
        FillRect(tex, 5, 9, 2, 2, eye);
        FillRect(tex, 9, 9, 2, 2, eye);
        Outline(tex, new Color(0.05f, 0f, 0.05f));
        return Finish(tex);
    }

    /// <summary>
    /// Just the monster's two glowing eyes, laid out to match <see cref="Monster"/>.
    /// Drawn ABOVE the darkness overlay so the player can spot the eyes in the dark.
    /// </summary>
    public static Sprite MonsterEyes()
    {
        Texture2D tex = NewTexture(16, 16, Color.clear);
        Color eye = new Color(1f, 0.2f, 0.12f);
        FillRect(tex, 5, 9, 2, 2, eye);
        FillRect(tex, 9, 9, 2, 2, eye);
        return Finish(tex);
    }

    // ---------- Items ----------

    /// <summary>A gold key. Tint it to tell the three keys apart.</summary>
    public static Sprite Key()
    {
        Texture2D tex = NewTexture(16, 16, Color.clear);
        Color gold = Color.white; // tinted per-key by the SpriteRenderer
        FillCircle(tex, 4.5f, 8f, 3.2f, gold);
        FillCircle(tex, 4.5f, 8f, 1.4f, Color.clear);
        FillRect(tex, 7, 7, 8, 2, gold);
        FillRect(tex, 12, 4, 2, 3, gold);
        FillRect(tex, 9, 5, 2, 2, gold);
        Outline(tex, new Color(0.2f, 0.12f, 0f));
        return Finish(tex);
    }

    /// <summary>A heavy wooden exit door with a lock plate.</summary>
    public static Sprite ExitDoor()
    {
        Texture2D tex = NewTexture(16, 16);
        FillRect(tex, 0, 0, 16, 16, new Color(0.3f, 0.17f, 0.08f));
        for (int x = 3; x < 16; x += 4) FillRect(tex, x, 0, 1, 16, new Color(0.2f, 0.11f, 0.05f));
        FillRect(tex, 2, 6, 3, 4, new Color(0.75f, 0.65f, 0.3f)); // lock plate
        Outline(tex, new Color(0.1f, 0.05f, 0.02f));
        return Finish(tex);
    }

    // ---------- Lighting ----------

    /// <summary>
    /// The darkness overlay that follows the player. It is opaque black except for a soft circle
    /// around the player and, when <paramref name="withBeam"/> is true, a flashlight cone pointing right
    /// (the overlay is rotated to match the direction the player faces).
    /// </summary>
    public static Sprite Darkness(float worldSize, float lightRadius, bool withBeam,
                                  float beamLength, float beamHalfAngle)
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float unitsPerPixel = worldSize / size;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float wx = (x + 0.5f - size / 2f) * unitsPerPixel;
                float wy = (y + 0.5f - size / 2f) * unitsPerPixel;
                float dist = Mathf.Sqrt(wx * wx + wy * wy);

                // 0 = fully lit, 1 = fully dark.
                float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lightRadius * 0.45f, lightRadius, dist));

                if (withBeam && wx > 0f)
                {
                    float angle = Mathf.Abs(Mathf.Atan2(wy, wx) * Mathf.Rad2Deg);
                    float angleFade = Mathf.InverseLerp(beamHalfAngle * 0.6f, beamHalfAngle, angle);
                    float distFade = Mathf.InverseLerp(beamLength * 0.7f, beamLength, dist);
                    float beamDark = Mathf.Max(angleFade, distFade);
                    dark = Mathf.Min(dark, Mathf.SmoothStep(0f, 1f, beamDark));
                }

                pixels[y * size + x] = new Color(0f, 0f, 0.02f, dark * 0.97f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / worldSize);
    }

    /// <summary>A 1x1 white square, used for UI bars and tinted backgrounds.</summary>
    public static Texture2D WhitePixel()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return tex;
    }

    // ---------- Drawing helpers ----------

    static Texture2D NewTexture(int w, int h) => NewTexture(w, h, Color.black);

    static Texture2D NewTexture(int w, int h, Color fill)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // crisp pixel art
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
        tex.SetPixels(pixels);
        return tex;
    }

    static Sprite Finish(Texture2D tex)
    {
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
    }

    static void FillRect(Texture2D tex, int x0, int y0, int w, int h, Color c)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && y >= 0 && x < tex.width && y < tex.height) tex.SetPixel(x, y, c);
    }

    static void FillCircle(Texture2D tex, float cx, float cy, float r, Color c)
    {
        for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) tex.SetPixel(x, y, c);
            }
    }

    /// <summary>Draws a 1-pixel outline around every opaque region.</summary>
    static void Outline(Texture2D tex, Color outline)
    {
        int w = tex.width, h = tex.height;
        Color[] src = tex.GetPixels();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (src[y * w + x].a > 0.5f) continue;
                bool touchesShape =
                    (x > 0 && src[y * w + x - 1].a > 0.5f) ||
                    (x < w - 1 && src[y * w + x + 1].a > 0.5f) ||
                    (y > 0 && src[(y - 1) * w + x].a > 0.5f) ||
                    (y < h - 1 && src[(y + 1) * w + x].a > 0.5f);
                if (touchesShape) tex.SetPixel(x, y, outline);
            }
        // Also darken the outer edge pixels of fully-opaque tiles (like the door).
        for (int x = 0; x < w; x++)
        {
            if (src[x].a > 0.5f) tex.SetPixel(x, 0, outline);
            if (src[(h - 1) * w + x].a > 0.5f) tex.SetPixel(x, h - 1, outline);
        }
        for (int y = 0; y < h; y++)
        {
            if (src[y * w].a > 0.5f) tex.SetPixel(0, y, outline);
            if (src[y * w + w - 1].a > 0.5f) tex.SetPixel(w - 1, y, outline);
        }
    }
}
