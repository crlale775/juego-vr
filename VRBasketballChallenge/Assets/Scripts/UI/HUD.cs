using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VRBasketball
{
    /// <summary>Botón de mundo que se activa mirándolo fijamente (o con un toque ciego).</summary>
    public class WorldButton : MonoBehaviour
    {
        public string id;
        public Color baseColor = new Color(0.12f, 0.14f, 0.20f, 0.92f);
        public Color hoverColor = new Color(0.20f, 0.42f, 0.75f, 0.95f);

        public Image background;
        public Image fill;
        public Text label;

        [HideInInspector] public float hover;

        public void SetHover(float value)
        {
            hover = Mathf.Clamp01(value);

            if (background != null)
            {
                background.color = Color.Lerp(baseColor, hoverColor, hover * 0.6f);
            }
            if (fill != null)
            {
                fill.fillAmount = hover;
            }
        }
    }

    /// <summary>
    /// Interfaz completa del juego: marcador dentro de la vista y menús en el mundo.
    /// Todo se elige con la MANO delante de la cámara trasera (el cursor de mano va
    /// solo sobre los botones): no hace falta ningún mando. La mirada y el toque en
    /// pantalla quedan solo como respaldo por si la cámara no está disponible.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public Transform head;
        [Tooltip("Punto fijo del jugador: sostiene los paneles de menú para poder mirarlos.")]
        public Transform station;
        [Tooltip("Rig estéreo: sus cámaras se usan para tocar los botones del menú con el dedo.")]
        public StereoRig rig;
        [Tooltip("Tiempo (s) que hay que mirar fijamente un botón para elegirlo.")]
        public float dwellTime = 1.1f;

        [Header("Control por mano (cámara trasera)")]
        [Tooltip("Permite elegir los botones del menú con la mano, sin mando ni tocar la pantalla.")]
        public bool handSelection = true;
        [Tooltip("Grados de giro del cursor con la mano en el borde del encuadre.")]
        public float handYawRange = 34f;
        [Tooltip("Grados de giro vertical del cursor con la mano en el borde del encuadre.")]
        public float handPitchRange = 24f;
        [Tooltip("Tiempo (s) que la mano tiene que quedarse sobre un botón para elegirlo.")]
        public float handDwellTime = 0.9f;
        [Tooltip("Cuánto tiene que verse la mano para hacerle caso.")]
        public float handMinCoverage = 0.015f;

        public event Action<string> OnAction;

        private Canvas _hudCanvas;
        private Canvas _menuCanvas;
        private Canvas _gameOverCanvas;

        private Text _scoreText;
        private Text _timeText;
        private Text _levelText;
        private Text _streakText;
        private Text _recordText;
        private Text _messageText;
        private Image _messageBack;
        private Image _crosshair;
        private Image _crosshairDot;
        private Text _menuInfo;
        private Text _menuTitle;
        private Text _gameOverText;
        private Image _menuBack;
        private Image _gameOverBack;
        private GameObject _handVisual;
        private RectTransform _handVisualRect;
        private Image _handGlow;
        private Image _gestureFill;
        private Image _gestureArrow;
        private Text _handHint;
        private UnityEngine.UI.RawImage _cameraPreview;
        private Image _cameraPreviewBorder;

        // Mano de verdad: los 21 puntos de MediaPipe dibujados como esqueleto.
        private GameObject _esqueleto;
        private Image[] _articulaciones = new Image[21];
        private Image[] _huesosMano = new Image[21];
        private GameObject[] _manoSimulada;
        private string _pistaTiro = "SUBE LA MANO DE GOLPE para lanzar";

        /// <summary>Conexiones entre los 21 puntos de la mano (21 huesos).</summary>
        private static readonly int[] Conexiones =
        {
            0, 1,  1, 2,  2, 3,  3, 4,
            0, 5,  5, 6,  6, 7,  7, 8,
            5, 9,  9, 10, 10, 11, 11, 12,
            9, 13, 13, 14, 14, 15, 15, 16,
            13, 17, 17, 18, 18, 19, 19, 20,
            0, 17
        };

        private bool _halloween;
        private bool _handShown;
        private float _handPulse;

        // Estado del gesto de lanzar (lo manda ThrowController cada fotograma).
        private float _gestureCharge;
        private bool _ballReady = true;
        private bool _gestureBlocked;
        private float _throwFlash;

        // Cursor de mano sobre los paneles del mundo (menú y fin de partida).
        private Image _menuCursor;
        private Image _gameOverCursor;
        private bool _handVisible;
        private Vector2 _handPos = new Vector2(0.5f, 0.5f);
        private float _handCoverage;

        private WorldButton[] _menuButtons;
        private WorldButton[] _gameOverButtons;
        private WorldButton _hovered;

        // Botón ya elegido: no se repite hasta que el cursor sale de él. Sin esto,
        // dejar la mano quieta sobre un botón lo encendería y apagaría sin parar.
        private WorldButton _blocked;
        private float _messageTimer;

        private const float CanvasScale = 0.0011f;

        public void Build()
        {
            BuildHud();
            BuildMenu();
            BuildGameOver();
        }

        // ------------------------------------------------------------------
        // Marcador
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            _hudCanvas = UiFactory.CreateCanvas(head, "HUD", RenderMode.WorldSpace, new Vector2(900f, 520f));
            _hudCanvas.transform.localPosition = new Vector3(0f, 0f, 1.55f);
            _hudCanvas.transform.localRotation = Quaternion.identity;
            _hudCanvas.transform.localScale = Vector3.one * CanvasScale;

            RectTransform root = _hudCanvas.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(900f, 520f);

            _scoreText = UiFactory.CreateText(_hudCanvas.transform, "Puntos", "PUNTOS 0", 42, Color.white, TextAnchor.UpperLeft);
            UiFactory.Place(_scoreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(420f, 60f));

            _timeText = UiFactory.CreateText(_hudCanvas.transform, "Tiempo", "01:15", 42, new Color(1f, 0.85f, 0.35f), TextAnchor.UpperRight);
            UiFactory.Place(_timeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(360f, 60f));

            _levelText = UiFactory.CreateText(_hudCanvas.transform, "Nivel", "NIVEL 1", 30, new Color(0.75f, 0.9f, 1f), TextAnchor.LowerLeft);
            UiFactory.Place(_levelText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 24f), new Vector2(700f, 44f));

            _streakText = UiFactory.CreateText(_hudCanvas.transform, "Racha", "ACIERTOS 0/0", 28, new Color(0.85f, 0.85f, 0.9f), TextAnchor.LowerRight);
            UiFactory.Place(_streakText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 24f), new Vector2(700f, 44f));

            _recordText = UiFactory.CreateText(_hudCanvas.transform, "Record", "RÉCORD 0", 24, new Color(0.7f, 0.75f, 0.85f), TextAnchor.UpperCenter);
            UiFactory.Place(_recordText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(400f, 40f));

            _messageBack = UiFactory.CreateRoundedPanel(_hudCanvas.transform, "FondoAviso", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_messageBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(720f, 70f));

            _messageText = UiFactory.CreateText(_hudCanvas.transform, "Aviso", "", 40, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Place(_messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(760f, 80f));

            // Punto de mira
            _crosshair = UiFactory.CreatePanel(_hudCanvas.transform, "Mira", new Color(1f, 1f, 1f, 0.55f));
            UiFactory.Place(_crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 3f));

            _crosshairDot = UiFactory.CreatePanel(_hudCanvas.transform, "MiraPunto", new Color(1f, 1f, 1f, 0.75f));
            UiFactory.Place(_crosshairDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 34f));

            // LA MANO DIBUJADA: es lo que el jugador ve y con lo que aprende el gesto.
            BuildHandVisual();

            // Preview de la cámara trasera en la esquina inferior izquierda.
            // Así el jugador ve la misma mano que el juego está usando para apuntar.
            _cameraPreviewBorder = UiFactory.CreatePanel(_hudCanvas.transform, "PreviewBorde",
                new Color(0.2f, 0.9f, 0.5f, 0.8f));
            UiFactory.Place(_cameraPreviewBorder.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(90f, 80f), new Vector2(164f, 124f));

            GameObject previewGo = new GameObject("CameraPreview",
                typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            previewGo.transform.SetParent(_hudCanvas.transform, false);
            _cameraPreview = previewGo.GetComponent<UnityEngine.UI.RawImage>();
            _cameraPreview.color = Color.white;
            // La cámara trasera NO se refleja: la mano se ve como está de verdad.
            RectTransform cprt = _cameraPreview.rectTransform;
            cprt.anchorMin = new Vector2(0f, 0f);
            cprt.anchorMax = new Vector2(0f, 0f);
            cprt.pivot = new Vector2(0f, 0f);
            cprt.anchoredPosition = new Vector2(92f, 81f);
            cprt.sizeDelta = new Vector2(160f, 120f);

            BuildEsqueletoMano();

            Text previewLabel = UiFactory.CreateText(_hudCanvas.transform, "PreviewLabel",
                "CÁMARA TRASERA", 14, new Color(0.4f, 1f, 0.6f), TextAnchor.UpperLeft);
            UiFactory.Place(previewLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(92f, 204f), new Vector2(160f, 22f));

            _handVisual.SetActive(false);
            _cameraPreviewBorder.gameObject.SetActive(false);
            _cameraPreview.gameObject.SetActive(false);

            SetMessage("");
        }

        /// <summary>
        /// Dibuja una mano (muñeca, palma y cinco dedos) que sigue a la mano real vista
        /// por la cámara. Lleva al lado una barra que se llena al subir la mano, con una
        /// flecha: así se entiende que el gesto de lanzar es SUBIRLA.
        /// </summary>
        private void BuildHandVisual()
        {
            _handVisual = new GameObject("ManoVisible", typeof(RectTransform));
            _handVisual.transform.SetParent(_hudCanvas.transform, false);
            _handVisualRect = _handVisual.GetComponent<RectTransform>();
            UiFactory.Place(_handVisualRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(180f, 240f));

            // Halo suave detrás: hace que la mano se vea bien en VR estéreo.
            _handGlow = UiFactory.CreateRoundedPanel(_handVisual.transform, "Halo", new Color(0.35f, 1f, 0.6f, 0.12f));
            UiFactory.Place(_handGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(170f, 200f));

            // Muñeca / antebrazo
            Image wrist = UiFactory.CreateRoundedPanel(_handVisual.transform, "Muneca", new Color(0.26f, 0.86f, 0.55f, 0.35f));
            UiFactory.Place(wrist.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(66f, 62f));

            // Palma
            Image palm = UiFactory.CreateRoundedPanel(_handVisual.transform, "Palma", new Color(0.32f, 0.96f, 0.62f, 0.45f));
            UiFactory.Place(palm.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 52f), new Vector2(112f, 96f));

            // Cinco dedos: el del medio es el más alto.
            float[] xs = { -44f, -22f, 0f, 22f, 44f };
            float[] alturas = { 62f, 80f, 88f, 76f, 58f };
            for (int i = 0; i < 5; i++)
            {
                Image finger = UiFactory.CreateRoundedPanel(_handVisual.transform, "Dedo" + i,
                    new Color(0.32f, 0.96f, 0.62f, 0.42f));
                UiFactory.Place(finger.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(xs[i], 140f), new Vector2(18f, alturas[i]));
            }

            // Barra del gesto, a la derecha de la mano.
            Image back = UiFactory.CreateRoundedPanel(_handVisual.transform, "GestoFondo", new Color(1f, 1f, 1f, 0.18f));
            UiFactory.Place(back.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(84f, 46f), new Vector2(26f, 150f));

            _gestureFill = UiFactory.CreateRoundedPanel(back.transform, "GestoRelleno", new Color(0.4f, 1f, 0.5f, 0.95f));
            RectTransform frt = _gestureFill.rectTransform;
            frt.anchorMin = new Vector2(0.5f, 0f);
            frt.anchorMax = new Vector2(0.5f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.anchoredPosition = new Vector2(0f, 2f);
            frt.sizeDelta = new Vector2(20f, 0f);

            // Flecha: un cuadrado girado 45° (rombo) que apunta hacia arriba.
            _gestureArrow = UiFactory.CreateRoundedPanel(back.transform, "Flecha", new Color(0.5f, 1f, 0.6f, 0.95f));
            UiFactory.Place(_gestureArrow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 164f), new Vector2(28f, 28f));
            _gestureArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Aviso que viaja con la mano.
            _handHint = UiFactory.CreateText(_handVisual.transform, "ManoAviso", "", 22,
                new Color(0.6f, 1f, 0.78f), TextAnchor.UpperCenter);
            UiFactory.Place(_handHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, -22f), new Vector2(560f, 34f));

            // Lo que se dibuja cuando NO hay puntos: la mano de ejemplo.
            _manoSimulada = new GameObject[]
            {
                _handGlow != null ? _handGlow.gameObject : null,
                wrist.gameObject,
                palm.gameObject,
                _handVisual.transform.Find("Dedo0") != null ? _handVisual.transform.Find("Dedo0").gameObject : null,
                _handVisual.transform.Find("Dedo1") != null ? _handVisual.transform.Find("Dedo1").gameObject : null,
                _handVisual.transform.Find("Dedo2") != null ? _handVisual.transform.Find("Dedo2").gameObject : null,
                _handVisual.transform.Find("Dedo3") != null ? _handVisual.transform.Find("Dedo3").gameObject : null,
                _handVisual.transform.Find("Dedo4") != null ? _handVisual.transform.Find("Dedo4").gameObject : null
            };
        }

        /// <summary>
        /// Esqueleto de la mano: 21 articulaciones y 21 huesos dibujados sobre la vista.
        /// Es lo que hace que se vea LA MANO DE VERDAD (los puntos que da MediaPipe)
        /// en vez de una mano de ejemplo.
        /// </summary>
        private void BuildEsqueletoMano()
        {
            _esqueleto = new GameObject("EsqueletoMano", typeof(RectTransform));
            _esqueleto.transform.SetParent(_hudCanvas.transform, false);

            RectTransform raiz = _esqueleto.GetComponent<RectTransform>();
            UiFactory.Place(raiz, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 520f));

            // Huesos: barras finas que unen dos puntos.
            for (int i = 0; i < 21; i++)
            {
                Image hueso = UiFactory.CreateRoundedPanel(_esqueleto.transform, "Hueso" + i,
                    new Color(0.45f, 1f, 0.7f, 0.55f));
                UiFactory.Place(hueso.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(20f, 7f));
                _huesosMano[i] = hueso;
            }

            // Articulaciones: un punto en cada dedo.
            for (int i = 0; i < 21; i++)
            {
                Image punto = UiFactory.CreateRoundedPanel(_esqueleto.transform, "Punto" + i,
                    new Color(1f, 0.98f, 0.9f, 0.85f));
                UiFactory.Place(punto.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(13f, 13f));
                _articulaciones[i] = punto;
            }

            _esqueleto.SetActive(false);
        }

        /// <summary>
        /// Dibuja los 21 puntos de la mano que ve MediaPipe. Si no hay puntos (el móvil
        /// no los da) se queda la mano de ejemplo de siempre.
        /// </summary>
        public void SetHandPoints(Vector3[] puntos)
        {
            if (_esqueleto == null)
            {
                return;
            }

            bool hay = puntos != null && puntos.Length >= 21;

            if (_esqueleto.activeSelf != hay)
            {
                _esqueleto.SetActive(hay);
            }

            if (_manoSimulada != null)
            {
                for (int i = 0; i < _manoSimulada.Length; i++)
                {
                    if (_manoSimulada[i] != null)
                    {
                        _manoSimulada[i].SetActive(!hay);
                    }
                }
            }

            if (!hay || _hudCanvas == null)
            {
                return;
            }

            RectTransform root = _hudCanvas.GetComponent<RectTransform>();
            float ancho = root.sizeDelta.x;
            float alto = root.sizeDelta.y;

            Vector2[] posiciones = new Vector2[21];

            for (int i = 0; i < 21; i++)
            {
                posiciones[i] = new Vector2(
                    (puntos[i].x - 0.5f) * ancho,
                    (puntos[i].y - 0.5f) * alto);
            }

            for (int i = 0; i < 21; i++)
            {
                if (_articulaciones[i] != null)
                {
                    _articulaciones[i].rectTransform.anchoredPosition = posiciones[i];
                }
            }

            for (int i = 0; i < 21; i++)
            {
                Image hueso = _huesosMano[i];
                if (hueso == null)
                {
                    continue;
                }

                Vector2 a = posiciones[Conexiones[i * 2]];
                Vector2 b = posiciones[Conexiones[i * 2 + 1]];
                Vector2 delta = b - a;

                hueso.rectTransform.anchoredPosition = (a + b) * 0.5f;
                hueso.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, delta.magnitude), 7f);
                hueso.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>Cómo hay que lanzar ahora mismo (lo manda el sistema de tiro).</summary>
        public void SetThrowHint(string pista)
        {
            if (!string.IsNullOrEmpty(pista))
            {
                _pistaTiro = pista;
            }
        }

        // ------------------------------------------------------------------
        // Menú principal
        // ------------------------------------------------------------------

        private void BuildMenu()
        {
            _menuCanvas = UiFactory.CreateCanvas(station, "Menu", RenderMode.WorldSpace, new Vector2(1000f, 700f));
            _menuCanvas.transform.localPosition = new Vector3(0f, -0.12f, 1.75f);
            // Sin girar: así el texto se lee derecho (con 180° se veía al revés).
            _menuCanvas.transform.localRotation = MenuRotation();
            _menuCanvas.transform.localScale = Vector3.one * CanvasScale;

            RectTransform root = _menuCanvas.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(1000f, 700f);

            _menuBack = UiFactory.CreateRoundedPanel(_menuCanvas.transform, "Fondo", new Color(0.03f, 0.05f, 0.1f, 0.88f));
            UiFactory.Stretch(_menuBack.rectTransform, 0f, 0f, 0f, 0f);

            _menuTitle = UiFactory.CreateText(_menuCanvas.transform, "Titulo", "VR BASKETBALL CHALLENGE", 54, new Color(1f, 0.72f, 0.32f), TextAnchor.UpperCenter);
            UiFactory.Place(_menuTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(940f, 70f));

            Text sub = UiFactory.CreateText(_menuCanvas.transform, "Subtitulo",
                "Mano detectada con IA (MediaPipe) por la cámara trasera: sin mando", 26,
                new Color(0.8f, 0.86f, 1f), TextAnchor.UpperCenter);
            UiFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(940f, 40f));

            _menuInfo = UiFactory.CreateText(_menuCanvas.transform, "Info", "", 24, new Color(0.72f, 0.85f, 0.95f), TextAnchor.UpperCenter);
            UiFactory.Place(_menuInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(920f, 90f));

            // Dos columnas de tres botones: se ve ordenado y deja aire para el título.
            string[] ids = { "start", "halloween", "gesto", "recenter", "stereo" };
            string[] labels =
            {
                "INICIAR PARTIDA", "MODO: CLÁSICO", "TIRO: NATURAL",
                "RECENTRAR VISTA", "VISTA: ESTÉREO"
            };

            _menuButtons = new WorldButton[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                int column = i % 2;
                int row = i / 2;
                float x = column == 0 ? -180f : 180f;
                float y = -262f - row * 76f;

                _menuButtons[i] = CreateButton(_menuCanvas.transform, ids[i], labels[i],
                    new Vector2(340f, 62f), new Vector2(x, y));
            }

            Text hint = UiFactory.CreateText(_menuCanvas.transform, "Ayuda",
                "TOCA EL BOTÓN en la pantalla para elegirlo (también vale la mano delante de la cámara trasera).\nEn el juego la mano se dibuja con MediaPipe (21 puntos). Para lanzar vale subirla, empujarla, hacer puño y abrir, o pellizcar.",
                19, new Color(0.6f, 0.66f, 0.78f), TextAnchor.LowerCenter);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(940f, 40f));

            _menuCursor = CreateHandCursor(_menuCanvas.transform);
        }

        /// <summary>Cursor que dibuja dónde está la mano sobre los paneles del mundo.</summary>
        private Image CreateHandCursor(Transform parent)
        {
            Image ring = UiFactory.CreateRoundedPanel(parent, "CursorMano", new Color(0.35f, 1f, 0.6f, 0.5f));
            UiFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(78f, 78f));

            Image dot = UiFactory.CreateRoundedPanel(ring.transform, "Centro", new Color(0.9f, 1f, 0.95f, 0.95f));
            UiFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(16f, 16f));

            ring.gameObject.SetActive(false);
            return ring;
        }

        private void BuildGameOver()
        {
            _gameOverCanvas = UiFactory.CreateCanvas(station, "FinPartida", RenderMode.WorldSpace, new Vector2(1000f, 620f));
            _gameOverCanvas.transform.localPosition = new Vector3(0f, -0.12f, 1.75f);
            _gameOverCanvas.transform.localRotation = MenuRotation();
            _gameOverCanvas.transform.localScale = Vector3.one * CanvasScale;

            RectTransform root = _gameOverCanvas.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(1000f, 620f);

            _gameOverBack = UiFactory.CreateRoundedPanel(_gameOverCanvas.transform, "Fondo", new Color(0.06f, 0.02f, 0.03f, 0.9f));
            UiFactory.Stretch(_gameOverBack.rectTransform, 0f, 0f, 0f, 0f);

            Text title = UiFactory.CreateText(_gameOverCanvas.transform, "Titulo", "FIN DE LA PARTIDA", 52, new Color(1f, 0.45f, 0.35f), TextAnchor.UpperCenter);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(940f, 70f));

            _gameOverText = UiFactory.CreateText(_gameOverCanvas.transform, "Resumen", "", 30, Color.white, TextAnchor.UpperCenter);
            UiFactory.Place(_gameOverText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(920f, 300f));

            _gameOverButtons = new WorldButton[2];
            _gameOverButtons[0] = CreateButton(_gameOverCanvas.transform, "retry", "REINTENTAR", new Vector2(580f, 62f), new Vector2(0f, -250f));
            _gameOverButtons[1] = CreateButton(_gameOverCanvas.transform, "menu", "VOLVER AL MENÚ", new Vector2(580f, 62f), new Vector2(0f, -326f));

            _gameOverCursor = CreateHandCursor(_gameOverCanvas.transform);
        }

        /// <summary>
        /// Los paneles del menú se leen derecho con la rotación normal. El parámetro
        /// ?menuflip=1 los gira 180° (queda espejado) y sirve solo para pruebas.
        /// </summary>
        private static Quaternion MenuRotation()
        {
            string url = Application.absoluteURL;
            bool flip = !string.IsNullOrEmpty(url) && url.ToLowerInvariant().Contains("menuflip=1");
            return flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        }

        private WorldButton CreateButton(Transform parent, string id, string label, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject("Boton_" + id, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            UiFactory.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);

            Image bg = go.GetComponent<Image>();
            bg.sprite = UiFactory.GetRoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1f;
            bg.color = new Color(0.12f, 0.14f, 0.20f, 0.92f);
            bg.raycastTarget = false;

            Image fill = UiFactory.CreatePanel(go.transform, "Progreso", new Color(0.3f, 0.65f, 1f, 0.55f));
            UiFactory.Stretch(fill.rectTransform, 2f, 2f, 2f, 2f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;

            Text text = UiFactory.CreateText(go.transform, "Texto", label, 26, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

            // El colisionador se mide en unidades LOCALES del botón. El lienzo del mundo
            // ya está escalado por CanvasScale, así que NO hay que volver a multiplicar:
            // si se multiplicaba, el colisionador quedaba en 0,4 mm y ningún rayo (ni la
            // mirada ni la mano) llegaba a tocar los botones del menú.
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 20f);
            collider.center = Vector3.zero;

            WorldButton button = go.AddComponent<WorldButton>();
            button.id = id;
            button.background = bg;
            button.fill = fill;
            button.label = text;
            button.SetHover(0f);
            return button;
        }

        // ------------------------------------------------------------------
        // Selección por mirada
        // ------------------------------------------------------------------

        private void Update()
        {
            UpdateMessage();
            UpdateCursor();
            UpdateSelection();
        }

        private WorldButton[] ActiveButtons()
        {
            if (_menuCanvas != null && _menuCanvas.gameObject.activeInHierarchy)
            {
                return _menuButtons;
            }

            if (_gameOverCanvas != null && _gameOverCanvas.gameObject.activeInHierarchy)
            {
                return _gameOverButtons;
            }

            return null;
        }

        /// <summary>¿Se está usando la mano ahora mismo para elegir?</summary>
        private bool HandSelectionActive
        {
            get
            {
                return handSelection && head != null && _handVisible && _handCoverage >= handMinCoverage;
            }
        }

        /// <summary>
        /// Dirección a la que apunta la mano: igual que el tiro, la posición de la palma
        /// en el encuadre gira la mirada. Como la cámara es la trasera, izquierda y
        /// derecha coinciden con el mundo real.
        /// </summary>
        private Ray HandRay()
        {
            Vector3 direction = head.forward;

            float yaw = (_handPos.x - 0.5f) * 2f * handYawRange;
            float pitch = (0.5f - _handPos.y) * 2f * handPitchRange;

            if (Mathf.Abs(yaw) > 0.01f)
            {
                direction = Quaternion.AngleAxis(yaw, Vector3.up) * direction;
            }

            if (Mathf.Abs(pitch) > 0.01f)
            {
                direction = Quaternion.AngleAxis(-pitch, head.right) * direction;
            }

            return new Ray(head.position, direction.normalized);
        }

        /// <summary>
        /// Elige botón: con la mano cuando la cámara la ve (el cursor va solo sobre el
        /// botón) y, si no, con la mirada. El toque en pantalla sigue valiendo como respaldo.
        /// </summary>
        private void UpdateSelection()
        {
            WorldButton[] active = ActiveButtons();

            bool tap = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
            Vector2 tapPos = Input.mousePosition;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                tap = true;
                tapPos = Input.GetTouch(0).position;
            }

            if (active == null || head == null)
            {
                if (_hovered != null)
                {
                    _hovered.SetHover(0f);
                    _hovered = null;
                }
                return;
            }

            // TACTO DIRECTO: si el dedo cae encima de un botón, se elige al instante.
            // Es lo que hace que el menú se pueda usar sin visor y sin apuntar con la
            // mano: tocas EMPEZAR (o cualquier modo) y entra.
            if (tap)
            {
                Camera cam = CameraForPoint(tapPos);
                if (cam != null)
                {
                    WorldButton direct = RaycastButton(active, cam.ScreenPointToRay(tapPos));
                    if (direct != null)
                    {
                        ActivarBoton(direct);
                        return;
                    }
                }
            }

            bool byHand = HandSelectionActive;
            Ray ray = byHand ? HandRay() : new Ray(head.position, head.forward);

            WorldButton hit = RaycastButton(active, ray);

            if (hit != _hovered && _hovered != null)
            {
                _hovered.SetHover(0f);
            }
            _hovered = hit;

            // Un botón ya elegido no se repite hasta que el cursor sale de él.
            if (_blocked != null && _blocked != _hovered)
            {
                _blocked = null;
            }

            if (_hovered == null || _hovered == _blocked)
            {
                return;
            }

            // Con la mano el botón se llena algo más rápido que con la mirada.
            float needed = byHand ? handDwellTime : dwellTime;
            float progress = _hovered.hover + Time.deltaTime / Mathf.Max(0.15f, needed);
            _hovered.SetHover(progress);

            if (progress >= 1f || tap)
            {
                ActivarBoton(_hovered);
            }
        }

        /// <summary>Elige un botón: lo bloquea para no repetirlo, suena y avisa al juego.</summary>
        private void ActivarBoton(WorldButton boton)
        {
            if (boton == null)
            {
                return;
            }

            _blocked = boton;
            if (_hovered == boton)
            {
                _hovered.SetHover(0f);
            }

            if (AudioSynth.I != null)
            {
                AudioSynth.I.Click();
            }

            if (OnAction != null)
            {
                OnAction(boton.id);
            }
        }

        /// <summary>Cámara del ojo sobre el que cayó el toque (para tocarlo en el botón).</summary>
        private Camera CameraForPoint(Vector2 punto)
        {
            if (rig != null)
            {
                Camera izquierdo = rig.LeftEye;
                Camera derecho = rig.RightEye;

                if (izquierdo != null && derecho != null)
                {
                    return punto.x < Screen.width * 0.5f ? izquierdo : derecho;
                }

                if (izquierdo != null)
                {
                    return izquierdo;
                }

                if (derecho != null)
                {
                    return derecho;
                }
            }

            return Camera.main;
        }

        /// <summary>
        /// Dibuja el cursor de la mano sobre el panel abierto (menú o fin de partida).
        /// Como parte de la mirada, el cursor solo aparece cuando el jugador está mirando
        /// el panel: si mira hacia otro lado, se esconde.
        /// </summary>
        private void UpdateCursor()
        {
            HandCursorVisible = false;

            if (_menuCursor == null || _gameOverCursor == null)
            {
                return;
            }

            bool menuOpen = _menuCanvas != null && _menuCanvas.gameObject.activeInHierarchy;
            bool overOpen = _gameOverCanvas != null && _gameOverCanvas.gameObject.activeInHierarchy;

            Image cursor = menuOpen ? _menuCursor : (overOpen ? _gameOverCursor : null);

            if (_menuCursor != cursor)
            {
                _menuCursor.gameObject.SetActive(false);
            }
            if (_gameOverCursor != cursor)
            {
                _gameOverCursor.gameObject.SetActive(false);
            }

            if (cursor == null || !HandSelectionActive)
            {
                return;
            }

            RectTransform canvasRect = cursor.transform.parent as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Ray ray = HandRay();

            Plane plane = new Plane(canvasRect.forward, canvasRect.position);
            float distance;
            if (!plane.Raycast(ray, out distance) || distance < 0.3f || distance > 4.5f)
            {
                cursor.gameObject.SetActive(false);
                return;
            }

            Vector3 point = ray.GetPoint(distance);
            Vector2 local = canvasRect.InverseTransformPoint(point);

            float halfW = canvasRect.sizeDelta.x * 0.5f;
            float halfH = canvasRect.sizeDelta.y * 0.5f;
            local.x = Mathf.Clamp(local.x, -halfW, halfW);
            local.y = Mathf.Clamp(local.y, -halfH, halfH);

            cursor.rectTransform.anchoredPosition = local;
            cursor.gameObject.SetActive(true);
            HandCursorVisible = true;
        }

        /// <summary>Botón del menú que está seleccionándose ahora mismo ("-" si ninguno).</summary>
        public string HoveredId { get { return _hovered != null ? _hovered.id : "-"; } }

        /// <summary>Cuánto se ha llenado el botón seleccionado (0..1).</summary>
        public float HoverProgress { get { return _hovered != null ? _hovered.hover : 0f; } }

        /// <summary>El cursor de la mano está dibujado sobre el panel abierto.</summary>
        public bool HandCursorVisible { get; private set; }

        /// <summary>La mano está delante de la cámara y se está usando para elegir.</summary>
        public bool HandDriven { get { return HandSelectionActive; } }

        /// <summary>Posición y tamaño reales de cada botón del menú (diagnóstico).</summary>
        public string MenuButtonsDump()
        {
            if (_menuButtons == null)
            {
                return "(sin botones)";
            }

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < _menuButtons.Length; i++)
            {
                if (_menuButtons[i] == null)
                {
                    continue;
                }

                RectTransform rt = _menuButtons[i].transform as RectTransform;
                if (rt == null)
                {
                    continue;
                }

                sb.Append(_menuButtons[i].id)
                  .Append(" pos=(")
                  .Append(rt.position.x.ToString("0.000")).Append(",")
                  .Append(rt.position.y.ToString("0.000")).Append(",")
                  .Append(rt.position.z.ToString("0.000")).Append(")")
                  .Append(" tam=(")
                  .Append((rt.rect.width * rt.lossyScale.x).ToString("0.000")).Append(",")
                  .Append((rt.rect.height * rt.lossyScale.y).ToString("0.000")).Append(") | ");
            }

            return sb.ToString();
        }

        /// <summary>Última posición de la mano vista por la cámara (0..1).</summary>
        public Vector2 HandPosition { get { return _handPos; } }

        /// <summary>Devuelve el botón activo más cercano que toque el rayo (si hay alguno).</summary>
        private WorldButton RaycastButton(WorldButton[] buttons, Ray ray)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 4.5f);
            WorldButton best = null;
            float bestDistance = float.MaxValue;

            for (int h = 0; h < hits.Length; h++)
            {
                WorldButton candidate = hits[h].collider.GetComponent<WorldButton>();
                if (candidate == null)
                {
                    continue;
                }

                if (hits[h].distance < bestDistance && Contains(buttons, candidate))
                {
                    bestDistance = hits[h].distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool Contains(WorldButton[] buttons, WorldButton button)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == button)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------

        public void SetScore(int score)
        {
            _scoreText.text = "PUNTOS " + score;
        }

        public void SetTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            _timeText.text = string.Format("{0:00}:{1:00}", total / 60, total % 60);
            _timeText.color = total <= 10 ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.85f, 0.35f);
        }

        public void SetLevel(int level, string nombre)
        {
            _levelText.text = "NIVEL " + level + "  ·  " + nombre;
        }

        public void SetStreak(int made, int shots)
        {
            _streakText.text = string.Format("ACIERTOS {0}/{1}", made, shots);
        }

        public void SetRecord(int record)
        {
            _recordText.text = "RÉCORD " + record;
        }

        public void SetMenuInfo(string text)
        {
            if (_menuInfo != null)
            {
                _menuInfo.text = text;
            }
        }

        public void SetButtonLabel(string id, string label)
        {
            WorldButton[] all = _menuButtons;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].id == id && all[i].label != null)
                {
                    all[i].label.text = label;
                }
            }
        }

        public bool IsHalloween { get { return _halloween; } }

        /// <summary>
        /// Estado del tiro, para dibujar la barra del gesto y decirle al jugador qué
        /// hacer ahora mismo (si puede lanzar o si tiene que esperar al balón).
        /// </summary>
        public void SetThrowState(float charge, bool ballReady, bool gestureBlocked, float sinceThrow)
        {
            _gestureCharge = Mathf.Clamp01(charge);
            _ballReady = ballReady;
            _gestureBlocked = gestureBlocked;

            if (sinceThrow < 0.25f)
            {
                _throwFlash = 1f;
            }
        }

        /// <summary>
        /// Guarda dónde ve la cámara la mano (lo usa el cursor del menú) y dibuja LA MANO
        /// en el juego, con su barra de gesto y el aviso de qué hacer.
        /// </summary>
        public void SetHand(bool detected, Vector2 position, float coverage, bool aiming)
        {
            _handVisible = detected;
            _handPos = position;
            _handCoverage = coverage;

            if (_handVisual == null)
            {
                return;
            }

            bool show = detected && coverage >= 0.008f;

            if (show != _handShown)
            {
                _handShown = show;
                _handVisual.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            RectTransform root = _hudCanvas.GetComponent<RectTransform>();
            float w = root.sizeDelta.x;
            float h = root.sizeDelta.y;

            Vector2 local = new Vector2((position.x - 0.5f) * w, (0.5f - position.y) * h);
            _handVisualRect.anchoredPosition = local;

            // Cuanto más cerca se ve la mano, más grande se dibuja.
            float scale = Mathf.Lerp(0.7f, 1.45f, Mathf.InverseLerp(0.01f, 0.16f, coverage));

            if (_throwFlash > 0f)
            {
                _throwFlash -= Time.deltaTime * 2.5f;
                scale *= 1f + 0.22f * Mathf.Clamp01(_throwFlash);
            }

            _handVisualRect.localScale = new Vector3(scale, scale, 1f);

            // Halo palpitante: se ve en los dos ojos del visor.
            _handPulse += Time.deltaTime * 3.2f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_handPulse);

            if (_handGlow != null)
            {
                bool flash = _throwFlash > 0f;
                Color gc = flash
                    ? new Color(1f, 0.95f, 0.5f, 0.35f)
                    : new Color(0.35f, 1f, 0.6f, 0.08f + 0.10f * pulse);
                _handGlow.color = gc;
            }

            // Barra del gesto: verde si se puede lanzar, naranja si hay que esperar.
            if (_gestureFill != null)
            {
                _gestureFill.rectTransform.sizeDelta = new Vector2(20f, 146f * _gestureCharge);
                _gestureFill.color = _ballReady
                    ? new Color(0.4f, 1f, 0.5f, 0.95f)
                    : new Color(1f, 0.65f, 0.25f, 0.95f);
            }

            if (_gestureArrow != null && _gestureFill != null)
            {
                _gestureArrow.color = _gestureCharge >= 1f
                    ? new Color(1f, 1f, 0.6f, 1f)
                    : new Color(0.5f, 1f, 0.6f, 0.85f + 0.15f * pulse);
            }

            // Qué tiene que hacer el jugador, ahora mismo.
            if (_handHint != null)
            {
                if (!_ballReady)
                {
                    _handHint.text = "BALÓN EN JUEGO · espera";
                    _handHint.color = new Color(1f, 0.72f, 0.35f);
                }
                else if (_gestureBlocked)
                {
                    _handHint.text = "GESTO SÍ · espera al balón";
                    _handHint.color = new Color(1f, 0.72f, 0.35f);
                }
                else if (_throwFlash > 0f)
                {
                    _handHint.text = "¡LANZADO!";
                    _handHint.color = new Color(1f, 0.95f, 0.5f);
                }
                else
                {
                    _handHint.text = _pistaTiro;
                    _handHint.color = new Color(0.6f, 1f, 0.78f);
                }
            }
        }

        /// <summary>Asigna la textura de la cámara trasera al preview del HUD.</summary>
        public void SetCameraPreview(WebCamTexture tex)
        {
            if (_cameraPreview == null || _cameraPreviewBorder == null)
            {
                return;
            }

            bool hasCamera = tex != null && tex.isPlaying;
            _cameraPreview.gameObject.SetActive(hasCamera);
            _cameraPreviewBorder.gameObject.SetActive(hasCamera);

            if (hasCamera)
            {
                _cameraPreview.texture = tex;
            }
        }

        /// <summary>Cambia el aspecto de toda la interfaz al ambiente de Halloween.</summary>
        public void SetHalloween(bool on)
        {
            _halloween = on;

            if (_menuBack != null)
            {
                _menuBack.color = on
                    ? new Color(0.12f, 0.04f, 0.17f, 0.93f)
                    : new Color(0.03f, 0.05f, 0.1f, 0.88f);
            }

            if (_gameOverBack != null)
            {
                _gameOverBack.color = on
                    ? new Color(0.15f, 0.03f, 0.06f, 0.93f)
                    : new Color(0.06f, 0.02f, 0.03f, 0.9f);
            }

            if (_menuTitle != null)
            {
                _menuTitle.text = on ? "NOCHE DE HALLOWEEN" : "VR BASKETBALL CHALLENGE";                _menuTitle.color = on ? new Color(1f, 0.56f, 0.14f) : new Color(1f, 0.72f, 0.32f);
            }

            Color baseColor = on ? new Color(0.15f, 0.06f, 0.21f, 0.94f) : new Color(0.12f, 0.14f, 0.20f, 0.92f);
            Color hoverColor = on ? new Color(0.95f, 0.45f, 0.10f, 0.95f) : new Color(0.20f, 0.42f, 0.75f, 0.95f);

            ApplyButtonColors(_menuButtons, baseColor, hoverColor);
            ApplyButtonColors(_gameOverButtons, baseColor, hoverColor);

            if (_scoreText != null)
            {
                _scoreText.color = on ? new Color(1f, 0.86f, 0.55f) : Color.white;
            }
            if (_levelText != null)
            {
                _levelText.color = on ? new Color(0.85f, 0.65f, 1f) : new Color(0.75f, 0.9f, 1f);
            }
        }

        private static void ApplyButtonColors(WorldButton[] buttons, Color baseColor, Color hoverColor)
        {
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    continue;
                }

                buttons[i].baseColor = baseColor;
                buttons[i].hoverColor = hoverColor;
                buttons[i].SetHover(0f);
            }
        }

        public void SetGameOverSummary(string text)
        {
            if (_gameOverText != null)
            {
                _gameOverText.text = text;
            }
        }

        public void ShowMenu(bool visible)
        {
            if (_menuCanvas != null)
            {
                _menuCanvas.gameObject.SetActive(visible);
            }
            if (!visible)
            {
                _hovered = null;
                _blocked = null;
            }
        }

        public void ShowGameOver(bool visible)
        {
            if (_gameOverCanvas != null)
            {
                _gameOverCanvas.gameObject.SetActive(visible);
            }
            if (!visible)
            {
                _hovered = null;
                _blocked = null;
            }
        }

        public void ShowHud(bool visible)
        {
            if (_hudCanvas != null)
            {
                _hudCanvas.gameObject.SetActive(visible);
            }
        }

        public void ShowMessage(string text, float duration = 1.6f)
        {
            _messageText.text = text;
            _messageTimer = duration;
        }

        private void SetMessage(string text)
        {
            _messageText.text = text;
            _messageTimer = 0f;
        }

        private void UpdateMessage()
        {
            if (_messageTimer <= 0f)
            {
                return;
            }

            _messageTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(_messageTimer);

            Color c = _messageText.color;
            c.a = alpha;
            _messageText.color = c;

            Color b = _messageBack.color;
            b.a = alpha * 0.45f;
            _messageBack.color = b;

            if (_messageTimer <= 0f)
            {
                _messageText.text = "";
                _messageText.color = Color.white;
                _messageBack.color = new Color(0f, 0f, 0f, 0f);
            }
        }
    }
}
