using UnityEngine;

/// <summary>
/// Client-side catalog for C++ Auth inventory item ids.
/// </summary>
public static class ItemCatalog
{
    public readonly struct Def
    {
        public readonly ushort Id;
        public readonly string Name;
        public readonly string Hint;
        public readonly Color Color;
        public readonly string IconResource;

        public Def(ushort id, string name, string hint, Color color, string iconResource)
        {
            Id = id;
            Name = name;
            Hint = hint;
            Color = color;
            IconResource = iconResource;
        }
    }

    private static readonly Def[] All =
    {
        new Def(1, "木宝箱", "粉尘 + 随机卡", new Color(0.55f, 0.38f, 0.22f), "UI/chest_wood"),
        new Def(2, "铁宝箱", "粉尘 + 随机卡", new Color(0.55f, 0.58f, 0.62f), "UI/chest_iron"),
        new Def(3, "金宝箱", "粉尘/金币 + 随机卡", new Color(0.85f, 0.68f, 0.22f), "UI/chest_gold"),
    };

    public static bool TryGet(ushort id, out Def def)
    {
        foreach (var d in All)
        {
            if (d.Id == id)
            {
                def = d;
                return true;
            }
        }

        def = default;
        return false;
    }

    public static Sprite LoadIcon(ushort id)
    {
        if (!TryGet(id, out var def) || string.IsNullOrEmpty(def.IconResource))
            return null;
        var tex = Resources.Load<Texture2D>(def.IconResource);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }
}
