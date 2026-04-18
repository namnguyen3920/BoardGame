using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class HexEditorUtils
{
    public const float BaseCellPixelRadius = 34f;

    public static Vector3[] GetHexVerts(Vector2 center, float radius)
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

    public static void DrawHexOutline(Vector3[] verts, float thickness)
    {
        Vector3[] loop = new Vector3[7];
        for (int i = 0; i < 6; i++) loop[i] = verts[i];
        loop[6] = verts[0];
        Handles.DrawAAPolyLine(thickness, loop);
    }

    public static bool PointInFlatHex(Vector2 point, Vector2 center, float radius)
    {
        float dx = Mathf.Abs(point.x - center.x);
        float dy = Mathf.Abs(point.y - center.y);
        const float SQRT3_OVER_2 = 0.8660254f;
        const float INV_SQRT3 = 0.5773503f;
        if (dy > radius * SQRT3_OVER_2) return false;
        if (dx + dy * INV_SQRT3 > radius) return false;
        return true;
    }

    public static void DrawArrowHead(Vector2 tip, Vector2 dir, float size)
    {
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector3 a = new Vector3(tip.x - dir.x * size + perp.x * size * 0.4f, tip.y - dir.y * size + perp.y * size * 0.4f, 0f);
        Vector3 b = new Vector3(tip.x - dir.x * size - perp.x * size * 0.4f, tip.y - dir.y * size - perp.y * size * 0.4f, 0f);
        Handles.DrawAAPolyLine(2f, a, new Vector3(tip.x, tip.y, 0f), b);
    }

    public static void DrawRotationBadge(Vector2 center, float radius, byte rotation, bool isActive)
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

    public static void DrawAxisLines(Vector2 origin, Rect area, float cell, int minQ, int maxQ)
    {
        Handles.color = new Color(0.32f, 0.42f, 0.56f, 0.35f);

        Handles.DrawAAPolyLine(1.5f,
            new Vector3(origin.x, area.y, 0f),
            new Vector3(origin.x, area.yMax, 0f));

        float sqrt3 = Mathf.Sqrt(3f);
        Vector2 rLine0 = origin + new Vector2(cell * 1.5f * (minQ - 0.5f), -cell * sqrt3 * (minQ - 0.5f) * 0.5f);
        Vector2 rLine1 = origin + new Vector2(cell * 1.5f * (maxQ + 0.5f), -cell * sqrt3 * (maxQ + 0.5f) * 0.5f);
        rLine0.y = Mathf.Clamp(rLine0.y, area.y, area.yMax);
        rLine1.y = Mathf.Clamp(rLine1.y, area.y, area.yMax);
        Handles.DrawAAPolyLine(1.5f,
            new Vector3(rLine0.x, rLine0.y, 0f),
            new Vector3(rLine1.x, rLine1.y, 0f));
    }

    public static void DrawCenterDecoration(Vector2 origin, float cell, float sqrt3)
    {
        float arrowLen = cell * 2.0f;
        Vector2 qTip = origin + new Vector2(arrowLen, 0f);
        Vector2 rTip = origin + new Vector2(0f, -sqrt3 * cell);

        Color axisArrowColor = new Color(0.55f, 0.75f, 0.95f, 0.75f);
        Handles.color = axisArrowColor;
        Handles.DrawAAPolyLine(2f, new Vector3(origin.x, origin.y, 0f), new Vector3(qTip.x, qTip.y, 0f));
        Handles.DrawAAPolyLine(2f, new Vector3(origin.x, origin.y, 0f), new Vector3(rTip.x, rTip.y, 0f));

        DrawArrowHead(qTip, new Vector2(1f, 0f), cell * 0.20f);
        DrawArrowHead(rTip, new Vector2(0f, -1f), cell * 0.20f);

        Handles.Label(new Vector3(qTip.x + 5f, qTip.y - 7f, 0f), "q");
        Handles.Label(new Vector3(rTip.x + 5f, rTip.y - 7f, 0f), "r");

        Handles.color = new Color(axisArrowColor.r, axisArrowColor.g, axisArrowColor.b, 0.45f);
        Handles.DrawWireDisc(new Vector3(origin.x, origin.y, 0f), Vector3.forward, cell * 0.30f);
    }

    public static void DrawBoardBorder(Vector2 origin, float cell, float cMinPx, float cMaxPx, float cMinPy, float cMaxPy)
    {
        float margin = cell * 0.60f;
        float x0 = origin.x + cMinPx * cell - margin;
        float x1 = origin.x + cMaxPx * cell + margin;
        float y0 = origin.y + cMinPy * cell - margin;
        float y1 = origin.y + cMaxPy * cell + margin;

        Handles.color = new Color(0.55f, 0.75f, 0.95f, 0.50f);
        Handles.DrawAAPolyLine(2f,
            new Vector3(x0, y0, 0f),
            new Vector3(x1, y0, 0f),
            new Vector3(x1, y1, 0f),
            new Vector3(x0, y1, 0f),
            new Vector3(x0, y0, 0f));
    }

    public static void DrawRectBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    public static void ComputeCanvasBounds(List<HexCell> cells, int padding,
        out float minPx, out float maxPx, out float minPy, out float maxPy)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        if (cells != null && cells.Count > 0)
        {
            minPx = float.MaxValue; maxPx = float.MinValue;
            minPy = float.MaxValue; maxPy = float.MinValue;
            foreach (var c in cells)
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
            minPy = -sqrt3;  maxPy = sqrt3;
        }

        minPx -= padding * 1.5f;
        maxPx += padding * 1.5f;
        minPy -= padding * sqrt3;
        maxPy += padding * sqrt3;
    }

    public static Vector2 ComputeOrigin(Rect area, float allocW, float allocH,
        float w, float h, float cell, float minPx, float maxPy)
    {
        return new Vector2(
            area.x + (allocW - w) * 0.5f + cell - minPx * cell,
            area.y + (allocH - h) * 0.5f + cell + maxPy * cell);
    }
}
