using UnityEngine;
using Verse;

namespace CorvusProductionUI
{
    [StaticConstructorOnStartup]
    public static class CorvusStyle
    {
        public static readonly Color BgOuter = new Color(0.05f, 0.06f, 0.07f, 1f);
        public static readonly Color BgInner = new Color(0.08f, 0.09f, 0.11f, 1f);
        public static readonly Color Surface = new Color(0.11f, 0.13f, 0.15f, 0.98f);
        public static readonly Color SurfaceAlt = new Color(0.14f, 0.16f, 0.19f, 0.98f);
        public static readonly Color SurfaceInset = new Color(0.08f, 0.10f, 0.12f, 1f);
        public static readonly Color Border = new Color(0.24f, 0.29f, 0.33f, 0.90f);
        public static readonly Color BorderSoft = new Color(0.18f, 0.22f, 0.26f, 0.75f);

        public static readonly Color TextPrimary = new Color(0.88f, 0.91f, 0.94f, 1f);
        public static readonly Color TextSecondary = new Color(0.63f, 0.69f, 0.75f, 1f);
        public static readonly Color TextMuted = new Color(0.45f, 0.50f, 0.55f, 1f);

        public static readonly Color Accent = new Color(0.16f, 0.79f, 0.95f, 1f);
        public static readonly Color AccentDim = new Color(0.11f, 0.43f, 0.53f, 1f);
        public static readonly Color Warning = new Color(0.80f, 0.62f, 0.28f, 1f);
        public static readonly Color Danger = new Color(0.76f, 0.31f, 0.31f, 1f);
        public static readonly Color Success = new Color(0.38f, 0.82f, 0.62f, 1f);

        private static Texture2D solidTex;
        private static Texture2D windowGradientTex;
        private static Texture2D panelGradientTex;
        private static Texture2D buttonGradientTex;
        private static Texture2D panelStripeTex;
        private static Texture2D scrollTrackTex;
        private static Texture2D scrollThumbTex;
        private static Texture2D scrollThumbHoverTex;
        private static Texture2D scrollButtonTex;
        private static GUIStyle verticalScrollbarStyle;
        private static GUIStyle verticalScrollbarThumbStyle;
        private static GUIStyle verticalScrollbarButtonStyle;
        private static GUIStyle oldVerticalScrollbarStyle;
        private static GUIStyle oldVerticalScrollbarThumbStyle;
        private static GUIStyle oldVerticalScrollbarUpButtonStyle;
        private static GUIStyle oldVerticalScrollbarDownButtonStyle;
        private static int scrollStyleDepth;

        public static Texture2D SolidTex
        {
            get
            {
                if (solidTex == null)
                {
                    solidTex = new Texture2D(1, 1);
                    solidTex.SetPixel(0, 0, Color.white);
                    solidTex.Apply();
                }
                return solidTex;
            }
        }

        public static Texture2D WindowGradientTex => windowGradientTex ?? (windowGradientTex = CreateVerticalGradient(64,
            new Color(0.10f, 0.11f, 0.13f, 1f),
            new Color(0.04f, 0.05f, 0.06f, 1f)));

        public static Texture2D PanelGradientTex => panelGradientTex ?? (panelGradientTex = CreateVerticalGradient(32,
            new Color(0.15f, 0.17f, 0.20f, 1f),
            new Color(0.10f, 0.12f, 0.14f, 1f)));

        public static Texture2D ButtonGradientTex => buttonGradientTex ?? (buttonGradientTex = CreateVerticalGradient(24,
            new Color(0.18f, 0.21f, 0.24f, 1f),
            new Color(0.12f, 0.14f, 0.17f, 1f)));

        public static Texture2D PanelStripeTex => panelStripeTex ?? (panelStripeTex = CreateStripeTexture(64,
            new Color(1f, 1f, 1f, 0.018f),
            new Color(1f, 1f, 1f, 0f)));

