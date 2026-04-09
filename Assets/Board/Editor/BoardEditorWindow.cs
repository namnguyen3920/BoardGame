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

    private Vector2 paletteScroll;
    private Vector2 canvasScroll;

    private bool hoverValid;
    private int hoverQ, hoverR;
    private bool hoverExists;
    private short hoverValue;

    private Dictionary<(int q, int r), int> _lookup = new Dictionary<(int q, int r), int>();

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
        HandleKeyboard();

        DrawAssetBar();
        if (target == null) return;

        BuildLookup();

        DrawSliderRow();
        DrawPaletteStrip();
        DrawCanvas();
        DrawStatusBar();

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

        int minQ = 0, maxQ = 0, minR = 0, maxR = 0;
        if (target.cells != null && target.cells.Count > 0)
        {
            minQ = int.MaxValue; maxQ = int.MinValue;
            minR = int.MaxValue; maxR = int.MinValue;
            foreach (var c in target.cells)
            {
                if (c.q < minQ) minQ = c.q;
                if (c.q > maxQ) maxQ = c.q;
                if (c.r < minR) minR = c.r;
                if (c.r > maxR) maxR = c.r;
            }
        }
        minQ -= canvasPadding;
        maxQ += canvasPadding;
        minR -= canvasPadding;
        maxR += canvasPadding;

        float cell = BaseCellPixelRadius * zoom;
        float pad = cell * 0.10f;
        int cols = maxQ - minQ + 1;
        int rows = maxR - minR + 1;
        float w = cell * (3f * cols + 4f);
        float h = cell * Mathf.Sqrt(3f) * (rows + cols * 0.5f + 2f);
        Rect area = GUILayoutUtility.GetRect(w, h);

        Vector2 origin = new Vector2(
            area.x + cell * 2f - cell * 1.5f * minQ,
            area.y + cell * 2f - cell * Mathf.Sqrt(3f) * (minR + minQ * 0.5f)
        );

        Event e = Event.current;

        if (e.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(area, CanvasBgColor);
            DrawAxisLines(origin, area, cell, minQ, maxQ, minR, maxR);
        }

        bool changed = false;
        bool newHoverValid = false;
        int newHoverQ = 0, newHoverR = 0;
        bool newHoverExists = false;
        short newHoverValue = 0;

        for (int q = minQ; q <= maxQ; q++)
        {
            for (int r = minR; r <= maxR; r++)
            {
                float px = cell * 1.5f * q;
                float py = cell * Mathf.Sqrt(3f) * (r + q * 0.5f);
                Vector2 center = origin + new Vector2(px, py);

                bool exists = _lookup.TryGetValue((q, r), out int cellIdx)
                    && cellIdx >= 0
                    && cellIdx < target.cells.Count;
                short val = exists ? target.cells[cellIdx].value : (short)0;

                bool hit = !mouseOverOverlay && PointInFlatHex(e.mousePosition, center, cell);
                if (hit)
                {
                    newHoverValid = true;
                    newHoverQ = q;
                    newHoverR = r;
                    newHoverExists = exists;
                    newHoverValue = val;

                    if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                    {
                        if (ApplyTool(q, r, exists, val, e.button))
                        {
                            changed = true;
                            BuildLookup();
                        }
                        e.Use();
                    }
                }

                DrawHexCell(center, cell - pad, val, exists, hit, q == 0 && r == 0);
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

    private void DrawHexCell(Vector2 center, float radius, short val, bool exists, bool hover, bool isOrigin)
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
            GUI.DrawTextureWithTexCoords(thumbRect, thumb, uvRect);
        }
        else if (exists && val != 0)
        {
            Rect labelRect = new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2);
            GUI.Label(labelRect, val.ToString(), _cellValueLabel);
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
        }
    }

    private static void DrawAxisLines(Vector2 origin, Rect area, float cell, int minQ, int maxQ, int minR, int maxR)
    {
        Handles.color = AxisLineColor;

        float qAxisTopY = origin.y + cell * Mathf.Sqrt(3f) * (minR - 0.5f);
        float qAxisBotY = origin.y + cell * Mathf.Sqrt(3f) * (maxR + 0.5f);
        qAxisTopY = Mathf.Max(qAxisTopY, area.y);
        qAxisBotY = Mathf.Min(qAxisBotY, area.yMax);
        Handles.DrawAAPolyLine(1.5f,
            new Vector3(origin.x, qAxisTopY, 0f),
            new Vector3(origin.x, qAxisBotY, 0f));

        Vector2 rLine0 = origin + new Vector2(cell * 1.5f * (minQ - 0.5f), cell * Mathf.Sqrt(3f) * (minQ - 0.5f) * 0.5f);
        Vector2 rLine1 = origin + new Vector2(cell * 1.5f * (maxQ + 0.5f), cell * Mathf.Sqrt(3f) * (maxQ + 0.5f) * 0.5f);
        Handles.DrawAAPolyLine(1.5f,
            new Vector3(rLine0.x, rLine0.y, 0f),
            new Vector3(rLine1.x, rLine1.y, 0f));
    }

    private bool ApplyTool(int q, int r, bool exists, short current, int mouseButton)
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
                if (exists && current == (short)brush) return false;
                Undo.RecordObject(target, "Paint Cell");
                target.Set(q, r, (short)brush);
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
        GUILayout.Label($"Tool: {currentTool}    Brush: v{brush} {brushName}    Cells: {count}", GUILayout.ExpandWidth(false));

        GUILayout.FlexibleSpace();

        if (hoverValid)
        {
            string suffix = hoverExists ? $"v={hoverValue} {NameOf(hoverValue)}" : "(empty — paint to add)";
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
