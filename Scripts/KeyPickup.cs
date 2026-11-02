using UnityEngine;

/// <summary>A collectible key. Bobs and glows gently so it's easy to spot in the dark.</summary>
public class KeyPickup : MonoBehaviour
{
    public Color keyColor = Color.yellow;

    Vector3 startPos;
    Vector3 baseScale;
    bool collected;

    void Start()
    {
        startPos = transform.position;
        baseScale = transform.localScale;
    }

    void Update()
    {
        // Gentle float + pulse animation.
        float t = Time.time * 3f + startPos.x;
        transform.position = startPos + Vector3.up * Mathf.Sin(t) * 0.08f;
        transform.localScale = baseScale * (1f + Mathf.Sin(t * 1.3f) * 0.06f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !GameManager.IsPlaying) return;
        if (other.GetComponent<PlayerController>() == null) return;

        collected = true;
        GameManager.Instance.CollectKey(keyColor);
        Destroy(gameObject);
    }
}