        public static Texture2D ScrollTrackTex => scrollTrackTex ?? (scrollTrackTex = CreateVerticalGradient(24,
            new Color(0.10f, 0.11f, 0.13f, 1f),
            new Color(0.07f, 0.08f, 0.10f, 1f)));

        public static Texture2D ScrollThumbTex => scrollThumbTex ?? (scrollThumbTex = CreateVerticalGradient(24,
            new Color(0.34f, 0.38f, 0.42f, 1f),
            new Color(0.24f, 0.27f, 0.31f, 1f)));

        public static Texture2D ScrollThumbHoverTex => scrollThumbHoverTex ?? (scrollThumbHoverTex = CreateVerticalGradient(24,
            new Color(0.42f, 0.47f, 0.52f, 1f),
            new Color(0.30f, 0.35f, 0.40f, 1f)));

        public static Texture2D ScrollButtonTex => scrollButtonTex ?? (scrollButtonTex = CreateVerticalGradient(16,
            new Color(0.13f, 0.14f, 0.16f, 1f),
            new Color(0.10f, 0.11f, 0.13f, 1f)));

        public static Texture2D CreateVerticalGradient(int height, Color top, Color bottom)
        {
            var tex = new Texture2D(1, height);
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public static Texture2D CreateHorizontalGradient3(int width, Color left, Color center, Color right)
        {
            var tex = new Texture2D(width, 1);
            int half = width / 2;
            for (int x = 0; x < width; x++)
            {
                Color c;
                if (x < half)
                {
                    float t = (float)x / half;
                    c = Color.Lerp(left, center, t);
                }
                else
                {
                    float t = (float)(x - half) / half;
                    c = Color.Lerp(center, right, t);
                }
                tex.SetPixel(x, 0, c);
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        public static Texture2D CreateStripeTexture(int size, Color stripe, Color transparent)
        {
            var tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int d = (x + y) % 18;
                    tex.SetPixel(x, y, d < 2 ? stripe : transparent);
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        public static void DrawWindowBackground(Rect rect)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, WindowGradientTex, ScaleMode.StretchToFill);
            GUI.DrawTexture(rect, PanelStripeTex, ScaleMode.StretchToFill);

            GUI.color = new Color(0f, 0f, 0f, 0.18f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, rect.height * 0.18f), SolidTex);

            DrawBorder(rect, Border, 1f);
            GUI.color = Color.white;
        }

        private static void EnsureScrollbarStyles()
        {
            if (verticalScrollbarStyle != null) return;

            verticalScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar)
            {
                fixedWidth = 12f,
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0)
            };
            verticalScrollbarStyle.normal.background = ScrollTrackTex;
            verticalScrollbarStyle.hover.background = ScrollTrackTex;
            verticalScrollbarStyle.active.background = ScrollTrackTex;

            verticalScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb)
            {
                fixedWidth = 12f,
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(0, 0, 0, 0)
            };
            verticalScrollbarThumbStyle.normal.background = ScrollThumbTex;
            verticalScrollbarThumbStyle.hover.background = ScrollThumbHoverTex;
            verticalScrollbarThumbStyle.active.background = ScrollThumbHoverTex;

