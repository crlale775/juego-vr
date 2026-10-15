using UnityEngine;
using UnityEngine.UI;

namespace VRBasketball
{
    /// <summary>Ayudas para crear interfaz por código (no se necesitan prefabs).</summary>
    public static class UiFactory
    {
        private static Font _font;
        private static Sprite _rounded;

        /// <summary>
        /// Esquinas redondeadas generadas por código (64x64, 9-slice). Da un aspecto
        /// mucho más cuidado que los rectángulos duros, sin necesitar imágenes.
        /// </summary>
        public static Sprite GetRoundedSprite()
        {
            if (_rounded != null)
            {
                return _rounded;
            }

            const int size = 64;
            const int radius = 20;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Redondeado";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f;
                    float dy = 0f;

                    if (x < radius) dx = radius - x - 0.5f;
                    else if (x >= size - radius) dx = x - (size - radius) + 0.5f;

                    if (y < radius) dy = radius - y - 0.5f;
                    else if (y >= size - radius) dy = y - (size - radius) + 0.5f;

                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply(false, false);

            _rounded = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            _rounded.name = "Redondeado";
            return _rounded;
        }

        public static Font GetFont()
        {
            if (_font != null)
            {
                return _font;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
            }

            return _font;
        }

        public static Color Rgba(float r, float g, float b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        public static Image CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Panel con esquinas redondeadas (para fondos y botones).</summary>
        public static Image CreateRoundedPanel(Transform parent, string name, Color color)
        {
            Image img = CreatePanel(parent, name, color);
            img.sprite = GetRoundedSprite();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        /// <summary>Material transparente (telarañas, humo, luces falsas).</summary>
        public static Material MakeTransparentMaterial(string name, Color color, Texture2D texture)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material m = new Material(shader);
            m.name = name;
            m.SetOverrideTag("RenderType", "Transparent");

            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (texture != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);

            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = 3000;
            return m;
        }

        public static Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = GetFont();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>
        /// Crea un lienzo. En pantalla completa se ajusta por ALTURA (match = 1) para que
        /// el diseño vertical coincida siempre con la referencia, sea cual sea la pantalla.
        /// Los lienzos en el mundo no llevan CanvasScaler: se escalan a mano.
        /// </summary>
        public static Canvas CreateCanvas(Transform parent, string name, RenderMode mode, Vector2 referenceSize, float match = 1f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = mode;

            if (mode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.sortingOrder = 100;

                CanvasScaler scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = referenceSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = match;

                // Sin GraphicRaycaster el sistema de eventos no puede ver los botones
                // y la pantalla de calibración quedaría sin poder pulsarse.
                go.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        /// <summary>Ajusta un RectTransform a un anclaje completo con márgenes.</summary>
        public static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Coloca un elemento anclado a una esquina con un tamaño fijo.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Material MakeStandardMaterial(string name, Color color, float smoothness, float metallic)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            Material m = new Material(shader);
            m.name = name;
            if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", color);
            }
            if (m.HasProperty("_Glossiness"))
            {
                m.SetFloat("_Glossiness", smoothness);
            }
            if (m.HasProperty("_Metallic"))
            {
                m.SetFloat("_Metallic", metallic);
            }
            return m;
        }
    }
}
