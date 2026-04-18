using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BoardEditorWindow : EditorWindow
{
    private enum Tool { Paint, Erase, Fill, Eyedropper }

    [SerializeField] private BoardData target;
    [SerializeField] private TilePalette palette;
    [SerializeField] private int brush = 1;
    [SerializeField] private Tool currentTool = Tool.Paint;
    [SerializeField] private float zoom = 1f;
    [SerializeField] private int canvasPadding = 3;
    [SerializeField] private byte brushRotation = 0;

    private Vector2 paletteScroll;
    private Vector2 canvasScroll;

    private bool hoverValid;
    private int hoverQ, hoverR;
    private bool hoverExists;
    private short hoverValue;
    private byte hoverRotation;

    private Dictionary<(int q, int r), int> _lookup = new Dictionary<(int q, int r), int>();

    private bool _rotGestureActive;
    private int _rotGestureQ, _rotGestureR;
    private float _rotGestureStartY;
    private int _rotGestureApplied;


    private const float BaseCellPixelRadius = 34f;
    private const float SidebarWidth = 156f;
    private const int ThumbSize = 72;

    private static readonly Color CanvasBgColor       = new Color(0.135f, 0.138f, 0.155f, 1f);
    private static readonly Color AxisLineColor       = new Color(0.32f, 0.42f, 0.56f, 0.35f);
    private static readonly Color GhostFillColor      = new Color(0.205f, 0.205f, 0.225f, 0.95f);
    private static readonly Color GhostOutlineColor   = new Color(0.38f, 0.38f, 0.44f, 0.70f);
    private static readonly Color PaintedBaseColor    = new Color(0.30f, 0.30f, 0.35f, 1f);
    private static readonly Color PaintedShadowColor  = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color PaintedRimColor     = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color PaintedOutlineColor = new Color(0.04f, 0.04f, 0.06f, 1f);
    private static readonly Color OriginOutlineColor  = new Color(0.35f, 0.85f, 0.98f, 0.95f);
    private static readonly Color HoverFillColor      = new Color(1f, 0.92f, 0.25f, 0.20f);
    private static readonly Color HoverRingColor      = new Color(1f, 0.92f, 0.25f, 1f);
    private static readonly Color PaletteSelectedBg   = new Color(0.22f, 0.44f, 0.66f, 0.35f);
    private static readonly Color BoardBorderColor    = new Color(0.55f, 0.75f, 0.95f, 0.50f);
    private static readonly Color AxisArrowColor      = new Color(0.55f, 0.75f, 0.95f, 0.75f);

    private GUIStyle _cellValueLabel;
    private GUIStyle _paletteTitle;

    private Scene _previewScene;
    private Camera _previewCam;
    private Light _previewKeyLight;
    private Light _previewFillLight;
    private RenderTexture _previewRT;
    private Dictionary<GameObject, Texture2D> _topDownCache = new Dictionary<GameObject, Texture2D>();
    private const int PreviewTextureSize = 192;
    private const float TilePreviewYRotation = 30f;

    [MenuItem("Tools/Board Editor")]
    public static void Open()
    {
        var w = GetWindow<BoardEditorWindow>("Board Editor");
        w.minSize = new Vector2(780, 480);
    }

    private void OnGUI()
    {
        BuildStyles();

        DrawAssetBar();
        if (target == null) return;

        BuildLookup();

        DrawSliderRow();
        DrawPaletteStrip();
        DrawCanvas();
        DrawStatusBar();
        HandleKeyboard();

        if (palette != null && AssetPreview.IsLoadingAssetPreviews())
        {
            Repaint();
        }
    }

    private void BuildLookup()
    {
        _lookup.Clear();
        if (target.cells == null) return;
        for (int i = 0; i < target.cells.Count; i++)
        {
            var c = target.cells[i];
            _lookup[(c.q, c.r)] = i;
        }
    }

    private void DrawAssetBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        target = (BoardData)EditorGUILayout.ObjectField("Board Data", target, typeof(BoardData), false);
        palette = (TilePalette)EditorGUILayout.ObjectField("Tile Palette", palette, typeof(TilePalette), false);
        EditorGUILayout.EndVertical();

        if (target == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Board Data asset to begin.\nCreate one via Assets → Create → BoardGame → Board Data.",
                MessageType.Info);
        }
        else if (palette == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Tile Palette so you can paint tiles.\nCreate one via Assets → Create → BoardGame → Tile Palette.",
                MessageType.Info);
        }
    }

    private void DrawSliderRow()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(8);

        GUILayout.Label(new GUIContent("Padding", "Empty ring of ghost cells drawn around existing content — paint into them to grow the board."), GUILayout.Width(62));
        canvasPadding = EditorGUILayout.IntSlider(canvasPadding, 1, 12, GUILayout.MinWidth(140));

        GUILayout.Space(16);
        DrawToolbarSeparator();
        GUILayout.Space(16);

        GUILayout.Label("Zoom", GUILayout.Width(46));
        zoom = GUILayout.HorizontalSlider(zoom, 0.4f, 2.5f, GUILayout.MinWidth(140));

        GUILayout.Space(8);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    private static void DrawToolbarSeparator()
    {
        Rect r = GUILayoutUtility.GetRect(1f, 28f, GUILayout.Width(1f), GUILayout.ExpandHeight(false));
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(r, new Color(0.42f, 0.42f, 0.46f, 0.5f));
        }
    }

    private void DrawPaletteStrip()
    {
        int count = palette == null ? 0 : palette.Count;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (count == 0)
        {
            EditorGUILayout.LabelField("Tile Palette is empty — add prefabs to the Tile Palette asset.", EditorStyles.miniLabel);
        }
        else
        {
            paletteScroll = EditorGUILayout.BeginScrollView(
                paletteScroll,
                false,
                false,
                GUILayout.Height(ThumbSize + 34));

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < count; i++)
            {
                DrawPaletteEntry(i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPaletteEntry(int index)
    {
        GameObject prefab = palette.Get(index);
        string label = prefab != null ? prefab.name : (index == 0 ? "(empty)" : $"#{index}");

        bool selected = brush == index && currentTool != Tool.Erase;

        Rect cardBgRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (selected && Event.current.type == EventType.Repaint && cardBgRect.width > 0f)
        {
            EditorGUI.DrawRect(cardBgRect, PaletteSelectedBg);
        }

        Rect iconRect = GUILayoutUtility.GetRect(ThumbSize, ThumbSize, GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize));

        Texture2D thumb = GetPrefabThumb(prefab);
        if (thumb != null)
        {
            GUI.DrawTexture(iconRect, thumb, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(iconRect, new Color(0.18f, 0.18f, 0.18f));
            GUI.Label(iconRect, index == 0 ? "empty" : $"#{index}", _cellValueLabel);
        }

        if (selected)
        {
            DrawRectBorder(iconRect, new Color(0.35f, 0.80f, 1f), 2f);
        }

        string hotkey = index < 9 ? (index + 1).ToString() : (index == 9 ? "0" : null);
        if (hotkey != null)
        {
            GUI.Label(
                new Rect(iconRect.x + 3, iconRect.y + 1, 16, 14),
                hotkey,
                EditorStyles.whiteBoldLabel);
        }

        GUI.Label(
            new Rect(iconRect.xMax - 22, iconRect.y + 1, 20, 14),
            $"v{index}",
            EditorStyles.whiteMiniLabel);

        GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(ThumbSize + 8));

        Rect lastRect = GUILayoutUtility.GetLastRect();
        Rect cardRect = new Rect(iconRect.x, iconRect.y, iconRect.width, iconRect.height + lastRect.height + 4);
        if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
        {
            brush = index;
            if (currentTool == Tool.Erase) currentTool = Tool.Paint;
            Event.current.Use();
            Repaint();
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(2);
    }

    private void DrawCanvas()
    {
        Rect canvasContainer = EditorGUILayout.BeginVertical();

        Rect paintBtnRect, eraseBtnRect, removeAllRect;
        bool overlayValid = canvasContainer.width > 0f;
        if (overlayValid)
        {
            const float btnSize = 38f;
            float totalW = btnSize * 3f;
            float ox = canvasContainer.xMax - 12f - totalW;
            float oy = canvasContainer.y + 12f;
            paintBtnRect = new Rect(ox, oy, btnSize, btnSize);
            eraseBtnRect = new Rect(ox + btnSize, oy, btnSize, btnSize);
            removeAllRect = new Rect(ox + btnSize * 2f, oy, btnSize, btnSize);
        }
        else
        {
            paintBtnRect = eraseBtnRect = removeAllRect = Rect.zero;
        }

        Vector2 mousePos = Event.current.mousePosition;
        bool mouseOverOverlay = overlayValid && (
            paintBtnRect.Contains(mousePos)
            || eraseBtnRect.Contains(mousePos)
            || removeAllRect.Contains(mousePos));

        canvasScroll = EditorGUILayout.BeginScrollView(canvasScroll, GUI.skin.box);

        float minPx, maxPx, minPy, maxPy;
        float sqrt3 = Mathf.Sqrt(3f);
        if (target.cells != null && target.cells.Count > 0)
        {
            minPx = float.MaxValue; maxPx = float.MinValue;
            minPy = float.MaxValue; maxPy = float.MinValue;
            foreach (var c in target.cells)
            {
                float px = 1.5f * c.q;
                float py = sqrt3 * (c.r + c.q * 0.5f);
                if (px < minPx) minPx = px;
                if (px > maxPx) maxPx = px;
                if (py < minPy) minPy = py;
                if (py > maxPy) maxPy = py;
            }
        }
        else
        {
            minPx = -1.5f; maxPx = 1.5f;
            minPy = -sqrt3; maxPy = sqrt3;
        }

        float contentMinPx = minPx, contentMaxPx = maxPx, contentMinPy = minPy, contentMaxPy = maxPy;
        bool hasContent = target.cells != null && target.cells.Count > 0;

        minPx -= canvasPadding * 1.5f;
        maxPx += canvasPadding * 1.5f;
        minPy -= canvasPadding * sqrt3;
        maxPy += canvasPadding * sqrt3;

        float cell = BaseCellPixelRadius * zoom;
        float pad = cell * 0.10f;
        float w = (maxPx - minPx + 2f) * cell;
        float h = (maxPy - minPy + 2f) * cell;

        const float scrollPad = 28f;
        float viewW = canvasContainer.width > 1f ? canvasContainer.width - 6f : 0f;
        float viewH = canvasContainer.height > 1f ? canvasContainer.height - 6f : 0f;
        float allocW = Mathf.Max(w + scrollPad * 2f, viewW);
        float allocH = Mathf.Max(h + scrollPad * 2f, viewH);
        Rect area = GUILayoutUtility.GetRect(allocW, allocH);

        int minQ = Mathf.FloorToInt(minPx / 1.5f);
        int maxQ = Mathf.CeilToInt(maxPx / 1.5f);

        Vector2 origin = new Vector2(
            area.x + (allocW - w) * 0.5f + cell - minPx * cell,
            area.y + (allocH - h) * 0.5f + cell - minPy * cell
        );

        Event e = Event.current;

        if (e.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(new Rect(area.x - 9999f, area.y - 9999f, 99999f, 99999f), CanvasBgColor);
            DrawAxisLines(origin, area, cell, minQ, maxQ);
            DrawCenterDecoration(origin, cell, sqrt3);
        }

        bool changed = false;

        if (_rotGestureActive)
        {
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                float dragDelta = e.mousePosition.y - _rotGestureStartY;
                int targetStep = Mathf.RoundToInt(dragDelta / (cell * 0.55f));
                int steps = targetStep - _rotGestureApplied;
                if (steps != 0 && _lookup.TryGetValue((_rotGestureQ, _rotGestureR), out int gIdx))
                {
                    Undo.RecordObject(target, "Rotate Cell");
                    HexCell gc = target.cells[gIdx];
                    gc.rotation = (byte)(((gc.rotation + steps) % 6 + 6) % 6);
                    target.cells[gIdx] = gc;
                    _rotGestureApplied = targetStep;
                    EditorUtility.SetDirty(target);
                    RepaintLiveTile(_rotGestureQ, _rotGestureR);
                    changed = true;
                }
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 2)
            {
                _rotGestureActive = false;
                e.Use();
                Repaint();
            }
        }

        bool newHoverValid = false;
        int newHoverQ = 0, newHoverR = 0;
        bool newHoverExists = false;
        short newHoverValue = 0;
        byte newHoverRotation = 0;

        for (int q = minQ; q <= maxQ; q++)
        {
            int rMin = Mathf.FloorToInt(minPy / sqrt3 - q * 0.5f);
            int rMax = Mathf.CeilToInt(maxPy / sqrt3 - q * 0.5f);
            for (int r = rMin; r <= rMax; r++)
            {
                float px = cell * 1.5f * q;
                float py = cell * Mathf.Sqrt(3f) * (r + q * 0.5f);
                Vector2 center = origin + new Vector2(px, py);

                bool exists = _lookup.TryGetValue((q, r), out int cellIdx)
                    && cellIdx >= 0
                    && cellIdx < target.cells.Count;
                short val = exists ? target.cells[cellIdx].value : (short)0;
                byte rot = exists ? target.cells[cellIdx].rotation : (byte)0;

                bool hit = !mouseOverOverlay && PointInFlatHex(e.mousePosition, center, cell);
                if (hit)
                {
                    newHoverValid = true;
                    newHoverQ = q;
                    newHoverR = r;
                    newHoverExists = exists;
                    newHoverValue = val;
                    if (exists) newHoverRotation = rot;

                    if (e.type == EventType.MouseDown && e.button == 2 && exists)
                    {
                        _rotGestureActive = true;
                        _rotGestureQ = q;
                        _rotGestureR = r;
                        _rotGestureStartY = e.mousePosition.y;
                        _rotGestureApplied = 0;
                        e.Use();
                    }
                    else if (e.type == EventType.ScrollWheel && exists)
                    {
                        if (_lookup.TryGetValue((q, r), out int sIdx))
                        {
                            Undo.RecordObject(target, "Rotate Cell");
                            HexCell sc = target.cells[sIdx];
                            int dir = e.delta.y > 0f ? 1 : -1;
                            sc.rotation = (byte)(((sc.rotation + dir) % 6 + 6) % 6);
                            target.cells[sIdx] = sc;
                            EditorUtility.SetDirty(target);
                            RepaintLiveTile(q, r);
                            changed = true;
                            BuildLookup();
                        }
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                    {
                        if (ApplyTool(q, r, exists, val, rot, e.button))
                        {
                            changed = true;
                            BuildLookup();
                            RepaintLiveTile(q, r);
                        }
                        e.Use();
                    }
                }

                DrawHexCell(center, cell - pad, val, rot, exists, hit, q == 0 && r == 0,
                    _rotGestureActive && _rotGestureQ == q && _rotGestureR == r);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        if (overlayValid)
        {
            DrawOverlayBackdrop(paintBtnRect, removeAllRect);
            DrawOverlayToolButton(paintBtnRect, Tool.Paint, "Grid.PaintTool", "Paint (B)");
            DrawOverlayToolButton(eraseBtnRect, Tool.Erase, "Grid.EraserTool", "Erase (E)");
            DrawOverlayRemoveAllButton(removeAllRect);
        }

        hoverValid = newHoverValid;
        hoverQ = newHoverQ;
        hoverR = newHoverR;
        hoverExists = newHoverExists;
        hoverValue = newHoverValue;
        hoverRotation = newHoverRotation;

        if (changed)
        {
            EditorUtility.SetDirty(target);
            Repaint();
        }
    }

    private static void DrawOverlayBackdrop(Rect firstBtn, Rect lastBtn)
    {
        if (Event.current.type != EventType.Repaint) return;
        Rect backdrop = new Rect(
            firstBtn.x - 6f,
            firstBtn.y - 6f,
            (lastBtn.xMax - firstBtn.x) + 12f,
            firstBtn.height + 12f);
        EditorGUI.DrawRect(backdrop, new Color(0.10f, 0.10f, 0.12f, 0.85f));
    }

    private void DrawOverlayToolButton(Rect rect, Tool tool, string iconName, string tooltip)
    {
        bool active = currentTool == tool;
        Color prev = GUI.backgroundColor;
        if (active) GUI.backgroundColor = new Color(0.45f, 0.80f, 1f);

        GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
        GUIContent content = iconContent != null && iconContent.image != null
            ? new GUIContent(iconContent.image, tooltip)
            : new GUIContent(tool.ToString().Substring(0, 1), tooltip);

        if (GUI.Button(rect, content))
        {
            currentTool = tool;
        }
        GUI.backgroundColor = prev;
    }

    private void DrawOverlayRemoveAllButton(Rect rect)
    {
        GUIContent iconContent = EditorGUIUtility.IconContent("TreeEditor.Trash");
        GUIContent content = iconContent != null && iconContent.image != null
            ? new GUIContent(iconContent.image, "Remove All — clear every cell from the board")
            : new GUIContent("Clear", "Remove All — clear every cell from the board");

        if (GUI.Button(rect, content))
        {
            if (EditorUtility.DisplayDialog(
                "Remove All",
                "Remove every cell from the board?",
                "Remove", "Cancel"))
            {
                Undo.RecordObject(target, "Remove All Cells");
                target.Clear();
                EditorUtility.SetDirty(target);
                BuildLookup();
                Repaint();
            }
        }
    }

    private void DrawHexCell(Vector2 center, float radius, short val, byte rotation, bool exists, bool hover, bool isOrigin, bool isActiveGesture = false)
    {
        bool isRepaint = Event.current.type == EventType.Repaint;

        Vector3[] verts = GetHexVerts(center, radius);

        Color fill;
        Texture2D thumb = null;
        if (!exists)
        {
            fill = GhostFillColor;
        }
        else
        {
            GameObject prefab = palette != null ? palette.Get(val) : null;
            thumb = GetPrefabThumb(prefab);
            fill = thumb != null
                ? PaintedBaseColor
                : Color.HSVToRGB((val * 0.137f) % 1f, 0.55f, 0.85f);
        }

        bool hasThumb = exists && thumb != null;

        if (isRepaint && exists && !hasThumb)
        {
            Vector3[] shadowVerts = GetHexVerts(center + new Vector2(0f, radius * 0.08f), radius);
            Handles.color = PaintedShadowColor;
            Handles.DrawAAConvexPolygon(shadowVerts);
        }

        if (isRepaint && !hasThumb)
        {
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(verts);
        }

        if (isRepaint && hover)
        {
            Handles.color = HoverFillColor;
            Handles.DrawAAConvexPolygon(verts);
        }

        if (exists && thumb != null)
        {
            const float HexHeightFrac = 0.8660254f;
            float rectW = radius * 2f;
            float rectH = radius * Mathf.Sqrt(3f);
            Rect thumbRect = new Rect(center.x - rectW * 0.5f, center.y - rectH * 0.5f, rectW, rectH);
            Rect uvRect = new Rect(0f, (1f - HexHeightFrac) * 0.5f, 1f, HexHeightFrac);
            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotation * 60f, center);
            GUI.DrawTextureWithTexCoords(thumbRect, thumb, uvRect);
            GUI.matrix = savedMatrix;
        }

        if (isRepaint && exists && !hasThumb)
        {
            Vector3[] innerVerts = GetHexVerts(center, radius - 2f);
            Handles.color = PaintedRimColor;
            DrawHexOutline(innerVerts, 1.5f);
        }

        if (isRepaint)
        {
            if (isOrigin)
            {
                Handles.color = OriginOutlineColor;
                DrawHexOutline(verts, 2.5f);
            }
            else if (hasThumb)
            {
                Handles.color = GhostOutlineColor;
                DrawHexOutline(verts, 1.2f);
            }
            else
            {
                Handles.color = exists ? PaintedOutlineColor : GhostOutlineColor;
                DrawHexOutline(verts, exists ? 2f : 1.2f);
            }

            if (hover)
            {
                Handles.color = HoverRingColor;
                DrawHexOutline(verts, 3f);
            }

            if (exists && (rotation != 0 || isActiveGesture))
            {
                DrawRotationBadge(center, radius, rotation, isActiveGesture);
            }
        }
    }

    private static void DrawAxisLines(Vector2 origin, Rect area, float cell, int minQ, int maxQ)
    {
        Handles.color = AxisLineColor;

        Handles.DrawAAPolyLine(1.5f,
            new Vector3(origin.x, area.y, 0f),
            new Vector3(origin.x, area.yMax, 0f));

        float sqrt3 = Mathf.Sqrt(3f);
        Vector2 rLine0 = origin + new Vector2(cell * 1.5f * (minQ - 0.5f), cell * sqrt3 * (minQ - 0.5f) * 0.5f);
        Vector2 rLine1 = origin + new Vector2(cell * 1.5f * (maxQ + 0.5f), cell * sqrt3 * (maxQ + 0.5f) * 0.5f);
        rLine0.y = Mathf.Clamp(rLine0.y, area.y, area.yMax);
        rLine1.y = Mathf.Clamp(rLine1.y, area.y, area.yMax);
        Handles.DrawAAPolyLine(1.5f,
            new Vector3(rLine0.x, rLine0.y, 0f),
            new Vector3(rLine1.x, rLine1.y, 0f));
    }

    private static void DrawCenterDecoration(Vector2 origin, float cell, float sqrt3)
    {
        float arrowLen = cell * 2.0f;

        // q+ axis: screen right
        Vector2 qTip = origin + new Vector2(arrowLen, 0f);
        // r+ axis: hex r direction (x=0, y=sqrt3 per unit)
        Vector2 rTip = origin + new Vector2(0f, sqrt3 * cell);

        Handles.color = AxisArrowColor;
        Handles.DrawAAPolyLine(2f, new Vector3(origin.x, origin.y, 0f), new Vector3(qTip.x, qTip.y, 0f));
        Handles.DrawAAPolyLine(2f, new Vector3(origin.x, origin.y, 0f), new Vector3(rTip.x, rTip.y, 0f));

        DrawArrowHead(qTip, new Vector2(1f, 0f), cell * 0.20f);
        DrawArrowHead(rTip, new Vector2(0f, 1f), cell * 0.20f);

        Handles.Label(new Vector3(qTip.x + 5f, qTip.y - 7f, 0f), "q");
        Handles.Label(new Vector3(rTip.x + 5f, rTip.y - 7f, 0f), "r");

        Handles.color = new Color(AxisArrowColor.r, AxisArrowColor.g, AxisArrowColor.b, 0.45f);
        Handles.DrawWireDisc(new Vector3(origin.x, origin.y, 0f), Vector3.forward, cell * 0.30f);
    }

    private static void DrawArrowHead(Vector2 tip, Vector2 dir, float size)
    {
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector3 a = new Vector3(tip.x - dir.x * size + perp.x * size * 0.4f, tip.y - dir.y * size + perp.y * size * 0.4f, 0f);
        Vector3 b = new Vector3(tip.x - dir.x * size - perp.x * size * 0.4f, tip.y - dir.y * size - perp.y * size * 0.4f, 0f);
        Handles.DrawAAPolyLine(2f, a, new Vector3(tip.x, tip.y, 0f), b);
    }

    private static void DrawRotationBadge(Vector2 center, float radius, byte rotation, bool isActive)
    {
        float angleDeg = rotation * 60f;
        Color arcColor = isActive
            ? new Color(1f, 0.75f, 0.15f, 1f)
            : new Color(0.55f, 0.88f, 1f, 0.80f);

        float arcR = radius * 0.34f;
        Handles.color = arcColor;

        if (angleDeg > 1f)
        {
            Handles.DrawWireArc(
                new Vector3(center.x, center.y, 0f),
                Vector3.forward,
                new Vector3(1f, 0f, 0f),
                angleDeg,
                arcR);

            float tipRad = angleDeg * Mathf.Deg2Rad;
            Vector2 tip = center + new Vector2(Mathf.Cos(tipRad), Mathf.Sin(tipRad)) * arcR;
            Vector2 tipDir = new Vector2(-Mathf.Sin(tipRad), Mathf.Cos(tipRad));
            DrawArrowHead(tip, tipDir, arcR * 0.38f);
        }
        else
        {
            Handles.DrawWireDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, arcR * 0.4f);
        }

    }

    private static void DrawBoardBorder(Vector2 origin, float cell, float cMinPx, float cMaxPx, float cMinPy, float cMaxPy)
    {
        float margin = cell * 0.60f;
        float x0 = origin.x + cMinPx * cell - margin;
        float x1 = origin.x + cMaxPx * cell + margin;
        float y0 = origin.y + cMinPy * cell - margin;
        float y1 = origin.y + cMaxPy * cell + margin;

        Handles.color = BoardBorderColor;
        Handles.DrawAAPolyLine(2f,
            new Vector3(x0, y0, 0f),
            new Vector3(x1, y0, 0f),
            new Vector3(x1, y1, 0f),
            new Vector3(x0, y1, 0f),
            new Vector3(x0, y0, 0f));
    }

    private bool ApplyTool(int q, int r, bool exists, short current, byte currentRotation, int mouseButton)
    {
        if (mouseButton == 1)
        {
            if (!exists) return false;
            Undo.RecordObject(target, "Remove Cell");
            target.RemoveAt(q, r);
            return true;
        }

        switch (currentTool)
        {
            case Tool.Paint:
                if (exists && current == (short)brush && currentRotation == brushRotation) return false;
                Undo.RecordObject(target, "Paint Cell");
                target.Set(q, r, (short)brush, brushRotation);
                return true;

            case Tool.Erase:
                if (!exists) return false;
                Undo.RecordObject(target, "Erase Cell");
                target.RemoveAt(q, r);
                return true;

            case Tool.Fill:
                if (!exists) return false;
                if (current == (short)brush) return false;
                FloodFill(q, r, current, (short)brush);
                return true;

            case Tool.Eyedropper:
                if (!exists) return false;
                brush = current;
                currentTool = Tool.Paint;
                return false;
        }
        return false;
    }

    private void FloodFill(int startQ, int startR, short oldVal, short newVal)
    {
        Undo.RecordObject(target, "Fill Region");

        var queue = new Queue<(int q, int r)>();
        var visited = new HashSet<(int, int)>();

        queue.Enqueue((startQ, startR));
        visited.Add((startQ, startR));

        int[,] dirs = new int[,]
        {
            { +1, 0 }, { -1, 0 },
            { 0, +1 }, { 0, -1 },
            { +1, -1 }, { -1, +1 },
        };

        while (queue.Count > 0)
        {
            var (q, r) = queue.Dequeue();
            target.Set(q, r, newVal);

            for (int d = 0; d < 6; d++)
            {
                int nq = q + dirs[d, 0];
                int nr = r + dirs[d, 1];
                var key = (nq, nr);
                if (visited.Contains(key)) continue;

                if (!_lookup.TryGetValue(key, out int nIdx)) continue;
                if (target.cells[nIdx].value != oldVal) continue;

                visited.Add(key);
                queue.Enqueue((nq, nr));
            }
        }

        EditorUtility.SetDirty(target);
    }

    private void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int count = target != null ? target.Count : 0;
        string brushName = NameOf(brush);
        int displayRot = (hoverValid && hoverExists) ? hoverRotation * 60 : brushRotation * 60;
        GUILayout.Label($"Tool: {currentTool}    Brush: v{brush} {brushName}    Rot: {displayRot}°    Cells: {count}", GUILayout.ExpandWidth(false));

        GUILayout.FlexibleSpace();

        if (hoverValid)
        {
            string suffix = hoverExists ? $"v={hoverValue} {NameOf(hoverValue)}    rot={hoverRotation * 60}°" : "(empty — paint to add)";
            GUILayout.Label($"Hover  (q={hoverQ}, r={hoverR})    {suffix}", GUILayout.ExpandWidth(false));
        }
        else
        {
            GUILayout.Label("Hover  —", GUILayout.ExpandWidth(false));
        }

        EditorGUILayout.EndHorizontal();
    }

    private string NameOf(int value)
    {
        if (palette == null) return value == 0 ? "(empty)" : $"#{value}";
        GameObject go = palette.Get(value);
        if (go != null) return go.name;
        return value == 0 ? "(empty)" : $"#{value}";
    }

    private void HandleKeyboard()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;

        switch (e.keyCode)
        {
            case KeyCode.B: currentTool = Tool.Paint; e.Use(); Repaint(); break;
            case KeyCode.E: currentTool = Tool.Erase; e.Use(); Repaint(); break;
            case KeyCode.G: currentTool = Tool.Fill; e.Use(); Repaint(); break;
            case KeyCode.I: currentTool = Tool.Eyedropper; e.Use(); Repaint(); break;
            case KeyCode.R:
                if (hoverValid && hoverExists)
                {
                    if (_lookup.TryGetValue((hoverQ, hoverR), out int rIdx))
                    {
                        Undo.RecordObject(target, "Rotate Cell");
                        HexCell rc = target.cells[rIdx];
                        rc.rotation = (byte)((rc.rotation + 1) % 6);
                        target.cells[rIdx] = rc;
                        EditorUtility.SetDirty(target);
                        RepaintLiveTile(hoverQ, hoverR);
                    }
                }
                else
                {
                    brushRotation = (byte)((brushRotation + 1) % 6);
                }
                e.Use();
                Repaint();
                break;
            case KeyCode.LeftBracket:
                if (palette != null && palette.Count > 0)
                {
                    brush = Mathf.Max(0, brush - 1);
                    if (currentTool == Tool.Erase) currentTool = Tool.Paint;
                    e.Use(); Repaint();
                }
                break;
            case KeyCode.RightBracket:
                if (palette != null && palette.Count > 0)
                {
                    brush = Mathf.Min(palette.Count - 1, brush + 1);
                    if (currentTool == Tool.Erase) currentTool = Tool.Paint;
                    e.Use(); Repaint();
                }
                break;
            case KeyCode.Alpha1: SelectBrushSlot(0, e); break;
            case KeyCode.Alpha2: SelectBrushSlot(1, e); break;
            case KeyCode.Alpha3: SelectBrushSlot(2, e); break;
            case KeyCode.Alpha4: SelectBrushSlot(3, e); break;
            case KeyCode.Alpha5: SelectBrushSlot(4, e); break;
            case KeyCode.Alpha6: SelectBrushSlot(5, e); break;
            case KeyCode.Alpha7: SelectBrushSlot(6, e); break;
            case KeyCode.Alpha8: SelectBrushSlot(7, e); break;
            case KeyCode.Alpha9: SelectBrushSlot(8, e); break;
            case KeyCode.Alpha0: SelectBrushSlot(9, e); break;
        }
    }

    private void SelectBrushSlot(int index, Event e)
    {
        if (palette == null || index < 0 || index >= palette.Count) return;
        brush = index;
        if (currentTool == Tool.Erase) currentTool = Tool.Paint;
        e.Use();
        Repaint();
    }

    private Texture2D GetPrefabThumb(GameObject prefab)
    {
        if (prefab == null) return null;

        if (_topDownCache.TryGetValue(prefab, out Texture2D cached) && cached != null)
        {
            return cached;
        }

        Texture2D rendered = RenderTopDown(prefab);
        if (rendered != null)
        {
            _topDownCache[prefab] = rendered;
            return rendered;
        }

        Texture2D fallback = AssetPreview.GetAssetPreview(prefab);
        if (fallback == null) fallback = AssetPreview.GetMiniThumbnail(prefab);
        return fallback;
    }

    private Texture2D RenderTopDown(GameObject prefab)
    {
        EnsurePreviewScene();

        GameObject go = null;
        try
        {
            go = Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(go, _previewScene);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            Bounds tightBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) tightBounds.Encapsulate(renderers[i].bounds);

            float extent = Mathf.Max(tightBounds.extents.x, tightBounds.extents.z);
            if (extent < 0.001f) extent = 0.5f;

            go.transform.rotation = Quaternion.Euler(0f, TilePreviewYRotation, 0f);

            _previewCam.orthographicSize = extent;
            _previewCam.transform.position = tightBounds.center + Vector3.up * (tightBounds.extents.y + 20f);
            _previewCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            RenderTexture prev = RenderTexture.active;
            _previewCam.targetTexture = _previewRT;
            _previewCam.Render();
            _previewCam.targetTexture = null;

            RenderTexture.active = _previewRT;
            Texture2D result = new Texture2D(PreviewTextureSize, PreviewTextureSize, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, PreviewTextureSize, PreviewTextureSize), 0, 0);
            result.Apply();
            RenderTexture.active = prev;

            return result;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BoardEditor] Top-down render failed for '{prefab.name}': {ex.Message}");
            return null;
        }
        finally
        {
            if (go != null) DestroyImmediate(go);
        }
    }

    private void EnsurePreviewScene()
    {
        if (!_previewScene.IsValid())
        {
            _previewScene = EditorSceneManager.NewPreviewScene();
        }

        if (_previewCam == null)
        {
            GameObject camGO = new GameObject("BoardEditor_PreviewCam");
            camGO.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(camGO, _previewScene);
            _previewCam = camGO.AddComponent<Camera>();
            _previewCam.enabled = false;
            _previewCam.orthographic = true;
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCam.nearClipPlane = 0.01f;
            _previewCam.farClipPlane = 200f;
            _previewCam.scene = _previewScene;
        }

        if (_previewKeyLight == null)
        {
            GameObject keyGO = new GameObject("BoardEditor_KeyLight");
            keyGO.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(keyGO, _previewScene);
            _previewKeyLight = keyGO.AddComponent<Light>();
            _previewKeyLight.type = LightType.Directional;
            _previewKeyLight.intensity = 1.25f;
            _previewKeyLight.color = Color.white;
            _previewKeyLight.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
        }

        if (_previewFillLight == null)
        {
            GameObject fillGO = new GameObject("BoardEditor_FillLight");
            fillGO.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(fillGO, _previewScene);
            _previewFillLight = fillGO.AddComponent<Light>();
            _previewFillLight.type = LightType.Directional;
            _previewFillLight.intensity = 0.45f;
            _previewFillLight.color = new Color(0.85f, 0.88f, 1f);
            _previewFillLight.transform.rotation = Quaternion.Euler(-40f, -30f, 0f);
        }

        if (_previewRT == null)
        {
            _previewRT = new RenderTexture(PreviewTextureSize, PreviewTextureSize, 16, RenderTextureFormat.ARGB32);
            _previewRT.antiAliasing = 4;
            _previewRT.Create();
        }
    }

    private void RepaintLiveTile(int q, int r)
    {
        if (target == null) return;
        int idx = target.IndexOf(q, r);

        BoardSpawner[] spawners = Object.FindObjectsByType<BoardSpawner>(FindObjectsSortMode.None);
        foreach (BoardSpawner spawner in spawners)
        {
            if (spawner.data != target) continue;

            Transform child = null;
            for (int i = 0; i < spawner.transform.childCount; i++)
            {
                Transform c = spawner.transform.GetChild(i);
                if (c.name.StartsWith($"Tile_{q}_{r}_"))
                {
                    child = c;
                    break;
                }
            }
            if (child == null) continue;

            byte rotation = idx >= 0 ? target.cells[idx].rotation : (byte)0;
            Undo.RecordObject(child, "Rotate Tile");
            child.localRotation = Quaternion.Euler(0f, rotation * 60f, 0f);
        }
    }

    private void OnDisable()
    {
        foreach (var kv in _topDownCache)
        {
            if (kv.Value != null) DestroyImmediate(kv.Value);
        }
        _topDownCache.Clear();

        if (_previewRT != null)
        {
            _previewRT.Release();
            DestroyImmediate(_previewRT);
            _previewRT = null;
        }

        if (_previewCam != null) { DestroyImmediate(_previewCam.gameObject); _previewCam = null; }
        if (_previewKeyLight != null) { DestroyImmediate(_previewKeyLight.gameObject); _previewKeyLight = null; }
        if (_previewFillLight != null) { DestroyImmediate(_previewFillLight.gameObject); _previewFillLight = null; }

        if (_previewScene.IsValid())
        {
            EditorSceneManager.ClosePreviewScene(_previewScene);
        }
    }

    private void BuildStyles()
    {
        if (_cellValueLabel == null)
        {
            _cellValueLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
            };
            _cellValueLabel.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
        }

        if (_paletteTitle == null)
        {
            _paletteTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(4, 4, 4, 4),
            };
        }
    }

    private static void DrawRectBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static Vector3[] GetHexVerts(Vector2 center, float radius)
    {
        Vector3[] v = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI / 3f * i;
            v[i] = new Vector3(
                center.x + radius * Mathf.Cos(a),
                center.y + radius * Mathf.Sin(a),
                0f);
        }
        return v;
    }

    private static void DrawHexOutline(Vector3[] verts, float thickness)
    {
        Vector3[] loop = new Vector3[7];
        for (int i = 0; i < 6; i++) loop[i] = verts[i];
        loop[6] = verts[0];
        Handles.DrawAAPolyLine(thickness, loop);
    }

    private static bool PointInFlatHex(Vector2 point, Vector2 center, float radius)
    {
        float dx = Mathf.Abs(point.x - center.x);
        float dy = Mathf.Abs(point.y - center.y);
        const float SQRT3_OVER_2 = 0.8660254f;
        const float INV_SQRT3 = 0.5773503f;
        if (dy > radius * SQRT3_OVER_2) return false;
        if (dx + dy * INV_SQRT3 > radius) return false;
        return true;
    }
}