            verticalScrollbarButtonStyle = new GUIStyle(GUI.skin.verticalScrollbarUpButton)
            {
                fixedWidth = 12f,
                fixedHeight = 12f,
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };
            verticalScrollbarButtonStyle.normal.background = ScrollButtonTex;
            verticalScrollbarButtonStyle.hover.background = ScrollTrackTex;
            verticalScrollbarButtonStyle.active.background = ScrollTrackTex;
        }

        public static void BeginStyledScrollView(Rect outRect, ref Vector2 scrollPosition, Rect viewRect)
        {
            EnsureScrollbarStyles();

            if (scrollStyleDepth == 0)
            {
                oldVerticalScrollbarStyle = GUI.skin.verticalScrollbar;
                oldVerticalScrollbarThumbStyle = GUI.skin.verticalScrollbarThumb;
                oldVerticalScrollbarUpButtonStyle = GUI.skin.verticalScrollbarUpButton;
                oldVerticalScrollbarDownButtonStyle = GUI.skin.verticalScrollbarDownButton;

                GUI.skin.verticalScrollbar = verticalScrollbarStyle;
                GUI.skin.verticalScrollbarThumb = verticalScrollbarThumbStyle;
                GUI.skin.verticalScrollbarUpButton = verticalScrollbarButtonStyle;
                GUI.skin.verticalScrollbarDownButton = verticalScrollbarButtonStyle;
            }

            scrollStyleDepth++;
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        }

        public static void EndStyledScrollView()
        {
            Widgets.EndScrollView();

            scrollStyleDepth = Mathf.Max(0, scrollStyleDepth - 1);
            if (scrollStyleDepth == 0)
            {
                GUI.skin.verticalScrollbar = oldVerticalScrollbarStyle;
                GUI.skin.verticalScrollbarThumb = oldVerticalScrollbarThumbStyle;
                GUI.skin.verticalScrollbarUpButton = oldVerticalScrollbarUpButtonStyle;
                GUI.skin.verticalScrollbarDownButton = oldVerticalScrollbarDownButtonStyle;
            }
        }

        public static void DrawPanel(Rect rect, bool inset = false)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, inset ? SolidTex : PanelGradientTex, ScaleMode.StretchToFill);

            if (inset)
            {
                GUI.color = SurfaceInset;
                GUI.DrawTexture(rect, SolidTex);
            }

            if (!inset)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.10f);
                GUI.DrawTexture(rect, PanelStripeTex, ScaleMode.StretchToFill);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.03f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), SolidTex);

            DrawBorder(rect, inset ? BorderSoft : Border, 1f);
            GUI.color = Color.white;
        }

        public static void DrawSectionHeader(Rect rect, string label)
        {
            GUI.color = TextPrimary;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, label.ToUpperInvariant());

            float textWidth = Text.CalcSize(label.ToUpperInvariant()).x + 14f;
            GUI.color = new Color(AccentDim.r, AccentDim.g, AccentDim.b, 0.45f);
            GUI.DrawTexture(new Rect(rect.x + textWidth, rect.center.y, rect.width - textWidth, 1f), SolidTex);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        public static void DrawSeparator(Rect rect)
        {
            GUI.color = BorderSoft;
            GUI.DrawTexture(rect, SolidTex);
            GUI.color = Color.white;
        }

        public static void DrawListRow(Rect rect, float hoverT)
        {
            GUI.color = Color.Lerp(SurfaceInset, SurfaceAlt, hoverT * 0.65f);
            GUI.DrawTexture(rect, SolidTex);

            if (hoverT > 0f)
            {
                GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.06f * hoverT);
                GUI.DrawTexture(rect, SolidTex);
            }

            GUI.color = Color.Lerp(BorderSoft, AccentDim, hoverT * 0.4f);
            DrawBorder(rect, GUI.color, 1f);
            GUI.color = Color.white;
        }

        public static bool DrawButton(Rect rect, string label, float hoverT, bool enabled = true, bool active = false)
        {
            Color bg = active
                ? Color.Lerp(new Color(0.13f, 0.22f, 0.26f), new Color(0.14f, 0.28f, 0.33f), hoverT)
                : Color.Lerp(new Color(0.14f, 0.16f, 0.19f), new Color(0.18f, 0.21f, 0.24f), hoverT);

            if (!enabled)
            {
                bg = new Color(0.11f, 0.12f, 0.14f, 0.85f);
            }

            GUI.color = bg;
            GUI.DrawTexture(rect, ButtonGradientTex, ScaleMode.StretchToFill);

            GUI.color = enabled
                ? (active ? Accent : Border)
                : BorderSoft;
            DrawBorder(rect, GUI.color, 1f);

            GUI.color = enabled
                ? Color.Lerp(TextSecondary, TextPrimary, hoverT)
                : TextMuted;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return enabled && Widgets.ButtonInvisible(rect);
        }

        public static bool DrawSmallButton(Rect rect, string label, float hoverT, bool enabled = true, bool active = false)
        {
            Color bg = active
                ? Color.Lerp(new Color(0.12f, 0.21f, 0.25f), new Color(0.13f, 0.27f, 0.31f), hoverT)
                : Color.Lerp(new Color(0.12f, 0.14f, 0.17f), new Color(0.16f, 0.19f, 0.22f), hoverT);

            if (!enabled)
            {
                bg = new Color(0.10f, 0.11f, 0.13f, 0.82f);
            }

            GUI.color = bg;
            GUI.DrawTexture(rect, ButtonGradientTex, ScaleMode.StretchToFill);

            GUI.color = enabled
                ? (active ? Accent : BorderSoft)
                : BorderSoft;
            DrawBorder(rect, GUI.color, 1f);

            GUI.color = enabled
                ? Color.Lerp(TextSecondary, TextPrimary, hoverT)
                : TextMuted;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return enabled && Widgets.ButtonInvisible(rect);
        }

        public static bool DrawIconButton(Rect rect, string glyph, float hoverT, bool enabled = true, bool accent = false)
        {
            Color bg = accent
                ? Color.Lerp(new Color(0.11f, 0.24f, 0.28f), new Color(0.13f, 0.31f, 0.36f), hoverT)
                : Color.Lerp(new Color(0.12f, 0.14f, 0.17f), new Color(0.16f, 0.18f, 0.21f), hoverT);

            if (!enabled)
            {
                bg = new Color(0.10f, 0.11f, 0.13f, 0.82f);
            }

            GUI.color = bg;
            GUI.DrawTexture(rect, ButtonGradientTex, ScaleMode.StretchToFill);

            GUI.color = enabled
                ? (accent ? Accent : Border)
                : BorderSoft;
            DrawBorder(rect, GUI.color, 1f);

            GUI.color = enabled
                ? Color.Lerp(TextSecondary, accent ? TextPrimary : TextPrimary, hoverT)
                : TextMuted;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, glyph);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return enabled && Widgets.ButtonInvisible(rect);
        }

        public static void DrawBadge(Rect rect, string label, Color color)
        {
            GUI.color = new Color(color.r, color.g, color.b, 0.13f);
            GUI.DrawTexture(rect, SolidTex);
            GUI.color = new Color(color.r, color.g, color.b, 0.55f);
            DrawBorder(rect, GUI.color, 1f);
            GUI.color = color;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        public static void DrawHeaderTag(Rect rect, string label, float hoverT = 0f)
        {
            GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.10f + hoverT * 0.04f);
            GUI.DrawTexture(rect, SolidTex);
            GUI.color = Color.Lerp(new Color(AccentDim.r, AccentDim.g, AccentDim.b, 0.8f), Accent, hoverT * 0.5f);
            DrawBorder(rect, GUI.color, 1f);
            GUI.color = Color.Lerp(TextSecondary, TextPrimary, hoverT * 0.4f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        public static void DrawChip(Rect rect, string label, float hoverT, bool active = false)
        {
            Color bg = active
                ? new Color(Accent.r, Accent.g, Accent.b, 0.14f)
                : Color.Lerp(new Color(0.11f, 0.13f, 0.15f), new Color(0.14f, 0.16f, 0.19f), hoverT * 0.5f);

            GUI.color = bg;
            GUI.DrawTexture(rect, SolidTex);
            GUI.color = active ? Accent : Color.Lerp(BorderSoft, Border, hoverT * 0.4f);
            DrawBorder(rect, GUI.color, 1f);

            GUI.color = active ? TextPrimary : Color.Lerp(TextSecondary, TextPrimary, hoverT * 0.5f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        public static void DrawBorder(Rect rect, Color color, float thickness)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), SolidTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), SolidTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), SolidTex);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), SolidTex);
            GUI.color = Color.white;
        }
    }
}
