using System.Collections;
using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Seguimiento de la mano con la cámara TRASERA (la que apunta hacia delante, la que
    /// NO está en la pantalla) DURANTE todo el juego, para que se pueda jugar sin ningún
    /// mando: la mano elige los botones del menú, apunta y el gesto de subirla lanza.
    ///
    /// Se usa la cámara trasera porque es la única que puede ver la mano que el jugador
    /// tiene delante mientras mira la pantalla (o mientras lleva el teléfono en los lentes).
    ///
    /// Es el mismo detector que la calibración (segmentación por tono de piel) pero
    /// corriendo de forma continua y a baja resolución, para no comer presupuesto al
    /// juego. Si no ve la mano simplemente deja de informar y el juego sigue con la mirada
    /// y el toque como respaldo, para no quedarse nunca atascado.
    /// </summary>
    public class HandTracker : MonoBehaviour
    {
        [Header("Cámara")]
        public int width = 320;
        public int height = 240;
        public int fps = 30;
        [Tooltip("Analiza 1 de cada N fotogramas: menos trabajo por fotograma.")]
        public int analyzeEvery = 2;
        [Tooltip("Segundos de espera tras abrir la cámara antes de empezar a analizar (la cámara tarda en entregar imagen real).")]
        public float warmupSeconds = 1.5f;

        [Header("Seguimiento")]
        [Tooltip("Cuanto más alto, más rápido se pega la marca a la mano.")]
        public float smoothing = 16f;
        [Tooltip("Segundos sin ver la mano antes de considerarla perdida.")]
        public float lostAfter = 0.3f;
        [Tooltip("Tamaño mínimo de la mano (porcentaje del encuadre) para creer que es una mano. Bajar si la mano se ve pequeña.")]
        public float minCoverage = 0.008f;

        [Header("MediaPipe Hands (Android)")]
        [Tooltip("Usa la IA MediaPipe Hand Landmarker con la cámara trasera: da los 21 puntos de la mano.")]
        public bool usarMediaPipe = true;
        [Tooltip("Resolución que se le pide a la cámara para MediaPipe.")]
        public int anchoMediaPipe = 640;
        public int altoMediaPipe = 480;
        [Tooltip("Grados que se gira la imagen antes de analizarla. -1 = automático según el móvil.")]
        public int rotacionForzada = -1;
        [Tooltip("Manos que busca MediaPipe (con una basta para jugar).")]
        public int manosMax = 2;

        [Header("Mano simulada (pruebas)")]
        [Tooltip("Usa una mano inventada en vez de la cámara: sirve para probar sin móvil (?simular=1).")]
        public bool simular;
        [Tooltip("Guion de la mano simulada: subir, empujar, puno o pellizco.")]
        public string guionSimulado = "subir";

        /// <summary>Qué está dando la mano: "mediapipe", "tono" o "ninguna".</summary>
        public string Fuente { get; private set; }
        /// <summary>Los 21 puntos de la mano (0..1, y hacia ARRIBA) cuando los da MediaPipe.</summary>
        public Vector3[] Landmarks { get { return _puntos; } }
        /// <summary>Este fotograma hay puntos articulados (dedos) disponibles.</summary>
        public bool TienePuntos { get; private set; }
        /// <summary>0 = puño cerrado, 1 = mano abierta.</summary>
        public float Apertura { get; private set; }
        /// <summary>Distancia pulgar-índice en palmos: por debajo de 0.55 es pellizco.</summary>
        public float Pinza { get; private set; }
        public bool Puno { get; private set; }
        /// <summary>Dedos estirados según MediaPipe (0..5).</summary>
        public int Dedos { get; private set; }
        /// <summary>Tamaño de la mano en el encuadre (0..1): con MediaPipe, su ancho real.</summary>
        public float Tamano { get; private set; }

        // Lecturas de MediaPipe para el diagnóstico (?diag=1).
        public int MediaPipeGrados { get; private set; }
        public int MediaPipeCuadrosCamara { get; private set; }
        public int MediaPipeAnalizados { get; private set; }
        public int MediaPipeDetecciones { get; private set; }
        public int MediaPipeMs { get; private set; }
        public string MediaPipeError { get; private set; }
        /// <summary>
        /// Cuánto está estirado cada dedo (0 = doblado, 1 = estirado).
        /// Orden: índice, medio, anular, meñique y pulgar.
        /// </summary>
        public float[] DedosEstirados { get { return _estirados; } }

        /// <summary>La cámara está abierta y entregando imagen.</summary>
        public bool Running { get; private set; }
        /// <summary>Hay una mano a la vista ahora mismo.</summary>
        public bool Detected { get; private set; }
        /// <summary>
        /// Posición de la palma en el encuadre, 0..1 (x de izquierda a derecha del mundo,
        /// y de abajo a arriba). Con la cámara trasera conserva la orientación real.
        /// </summary>
        public Vector2 Position { get; private set; }
        /// <summary>Parte del encuadre que ocupa la mano (0..1).</summary>
        public float Coverage { get; private set; }
        /// <summary>Velocidad de la palma en encuadres por segundo.</summary>
        public Vector2 Velocity { get; private set; }
        /// <summary>Confianza del último análisis.</summary>
        public float Confidence { get; private set; }
        /// <summary>Dedos contados en el último análisis.</summary>
        public int Fingers { get; private set; }

        /// <summary>
        /// Posición de la palma SIN suavizar y número de avistamientos crudos.
        /// El gesto de lanzar se mide con esto: el suavizado se come las subidas rápidas.
        /// </summary>
        public Vector2 RawPosition { get; private set; }
        public int RawSampleCount { get; private set; }

        /// <summary>Rectángulo de la mano en el encuadre (0..1, y desde abajo). Permite dibujarla.</summary>
        public Rect Bounds { get; private set; }

        public bool CameraError { get; private set; }

        /// <summary>La imagen de esta cámara viene reflejada (solo la frontal).</summary>
        public bool Mirrored { get { return _mirror; } }

        /// <summary>Textura de la c\u00e1mara trasera (para mostrar preview en el HUD).</summary>
        public WebCamTexture CameraTexture { get { return _camera; } }

        /// <summary>Mayor velocidad hacia arriba vista desde el último reinicio (diagnóstico).</summary>
        public float PeakUpVelocity { get; private set; }
        /// <summary>Fotogramas analizados con mano desde el último reinicio (diagnóstico).</summary>
        public int Samples { get; private set; }

        public void ResetPeak()
        {
            PeakUpVelocity = 0f;
            Samples = 0;
        }

        private WebCamTexture _camera;
        private Color32[] _buffer;
        private byte[] _mask;
        private int _frameCounter;
        private float _lostTimer;
        private bool _opened;
        private float _warmupTimer;
        private bool _warmedUp;
        private bool _mirror;
        private Coroutine _openRoutine;

        // MediaPipe (Android): la mano que da la IA con sus 21 puntos.
        private MediaPipeHands _mediaPipe;
        private bool _usandoMediaPipe;
        private int _secuenciaMediaPipe = -1;
        private readonly Vector3[] _puntos = new Vector3[21];
        private readonly float[] _estirados = new float[5];

        private ManoSimulada _simulada;
        private float _tiempoSimulado;

        /// <summary>Está mandando los puntos una mano inventada (no la cámara).</summary>
        public bool Simulando { get { return simular; } }

        private void RefrescarDiagnosticoMediaPipe()
        {
            if (_mediaPipe == null)
            {
                return;
            }

            MediaPipeGrados = _mediaPipe.Grados;
            MediaPipeCuadrosCamara = _mediaPipe.Cuadros;
            MediaPipeAnalizados = _mediaPipe.Analizados;
            MediaPipeDetecciones = _mediaPipe.Detecciones;
            MediaPipeMs = _mediaPipe.Milisegundos;
            MediaPipeError = _mediaPipe.Error;
        }

        // Referencia SIN suavizar: la velocidad se mide entre dos detecciones
        // consecutivas, aunque en medio se pierda y se recupere la mano.
        private Vector2 _rawPosition;
        private float _rawTime;
        private bool _hasRaw;

        // ------------------------------------------------------------------
        // Camara
        // ------------------------------------------------------------------

        public void Open()
        {
            if (_opened)
            {
                return;
            }

            _opened = true;
            CameraError = false;
            _warmedUp = false;
            _warmupTimer = 0f;

            // Android moderno exige pedir el permiso de cámara EN TIEMPO DE EJECUCIÓN.
            // Sin esta petición WebCamTexture no encuentra ninguna cámara y la mano
            // nunca aparece en pantalla. Primero se pide el permiso y luego se abre.
            _openRoutine = StartCoroutine(OpenWhenAuthorized());
        }

        private IEnumerator OpenWhenAuthorized()
        {
            bool needsPermission = Application.isMobilePlatform
                                   && !Application.HasUserAuthorization(UserAuthorization.WebCam);

            if (needsPermission)
            {
                Debug.Log("VR Basketball: pidiendo permiso de cámara al jugador...");
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            _openRoutine = null;

            if (!_opened)
            {
                yield break;
            }

            if (Application.isMobilePlatform && !Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogWarning("VR Basketball: el jugador no dio permiso de cámara; se juega con mirada y toque.");
                CameraError = true;
                Running = false;
                yield break;
            }

            OpenNow();
        }

        private void OpenNow()
        {
            // 0) Mano simulada: para probar el juego sin cámara (?simular=1).
            if (simular)
            {
                _simulada = new ManoSimulada(guionSimulado);
                _tiempoSimulado = 0f;
                Running = true;
                Fuente = "simulada";
                Debug.Log("VR Basketball: mano SIMULADA (guion " + guionSimulado + ").");
                return;
            }

            // 1) Lo primero: MediaPipe Hand Landmarker (Android). Da la mano con sus
            // 21 puntos (dedos incluidos) y aguanta fondos con texturas, que es donde
            // el detector por color de piel falla.
            if (usarMediaPipe)
            {
                if (_mediaPipe == null)
                {
                    _mediaPipe = new MediaPipeHands();
                }

                if (_mediaPipe.Disponible && _mediaPipe.Abrir(anchoMediaPipe, altoMediaPipe, rotacionForzada, manosMax))
                {
                    _usandoMediaPipe = true;
                    _secuenciaMediaPipe = -1;
                    Running = true;
                    Fuente = "mediapipe";
                    Debug.Log("VR Basketball: mano con MediaPipe Hand Landmarker (cámara trasera).");
                    return;
                }

                Debug.LogWarning("VR Basketball: MediaPipe no arrancó; se usa el detector por tono de piel.");
            }

            _usandoMediaPipe = false;
            Fuente = "tono";

            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                Debug.LogWarning("VR Basketball: no hay cámaras disponibles en el dispositivo.");
                CameraError = true;
                return;
            }

            // Log de diagnóstico: lista todas las cámaras encontradas
            for (int i = 0; i < WebCamTexture.devices.Length; i++)
            {
                Debug.Log(string.Format("VR Basketball: cámara [{0}] '{1}' frontal={2}",
                    i, WebCamTexture.devices[i].name, WebCamTexture.devices[i].isFrontFacing));
            }

            string device = FindRearCamera();
            // La cámara TRASERA no se refleja: si la mano está a la derecha del encuadre,
            // está a la derecha de verdad, y el apuntado sale en el mismo sentido en que
            // el jugador mueve la mano. Solo la FRONTAL se refleja (como un espejo) para
            // que su control no salga invertido. Antes se forzaba espejo en la trasera y
            // eso dejaba la puntería al revés (los tiros salían al lado contrario).
            _mirror = IsFrontFacing(device);
            Debug.Log("VR Basketball: abriendo cámara: '" + device + "' (espejo=" + _mirror + ")");

            try
            {
                _camera = new WebCamTexture(device, width, height, fps);
                _camera.Play();
                Running = true;
                Debug.Log("VR Basketball: cámara Play() llamado, esperando warmup...");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("VR Basketball: no se pudo abrir la camara para la mano: " + e.Message);
                // Intentar con cualquier otra cámara disponible si falla la trasera
                TryFallbackCamera();
            }
        }

        private void TryFallbackCamera()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            for (int i = 0; i < devices.Length; i++)
            {
                try
                {
                    _camera = new WebCamTexture(devices[i].name, width, height, fps);
                    _camera.Play();
                    _mirror = devices[i].isFrontFacing;
                    Running = true;
                    Debug.Log("VR Basketball: usando cámara de repuesto: '" + devices[i].name + "'");
                    return;
                }
                catch { }
            }
            CameraError = true;
            Running = false;
            Debug.LogWarning("VR Basketball: no se pudo abrir ninguna cámara.");
        }

        private static bool IsFrontFacing(string deviceName)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].name == deviceName)
                {
                    return devices[i].isFrontFacing;
                }
            }
            return false;
        }

        /// <summary>
        /// Busca la cámara TRASERA (la que apunta hacia delante): primero la que Android
        /// marca como no frontal, después por nombre y, si no, la primera de la lista.
        /// </summary>
        public static string FindRearCamera()
        {
            WebCamDevice[] devices = WebCamTexture.devices;

            if (devices == null || devices.Length == 0)
            {
                return null;
            }

            // Primera opción: la que Android marca como NO frontal (la trasera).
            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing)
                {
                    return devices[i].name;
                }
            }

            // Segunda opción: por nombre.
            for (int i = 0; i < devices.Length; i++)
            {
                string n = devices[i].name.ToLowerInvariant();
                if (n.Contains("back") || n.Contains("rear") || n.Contains("environment") || n.Contains("trasera"))
                {
                    return devices[i].name;
                }
            }

            // Tercera opción: la primera de la lista (en Android suele ser la trasera).
            return devices[0].name;
        }

        public void Close()
        {
            if (_mediaPipe != null)
            {
                _mediaPipe.Cerrar();
            }

            _usandoMediaPipe = false;
            Fuente = "ninguna";
            TienePuntos = false;
            Dedos = 0;
            Apertura = 0f;
            Pinza = 1f;
            Puno = false;
            Tamano = 0f;

            for (int i = 0; i < _estirados.Length; i++)
            {
                _estirados[i] = 0f;
            }

            if (_camera != null)
            {
                if (_camera.isPlaying)
                {
                    _camera.Stop();
                }
                _camera = null;
            }

            if (_openRoutine != null)
            {
                StopCoroutine(_openRoutine);
                _openRoutine = null;
            }

            _opened = false;
            Running = false;
            Detected = false;
            Coverage = 0f;
            Bounds = new Rect(0f, 0f, 0f, 0f);
            RawSampleCount = 0;
            Velocity = Vector2.zero;
            _warmedUp = false;
            _warmupTimer = 0f;
            _frameCounter = 0;
        }

        private void OnDestroy()
        {
            Close();
        }

        // ------------------------------------------------------------------
        // Analisis continuo
        // ------------------------------------------------------------------

        private void Update()
        {
            if (!Running)
            {
                return;
            }

            // Mano simulada: no hay cámara ni MediaPipe de por medio.
            if (simular)
            {
                ActualizarSimulado();
                return;
            }

            // Con MediaPipe la imagen la lleva Java: aquí solo se lee el resultado.
            if (_usandoMediaPipe)
            {
                ActualizarMediaPipe();
                return;
            }

            if (_camera == null)
            {
                return;
            }

            if (!_camera.isPlaying)
            {
                return;
            }

            // La cámara tarda en entregar imagen real: esperar warmup antes de analizar
            if (!_warmedUp)
            {
                _warmupTimer += Time.deltaTime;
                if (_warmupTimer < warmupSeconds)
                {
                    return;
                }
                // Verificar que la cámara ya entrega resolución válida
                if (_camera.width < 16 || _camera.height < 16)
                {
                    return; // todavía no lista, seguir esperando
                }
                _warmedUp = true;
                Debug.Log(string.Format("VR Basketball: cámara lista {0}x{1}, iniciando detección de mano.",
                    _camera.width, _camera.height));
            }

            if (_camera.width < 16 || _camera.height < 16)
            {
                return;
            }

            _frameCounter++;

            if (_frameCounter % Mathf.Max(1, analyzeEvery) != 0)
            {
                DecayVelocity(2f);
                return;
            }

            Analyze();
            DecayVelocity(2f);
        }

        /// <summary>
        /// La velocidad solo se apaga poco a poco: si se borrara de golpe, a pocos
        /// fotogramas por segundo el gesto se perderia antes de poder lanzar.
        /// </summary>
        private void DecayVelocity(float perSecond)
        {
            if (!Detected)
            {
                return;
            }

            float keep = Mathf.Max(0f, 1f - Time.deltaTime * perSecond);
            Velocity *= keep;
        }

        private void Analyze()
        {
            int w = _camera.width;
            int h = _camera.height;

            if (_buffer == null || _buffer.Length != w * h)
            {
                _buffer = new Color32[w * h];
                _mask = new byte[w * h];
            }

            try
            {
                _camera.GetPixels32(_buffer);
            }
            catch
            {
                return;
            }

            HandMetrics m = HandAnalyzer.Analyze(_buffer, w, h, _mask);

            if (m.detected && m.skinRatio >= minCoverage)
            {
                // La cámara trasera NO se refleja: si la mano está a la izquierda del
                // encuadre, está a la izquierda de verdad. Solo la cámara frontal se
                // refleja (para que coincida con lo que se ve en la pantalla).
                Vector2 raw = _mirror
                    ? new Vector2(1f - m.centroid.x, m.centroid.y)
                    : new Vector2(m.centroid.x, m.centroid.y);

                Rect b = m.bounds;
                if (_mirror)
                {
                    b = new Rect(1f - b.x - b.width, b.y, b.width, b.height);
                }

                Ingesta(raw, Mathf.Max(m.skinRatio, m.bounds.height * m.bounds.width),
                    m.confidence, m.fingerRuns, b, null, false, Time.deltaTime);

                // El detector por color no da dedos articulados: se estima la apertura.
                TienePuntos = false;
                Apertura = m.openness;
                Puno = m.openness < 0.3f;
                Pinza = 1f;
                Dedos = m.fingerRuns;

                for (int i = 0; i < _estirados.Length; i++)
                {
                    _estirados[i] = m.openness;
                }
            }
            else
            {
                Perdida();
            }
        }

        /// <summary>Genera una mano inventada con su guion (pruebas sin móvil).</summary>
        private void ActualizarSimulado()
        {
            if (_simulada == null)
            {
                _simulada = new ManoSimulada(guionSimulado);
            }

            _tiempoSimulado += Time.deltaTime;
            _simulada.Actualizar(Time.deltaTime, _tiempoSimulado);

            for (int i = 0; i < _puntos.Length && i < _simulada.Puntos.Length; i++)
            {
                _puntos[i] = _simulada.Puntos[i];
            }

            AnalisisMano.MedirDedos(_puntos, _estirados, out float apertura, out int dedos);

            Rect recuadro = new Rect(
                _simulada.Centro.x - _simulada.Ancho * 0.5f,
                _simulada.Centro.y - _simulada.Alto * 0.5f,
                _simulada.Ancho,
                _simulada.Alto);

            Ingesta(_simulada.Centro, _simulada.Cobertura, 1f, dedos, recuadro, _puntos, true, Time.deltaTime);

            Dedos = dedos;
            Apertura = apertura;
            Pinza = AnalisisMano.MedirPinza(_puntos);
            Puno = apertura < 0.35f;
            TienePuntos = true;
        }

        /// <summary>Lee lo que ha visto MediaPipe en este fotograma.</summary>
        private void ActualizarMediaPipe()
        {
            if (_mediaPipe == null)
            {
                return;
            }

            _mediaPipe.Actualizar();
            RefrescarDiagnosticoMediaPipe();

            if (!_mediaPipe.Listo)
            {
                if (!string.IsNullOrEmpty(_mediaPipe.Error) && _mediaPipe.Error != "")
                {
                    CameraError = true;
                }
                return;
            }

            CameraError = false;

            if (!_mediaPipe.Detectado)
            {
                TienePuntos = false;
                Dedos = 0;
                Apertura = 0f;
                Pinza = 1f;
                Puno = false;
                Tamano = 0f;
                Perdida();
                return;
            }

            bool muestraNueva = _mediaPipe.Secuencia != _secuenciaMediaPipe;
            _secuenciaMediaPipe = _mediaPipe.Secuencia;

            if (muestraNueva)
            {
                // Recuadro de la mano en el encuadre (y hacia arriba, como el resto).
                Rect recuadro = new Rect(
                    _mediaPipe.Posicion.x - _mediaPipe.Ancho * 0.5f,
                    _mediaPipe.Posicion.y - _mediaPipe.Alto * 0.5f,
                    _mediaPipe.Ancho,
                    _mediaPipe.Alto);

                float ahora = Time.time;
                float dt = _hasRaw ? Mathf.Max(0.001f, ahora - _rawTime) : Time.deltaTime;

                Ingesta(_mediaPipe.Posicion, _mediaPipe.Cobertura, _mediaPipe.Confianza,
                    _mediaPipe.Dedos, recuadro, _mediaPipe.Puntos, true, dt);
            }

            Dedos = _mediaPipe.Dedos;
            Apertura = _mediaPipe.Apertura;
            Pinza = _mediaPipe.Pinza;
            Puno = _mediaPipe.Puno;
            TienePuntos = _mediaPipe.Detectado;

            for (int i = 0; i < _estirados.Length && i < _mediaPipe.Estirados.Length; i++)
            {
                _estirados[i] = _mediaPipe.Estirados[i];
            }
        }

        /// <summary>Un fotograma con mano: velocidad, suavizado y estado.</summary>
        private void Ingesta(Vector2 raw, float coverage, float confidence, int fingers, Rect bounds,
            Vector3[] puntos, bool conPuntos, float dtMuestreo)
        {
            float now = Time.time;

            float dt = now - _rawTime;
            if (_hasRaw && dt > 0.005f && dt < 1.2f)
            {
                Vector2 instant = (raw - _rawPosition) / dt;

                // Un salto imposible es un fallo de deteccion, no un movimiento.
                if (instant.magnitude < 14f)
                {
                    Velocity = Vector2.Lerp(Velocity, instant, 0.65f);
                }
            }
            else
            {
                Velocity = Vector2.zero;
            }

            // El suavizado se aplica con el tiempo que ha pasado de verdad entre dos
            // muestras: MediaPipe manda unas 10 por segundo, no una por fotograma.
            Position = Detected
                ? Vector2.Lerp(Position, raw, Mathf.Clamp01(smoothing * Mathf.Max(Time.deltaTime, dtMuestreo)))
                : raw;

            _rawPosition = raw;
            _rawTime = now;
            _hasRaw = true;

            RawPosition = raw;
            RawSampleCount++;

            if (conPuntos && puntos != null)
            {
                int total = Mathf.Min(_puntos.Length, puntos.Length);
                for (int i = 0; i < total; i++)
                {
                    _puntos[i] = puntos[i];
                }
            }

            Bounds = bounds;

            Samples++;
            if (Velocity.y > PeakUpVelocity)
            {
                PeakUpVelocity = Velocity.y;
            }

            Coverage = coverage;
            Confidence = confidence;
            Fingers = fingers;
            _lostTimer = lostAfter;
            Tamano = conPuntos && bounds.width > 0.0001f ? bounds.width : coverage;
            Detected = true;
        }

        /// <summary>Un fotograma sin mano: se mantiene un instante antes de apagarla.</summary>
        private void Perdida()
        {
            float now = Time.time;

            // Se recuerda un momento para no parpadear en cada fotograma perdido.
            _lostTimer -= Mathf.Max(0.001f, now - _rawTime);

            if (_lostTimer <= 0f)
            {
                Detected = false;
                Coverage = 0f;
                Confidence = 0f;
                Fingers = 0;
                Bounds = new Rect(0f, 0f, 0f, 0f);
            }

            // Tras un rato sin verla, el proximo avistamiento empieza de cero
            // (asi no se mide velocidad entre dos posiciones lejanas en el tiempo).
            if (now - _rawTime > 0.5f)
            {
                _hasRaw = false;
                Velocity = Vector2.zero;
            }
        }
    }
}
