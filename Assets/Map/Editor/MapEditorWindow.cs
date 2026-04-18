using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapEditorWindow : EditorWindow
{
    private enum MapEditMode { Map, Route }
    private enum MapTool     { Paint, Erase, Fill, Eyedropper }
    private enum RouteTool   { Place, Remove, Connect }

    [SerializeField] private MapEditMode _currentMode = MapEditMode.Map;

    [SerializeField] private BoardData     _board;
    [SerializeField] private NodeRouteData _routeData;

    [SerializeField] private float _zoom          = 1f;
    [SerializeField] private int   _canvasPadding = 3;

    private Vector2 _canvasScroll;
    private Vector2 _paletteScroll;
    private Vector2 _sidebarScroll;

    private readonly Dictionary<(int, int), int> _boardLookup   = new Dictionary<(int, int), int>();
    private readonly Dictionary<(int, int), int> _activeLookup  = new Dictionary<(int, int), int>();
    private readonly Dictionary<(int, int), int> _nodeLookup    = new Dictionary<(int, int), int>();
    private readonly Dictionary<int, Vector2>    _nodeScreenPos = new Dictionary<int, Vector2>();

    private bool  _hoverValid;
    private int   _hoverQ, _hoverR;
    private bool  _hoverExists;
    private short _hoverValue;
    private byte  _hoverRotation;

    // ── Map mode ─────────────────────────────────────────────────
    [SerializeField] private MapTool _mapTool       = MapTool.Paint;
    [SerializeField] private int     _brush         = 1;
    [SerializeField] private byte    _brushRotation = 0;
    [SerializeField] private int     _activeLayerIdx = 0;

    private bool  _rotGestureActive;
    private int   _rotGestureQ, _rotGestureR;
    private float _rotGestureStartY;
    private int   _rotGestureApplied;

    // ── Route mode ───────────────────────────────────────────────
    [SerializeField] private RouteTool _routeTool     = RouteTool.Place;
    private int     _selectedNodeIdx  = -1;
    private bool    _connectDragActive;
    private int     _connectSrcIdx;
    private Vector2 _connectDragPos;
    private bool    _hoverIsNode;
    private int     _hoverNodeIdx;

    private readonly List<string> _validationErrors = new List<string>();
    private bool _validationDirty = true;

    // ── Preview thumbnail system ──────────────────────────────────
    private readonly Dictionary<GameObject, Texture2D> _topDownCache = new Dictionary<GameObject, Texture2D>();
    private Scene         _previewScene;
    private Camera        _previewCam;
    private Light         _previewKeyLight;
    private Light         _previewFillLight;
    private RenderTexture _previewRT;
    private const int   PREVIEW_TEXTURE_SIZE    = 192;
    private const float TILE_PREVIEW_Y_ROTATION = 30f;

    // ── Canvas / layout constants ─────────────────────────────────
    private const float BASE_CELL_PIXEL_RADIUS = HexEditorUtils.BaseCellPixelRadius;
    private const float SIDEBAR_WIDTH          = 244f;
    private const int   THUMB_SIZE             = 72;

    // ── Canvas colors ─────────────────────────────────────────────
    private static readonly Color CANVAS_BG          = new Color(0.135f, 0.138f, 0.155f, 1f);
    private static readonly Color GHOST_FILL          = new Color(0.205f, 0.205f, 0.225f, 0.95f);
    private static readonly Color GHOST_OUTLINE       = new Color(0.38f,  0.38f,  0.44f,  0.70f);
    private static readonly Color PAINTED_SHADOW      = new Color(0f,     0f,     0f,     0.45f);
    private static readonly Color PAINTED_RIM         = new Color(1f,     1f,     1f,     0.12f);
    private static readonly Color PAINTED_OUTLINE     = new Color(0.04f,  0.04f,  0.06f,  1f);
    private static readonly Color ORIGIN_OUTLINE      = new Color(0.35f,  0.85f,  0.98f,  0.95f);
    private static readonly Color HOVER_FILL          = new Color(1f,     0.92f,  0.25f,  0.20f);
    private static readonly Color HOVER_RING          = new Color(1f,     0.92f,  0.25f,  1f);
    private static readonly Color PALETTE_SELECTED_BG = new Color(0.22f,  0.44f,  0.66f,  0.35f);
    private static readonly Color BOARD_CELL_FILL     = new Color(0.22f,  0.22f,  0.26f,  1f);
    private static readonly Color BOARD_CELL_OUTLINE  = new Color(0.35f,  0.35f,  0.42f,  0.8f);
    private static readonly Color HOVER_PLACE_FILL    = new Color(0.20f,  0.80f,  0.30f,  0.25f);
    private static readonly Color HOVER_PLACE_RING    = new Color(0.20f,  0.90f,  0.35f,  1f);
    private static readonly Color HOVER_REMOVE_FILL   = new Color(0.90f,  0.20f,  0.20f,  0.25f);
    private static readonly Color HOVER_REMOVE_RING   = new Color(1f,     0.30f,  0.30f,  1f);
    private static readonly Color HOVER_CONNECT_FILL  = new Color(0.35f,  0.70f,  1f,     0.25f);
    private static readonly Color HOVER_CONNECT_RING  = new Color(0.45f,  0.85f,  1f,     1f);
    private static readonly Color SELECTED_OUTLINE    = new Color(0.25f,  0.70f,  1f,     1f);
    private static readonly Color ARROW_COLOR         = new Color(0.60f,  0.65f,  0.75f,  0.75f);
    private static readonly Color ARROW_SELECTED      = new Color(0.35f,  0.85f,  1f,     1f);
    private static readonly Color CONNECT_DRAG_COLOR  = new Color(0.40f,  1f,     0.60f,  0.85f);

    // ── UI accent colors ──────────────────────────────────────────
    private static readonly Color ACCENT_MAP         = new Color(0.25f, 0.75f, 1f,    1f);
    private static readonly Color ACCENT_ROUTE       = new Color(1f,    0.68f, 0.18f, 1f);
    private static readonly Color ACCENT_ACTIVE_TOOL = new Color(0.35f, 0.80f, 1f,    1f);
    private static readonly Color SEP_COLOR          = new Color(0.42f, 0.42f, 0.46f, 0.5f);
    private static readonly Color STATUS_BG          = new Color(0.14f, 0.14f, 0.16f, 1f);

    private static Color NodeTypeColor(NodeType t)
    {
        switch (t)
        {
            case NodeType.Bonus: return new Color(1f,    0.78f, 0.10f, 1f);
            case NodeType.Fail:  return new Color(0.88f, 0.22f, 0.22f, 1f);
            default:             return new Color(0.62f, 0.62f, 0.70f, 1f);
        }
    }

    private static Color LayerColor(int idx)
    {
        if (idx == 0) return new Color(1f, 0.78f, 0.12f, 1f);
        return Color.HSVToRGB((idx * 0.618034f) % 1f, 0.62f, 0.88f);
    }

    private GUIStyle _cellValueLabel;
    private GUIStyle _nodeBadgeStyle;
    private GUIStyle _errorLabelStyle;
    private GUIStyle _validLabelStyle;
    private GUIStyle _sidebarRowStyle;
    private GUIStyle _sectionHeaderStyle;
    private GUIStyle _statusLabelStyle;

    // ── Layer helpers ─────────────────────────────────────────────
    private BoardLayerData BaseLayer    => LayerAt(0);
    private BoardLayerData ActiveLayer  => LayerAt(_activeLayerIdx);
    private TilePalette    ActivePalette => ActiveLayer?.Palette;

    private BoardLayerData LayerAt(int idx)
    {
        if (_board?.layers == null || idx < 0 || idx >= _board.layers.Count) return null;
        return _board.layers[idx];
    }

    private int LayerCount => _board?.layers?.Count ?? 0;

    [MenuItem("Tools/Map Editor")]
    public static void Open()
    {
        var w = GetWindow<MapEditorWindow>("Map Editor");
        w.minSize = new Vector2(900, 560);
    }

    private void OnGUI()
    {
        BuildStyles();
        DrawAssetBar();
        DrawModeToolbar();

        if (_currentMode == MapEditMode.Map)
        {
            if (_board == null) return;
            if (LayerCount == 0)
            {
                DrawLayerManagerStrip();
                DrawInfoBox("No layers — press  +  in the layer strip to add the base layer.", ACCENT_MAP);
                return;
            }
            _activeLayerIdx = Mathf.Clamp(_activeLayerIdx, 0, LayerCount - 1);
            BuildBoardLookup();
            BuildActiveLayerLookup();
            DrawLayerManagerStrip();
            if (ActivePalette != null)
                DrawPaletteStrip();
            else
                DrawInfoBox("Active layer has no Tile Palette — assign one in the layer strip above.", ACCENT_ROUTE);
            DrawMapCanvas();
            DrawMapStatusBar();
            HandleMapKeyboard();
        }
        else
        {
            if (_board == null || _routeData == null) return;
            BuildBoardLookup();
            BuildNodeLookup();
            DrawRouteToolRow();
            EditorGUILayout.BeginHorizontal();
            DrawRouteCanvas();
            DrawSidebar();
            EditorGUILayout.EndHorizontal();
            DrawRouteStatusBar();
            HandleRouteKeyboard();
        }

        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    // ════════════════════════════════════════════════════════════════
    // UI — ASSET BAR
    // ════════════════════════════════════════════════════════════════

    private void DrawAssetBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        DrawStatusDot(_board != null);
        _board = (BoardData)EditorGUILayout.ObjectField(
            "Board Data", _board, typeof(BoardData), false);

        GUILayout.Space(16f);
        DrawVertSep(20f);
        GUILayout.Space(16f);

        DrawStatusDot(_routeData != null);
        _routeData = (NodeRouteData)EditorGUILayout.ObjectField(
            "Node Route", _routeData, typeof(NodeRouteData), false);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (_board == null)
            DrawInfoBox("Assign a Board Data asset to begin.", new Color(0.9f, 0.6f, 0.1f));
        else if (_currentMode == MapEditMode.Route && _routeData == null)
            DrawInfoBox("Assign a Node Route asset to use Route mode.", ACCENT_ROUTE);
    }

    private static void DrawStatusDot(bool ok)
    {
        Rect r = GUILayoutUtility.GetRect(8f, 8f, GUILayout.Width(8f), GUILayout.Height(8f));
        r.y += (EditorGUIUtility.singleLineHeight - 8f) * 0.5f;
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(r, ok ? new Color(0.25f, 0.85f, 0.35f) : new Color(0.85f, 0.25f, 0.25f));
    }

    // ════════════════════════════════════════════════════════════════
    // UI — MODE TOOLBAR
    // ════════════════════════════════════════════════════════════════

    private void DrawModeToolbar()
    {
        Rect barRect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(barRect, new Color(0.16f, 0.16f, 0.18f, 1f));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(4f);

        MapEditMode prev = _currentMode;
        DrawModeTab(MapEditMode.Map,   "  ◈  Map  ",   ACCENT_MAP);
        DrawModeTab(MapEditMode.Route, "  ⬡  Route  ", ACCENT_ROUTE);

        GUILayout.FlexibleSpace();

        Color accent = _currentMode == MapEditMode.Map ? ACCENT_MAP : ACCENT_ROUTE;
        if (Event.current.type == EventType.Repaint)
        {
            Rect accentLine = new Rect(barRect.x, barRect.yMax - 2f, barRect.width, 2f);
            EditorGUI.DrawRect(accentLine, new Color(accent.r, accent.g, accent.b, 0.55f));
        }

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(1f);

        if (_currentMode != prev && prev == MapEditMode.Route)
        {
            _connectDragActive = false;
            _selectedNodeIdx   = -1;
        }
    }

    private void DrawModeTab(MapEditMode mode, string label, Color accent)
    {
        bool active = _currentMode == mode;

        GUIStyle style = new GUIStyle(EditorStyles.toolbarButton)
        {
            fontSize  = 11,
            fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
        };

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = active
            ? new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f, 1f)
            : new Color(0.22f, 0.22f, 0.24f, 1f);

        if (GUILayout.Toggle(active, label, style, GUILayout.Height(26f), GUILayout.MinWidth(80f)) && !active)
            _currentMode = mode;

        GUI.backgroundColor = prev;
    }

    // ════════════════════════════════════════════════════════════════
    // UI — LAYER MANAGER STRIP
    // ════════════════════════════════════════════════════════════════

    private void DrawLayerManagerStrip()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Layers", _sectionHeaderStyle, GUILayout.Width(46f));
        DrawVertSep(18f);
        GUILayout.Space(4f);

        for (int i = 0; i < LayerCount; i++)
        {
            BoardLayerData layer  = _board.layers[i];
            string         label  = layer != null ? layer.name : $"Layer {i + 1}";
            bool           active = i == _activeLayerIdx;
            Color          lc     = LayerColor(i);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = active
                ? new Color(lc.r * 0.55f, lc.g * 0.55f, lc.b * 0.55f, 1f)
                : new Color(0.22f, 0.22f, 0.24f, 1f);

            GUIStyle tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
            };

            if (GUILayout.Toggle(active, label, tabStyle, GUILayout.MinWidth(56f)) && !active)
            {
                _activeLayerIdx = i;
                _brush = 1;
                BuildActiveLayerLookup();
                Repaint();
            }
            GUI.backgroundColor = prev;

            if (active && Event.current.type == EventType.Repaint)
            {
                Rect lastR = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(new Rect(lastR.x, lastR.yMax - 2f, lastR.width, 2f), lc);
            }
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            AddLayer();
        if (LayerCount > 0 && GUILayout.Button("−", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            RemoveActiveLayer();

        EditorGUILayout.EndHorizontal();

        BoardLayerData activeLayer = ActiveLayer;
        if (activeLayer != null)
        {
            EditorGUILayout.BeginHorizontal();

            Color lc = LayerColor(_activeLayerIdx);
            Rect  bar = GUILayoutUtility.GetRect(4f, EditorGUIUtility.singleLineHeight + 2f,
                GUILayout.Width(4f));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(bar, lc);

            GUILayout.Space(4f);
            GUILayout.Label("Palette", GUILayout.Width(50f));

            TilePalette newPal = (TilePalette)EditorGUILayout.ObjectField(
                activeLayer.Palette, typeof(TilePalette), false);

            if (newPal != activeLayer.Palette)
            {
                Undo.RecordObject(activeLayer, "Set Layer Palette");
                activeLayer.Palette = newPal;
                EditorUtility.SetDirty(activeLayer);
                _topDownCache.Clear();
                Repaint();
            }

            if (activeLayer.Palette != null)
            {
                int cnt = activeLayer.Count;
                GUILayout.Label(
                    cnt == 0 ? "empty" : $"{cnt} cell{(cnt == 1 ? "" : "s")}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(54f));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════════════
    // UI — PALETTE STRIP
    // ════════════════════════════════════════════════════════════════

    private void DrawPaletteStrip()
    {
        TilePalette pal   = ActivePalette;
        int         count = pal != null ? pal.Count : 0;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Tiles", _sectionHeaderStyle, GUILayout.Width(36f));
        if (pal != null)
        {
            Color prev = GUI.contentColor;
            GUI.contentColor = new Color(0.6f, 0.6f, 0.65f);
            GUILayout.Label(pal.name, EditorStyles.miniLabel);
            GUI.contentColor = prev;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2f);

        if (count == 0)
        {
            GUILayout.Label("Palette is empty — add prefabs to the Tile Palette asset.", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, false, false,
                GUILayout.Height(THUMB_SIZE + 36));
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < count; i++)
                DrawPaletteEntry(pal, i);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPaletteEntry(TilePalette pal, int index)
    {
        GameObject prefab   = pal.Get(index);
        string     label    = prefab != null ? prefab.name : (index == 0 ? "(empty)" : $"#{index}");
        bool       selected = _brush == index && _mapTool != MapTool.Erase;

        Rect cardBg = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (Event.current.type == EventType.Repaint && cardBg.width > 0f)
        {
            if (selected)
                EditorGUI.DrawRect(cardBg, PALETTE_SELECTED_BG);
        }

        Rect iconRect = GUILayoutUtility.GetRect(THUMB_SIZE, THUMB_SIZE,
            GUILayout.Width(THUMB_SIZE), GUILayout.Height(THUMB_SIZE));

        Texture2D thumb = GetPrefabThumb(prefab);
        if (thumb != null)
            GUI.DrawTexture(iconRect, thumb, ScaleMode.ScaleToFit);
        else
        {
            EditorGUI.DrawRect(iconRect, new Color(0.15f, 0.15f, 0.17f));
            GUI.Label(iconRect, index == 0 ? "∅" : $"#{index}", _cellValueLabel);
        }

        if (selected)
            HexEditorUtils.DrawRectBorder(iconRect, ACCENT_MAP, 2f);

        string hotkey = index < 9 ? (index + 1).ToString() : (index == 9 ? "0" : null);
        if (hotkey != null)
        {
            GUIStyle hotkeyStyle = new GUIStyle(EditorStyles.whiteBoldLabel) { fontSize = 9 };
            GUI.Label(new Rect(iconRect.x + 3, iconRect.y + 1, 14, 13), hotkey, hotkeyStyle);
        }

        Color prev = GUI.contentColor;
        GUI.contentColor = new Color(0.6f, 0.6f, 0.65f);
        GUI.Label(new Rect(iconRect.xMax - 24, iconRect.y + 1, 22, 13), $"v{index}", EditorStyles.whiteMiniLabel);
        GUI.contentColor = prev;

        GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(THUMB_SIZE + 8));

        Rect lastRect = GUILayoutUtility.GetLastRect();
        Rect cardRect = new Rect(iconRect.x, iconRect.y, iconRect.width, iconRect.height + lastRect.height + 4);
        if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
        {
            _brush = index;
            if (_mapTool == MapTool.Erase) _mapTool = MapTool.Paint;
            Event.current.Use(); Repaint();
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }

    // ════════════════════════════════════════════════════════════════
    // UI — MAP STATUS BAR
    // ════════════════════════════════════════════════════════════════

    private void DrawMapStatusBar()
    {
        Rect barRect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(barRect, STATUS_BG);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        Color toolColor = _mapTool switch
        {
            MapTool.Paint      => ACCENT_MAP,
            MapTool.Erase      => HOVER_REMOVE_RING,
            MapTool.Fill       => new Color(0.45f, 0.90f, 0.45f),
            MapTool.Eyedropper => new Color(0.85f, 0.65f, 1f),
            _                  => Color.white,
        };

        DrawColoredLabel(_mapTool.ToString(), toolColor, EditorStyles.toolbarButton, GUILayout.Width(72f));

        string layerLabel = ActiveLayer != null ? ActiveLayer.name : "—";
        Color  lc         = LayerCount > 0 ? LayerColor(_activeLayerIdx) : Color.gray;
        DrawColoredLabel($"◈ {layerLabel}", lc, EditorStyles.toolbarButton, GUILayout.Width(100f));

        int    cellCount = ActiveLayer?.Count ?? 0;
        int    dispRot   = (_hoverValid && _hoverExists) ? _hoverRotation * 60 : _brushRotation * 60;
        GUILayout.Label(
            $"Rot: {dispRot}°    Cells: {cellCount}",
            EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        GUILayout.FlexibleSpace();

        if (_hoverValid)
        {
            string suf = _hoverExists
                ? $"v={_hoverValue} {PaletteName(ActivePalette, _hoverValue)}    rot={_hoverRotation * 60}°"
                : "empty — paint to place";
            DrawColoredLabel($"({_hoverQ}, {_hoverR})", new Color(0.85f, 0.85f, 0.60f),
                EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
            GUILayout.Label($"  {suf}", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
        }
        else
        {
            GUILayout.Label("—", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
        }

        EditorGUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════════════
    // UI — MAP KEYBOARD
    // ════════════════════════════════════════════════════════════════

    private void HandleMapKeyboard()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        switch (e.keyCode)
        {
            case KeyCode.B: _mapTool = MapTool.Paint;      e.Use(); Repaint(); break;
            case KeyCode.E: _mapTool = MapTool.Erase;      e.Use(); Repaint(); break;
            case KeyCode.G: _mapTool = MapTool.Fill;       e.Use(); Repaint(); break;
            case KeyCode.I: _mapTool = MapTool.Eyedropper; e.Use(); Repaint(); break;
            case KeyCode.R:
                if (_activeLayerIdx == 0 && _hoverValid && _hoverExists)
                {
                    if (_activeLookup.TryGetValue((_hoverQ, _hoverR), out int rIdx) && ActiveLayer != null)
                    {
                        Undo.RecordObject(ActiveLayer, "Rotate Cell");
                        HexCell rc = ActiveLayer.Cells[rIdx];
                        rc.rotation = (byte)((rc.rotation + 1) % 6);
                        ActiveLayer.Cells[rIdx] = rc;
                        EditorUtility.SetDirty(ActiveLayer);
                        RepaintLiveTile(_hoverQ, _hoverR);
                    }
                }
                else _brushRotation = (byte)((_brushRotation + 1) % 6);
                e.Use(); Repaint(); break;
            case KeyCode.LeftBracket:
                { var p = ActivePalette; if (p != null && p.Count > 0) { _brush = Mathf.Max(0, _brush - 1); if (_mapTool == MapTool.Erase) _mapTool = MapTool.Paint; e.Use(); Repaint(); } }
                break;
            case KeyCode.RightBracket:
                { var p = ActivePalette; if (p != null && p.Count > 0) { _brush = Mathf.Min(p.Count - 1, _brush + 1); if (_mapTool == MapTool.Erase) _mapTool = MapTool.Paint; e.Use(); Repaint(); } }
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
        var p = ActivePalette;
        if (p == null || index < 0 || index >= p.Count) return;
        _brush = index;
        if (_mapTool == MapTool.Erase) _mapTool = MapTool.Paint;
        e.Use(); Repaint();
    }

    // ════════════════════════════════════════════════════════════════
    // UI — ROUTE TOOL ROW
    // ════════════════════════════════════════════════════════════════

    private void DrawRouteToolRow()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        DrawRouteToolButton(RouteTool.Place,   "Place",   "Grid.PaintTool",  84f);
        DrawRouteToolButton(RouteTool.Remove,  "Remove",  "Grid.EraserTool", 74f);
        DrawRouteToolButton(RouteTool.Connect, "Connect", "AvatarSelector",  78f);

        DrawVertSep();

        GUILayout.Label("Pad", EditorStyles.toolbarButton, GUILayout.Width(28f));
        _canvasPadding = EditorGUILayout.IntSlider(_canvasPadding, 1, 8, GUILayout.MinWidth(60f));
        DrawVertSep();
        GUILayout.Label("Zoom", EditorStyles.toolbarButton, GUILayout.Width(36f));
        _zoom = GUILayout.HorizontalSlider(_zoom, 0.4f, 2.5f, GUILayout.MinWidth(70f));

        GUILayout.FlexibleSpace();

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.70f, 0.22f, 0.22f);
        if (GUILayout.Button("Clear All", EditorStyles.toolbarButton, GUILayout.Width(66f)))
        {
            GUI.backgroundColor = prev;
            if (EditorUtility.DisplayDialog("Clear All Nodes",
                "Remove every node and connection from this route?", "Clear", "Cancel"))
            {
                Undo.RecordObject(_routeData, "Clear All Nodes");
                _routeData.Nodes.Clear();
                _selectedNodeIdx = -1;
                _validationDirty = true;
                EditorUtility.SetDirty(_routeData);
                Repaint();
            }
        }
        GUI.backgroundColor = prev;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawRouteToolButton(RouteTool tool, string label, string iconName, float width)
    {
        bool       active = _routeTool == tool;
        GUIContent icon   = EditorGUIUtility.IconContent(iconName);
        GUIContent btn    = icon?.image != null
            ? new GUIContent($" {label}", icon.image)
            : new GUIContent(label);

        GUIStyle style = new GUIStyle(EditorStyles.toolbarButton)
        {
            fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
        };

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = active
            ? new Color(ACCENT_ROUTE.r * 0.55f, ACCENT_ROUTE.g * 0.55f, ACCENT_ROUTE.b * 0.55f, 1f)
            : Color.white;

        if (GUILayout.Toggle(active, btn, style, GUILayout.Width(width)) && !active)
            _routeTool = tool;

        GUI.backgroundColor = prev;
    }

    // ════════════════════════════════════════════════════════════════
    // UI — ROUTE STATUS BAR
    // ════════════════════════════════════════════════════════════════

    private void DrawRouteStatusBar()
    {
        if (Event.current.type == EventType.Repaint)
        {
            Rect barRect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, STATUS_BG);
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        DrawColoredLabel(_routeTool.ToString(), ACCENT_ROUTE,
            EditorStyles.toolbarButton, GUILayout.Width(72f));

        int count = _routeData != null ? _routeData.Count : 0;
        GUILayout.Label($"Nodes: {count}", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        GUILayout.FlexibleSpace();

        if (_hoverValid)
        {
            string nodeInfo = _hoverIsNode
                ? $"Node {_hoverNodeIdx}"
                : "empty board cell";
            DrawColoredLabel($"({_hoverQ}, {_hoverR})", new Color(0.85f, 0.85f, 0.60f),
                EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
            GUILayout.Label($"  {nodeInfo}", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
        }
        else GUILayout.Label("—", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

        EditorGUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════════════
    // UI — SHARED HELPERS
    // ════════════════════════════════════════════════════════════════

    private static void DrawVertSep(float height = 28f)
    {
        Rect r = GUILayoutUtility.GetRect(1f, height, GUILayout.Width(1f), GUILayout.ExpandHeight(false));
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(r, SEP_COLOR);
    }

    private static void DrawColoredLabel(string text, Color color, GUIStyle style, params GUILayoutOption[] opts)
    {
        Color prev = GUI.contentColor;
        GUI.contentColor = color;
        GUILayout.Label(text, style, opts);
        GUI.contentColor = prev;
    }

    private static void DrawInfoBox(string message, Color accent)
    {
        Rect r = EditorGUILayout.GetControlRect(false, 28f);
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(r, new Color(accent.r, accent.g, accent.b, 0.08f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), accent);
        }
        GUI.Label(new Rect(r.x + 8f, r.y, r.width - 8f, r.height), message, EditorStyles.miniLabel);
    }

    // ════════════════════════════════════════════════════════════════
    // SHARED CANVAS INFRASTRUCTURE
    // ════════════════════════════════════════════════════════════════

    private void BuildBoardLookup()
    {
        _boardLookup.Clear();
        List<HexCell> baseCells = BaseLayer?.Cells;
        if (baseCells == null) return;
        for (int i = 0; i < baseCells.Count; i++)
        {
            var c = baseCells[i];
            _boardLookup[(c.q, c.r)] = i;
        }
    }

    private void BuildActiveLayerLookup()
    {
        _activeLookup.Clear();
        List<HexCell> cells = ActiveLayer?.Cells;
        if (cells == null) return;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            _activeLookup[(c.q, c.r)] = i;
        }
    }

    private void BuildNodeLookup()
    {
        _nodeLookup.Clear();
        if (_routeData?.Nodes == null) return;
        for (int i = 0; i < _routeData.Nodes.Count; i++)
        {
            var n = _routeData.Nodes[i];
            _nodeLookup[(n.Q, n.R)] = i;
        }
    }

    private Rect BeginHexCanvas(out Vector2 origin, out float cell,
        out int minQ, out int maxQ, out float minPy, out float maxPy)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        HexEditorUtils.ComputeCanvasBounds(BaseLayer?.Cells, _canvasPadding,
            out float minPx, out float maxPx, out minPy, out maxPy);

        cell = BASE_CELL_PIXEL_RADIUS * _zoom;
        float w = (maxPx - minPx + 2f) * cell;
        float h = (maxPy - minPy + 2f) * cell;

        Rect container = EditorGUILayout.BeginVertical();
        _canvasScroll  = EditorGUILayout.BeginScrollView(_canvasScroll, GUI.skin.box);

        const float SCROLL_PAD = 28f;
        float viewW  = container.width  > 1f ? container.width  - 6f : 0f;
        float viewH  = container.height > 1f ? container.height - 6f : 0f;
        float allocW = Mathf.Max(w + SCROLL_PAD * 2f, viewW);
        float allocH = Mathf.Max(h + SCROLL_PAD * 2f, viewH);
        Rect area    = GUILayoutUtility.GetRect(allocW, allocH);

        minQ = Mathf.FloorToInt(minPx / 1.5f);
        maxQ = Mathf.CeilToInt(maxPx  / 1.5f);
        origin = HexEditorUtils.ComputeOrigin(area, allocW, allocH, w, h, cell, minPx, maxPy);

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(new Rect(area.x - 9999f, area.y - 9999f, 99999f, 99999f), CANVAS_BG);
            HexEditorUtils.DrawAxisLines(origin, area, cell, minQ, maxQ);
            HexEditorUtils.DrawCenterDecoration(origin, cell, sqrt3);
        }

        return area;
    }

    private void EndHexCanvas(bool changed, Object dirtyTarget)
    {
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        if (changed) { EditorUtility.SetDirty(dirtyTarget); Repaint(); }
    }

    // ════════════════════════════════════════════════════════════════
    // CANVAS TOOLBAR OVERLAY
    // ════════════════════════════════════════════════════════════════

    private void HandleCanvasOverlayClick(Rect canvasArea, Event e)
    {
        if (e.type != EventType.MouseDown) return;

        const float BTN_SIZE = 32f;
        const float SPACING  = 6f;
        const float MARGIN   = 8f;
        const float PANEL_W  = BTN_SIZE + MARGIN * 2f;

        float btnX = canvasArea.xMax - PANEL_W - MARGIN;
        float btnY = canvasArea.y + MARGIN;
        Rect paintRect = new Rect(btnX, btnY, BTN_SIZE, BTN_SIZE);
        btnY += BTN_SIZE + SPACING;
        Rect eraseRect = new Rect(btnX, btnY, BTN_SIZE, BTN_SIZE);

        if (paintRect.Contains(e.mousePosition)) { _mapTool = MapTool.Paint; e.Use(); }
        else if (eraseRect.Contains(e.mousePosition)) { _mapTool = MapTool.Erase; e.Use(); }
    }

    private void DrawMapCanvasToolOverlay(Rect canvasArea)
    {
        if (canvasArea.width < 1f) return;

        const float BTN_SIZE = 32f;
        const float SPACING  = 6f;
        const float MARGIN   = 8f;
        const float PANEL_W  = BTN_SIZE + MARGIN * 2f;
        const float PANEL_H  = (BTN_SIZE + SPACING) * 2f + MARGIN * 2f - SPACING;

        Rect panelRect = new Rect(canvasArea.xMax - PANEL_W - MARGIN, canvasArea.y + MARGIN, PANEL_W, PANEL_H);

        Handles.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);
        Handles.DrawSolidRectangleWithOutline(panelRect, Handles.color, Handles.color);

        Handles.color = ACCENT_MAP;
        Handles.DrawSolidRectangleWithOutline(
            new Rect(panelRect.xMax - 2f, panelRect.y, 2f, panelRect.height),
            Handles.color, Handles.color);

        float btnY = panelRect.y + MARGIN;
        float btnX = panelRect.x + MARGIN;
        DrawToolButton(MapTool.Paint, btnX, btnY);
        btnY += BTN_SIZE + SPACING;
        DrawToolButton(MapTool.Erase, btnX, btnY);
    }

    private void DrawToolButton(MapTool tool, float xPos, float yPos)
    {
        const float BTN_SIZE = 32f;

        Rect btnRect = new Rect(xPos, yPos, BTN_SIZE, BTN_SIZE);
        bool active = _mapTool == tool;

        Handles.color = active ? ACCENT_ACTIVE_TOOL : new Color(0.25f, 0.25f, 0.28f, 0.7f);
        Handles.DrawSolidRectangleWithOutline(btnRect, Handles.color, Handles.color);

        if (active)
        {
            Handles.color = ACCENT_MAP;
            Handles.DrawSolidRectangleWithOutline(
                new Rect(btnRect.xMax - 2f, btnRect.y, 2f, btnRect.height),
                Handles.color, Handles.color);
        }

        string icon = tool == MapTool.Paint ? "✏" : "✕";
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
        };
        style.normal.textColor = Color.white;
        GUI.Label(btnRect, icon, style);
    }

    // ════════════════════════════════════════════════════════════════
    // MAP CANVAS (logic unchanged)
    // ════════════════════════════════════════════════════════════════

    private void DrawMapCanvas()
    {
        float sqrt3      = Mathf.Sqrt(3f);
        bool  onOverlay  = _activeLayerIdx > 0;
        BoardLayerData activeLayer = ActiveLayer;

        Rect area = BeginHexCanvas(out Vector2 origin, out float cell,
            out int minQ, out int maxQ, out float minPy, out float maxPy);

        float pad = cell * 0.10f;
        Event e   = Event.current;

        if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition) && e.control)
        {
            _zoom = Mathf.Clamp(_zoom + (e.delta.y > 0f ? 0.1f : -0.1f), 0.4f, 2.5f);
            e.Use();
            Repaint();
        }

        HandleCanvasOverlayClick(area, e);

        if (!onOverlay && _rotGestureActive)
        {
            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                float delta      = e.mousePosition.y - _rotGestureStartY;
                int   targetStep = Mathf.RoundToInt(delta / (cell * 0.55f));
                int   steps      = targetStep - _rotGestureApplied;
                if (steps != 0 && _activeLookup.TryGetValue((_rotGestureQ, _rotGestureR), out int gIdx))
                {
                    Undo.RecordObject(activeLayer, "Rotate Cell");
                    HexCell gc = activeLayer.Cells[gIdx];
                    gc.rotation = (byte)(((gc.rotation + steps) % 6 + 6) % 6);
                    activeLayer.Cells[gIdx] = gc;
                    _rotGestureApplied = targetStep;
                    EditorUtility.SetDirty(activeLayer);
                    RepaintLiveTile(_rotGestureQ, _rotGestureR);
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 2)
            {
                _rotGestureActive = false;
                e.Use(); Repaint();
            }
        }

        bool  newHoverValid  = false;
        int   newHoverQ = 0, newHoverR = 0;
        bool  newHoverExists = false;
        short newHoverValue  = 0;
        byte  newHoverRot    = 0;
        bool  changed        = false;

        for (int q = minQ; q <= maxQ; q++)
        {
            int rMin = Mathf.FloorToInt(minPy / sqrt3 - q * 0.5f);
            int rMax = Mathf.CeilToInt(maxPy  / sqrt3 - q * 0.5f);
            for (int r = rMin; r <= rMax; r++)
            {
                float   px     = cell * 1.5f  * q;
                float   py     = cell * sqrt3 * (r + q * 0.5f);
                Vector2 center = origin + new Vector2(px, -py);

                bool  baseExists = _boardLookup.TryGetValue((q, r), out int baseIdx)
                                   && baseIdx >= 0 && BaseLayer != null && baseIdx < BaseLayer.Cells.Count;
                short baseVal    = baseExists ? BaseLayer.Cells[baseIdx].value    : (short)0;
                byte  baseRot    = baseExists ? BaseLayer.Cells[baseIdx].rotation : (byte)0;

                int   aIdx       = -1;
                bool  activeExists = onOverlay && activeLayer != null
                                     && _activeLookup.TryGetValue((q, r), out aIdx)
                                     && aIdx >= 0 && aIdx < activeLayer.Cells.Count;
                short activeVal  = activeExists ? activeLayer.Cells[aIdx].value    : (short)0;
                byte  activeRot  = activeExists ? activeLayer.Cells[aIdx].rotation : (byte)0;

                bool  editExists = onOverlay ? activeExists : baseExists;
                short editVal    = onOverlay ? activeVal    : baseVal;
                byte  editRot    = onOverlay ? activeRot    : baseRot;

                bool hit = onOverlay
                    ? (baseExists && HexEditorUtils.PointInFlatHex(e.mousePosition, center, cell))
                    : HexEditorUtils.PointInFlatHex(e.mousePosition, center, cell);

                if (hit)
                {
                    newHoverValid  = true;
                    newHoverQ      = q;
                    newHoverR      = r;
                    newHoverExists = editExists;
                    newHoverValue  = editVal;
                    newHoverRot    = editRot;

                    if (!onOverlay && e.type == EventType.MouseDown && e.button == 2 && baseExists)
                    {
                        _rotGestureActive  = true;
                        _rotGestureQ       = q;
                        _rotGestureR       = r;
                        _rotGestureStartY  = e.mousePosition.y;
                        _rotGestureApplied = 0;
                        e.Use();
                    }
                    else if (!onOverlay && e.type == EventType.ScrollWheel && baseExists)
                    {
                        if (_activeLookup.TryGetValue((q, r), out int sIdx))
                        {
                            Undo.RecordObject(activeLayer, "Rotate Cell");
                            HexCell sc  = activeLayer.Cells[sIdx];
                            int     dir = e.delta.y > 0f ? 1 : -1;
                            sc.rotation = (byte)(((sc.rotation + dir) % 6 + 6) % 6);
                            activeLayer.Cells[sIdx] = sc;
                            EditorUtility.SetDirty(activeLayer);
                            RepaintLiveTile(q, r);
                            changed = true;
                            BuildActiveLayerLookup();
                        }
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                    {
                        if (ApplyMapTool(q, r, editExists, editVal, editRot, e.button, activeLayer))
                        {
                            changed = true;
                            BuildActiveLayerLookup();
                            if (!onOverlay) { BuildBoardLookup(); RepaintLiveTile(q, r); }
                        }
                        e.Use();
                    }
                }

                DrawMapHexCell(center, cell - pad, q, r,
                    baseExists, baseVal, baseRot,
                    activeExists, activeVal, activeRot,
                    activeLayer, hit, q == 0 && r == 0, onOverlay,
                    !onOverlay && _rotGestureActive && _rotGestureQ == q && _rotGestureR == r);
            }
        }

        _hoverValid    = newHoverValid;
        _hoverQ        = newHoverQ;
        _hoverR        = newHoverR;
        _hoverExists   = newHoverExists;
        _hoverValue    = newHoverValue;
        _hoverRotation = newHoverRot;

        if (Event.current.type == EventType.Repaint)
            DrawMapCanvasToolOverlay(area);

        EndHexCanvas(changed, activeLayer != null ? (Object)activeLayer : _board);
    }

    private bool ApplyMapTool(int q, int r, bool exists, short current, byte currentRot,
        int btn, BoardLayerData layer)
    {
        if (layer == null) return false;

        if (btn == 1)
        {
            if (!exists) return false;
            Undo.RecordObject(layer, "Remove Cell");
            layer.RemoveAt(q, r);
            return true;
        }

        switch (_mapTool)
        {
            case MapTool.Paint:
                if (exists && current == (short)_brush && currentRot == _brushRotation) return false;
                Undo.RecordObject(layer, "Paint Cell");
                layer.Set(q, r, (short)_brush, _brushRotation);
                return true;

            case MapTool.Erase:
                if (!exists) return false;
                Undo.RecordObject(layer, "Erase Cell");
                layer.RemoveAt(q, r);
                return true;

            case MapTool.Fill:
                if (_activeLayerIdx > 0 || !exists || current == (short)_brush) return false;
                BaseFloodFill(q, r, current, (short)_brush, layer);
                return true;

            case MapTool.Eyedropper:
                if (!exists) return false;
                _brush   = current;
                _mapTool = MapTool.Paint;
                return false;
        }
        return false;
    }

    private void BaseFloodFill(int startQ, int startR, short oldVal, short newVal, BoardLayerData layer)
    {
        Undo.RecordObject(layer, "Fill Region");
        var queue   = new Queue<(int, int)>();
        var visited = new HashSet<(int, int)>();
        queue.Enqueue((startQ, startR));
        visited.Add((startQ, startR));

        int[,] dirs = { { +1, 0 }, { -1, 0 }, { 0, +1 }, { 0, -1 }, { +1, -1 }, { -1, +1 } };

        while (queue.Count > 0)
        {
            var (q, r) = queue.Dequeue();
            layer.Set(q, r, newVal);
            for (int d = 0; d < 6; d++)
            {
                int nq = q + dirs[d, 0], nr = r + dirs[d, 1];
                var key = (nq, nr);
                if (visited.Contains(key)) continue;
                if (!_activeLookup.TryGetValue(key, out int nIdx)) continue;
                if (layer.Cells[nIdx].value != oldVal) continue;
                visited.Add(key);
                queue.Enqueue((nq, nr));
            }
        }
        EditorUtility.SetDirty(layer);
    }

    private void DrawMapHexCell(Vector2 center, float radius,
        int q, int r,
        bool baseExists, short baseVal, byte baseRot,
        bool overlayExists, short overlayVal, byte overlayRot,
        BoardLayerData activeLayer, bool hover, bool isOrigin,
        bool onOverlay, bool isActiveGesture)
    {
        bool      isRepaint = Event.current.type == EventType.Repaint;
        Vector3[] verts     = HexEditorUtils.GetHexVerts(center, radius);

        if (!baseExists)
        {
            if (isRepaint)
            {
                Handles.color = GHOST_FILL;
                Handles.DrawAAConvexPolygon(verts);
                Handles.color = GHOST_OUTLINE;
                HexEditorUtils.DrawHexOutline(verts, 1.2f);
            }
            return;
        }

        {
            TilePalette basePalette = BaseLayer?.Palette;
            GameObject  prefab      = basePalette != null ? basePalette.Get(baseVal) : null;
            Texture2D   thumb       = GetPrefabThumb(prefab);
            bool        hasThumb    = thumb != null;

            if (isRepaint && !hasThumb)
            {
                Vector3[] shadow = HexEditorUtils.GetHexVerts(center + new Vector2(0f, radius * 0.08f), radius);
                Handles.color = PAINTED_SHADOW;
                Handles.DrawAAConvexPolygon(shadow);
                Handles.color = Color.HSVToRGB((baseVal * 0.137f) % 1f, 0.55f, 0.85f);
                Handles.DrawAAConvexPolygon(verts);
            }

            if (hasThumb)
            {
                const float HEX_H_FRAC = 0.8660254f;
                float rw = radius * 2f, rh = radius * Mathf.Sqrt(3f);
                Rect  thumbRect = new Rect(center.x - rw * 0.5f, center.y - rh * 0.5f, rw, rh);
                Rect  uvRect    = new Rect(0f, (1f - HEX_H_FRAC) * 0.5f, 1f, HEX_H_FRAC);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(baseRot * 60f, center);
                GUI.DrawTextureWithTexCoords(thumbRect, thumb, uvRect);
                GUI.matrix = saved;
            }

            if (isRepaint && !hasThumb)
            {
                Vector3[] inner = HexEditorUtils.GetHexVerts(center, radius - 2f);
                Handles.color = PAINTED_RIM;
                HexEditorUtils.DrawHexOutline(inner, 1.5f);
            }

            if (isRepaint)
            {
                if (isOrigin)
                {
                    Handles.color = ORIGIN_OUTLINE;
                    HexEditorUtils.DrawHexOutline(verts, 2.5f);
                }
                else
                {
                    Handles.color = hasThumb ? GHOST_OUTLINE : PAINTED_OUTLINE;
                    HexEditorUtils.DrawHexOutline(verts, hasThumb ? 1.2f : 2f);
                }

                if (!onOverlay && (baseRot != 0 || isActiveGesture))
                    HexEditorUtils.DrawRotationBadge(center, radius, baseRot, isActiveGesture);
            }
        }

        if (onOverlay && overlayExists && activeLayer != null)
        {
            TilePalette pal   = activeLayer.Palette;
            GameObject  prefab = pal != null ? pal.Get(overlayVal) : null;
            Texture2D   thumb  = GetPrefabThumb(prefab);

            if (isRepaint && thumb == null)
            {
                Handles.color = new Color(0f, 0f, 0f, 0.30f);
                Handles.DrawAAConvexPolygon(verts);
                Handles.color = Color.HSVToRGB((overlayVal * 0.137f) % 1f, 0.65f, 0.90f);
                Handles.DrawAAConvexPolygon(HexEditorUtils.GetHexVerts(center, radius * 0.78f));
            }

            if (thumb != null)
            {
                const float HEX_H_FRAC = 0.8660254f;
                float rw = radius * 2f, rh = radius * Mathf.Sqrt(3f);
                Rect  thumbRect = new Rect(center.x - rw * 0.5f, center.y - rh * 0.5f, rw, rh);
                Rect  uvRect    = new Rect(0f, (1f - HEX_H_FRAC) * 0.5f, 1f, HEX_H_FRAC);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(overlayRot * 60f, center);
                GUI.DrawTextureWithTexCoords(thumbRect, thumb, uvRect);
                GUI.matrix = saved;
            }

            if (isRepaint)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.50f);
                HexEditorUtils.DrawHexOutline(verts, 2f);
            }
        }

        if (isRepaint && hover)
        {
            bool  editExists = onOverlay ? overlayExists : baseExists;
            Color hFill, hRing;
            if      (_mapTool == MapTool.Erase && editExists)  { hFill = HOVER_REMOVE_FILL; hRing = HOVER_REMOVE_RING; }
            else if (_mapTool == MapTool.Paint && !editExists)  { hFill = HOVER_PLACE_FILL;  hRing = HOVER_PLACE_RING; }
            else                                                 { hFill = HOVER_FILL;        hRing = HOVER_RING; }
            Handles.color = hFill;
            Handles.DrawAAConvexPolygon(verts);
            Handles.color = hRing;
            HexEditorUtils.DrawHexOutline(verts, 3f);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // LAYER MANAGEMENT
    // ════════════════════════════════════════════════════════════════

    private void AddLayer()
    {
        string boardPath = AssetDatabase.GetAssetPath(_board);
        string dir       = System.IO.Path.GetDirectoryName(boardPath);
        int    n         = LayerCount + 1;
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/Layer_{n}.asset");

        BoardLayerData newLayer = CreateInstance<BoardLayerData>();
        AssetDatabase.CreateAsset(newLayer, assetPath);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(_board, "Add Layer");
        if (_board.layers == null) _board.layers = new List<BoardLayerData>();
        _board.layers.Add(newLayer);
        _activeLayerIdx = _board.layers.Count - 1;
        _brush = 1;
        EditorUtility.SetDirty(_board);
        BuildActiveLayerLookup();
        Repaint();
    }

    private void RemoveActiveLayer()
    {
        if (_activeLayerIdx < 0 || _activeLayerIdx >= LayerCount) return;
        string layerName = _board.layers[_activeLayerIdx]?.name ?? "this layer";
        if (!EditorUtility.DisplayDialog("Remove Layer",
            $"Remove '{layerName}' from the board? The asset file is kept on disk.",
            "Remove", "Cancel")) return;

        Undo.RecordObject(_board, "Remove Layer");
        _board.layers.RemoveAt(_activeLayerIdx);
        _activeLayerIdx = Mathf.Clamp(_activeLayerIdx, 0, Mathf.Max(0, LayerCount - 1));
        EditorUtility.SetDirty(_board);
        BuildActiveLayerLookup();
        Repaint();
    }

    private void RepaintLiveTile(int q, int r)
    {
        BoardLayerData baseLayer = BaseLayer;
        if (baseLayer == null) return;
        int idx = baseLayer.IndexOf(q, r);
        BoardSpawner[] spawners = Object.FindObjectsByType<BoardSpawner>(FindObjectsSortMode.None);
        foreach (BoardSpawner spawner in spawners)
        {
            if (spawner.data != _board) continue;
            Transform child = null;
            for (int i = 0; i < spawner.transform.childCount; i++)
            {
                Transform c = spawner.transform.GetChild(i);
                if (c.name.StartsWith($"Tile_{q}_{r}_")) { child = c; break; }
            }
            if (child == null) continue;
            byte rot = idx >= 0 ? baseLayer.Cells[idx].rotation : (byte)0;
            Undo.RecordObject(child, "Rotate Tile");
            child.localRotation = Quaternion.Euler(0f, rot * 60f, 0f);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // ROUTE CANVAS (logic unchanged)
    // ════════════════════════════════════════════════════════════════

    private void DrawRouteCanvas()
    {
        float sqrt3 = Mathf.Sqrt(3f);
        Rect area = BeginHexCanvas(out Vector2 origin, out float cell,
            out int minQ, out int maxQ, out float minPy, out float maxPy);

        _nodeScreenPos.Clear();
        Event e = Event.current;

        bool newHoverValid   = false;
        int  newHoverQ = 0, newHoverR = 0;
        bool newHoverIsNode  = false;
        int  newHoverNodeIdx = -1;
        bool changed         = false;

        for (int q = minQ; q <= maxQ; q++)
        {
            int rMin = Mathf.FloorToInt(minPy / sqrt3 - q * 0.5f);
            int rMax = Mathf.CeilToInt(maxPy  / sqrt3 - q * 0.5f);
            for (int r = rMin; r <= rMax; r++)
            {
                float   px     = cell * 1.5f  * q;
                float   py     = cell * sqrt3 * (r + q * 0.5f);
                Vector2 center = origin + new Vector2(px, -py);

                bool isOnBoard = _boardLookup.ContainsKey((q, r));
                bool hasNode   = _nodeLookup.TryGetValue((q, r), out int nodeListIdx);

                if (hasNode)
                    _nodeScreenPos[_routeData.Nodes[nodeListIdx].Index] = center;

                bool hit = isOnBoard && HexEditorUtils.PointInFlatHex(e.mousePosition, center, cell);
                if (hit)
                {
                    newHoverValid    = true;
                    newHoverQ        = q;
                    newHoverR        = r;
                    newHoverIsNode   = hasNode;
                    newHoverNodeIdx  = hasNode ? _routeData.Nodes[nodeListIdx].Index : -1;

                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        if (_routeTool == RouteTool.Connect && hasNode)
                        {
                            _connectDragActive = true;
                            _connectSrcIdx     = _routeData.Nodes[nodeListIdx].Index;
                            _connectDragPos    = center;
                            e.Use();
                        }
                        else if (_routeTool != RouteTool.Connect)
                        {
                            if (ApplyRouteTool(q, r, hasNode, nodeListIdx))
                            {
                                changed          = true;
                                _validationDirty = true;
                                BuildNodeLookup();
                            }
                            if (hasNode) _selectedNodeIdx = _routeData.Nodes[nodeListIdx].Index;
                            e.Use();
                        }
                    }
                    else if (e.type == EventType.MouseUp && e.button == 0 && _connectDragActive)
                    {
                        if (hasNode)
                        {
                            int tgt = _routeData.Nodes[nodeListIdx].Index;
                            if (tgt != _connectSrcIdx)
                            {
                                RouteAddConnection(_connectSrcIdx, tgt);
                                changed          = true;
                                _validationDirty = true;
                            }
                        }
                        _connectDragActive = false;
                        e.Use(); Repaint();
                    }
                }

                DrawRouteHexCell(center, cell, isOnBoard, hasNode, nodeListIdx, hit, q == 0 && r == 0);
            }
        }

        if (_connectDragActive)
        {
            if (e.type == EventType.MouseDrag) { _connectDragPos = e.mousePosition; Repaint(); }
            else if (e.type == EventType.MouseUp && e.button == 0) { _connectDragActive = false; Repaint(); }
        }

        if (e.type == EventType.Repaint)
        {
            DrawAllRouteArrows(cell);
            if (_connectDragActive && _nodeScreenPos.TryGetValue(_connectSrcIdx, out Vector2 srcPos))
            {
                Handles.color = CONNECT_DRAG_COLOR;
                Handles.DrawAAPolyLine(2.2f,
                    new Vector3(srcPos.x, srcPos.y, 0f),
                    new Vector3(_connectDragPos.x, _connectDragPos.y, 0f));
            }
        }

        _hoverValid   = newHoverValid;
        _hoverQ       = newHoverQ;
        _hoverR       = newHoverR;
        _hoverIsNode  = newHoverIsNode;
        _hoverNodeIdx = newHoverNodeIdx;

        EndHexCanvas(changed, _routeData);
    }

    private bool ApplyRouteTool(int q, int r, bool hasNode, int nodeListIdx)
    {
        switch (_routeTool)
        {
            case RouteTool.Place:
                if (hasNode) return false;
                Undo.RecordObject(_routeData, "Place Node");
                _routeData.Nodes.Add(new NodeEntry(_routeData.NextAvailableIndex(), NodeType.Normal, q, r));
                return true;

            case RouteTool.Remove:
                if (!hasNode) return false;
                int removedIdx = _routeData.Nodes[nodeListIdx].Index;
                Undo.RecordObject(_routeData, "Remove Node");
                _routeData.Nodes.RemoveAt(nodeListIdx);
                RouteRemoveAllRefsTo(removedIdx);
                if (_selectedNodeIdx == removedIdx) _selectedNodeIdx = -1;
                return true;
        }
        return false;
    }

    private void DrawRouteHexCell(Vector2 center, float cell, bool isOnBoard, bool hasNode,
        int nodeListIdx, bool hover, bool isOrigin)
    {
        if (Event.current.type != EventType.Repaint) return;

        float     pad   = cell * 0.10f;
        Vector3[] verts = HexEditorUtils.GetHexVerts(center, cell - pad);

        if (!isOnBoard)
        {
            Handles.color = GHOST_FILL;
            Handles.DrawAAConvexPolygon(verts);
            Handles.color = GHOST_OUTLINE;
            HexEditorUtils.DrawHexOutline(verts, 1f);
            return;
        }

        if (hasNode)
        {
            NodeEntry node     = _routeData.Nodes[nodeListIdx];
            Color     nc       = NodeTypeColor(node.Type);
            Color     darkFill = new Color(nc.r * 0.35f, nc.g * 0.35f, nc.b * 0.35f, 1f);
            Handles.color = darkFill;
            Handles.DrawAAConvexPolygon(verts);
            Vector3[] rim = HexEditorUtils.GetHexVerts(center, cell - pad - 3f);
            Handles.color = nc;
            HexEditorUtils.DrawHexOutline(rim, 2.5f);
        }
        else
        {
            Handles.color = BOARD_CELL_FILL;
            Handles.DrawAAConvexPolygon(verts);
            Handles.color = BOARD_CELL_OUTLINE;
            HexEditorUtils.DrawHexOutline(verts, 1.2f);
        }

        if (hover)
        {
            Color hFill, hRing;
            if      (_routeTool == RouteTool.Place   && !hasNode) { hFill = HOVER_PLACE_FILL;   hRing = HOVER_PLACE_RING; }
            else if (_routeTool == RouteTool.Remove  &&  hasNode) { hFill = HOVER_REMOVE_FILL;  hRing = HOVER_REMOVE_RING; }
            else if (_routeTool == RouteTool.Connect &&  hasNode) { hFill = HOVER_CONNECT_FILL; hRing = HOVER_CONNECT_RING; }
            else                                                   { hFill = HOVER_FILL;         hRing = HOVER_RING; }
            Handles.color = hFill;
            Handles.DrawAAConvexPolygon(verts);
            Handles.color = hRing;
            HexEditorUtils.DrawHexOutline(verts, 2.5f);
        }

        if (isOrigin)
        {
            Handles.color = ORIGIN_OUTLINE;
            HexEditorUtils.DrawHexOutline(verts, 2f);
        }

        if (hasNode)
        {
            NodeEntry node = _routeData.Nodes[nodeListIdx];
            if (_selectedNodeIdx == node.Index)
            {
                Handles.color = SELECTED_OUTLINE;
                HexEditorUtils.DrawHexOutline(verts, 3.5f);
            }
            float badgeSize = cell * 0.55f;
            Rect  badgeRect = new Rect(
                center.x - badgeSize * 0.5f, center.y - badgeSize * 0.5f, badgeSize, badgeSize);
            GUI.Label(badgeRect, node.Index.ToString(), _nodeBadgeStyle);
        }
    }

    private void DrawAllRouteArrows(float cell)
    {
        if (_routeData.Nodes == null) return;
        foreach (NodeEntry node in _routeData.Nodes)
        {
            if (node.NextIndices == null || node.NextIndices.Length == 0) continue;
            if (!_nodeScreenPos.TryGetValue(node.Index, out Vector2 src)) continue;
            bool isSrcSelected = _selectedNodeIdx == node.Index;
            foreach (int nxt in node.NextIndices)
            {
                if (!_nodeScreenPos.TryGetValue(nxt, out Vector2 dst)) continue;
                DrawRouteArrow(src, dst, cell, isSrcSelected);
            }
        }
    }

    private void DrawRouteArrow(Vector2 from, Vector2 to, float cell, bool highlighted)
    {
        Vector2 delta = to - from;
        float   dist  = delta.magnitude;
        if (dist < 0.001f) return;
        Vector2 dir  = delta / dist;
        Vector2 perp = new Vector2(-dir.y, dir.x) * 5f;
        float   edge = cell * 0.68f;
        Vector2 start = from + dir * edge + perp;
        Vector2 end   = to   - dir * edge + perp;
        Handles.color = highlighted ? ARROW_SELECTED : ARROW_COLOR;
        Handles.DrawAAPolyLine(highlighted ? 2.5f : 1.8f,
            new Vector3(start.x, start.y, 0f), new Vector3(end.x, end.y, 0f));
        HexEditorUtils.DrawArrowHead(end, dir, cell * 0.18f);
    }

    private void RouteAddConnection(int fromIdx, int toIdx)
    {
        for (int i = 0; i < _routeData.Nodes.Count; i++)
        {
            if (_routeData.Nodes[i].Index != fromIdx) continue;
            NodeEntry entry = _routeData.Nodes[i];
            if (entry.NextIndices != null)
                foreach (int n in entry.NextIndices)
                    if (n == toIdx) return;

            Undo.RecordObject(_routeData, "Connect Nodes");
            int   oldLen  = entry.NextIndices?.Length ?? 0;
            int[] newNext = new int[oldLen + 1];
            if (entry.NextIndices != null) System.Array.Copy(entry.NextIndices, newNext, oldLen);
            newNext[oldLen]   = toIdx;
            entry.NextIndices = newNext;
            _routeData.Nodes[i] = entry;
            EditorUtility.SetDirty(_routeData);
            return;
        }
    }

    private void RouteRemoveConnectionAt(int nodeListIdx, int connArrayIdx)
    {
        NodeEntry entry = _routeData.Nodes[nodeListIdx];
        if (entry.NextIndices == null || connArrayIdx >= entry.NextIndices.Length) return;
        Undo.RecordObject(_routeData, "Disconnect Nodes");
        var list = new List<int>(entry.NextIndices);
        list.RemoveAt(connArrayIdx);
        entry.NextIndices = list.ToArray();
        _routeData.Nodes[nodeListIdx] = entry;
        EditorUtility.SetDirty(_routeData);
    }

    private void RouteRemoveAllRefsTo(int removedIdx)
    {
        for (int i = 0; i < _routeData.Nodes.Count; i++)
        {
            NodeEntry entry = _routeData.Nodes[i];
            if (entry.NextIndices == null) continue;
            bool found = false;
            foreach (int n in entry.NextIndices) if (n == removedIdx) { found = true; break; }
            if (!found) continue;
            var list = new List<int>(entry.NextIndices);
            list.Remove(removedIdx);
            entry.NextIndices = list.ToArray();
            _routeData.Nodes[i] = entry;
        }
    }

    private void HandleRouteKeyboard()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        switch (e.keyCode)
        {
            case KeyCode.P:         _routeTool = RouteTool.Place;   e.Use(); Repaint(); break;
            case KeyCode.C:         _routeTool = RouteTool.Connect; e.Use(); Repaint(); break;
            case KeyCode.Delete:
            case KeyCode.Backspace: _routeTool = RouteTool.Remove;  e.Use(); Repaint(); break;
            case KeyCode.T:
                if (_hoverValid && _hoverIsNode) CycleNodeType(_hoverQ, _hoverR);
                break;
            case KeyCode.Escape:
                if (_connectDragActive) { _connectDragActive = false; e.Use(); Repaint(); }
                break;
        }
    }

    private void CycleNodeType(int q, int r)
    {
        if (!_nodeLookup.TryGetValue((q, r), out int listIdx)) return;
        Undo.RecordObject(_routeData, "Cycle Node Type");
        NodeEntry entry    = _routeData.Nodes[listIdx];
        int       typeCount = System.Enum.GetValues(typeof(NodeType)).Length;
        entry.Type = (NodeType)(((int)entry.Type + 1) % typeCount);
        _routeData.Nodes[listIdx] = entry;
        _validationDirty = true;
        EditorUtility.SetDirty(_routeData);
        Repaint();
    }

    // ════════════════════════════════════════════════════════════════
    // ROUTE SIDEBAR
    // ════════════════════════════════════════════════════════════════

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(SIDEBAR_WIDTH));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Route Nodes", _sectionHeaderStyle);
        GUILayout.FlexibleSpace();
        int nodeCount = _routeData?.Count ?? 0;
        DrawColoredLabel(nodeCount.ToString(), ACCENT_ROUTE, EditorStyles.miniBoldLabel, GUILayout.Width(24f));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2f);
        _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

        if (_routeData.Nodes == null || _routeData.Nodes.Count == 0)
            EditorGUILayout.LabelField("No nodes placed yet.", EditorStyles.centeredGreyMiniLabel);
        else
        {
            var sorted = new List<(int listIdx, NodeEntry entry)>(_routeData.Nodes.Count);
            for (int i = 0; i < _routeData.Nodes.Count; i++)
                sorted.Add((i, _routeData.Nodes[i]));
            sorted.Sort((a, b) => a.entry.Index.CompareTo(b.entry.Index));
            foreach (var (listIdx, entry) in sorted)
                DrawNodeRow(listIdx, entry);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(4f);
        DrawValidationPanel();
        EditorGUILayout.EndVertical();
    }

    private void DrawNodeRow(int listIdx, NodeEntry entry)
    {
        bool  isSelected = _selectedNodeIdx == entry.Index;
        Color nc         = NodeTypeColor(entry.Type);
        Color rowBg      = isSelected
            ? new Color(nc.r * 0.25f, nc.g * 0.25f, nc.b * 0.25f, 0.55f)
            : new Color(0.18f, 0.18f, 0.20f, 0.35f);

        Rect rowRect = EditorGUILayout.BeginVertical(_sidebarRowStyle);
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rowRect, rowBg);
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3f, rowRect.height), nc);
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6f);
        DrawColoredLabel($"#{entry.Index}", nc, EditorStyles.miniBoldLabel, GUILayout.Width(28f));

        NodeType newType = (NodeType)EditorGUILayout.EnumPopup(entry.Type, GUILayout.Width(58f));
        if (newType != entry.Type)
        {
            Undo.RecordObject(_routeData, "Change Node Type");
            NodeEntry m = entry; m.Type = newType;
            _routeData.Nodes[listIdx] = m;
            EditorUtility.SetDirty(_routeData);
            _validationDirty = true;
        }

        GUILayout.FlexibleSpace();
        bool selectClicked = GUILayout.Button(isSelected ? "◉" : "◎",
            EditorStyles.miniButton, GUILayout.Width(20f));
        if (selectClicked) { _selectedNodeIdx = isSelected ? -1 : entry.Index; Repaint(); }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6f);
        string newLabel = EditorGUILayout.TextField(entry.Label ?? string.Empty, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();

        if (newLabel != (entry.Label ?? string.Empty))
        {
            Undo.RecordObject(_routeData, "Edit Node Label");
            NodeEntry m = entry; m.Label = newLabel;
            _routeData.Nodes[listIdx] = m;
            EditorUtility.SetDirty(_routeData);
        }

        if (entry.NextIndices != null && entry.NextIndices.Length > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6f);
            Color prev = GUI.contentColor;
            GUI.contentColor = new Color(0.6f, 0.6f, 0.65f);
            GUILayout.Label("→", EditorStyles.miniLabel, GUILayout.Width(12f));
            GUI.contentColor = prev;
            int removeAt = -1;
            for (int j = 0; j < entry.NextIndices.Length; j++)
            {
                DrawColoredLabel(entry.NextIndices[j].ToString(), ACCENT_ROUTE,
                    EditorStyles.miniLabel, GUILayout.Width(18f));
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(14f))) removeAt = j;
            }
            EditorGUILayout.EndHorizontal();
            if (removeAt >= 0) { RouteRemoveConnectionAt(listIdx, removeAt); _validationDirty = true; Repaint(); }
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label("no connections", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(2f);
    }

    private void DrawValidationPanel()
    {
        EditorGUILayout.LabelField("Validation", _sectionHeaderStyle);

        if (GUILayout.Button("Validate Route", GUILayout.Height(22f)))
            RunValidation();

        if (_validationDirty)
        {
            DrawInfoBox("Route modified — press Validate to refresh.", new Color(0.7f, 0.7f, 0.2f));
        }
        else if (_validationErrors.Count == 0)
        {
            DrawInfoBox("✓  Route is valid", new Color(0.25f, 0.85f, 0.35f));
        }
        else
        {
            foreach (string err in _validationErrors)
                GUILayout.Label($"●  {err}", _errorLabelStyle);
        }
    }

    private void RunValidation()
    {
        _validationErrors.Clear();
        if (_routeData?.Nodes != null)
            _validationErrors.AddRange(new RouteSystem(_routeData.Nodes).Validate());
        _validationDirty = false;
        Repaint();
    }

    // ════════════════════════════════════════════════════════════════
    // PREVIEW THUMBNAILS
    // ════════════════════════════════════════════════════════════════

    private Texture2D GetPrefabThumb(GameObject prefab)
    {
        if (prefab == null) return null;
        if (_topDownCache.TryGetValue(prefab, out Texture2D cached) && cached != null)
            return cached;
        Texture2D rendered = RenderTopDown(prefab);
        if (rendered != null) { _topDownCache[prefab] = rendered; return rendered; }
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
            if (renderers == null || renderers.Length == 0) return null;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float extent = Mathf.Max(b.extents.x, b.extents.z);
            if (extent < 0.001f) extent = 0.5f;

            go.transform.rotation          = Quaternion.Euler(0f, TILE_PREVIEW_Y_ROTATION, 0f);
            _previewCam.orthographicSize    = extent;
            _previewCam.transform.position  = b.center + Vector3.up * (b.extents.y + 20f);
            _previewCam.transform.rotation  = Quaternion.Euler(90f, 0f, 0f);

            RenderTexture prev = RenderTexture.active;
            _previewCam.targetTexture = _previewRT;
            _previewCam.Render();
            _previewCam.targetTexture = null;

            RenderTexture.active = _previewRT;
            Texture2D result = new Texture2D(PREVIEW_TEXTURE_SIZE, PREVIEW_TEXTURE_SIZE, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, PREVIEW_TEXTURE_SIZE, PREVIEW_TEXTURE_SIZE), 0, 0);
            result.Apply();
            RenderTexture.active = prev;
            return result;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MapEditor] Top-down render failed for '{prefab.name}': {ex.Message}");
            return null;
        }
        finally { if (go != null) DestroyImmediate(go); }
    }

    private void EnsurePreviewScene()
    {
        if (!_previewScene.IsValid())
            _previewScene = EditorSceneManager.NewPreviewScene();

        if (_previewCam == null)
        {
            GameObject camGO = new GameObject("MapEditor_PreviewCam");
            camGO.hideFlags  = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(camGO, _previewScene);
            _previewCam = camGO.AddComponent<Camera>();
            _previewCam.enabled         = false;
            _previewCam.orthographic    = true;
            _previewCam.clearFlags      = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCam.nearClipPlane   = 0.01f;
            _previewCam.farClipPlane    = 200f;
            _previewCam.scene           = _previewScene;
        }

        if (_previewKeyLight == null)
        {
            GameObject keyGO = new GameObject("MapEditor_KeyLight");
            keyGO.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(keyGO, _previewScene);
            _previewKeyLight = keyGO.AddComponent<Light>();
            _previewKeyLight.type      = LightType.Directional;
            _previewKeyLight.intensity = 1.25f;
            _previewKeyLight.color     = Color.white;
            _previewKeyLight.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
        }

        if (_previewFillLight == null)
        {
            GameObject fillGO = new GameObject("MapEditor_FillLight");
            fillGO.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(fillGO, _previewScene);
            _previewFillLight = fillGO.AddComponent<Light>();
            _previewFillLight.type      = LightType.Directional;
            _previewFillLight.intensity = 0.45f;
            _previewFillLight.color     = new Color(0.85f, 0.88f, 1f);
            _previewFillLight.transform.rotation = Quaternion.Euler(-40f, -30f, 0f);
        }

        if (_previewRT == null)
        {
            _previewRT = new RenderTexture(PREVIEW_TEXTURE_SIZE, PREVIEW_TEXTURE_SIZE, 16, RenderTextureFormat.ARGB32);
            _previewRT.antiAliasing = 4;
            _previewRT.Create();
        }
    }

    private void OnDisable()
    {
        foreach (var kv in _topDownCache)
            if (kv.Value != null) DestroyImmediate(kv.Value);
        _topDownCache.Clear();

        if (_previewRT      != null) { _previewRT.Release(); DestroyImmediate(_previewRT); _previewRT = null; }
        if (_previewCam      != null) { DestroyImmediate(_previewCam.gameObject);       _previewCam      = null; }
        if (_previewKeyLight  != null) { DestroyImmediate(_previewKeyLight.gameObject);  _previewKeyLight  = null; }
        if (_previewFillLight != null) { DestroyImmediate(_previewFillLight.gameObject); _previewFillLight = null; }
        if (_previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(_previewScene);
    }

    // ════════════════════════════════════════════════════════════════
    // STYLES & UTILITIES
    // ════════════════════════════════════════════════════════════════

    private void BuildStyles()
    {
        if (_cellValueLabel == null)
        {
            _cellValueLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 10,
            };
            _cellValueLabel.normal.textColor = new Color(0.8f, 0.8f, 0.85f, 0.9f);
        }

        if (_nodeBadgeStyle == null)
        {
            _nodeBadgeStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 11,
            };
            _nodeBadgeStyle.normal.textColor = Color.white;
        }

        if (_errorLabelStyle == null)
        {
            _errorLabelStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _errorLabelStyle.normal.textColor = new Color(1f, 0.38f, 0.38f);
        }

        if (_validLabelStyle == null)
        {
            _validLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            _validLabelStyle.normal.textColor = new Color(0.35f, 0.85f, 0.45f);
        }

        if (_sidebarRowStyle == null)
            _sidebarRowStyle = new GUIStyle { margin = new RectOffset(0, 0, 1, 1) };

        if (_sectionHeaderStyle == null)
        {
            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleLeft,
            };
            _sectionHeaderStyle.normal.textColor = new Color(0.80f, 0.82f, 0.90f);
        }

        if (_statusLabelStyle == null)
        {
            _statusLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            _statusLabelStyle.normal.textColor = new Color(0.72f, 0.72f, 0.75f);
        }
    }

    private string PaletteName(TilePalette pal, int value)
    {
        if (pal == null) return value == 0 ? "(empty)" : $"#{value}";
        GameObject go = pal.Get(value);
        return go != null ? go.name : (value == 0 ? "(empty)" : $"#{value}");
    }
}
