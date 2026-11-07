using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Render-only, noninteractive peripheral flow. Analytic/private seeds,
/// no particles, textures, physics, global Random, input, camera or player writes.</summary>
[DisallowMultipleComponent]
public sealed class PlayerSpeedStreaks : MaskableGraphic
{
    public const int StreakCount = 28;
    public Canvas Overlay { get; private set; }
    float amount, opacity, edgeBand, clock;
    Vector3 movement;

    public static PlayerSpeedStreaks Create(Camera owner)
    {
        var go = new GameObject("[Player FX] Peripheral speed streaks", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -100; canvas.targetDisplay = owner.targetDisplay;
        var group = go.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
        var shape = new GameObject("Edge-only flow", typeof(RectTransform), typeof(CanvasRenderer));
        shape.hideFlags = HideFlags.DontSave; shape.transform.SetParent(go.transform, false);
        var graphic = shape.AddComponent<PlayerSpeedStreaks>(); graphic.Overlay = canvas;
        graphic.color = new Color(.82f, .92f, 1, 1); graphic.raycastTarget = false;
        graphic.rectTransform.anchorMin = Vector2.zero; graphic.rectTransform.anchorMax = Vector2.one;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
        graphic.Hide(); return graphic;
    }
    public void Present(float strength, Vector3 localMovement, float dt, float maximumOpacity, float band, Rect viewport)
    {
        amount = Mathf.Clamp01(strength); opacity = Mathf.Clamp(maximumOpacity, 0, .35f); edgeBand = Mathf.Clamp(band, .05f, .22f);
        movement = localMovement;
        if(amount <= .001f || opacity <= 0) { Hide(); return; }
        clock += Mathf.Clamp(dt, 0, .2f);
        // The canvas is scene-owned and independent of the camera's transform.
        // Limit its graphic to the owner's viewport, including non-fullscreen views.
        rectTransform.anchorMin = viewport.min; rectTransform.anchorMax = viewport.max;
        rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
        if(Overlay != null) Overlay.enabled = true;
        SetVerticesDirty();
    }
    public void Hide()
    {
        amount = 0; clock = 0;
        if(Overlay != null) Overlay.enabled = false;
        canvasRenderer.Clear();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if(amount <= .001f || opacity <= 0) return;
        Rect screen = rectTransform.rect;
        float scale = Mathf.Min(screen.width, screen.height);
        if(scale < 32) return;
        float diagonal = screen.size.magnitude * 2;
        for(int i = 0; i < StreakCount; i++)
        {
            Rect band = Band(screen, i % 4);
            float halfWidth = scale * Mathf.Lerp(.0008f, .0015f, Seed(i, 1));
            band.xMin += halfWidth; band.xMax -= halfWidth; band.yMin += halfWidth; band.yMax -= halfWidth;
            if(band.width <= 0 || band.height <= 0) continue;
            Vector2 anchor = new Vector2(Mathf.Lerp(band.xMin, band.xMax, Seed(i, 2)), Mathf.Lerp(band.yMin, band.yMax, Seed(i, 3)));
            Vector2 radial = new Vector2((anchor.x - screen.center.x) / (screen.width * .5f), (anchor.y - screen.center.y) / (screen.height * .5f));
            // Forward motion expands outward; backward motion contracts inward.
            // Lateral/vertical movement flows opposite its camera-space direction.
            Vector2 flow = radial * movement.z - new Vector2(movement.x, movement.y) * 1.25f;
            if(flow.sqrMagnitude < .0001f) continue;
            flow.Normalize();
            Vector2 begin = anchor - flow * diagonal, end = anchor + flow * diagonal;
            if(!Clip(band, ref begin, ref end)) continue;
            float path = Vector2.Distance(begin, end);
            float length = Mathf.Min(path * .7f, scale * Mathf.Lerp(.035f, .095f, Seed(i, 4)) * Mathf.Lerp(.7f, 1, amount));
            float progress = Mathf.Repeat(clock * Mathf.Lerp(1.6f, 3, amount) * Mathf.Lerp(.8f, 1.2f, Seed(i, 5)) + Seed(i, 6), 1);
            Vector2 head = begin + flow * ((path + length) * progress), tail = head - flow * length;
            if(!Clip(band, ref tail, ref head)) continue;
            float life = Mathf.SmoothStep(0, 1, Mathf.Min(progress * 8, (1 - progress) * 8));
            AddStroke(mesh, tail, head, halfWidth, amount * opacity * life * Mathf.Lerp(.6f, 1, Seed(i, 7)));
        }
    }
    Rect Band(Rect screen, int side)
    {
        if(side == 0) return Rect.MinMaxRect(screen.xMin, screen.yMin, screen.xMin + screen.width * edgeBand, screen.yMax);
        if(side == 1) return Rect.MinMaxRect(screen.xMax - screen.width * edgeBand, screen.yMin, screen.xMax, screen.yMax);
        if(side == 2) return Rect.MinMaxRect(screen.xMin, screen.yMax - screen.height * edgeBand, screen.xMax, screen.yMax);
        return Rect.MinMaxRect(screen.xMin, screen.yMin, screen.xMax, screen.yMin + screen.height * edgeBand);
    }
    void AddStroke(VertexHelper mesh, Vector2 tail, Vector2 head, float width, float alpha)
    {
        Vector2 delta = head - tail;
        if(delta.sqrMagnitude < 1 || alpha <= .0001f) return;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
        int first = mesh.currentVertCount;
        for(int row = 0; row < 3; row++)
        {
            Vector2 centre = Vector2.Lerp(tail, head, row * .5f);
            for(int column = 0; column < 3; column++)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = centre + normal * ((column - 1) * width * (row == 1 ? 1 : .15f));
                Color tint = color; tint.a *= row == 1 && column == 1 ? alpha : 0; vertex.color = tint;
                mesh.AddVert(vertex);
            }
        }
        for(int row = 0; row < 2; row++) for(int column = 0; column < 2; column++)
        {
            int a = first + row * 3 + column, b = a + 3;
            mesh.AddTriangle(a, b, b + 1); mesh.AddTriangle(a, b + 1, a + 1);
        }
    }
    static float Seed(int index, int channel)
    {
        unchecked
        {
            uint value = (uint)(index + 1) * 747796405u + (uint)channel * 2891336453u;
            value = ((value >> (int)((value >> 28) + 4)) ^ value) * 277803737u;
            value = (value >> 22) ^ value;
            return (value & 0x00ffffffu) / 16777216f;
        }
    }
    static bool Clip(Rect bounds, ref Vector2 a, ref Vector2 b)
    {
        Vector2 delta = b - a; float low = 0, high = 1;
        if(!ClipPlane(-delta.x, a.x - bounds.xMin, ref low, ref high) ||
            !ClipPlane(delta.x, bounds.xMax - a.x, ref low, ref high) ||
            !ClipPlane(-delta.y, a.y - bounds.yMin, ref low, ref high) ||
            !ClipPlane(delta.y, bounds.yMax - a.y, ref low, ref high)) return false;
        b = a + delta * high; a += delta * low; return true;
    }
    static bool ClipPlane(float direction, float distance, ref float low, ref float high)
    {
        if(Mathf.Abs(direction) < .00001f) return distance >= 0;
        float ratio = distance / direction;
        if(direction < 0) { if(ratio > high) return false; low = Mathf.Max(low, ratio); }
        else { if(ratio < low) return false; high = Mathf.Min(high, ratio); }
        return true;
    }
}
