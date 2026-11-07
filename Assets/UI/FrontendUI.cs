using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>Shared, read-only visual vocabulary for the menu and hold-Tab field record.</summary>
public static class FrontendUI
{
    public static readonly Color Ink = new Color(.025f, .045f, .047f, .97f);
    public static readonly Color PanelColor = new Color(.045f, .078f, .077f, .96f);
    public static readonly Color Line = new Color(.25f, .33f, .30f, .75f);
    public static readonly Color Gold = new Color(.85f, .73f, .47f);
    public static readonly Color Paper = new Color(.93f, .94f, .88f);
    public static readonly Color Muted = new Color(.61f, .69f, .64f);
    static Font font;
    public static Font Font { get { if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf"); return font; } }

    public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    public static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    public static RectTransform Full(Transform parent, string name)
    { var rect = Rect(parent, name, 0, 0, 0, 0); Stretch(rect); return rect; }
    public static RectTransform Image(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
    { var r = Rect(parent, name, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return r; }
    public static void Photo(Transform parent, string name, Sprite sprite, Color tint)
    {
        var crop = Full(parent, name); crop.gameObject.AddComponent<RectMask2D>();
        var image = Full(crop, "Image").gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = tint; image.raycastTarget = false;
        if (sprite != null)
        {
            var fit = image.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }
    }
    public static RectTransform Shade(Transform parent, string name, Color color, bool raycast = false)
    { var r = Full(parent, name); var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return r; }
    public static Text Text(Transform parent, string name, string content, float x, float y, float w, float h, int size, Color color, FontStyle style = FontStyle.Normal)
    {
        var r = Rect(parent, name, x, y, w, h); var text = r.gameObject.AddComponent<Text>();
        text.font = Font; text.text = content; text.fontSize = size; text.fontStyle = style; text.color = color;
        text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
    }
    public static Button Button(Transform parent, string name, string label, float x, float y, float w, float h, bool primary = false)
    {
        var r = Image(parent, name, x, y, w, h, primary ? Gold : PanelColor, true); var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = r.GetComponent<Image>(); var c = b.colors;
        c.normalColor = Color.white; c.highlightedColor = new Color(1.15f, 1.15f, 1.12f); c.pressedColor = new Color(.70f, .77f, .70f); c.selectedColor = new Color(1.1f, 1.12f, 1.08f); c.disabledColor = new Color(.5f, .5f, .5f); b.colors = c;
        Text(r, "Label", label, 22, 0, w - 44, h, 22, primary ? Ink : Paper, FontStyle.Bold);
        return b;
    }
    public static Slider Slider(Transform parent, string name, float x, float y, float width, float min, float max, out Text value)
    {
        var r = Rect(parent, name, x, y, width, 80);
        Text(r, "Label", name, 0, 0, width - 100, 30, 20, Paper);
        value = Text(r, "Value", "", width - 100, 0, 100, 30, 19, Gold); value.alignment = TextAnchor.MiddleRight;
        var track = Image(r, "Track", 0, 49, width, 6, Line, true);
        var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
        var fillArea = Rect(track, "Fill Area", 0, 0, width, 6); Stretch(fillArea);
        var fill = Image(fillArea, "Fill", 0, 0, width, 6, Gold); Stretch(fill); slider.fillRect = fill;
        var handleArea = Full(track, "Handle Area"); handleArea.offsetMin = new Vector2(8, 0); handleArea.offsetMax = new Vector2(-8, 0);
        var handle = Image(handleArea, "Handle", 0, -5, 16, 16, Paper, true); handle.pivot = new Vector2(.5f, .5f);
        handle.anchorMin = handle.anchorMax = new Vector2(0, .5f); handle.anchoredPosition = Vector2.zero;
        slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        // A taller invisible target makes mouse/touch interaction practical.
        var target = Image(track, "Hit Area", 0, -15, width, 36, Color.clear, true); target.SetAsFirstSibling();
        return slider;
    }
    public static Toggle Toggle(Transform parent, string label, float x, float y, float width)
    {
        var r = Rect(parent, label, x, y, width, 44); var t = r.gameObject.AddComponent<Toggle>();
        var box = Image(r, "Box", width - 36, 6, 30, 30, Line, true);
        var check = Image(box, "Check", 6, 6, 18, 18, Gold); t.targetGraphic = box.GetComponent<Image>(); t.graphic = check.GetComponent<Image>();
        Text(r, "Label", label, 0, 0, width - 50, 44, 20, Paper);
        var target = Image(r, "Hit Area", 0, 0, width, 44, Color.clear, true); target.SetAsFirstSibling(); return t;
    }
    public static RectTransform Canvas(Transform parent, string name, int order, bool interactive)
    {
        var root = Rect(parent, name, 0, 0, 1920, 1080); var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        if (interactive) root.gameObject.AddComponent<GraphicRaycaster>();
        else { var group = root.gameObject.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false; }
        return root;
    }
    public static void Fit(RectTransform design)
    {
        if (design == null) return; var parent = design.parent as RectTransform; if (parent == null) return;
        float scale = Mathf.Min(parent.rect.width / 1920f, parent.rect.height / 1080f);
        if (scale > 0) design.localScale = Vector3.one * scale;
    }
    public static RectTransform Design(Transform canvas, string name)
    {
        // Fit the entire design rather than cropping on 4:3 or ultrawide displays.
        var rect = Rect(canvas, name, 0, 0, 1920, 1080); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
        return rect;
    }
}
