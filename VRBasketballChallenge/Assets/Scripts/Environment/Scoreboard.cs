using UnityEngine;
using UnityEngine.UI;

namespace VRBasketball
{
    /// <summary>
    /// Marcador colgado del techo, con cuatro caras (como el de un pabellón de verdad).
    /// Se ve desde cualquier lado y deja ver la puntuación y el tiempo sin depender
    /// del marcador flotante de la vista.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        [Tooltip("Escala del panel en el mundo (cada cara mide 1700x620 unidades).")]
        public float scale = 0.00135f;

        private const int Faces = 4;

        private Text[] _scoreText = new Text[Faces];
        private Text[] _timeText = new Text[Faces];
        private Text[] _levelText = new Text[Faces];
        private Text[] _titleText = new Text[Faces];

        private int _lastScore = -1;
        private int _lastLevel = -1;
        private int _lastSeconds = -1;
        private bool _halloween;

        public void Build()
        {
            // Estructura del marcador
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CuerpoMarcador";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(2.45f, 0.92f, 2.45f);
            body.GetComponent<Renderer>().sharedMaterial =
                UiFactory.MakeStandardMaterial("CarcasaMarcador", new Color(0.06f, 0.06f, 0.08f), 0.2f, 0.1f);
            Collider col = body.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            // Cable de sujeción al techo
            GameObject cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cable.name = "Suspension";
            cable.transform.SetParent(transform, false);
            cable.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            cable.transform.localScale = new Vector3(0.07f, 0.9f, 0.07f);
            cable.GetComponent<Renderer>().sharedMaterial =
                UiFactory.MakeStandardMaterial("Cable", new Color(0.12f, 0.12f, 0.14f), 0.1f, 0.2f);
            Collider cableCol = cable.GetComponent<Collider>();
            if (cableCol != null)
            {
                Destroy(cableCol);
            }

            for (int i = 0; i < Faces; i++)
            {
                BuildFace(i);
            }
        }

        private void BuildFace(int index)
        {
            float angle = index * 90f;
            float radius = 1.24f;

            GameObject root = new GameObject("Cara" + index);
            root.transform.SetParent(transform, false);
            root.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 forward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            root.transform.localPosition = forward * radius;

            Canvas canvas = UiFactory.CreateCanvas(root.transform, "Marcador" + index,
                RenderMode.WorldSpace, new Vector2(1700f, 620f));
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one * scale;

            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1700f, 620f);

            Image back = UiFactory.CreateRoundedPanel(canvas.transform, "Fondo", new Color(0.02f, 0.02f, 0.03f, 1f));
            UiFactory.Stretch(back.rectTransform, 0f, 0f, 0f, 0f);

            Image frame = UiFactory.CreateRoundedPanel(canvas.transform, "Marco", new Color(0.10f, 0.10f, 0.12f, 1f));
            UiFactory.Stretch(frame.rectTransform, 12f, 12f, 12f, 12f);

            Image inner = UiFactory.CreateRoundedPanel(canvas.transform, "Interior", new Color(0.02f, 0.02f, 0.03f, 1f));
            UiFactory.Stretch(inner.rectTransform, 24f, 24f, 24f, 24f);

            _titleText[index] = UiFactory.CreateText(canvas.transform, "Titulo", "VR BASKETBALL", 50,
                new Color(0.95f, 0.55f, 0.15f), TextAnchor.UpperCenter);
            UiFactory.Place(_titleText[index].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -48f), new Vector2(1500f, 66f));

            Text labelPoints = UiFactory.CreateText(canvas.transform, "EtiquetaPuntos", "PUNTOS", 42,
                new Color(0.55f, 0.6f, 0.7f), TextAnchor.UpperCenter);
            UiFactory.Place(labelPoints.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-380f, 118f), new Vector2(600f, 56f));

            _scoreText[index] = UiFactory.CreateText(canvas.transform, "Puntos", "0", 190,
                new Color(1f, 0.72f, 0.18f), TextAnchor.MiddleCenter);
            UiFactory.Place(_scoreText[index].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-380f, -20f), new Vector2(700f, 210f));

            Text labelTime = UiFactory.CreateText(canvas.transform, "EtiquetaTiempo", "TIEMPO", 42,
                new Color(0.55f, 0.6f, 0.7f), TextAnchor.UpperCenter);
            UiFactory.Place(labelTime.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(370f, 118f), new Vector2(560f, 56f));

            _timeText[index] = UiFactory.CreateText(canvas.transform, "Tiempo", "01:15", 120,
                new Color(1f, 0.42f, 0.35f), TextAnchor.MiddleCenter);
            UiFactory.Place(_timeText[index].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(370f, 30f), new Vector2(560f, 150f));

            _levelText[index] = UiFactory.CreateText(canvas.transform, "Nivel", "NIVEL 1", 56,
                new Color(0.75f, 0.9f, 1f), TextAnchor.MiddleCenter);
            UiFactory.Place(_levelText[index].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(370f, -105f), new Vector2(560f, 80f));
        }

        /// <summary>Aspecto del marcador según el modo (clásico o Halloween).</summary>
        public void SetHalloween(bool on)
        {
            _halloween = on;

            for (int i = 0; i < Faces; i++)
            {
                if (_titleText[i] == null)
                {
                    continue;
                }

                _titleText[i].text = on ? "NOCHE DE HALLOWEEN" : "VR BASKETBALL";
                _titleText[i].color = on ? new Color(1f, 0.45f, 0.08f) : new Color(0.95f, 0.55f, 0.15f);
                _scoreText[i].color = on ? new Color(1f, 0.55f, 0.12f) : new Color(1f, 0.72f, 0.18f);
            }
        }

        private void Update()
        {
            GameManager gm = GameManager.I;
            if (gm == null || _scoreText[0] == null)
            {
                return;
            }

            if (gm.currentScore != _lastScore)
            {
                _lastScore = gm.currentScore;
                SetAll(_scoreText, _lastScore.ToString());
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(gm.currentTime));
            if (seconds != _lastSeconds)
            {
                _lastSeconds = seconds;
                SetAll(_timeText, string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60));
            }

            int level = gm.difficulty != null ? gm.difficulty.Level : 1;
            if (level != _lastLevel)
            {
                _lastLevel = level;
                SetAll(_levelText, "NIVEL " + level);
            }
        }

        private static void SetAll(Text[] target, string value)
        {
            for (int i = 0; i < target.Length; i++)
            {
                if (target[i] != null)
                {
                    target[i].text = value;
                }
            }
        }
    }
}
