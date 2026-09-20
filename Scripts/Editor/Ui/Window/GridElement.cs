using UnityEngine;
using UnityEngine.UIElements;

namespace XNodeEditor.Ui {
    public sealed class GridElement : VisualElement {
        public const float DefaultMinorStep = 20f;
        public const float DefaultMajorStep = 100f;

        public Vector2 Pan;
        public float Scale = 1f;
        public float MinorStep = DefaultMinorStep;
        public float MajorStep = DefaultMajorStep;
        public Color BackgroundColor = new Color32(48, 48, 48, 255);
        public Color MinorColor = new Color(1f, 1f, 1f, 0.02f);
        public Color MajorColor = new Color(1f, 1f, 1f, 0.05f);
        public Color OriginColor = new Color(1f, 1f, 1f, 0.1f);

        public GridElement() {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public static Vector2 Snap(Vector2 point, float step = DefaultMinorStep) {
            float size = Mathf.Max(1f, step);
            return new Vector2(
                Mathf.Round(point.x / size) * size,
                Mathf.Round(point.y / size) * size);
        }

        void OnGenerateVisualContent(MeshGenerationContext ctx) {
            Rect rect = contentRect;
            if (rect.width < 2f || rect.height < 2f) return;

            float scale = Mathf.Max(0.05f, Scale);
            float minor = Mathf.Max(1f, MinorStep);
            float major = Mathf.Max(minor, MajorStep);
            Painter2D painter = ctx.painter2D;

            float minorPx = minor * scale;
            if (minorPx >= 8f)
                DrawGrid(painter, rect, scale, minor, 1f, MinorColor, major, skipMajor: true);

            float majorPx = major * scale;
            if (majorPx >= 4f)
                DrawGrid(painter, rect, scale, major, 2f, MajorColor, major, skipMajor: false);

            DrawOrigin(painter, rect);
        }

        void DrawGrid(
            Painter2D painter,
            Rect rect,
            float scale,
            float step,
            float width,
            Color color,
            float majorStep,
            bool skipMajor) {
            painter.lineWidth = width;
            painter.strokeColor = color;
            painter.BeginPath();

            float xMin = -Pan.x / scale;
            float xMax = (rect.width - Pan.x) / scale;
            int ix0 = Mathf.FloorToInt(xMin / step);
            int ix1 = Mathf.CeilToInt(xMax / step);
            int count = 0;
            for (int i = ix0; i <= ix1; i++) {
                float g = i * step;
                if (IsOrigin(g)) continue;
                if (skipMajor && IsStep(g, majorStep)) continue;
                float x = Pan.x + g * scale;
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, rect.height));
                if (++count > 400) break;
            }

            float yMin = -Pan.y / scale;
            float yMax = (rect.height - Pan.y) / scale;
            int iy0 = Mathf.FloorToInt(yMin / step);
            int iy1 = Mathf.CeilToInt(yMax / step);
            for (int i = iy0; i <= iy1; i++) {
                float g = i * step;
                if (IsOrigin(g)) continue;
                if (skipMajor && IsStep(g, majorStep)) continue;
                float y = Pan.y + g * scale;
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(rect.width, y));
                if (++count > 800) break;
            }

            painter.Stroke();
        }

        void DrawOrigin(Painter2D painter, Rect rect) {
            painter.lineWidth = 2f;
            painter.strokeColor = OriginColor;
            painter.BeginPath();
            float x = Pan.x;
            if (x >= -2f && x <= rect.width + 2f) {
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, rect.height));
            }
            float y = Pan.y;
            if (y >= -2f && y <= rect.height + 2f) {
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(rect.width, y));
            }
            painter.Stroke();
        }

        static bool IsOrigin(float value) {
            return Mathf.Abs(value) < 0.01f;
        }

        static bool IsStep(float value, float step) {
            float n = value / step;
            return Mathf.Abs(n - Mathf.Round(n)) < 0.001f;
        }
    }
}
