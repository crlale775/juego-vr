using System;
using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Sistema de tiro. El jugador apunta y lanza <b>con la mano delante de la cámara</b>
    /// (sin ningún mando): la posición de la palma gira la puntería y el gesto de subirla
    /// lanza el balón. Si la cámara no ve la mano, siguen funcionando la mirada para
    /// apuntar y el toque en pantalla (botón de Cardboard) para lanzar.
    /// La calibración de la mano decide la fuerza, el tamaño del balón y la precisión.
    /// </summary>
    public class ThrowController : MonoBehaviour
    {
        [Header("Referencias")]
        public Transform head;
        public Transform handAnchor;
        public HoopController hoop;
        public BallController ball;
        [Tooltip("Rastreador de la mano por cámara (opcional pero recomendado).")]
        public HandTracker handTracker;

        [Header("Control por mano")]
        [Tooltip("Permite apuntar y lanzar con la mano delante de la cámara.")]
        public bool handControl = true;
        [Tooltip("Grados de giro horizontal con la mano en el borde del encuadre.")]
        public float handYawRange = 32f;
        [Tooltip("Grados de giro vertical con la mano en el borde del encuadre.")]
        public float handPitchRange = 20f;
        [Header("Gestos de lanzar")]
        [Tooltip("Modo de tiro: todos, subir, empujar, puno, pellizco o toque.")]
        public string modoGesto = "todos";
        [Tooltip("Tiempo (s) en el que se busca la subida de la mano. Más alto = más fácil.")]
        public float riseWindow = 0.6f;
        [Tooltip("Cuánto tiene que subir la mano dentro de esa ventana (0..1 del encuadre).")]
        public float riseDistance = 0.09f;
        [Tooltip("Velocidad hacia arriba (encuadres por segundo) que confirma el lanzamiento.")]
        public float handThrowSpeed = 0.18f;
        [Tooltip("Ventana (s) del empujón hacia la cámara.")]
        public float pushWindow = 0.7f;
        [Tooltip("Cuánto tiene que crecer la mano para contar como empujón (1.16 = un 16% más grande).")]
        public float pushRatio = 1.16f;
        [Tooltip("Tiempo (s) máximo entre cerrar el puño y abrirlo.")]
        public float fistWindow = 0.9f;
        [Tooltip("Apertura por debajo de la cual se considera puño cerrado.")]
        public float fistClosed = 0.42f;
        [Tooltip("Apertura por encima de la cual la mano está abierta.")]
        public float fistOpen = 0.60f;
        [Tooltip("Tiempo (s) máximo entre pellizcar y soltar.")]
        public float pinchWindow = 0.8f;
        [Tooltip("Pellizco: por debajo de esto el pulgar y el índice están juntos.")]
        public float pinchClosed = 0.65f;
        [Tooltip("Pellizco: por encima de esto se han separado (se suelta el balón).")]
        public float pinchOpen = 0.90f;
        [Tooltip("Cuánto tiene que verse la mano para hacerle caso.")]
        public float handMinCoverage = 0.015f;
        [Tooltip("Tiempo (s) que se recuerda un gesto hecho mientras el balón no estaba listo.")]
        public float pendingThrowWindow = 0.8f;

        [Header("Tiro")]
        [Tooltip("Ángulo de salida base en grados. Alto = arco cómodo que entra fácil.")]
        public float baseElevation = 55f;
        [Tooltip("Cuánto manda la puntería en el ángulo (0 = ángulo fijo del juego).")]
        public float elevationFromAim = 0.4f;
        [Tooltip("Ángulo mínimo de salida (grados). Por debajo, el balón llega plano y no entra.")]
        public float minElevation = 48f;
        [Tooltip("Ángulo máximo de salida (grados).")]
        public float maxElevation = 65f;
        [Tooltip("Error angular máximo por calibración débil, en grados.")]
        public float maxSpread = 1.0f;
        [Tooltip("Ayuda de puntería mínima. Alta = el balón se corrige hacia el aro.")]
        public float minAssist = 0.99f;
        [Tooltip("Ayuda de puntería máxima.")]
        public float maxAssist = 1f;
        [Tooltip("Ángulo (grados) dentro del cual la ayuda es total. Fuera de ahí aún ayuda algo.")]
        public float assistCone = 90f;
        [Tooltip("Cuánto se adelanta la puntería al movimiento del aro (0 = nada, 1 = todo).")]
        public float hoopLead = 0.75f;

        [Header("Entrada")]
        [Tooltip("Umbral de la sacudida del teléfono (g/s).")]
        public float flickThreshold = 14f;
        [Tooltip("Tiempo entre tiros.")]
        public float cooldown = 0.55f;

        public event Action OnThrow;

        public bool CanThrow
        {
            get { return _cooldown <= 0f && ball != null && !ball.IsFlying; }
        }

        private float _cooldown;
        private Vector3 _lastAcceleration;
        private float _flickBoost;
        private float _flickWindow;
        private Vector3 _pointerDownPosition;
        private float _pointerDownTime;

        private HandProfile _profile;

        private bool _handWasVisible;
        private float _lostHandTimer;
        private float _lastHandY = 1f;
        private float _lastHandUpSpeed;

        // Estado de los gestos que no son la subida (empujón, puño y pellizco).
        private bool _empujonArmado = true;
        private bool _punoCerrado;
        private float _punoTiempo;
        private bool _pellizcoCerrado;
        private float _pellizcoTiempo;
        private string _pendingGesto = "subir";

        private const string ClaveModo = "VRBB_GestoTiro_v1";

        /// <summary>Todas las formas de lanzar, en el orden en que se eligen en el menú.</summary>
        public static readonly string[] Modos = { "todos", "subir", "empujar", "puno", "pellizco", "toque" };

        /// <summary>Empujón actual: cuánto ha crecido la mano (1 = mismo tamaño).</summary>
        public float Push { get; private set; }
        /// <summary>Último gesto reconocido (diagnóstico).</summary>
        public string ultimoGesto = "-";

        [HideInInspector] public int gestosSubir;
        [HideInInspector] public int gestosEmpujar;
        [HideInInspector] public int gestosPuno;
        [HideInInspector] public int gestosPellizco;

        // Historial corto de las posiciones CRUDAS de la mano. El gesto de lanzar se
        // mide como "cuánto subió en los últimos X segundos": con una sola velocidad
        // instantánea se perdían muchas subidas (por eso a veces lanzaba y a veces no).
        private const int RiseSlots = 32;
        private readonly float[] _riseY = new float[RiseSlots];
        private readonly float[] _riseT = new float[RiseSlots];
        private readonly float[] _muestraTamano = new float[RiseSlots];
        private readonly float[] _muestraApertura = new float[RiseSlots];
        private readonly float[] _muestraPinza = new float[RiseSlots];
        private int _riseHead;
        private int _riseCount;
        private int _lastRawSamples;
        private float _riseArmedY = 1f;
        private float _blockedTimer;

        // Gesto visto mientras el balón no estaba listo: se recuerda un momento y sale
        // en cuanto el balón vuelve. Sin esto muchos gestos se perdían.
        private float _pendingThrow;

        /// <summary>0..1 de lo avanzado que está el gesto de subir la mano (para la barra del HUD).</summary>
        public float GestureCharge { get; private set; }
        /// <summary>Segundos desde el último tiro (para el destello de "lanzado").</summary>
        public float SinceLastThrow { get; private set; } = 99f;
        /// <summary>El balón está en la mano: se puede lanzar ya.</summary>
        public bool BallReady { get { return CanThrow; } }
        /// <summary>Se vio el gesto pero el balón no estaba listo (aviso para el jugador).</summary>
        public bool GestureBlocked { get; private set; }
        /// <summary>Subida actual de la mano en la ventana del gesto (0..1).</summary>
        public float GestureRise { get; private set; }

        /// <summary>Nombre corto del modo de tiro elegido (para el menú).</summary>
        public string NombreModo
        {
            get
            {
                switch (modoGesto)
                {
                    case "subir": return "SUBIR LA MANO";
                    case "empujar": return "EMPUJAR";
                    case "puno": return "PUÑO Y ABRIR";
                    case "pellizco": return "PELLIZCO";
                    case "toque": return "TOQUE EN PANTALLA";
                    default: return "NATURAL (TODAS)";
                }
            }
        }

        /// <summary>Cómo lanzar ahora mismo, en una frase (avisos del HUD).</summary>
        public string Pista
        {
            get
            {
                switch (modoGesto)
                {
                    case "subir": return "SUBE LA MANO DE GOLPE para lanzar";
                    case "empujar": return "EMPUJA LA MANO hacia la cámara para lanzar";
                    case "puno": return "CIERRA EL PUÑO Y ÁBRELO para lanzar";
                    case "pellizco": return "PELLIZCA con el pulgar y suelta para lanzar";
                    case "toque": return "TOCA LA PANTALLA para lanzar";
                    default: return "Sube la mano, empuja, cierra el puño y ábrelo o pellizca";
                }
            }
        }

        /// <summary>Hay una mano a la vista y se está usando para apuntar.</summary>
        public bool HandActive
        {
            get { return handControl && handTracker != null && handTracker.Detected && handTracker.Coverage >= handMinCoverage; }
        }

        public HandTracker Tracker { get { return handTracker; } }

        /// <summary>Gestos de lanzamiento vistos por la cámara (aunque el tiro estuviera en espera).</summary>
        [HideInInspector] public int handGestures;
        /// <summary>Tiros que salieron de verdad con el gesto de la mano.</summary>
        [HideInInspector] public int handThrows;
        [HideInInspector] public int tapThrows;
        [HideInInspector] public Vector3 lastAim = Vector3.forward;
        /// <summary>Escribe en el registro cada tiro (para el modo diagnóstico).</summary>
        [HideInInspector] public bool logTiros;

        public void RefreshProfile()
        {
            _profile = CalibrationStore.Current;
        }

        private void Start()
        {
            RefreshProfile();
            _lastAcceleration = Input.acceleration;

            // El modo de tiro se recuerda entre partidas.
            string guardado = PlayerPrefs.GetString(ClaveModo, modoGesto);
            if (System.Array.IndexOf(Modos, guardado) >= 0)
            {
                modoGesto = guardado;
            }
        }

        /// <summary>Pasa al siguiente modo de tiro (botón TIRO del menú).</summary>
        public void CiclarModoGesto()
        {
            int i = System.Array.IndexOf(Modos, modoGesto);
            modoGesto = Modos[(i + 1 + Modos.Length) % Modos.Length];
            GuardarModoGesto();
        }

        /// <summary>Recuerda el modo de tiro entre partidas.</summary>
        public void GuardarModoGesto()
        {
            PlayerPrefs.SetString(ClaveModo, modoGesto);
            PlayerPrefs.Save();
        }

        /// <summary>¿Está permitido este gesto con el modo elegido?</summary>
        public bool Activo(string gesto)
        {
            return modoGesto == "todos" || modoGesto == gesto;
        }

        private void Update()
        {
            if (_cooldown > 0f)
            {
                _cooldown -= Time.deltaTime;
            }

            if (_flickWindow > 0f)
            {
                _flickWindow -= Time.deltaTime;
            }
            else
            {
                _flickBoost = 0f;
            }

            SinceLastThrow += Time.deltaTime;

            if (_blockedTimer > 0f)
            {
                _blockedTimer -= Time.deltaTime;
                GestureBlocked = true;
            }
            else
            {
                GestureBlocked = false;
            }

            PushHandSamples();
            DetectFlick();
            DetectHandThrow();
            DetectTap();
            FlushPendingThrow();

            if (ball != null && ball.IsFlying)
            {
                return;
            }

            if (handAnchor != null && ball != null)
            {
                ball.transform.position = handAnchor.position;
                ball.transform.rotation = handAnchor.rotation;
            }
        }

        private void DetectFlick()
        {
            Vector3 a = Input.acceleration;
            float delta = (a - _lastAcceleration).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastAcceleration = a;

            if (delta > flickThreshold && a.y > 0.15f && _cooldown <= 0f && !ball.IsFlying)
            {
                _flickBoost = 0.06f;   // pequeño extra de fuerza al lanzar con la mano
                _flickWindow = 0.5f;
            }
        }

        private void DetectTap()
        {
            bool pressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
            Vector3 position = Input.mousePosition;

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                pressed |= t.phase == TouchPhase.Began;
                if (t.phase == TouchPhase.Began)
                {
                    position = t.position;
                }
            }

            if (pressed)
            {
                _pointerDownPosition = position;
                _pointerDownTime = Time.time;
            }

            bool released = Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.Space);
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended)
            {
                released = true;
                position = Input.GetTouch(0).position;
            }

            if (!released)
            {
                return;
            }

            // Cuenta como toque solo si el dedo apenas se movió (evita chocar con mirar arrastrando).
            float moved = Vector3.Distance(position, _pointerDownPosition);
            float held = Time.time - _pointerDownTime;

            if (moved < 90f && held < 1.2f)
            {
                TryThrow();
            }
        }

        /// <summary>
        /// Gesto de lanzamiento con la mano: subirla rápido delante de la cámara.
        /// Es la forma de lanzar sin mando ni botón.
        /// </summary>
        /// <summary>Guarda la posición cruda de la mano cada vez que llega un fotograma nuevo.</summary>
        private void PushHandSamples()
        {
            if (handTracker == null || !handTracker.Detected)
            {
                return;
            }

            if (handTracker.RawSampleCount == _lastRawSamples)
            {
                return;   // el mismo fotograma: no se repite
            }

            _lastRawSamples = handTracker.RawSampleCount;

            _riseY[_riseHead] = handTracker.RawPosition.y;
            _riseT[_riseHead] = Time.time;
            _muestraTamano[_riseHead] = handTracker.Tamano;
            _muestraApertura[_riseHead] = handTracker.Apertura;
            _muestraPinza[_riseHead] = handTracker.Pinza;
            _riseHead = (_riseHead + 1) % RiseSlots;

            if (_riseCount < RiseSlots)
            {
                _riseCount++;
            }
        }

        /// <summary>Cuánto ha subido la mano dentro de la ventana del gesto (0..1).</summary>
        private float RiseAmount()
        {
            if (_riseCount < 2)
            {
                return 0f;
            }

            int newest = (_riseHead - 1 + RiseSlots) % RiseSlots;
            float now = _riseT[newest];
            float lowest = _riseY[newest];

            for (int i = 0; i < _riseCount; i++)
            {
                int idx = (_riseHead - 1 - i + RiseSlots * 2) % RiseSlots;

                if (now - _riseT[idx] > riseWindow)
                {
                    break;   // los siguientes son aún más antiguos
                }

                if (_riseY[idx] < lowest)
                {
                    lowest = _riseY[idx];
                }
            }

            return Mathf.Clamp01(_riseY[newest] - lowest);
        }

        private void DetectHandThrow()
        {
            if (ball == null)
            {
                return;
            }

            if (!HandActive)
            {
                GestureRise = 0f;
                GestureCharge = Mathf.MoveTowards(GestureCharge, 0f, Time.deltaTime * 3f);
                _punoCerrado = false;
                _pellizcoCerrado = false;
                DetectHandRelease();
                return;
            }

            _handWasVisible = true;
            _lostHandTimer = 0f;
            _lastHandY = handTracker.Position.y;
            _lastHandUpSpeed = Mathf.Max(0f, handTracker.Velocity.y);

            float rise = RiseAmount();
            GestureRise = rise;

            float empujon = MedirEmpujon();
            Push = empujon;

            // La barra del HUD se llena con cualquiera de los gestos y baja sola cuando
            // la mano se queda quieta: es el aviso de que el gesto se está notando.
            float cargaSubida = Mathf.Clamp01(rise / Mathf.Max(0.01f, riseDistance));
            float cargaEmpujon = Mathf.Clamp01((empujon - 1f) / Mathf.Max(0.01f, pushRatio - 1f));
            float carga = Mathf.Max(cargaSubida, cargaEmpujon);

            GestureCharge = carga > 0.01f
                ? Mathf.Max(GestureCharge, carga)
                : Mathf.MoveTowards(GestureCharge, 0f, Time.deltaTime * 3f);

            // Se prueban todas las formas de lanzar activas: la primera que se cumple lanza.
            if (Activo("subir") && SubidaLista(rise))
            {
                RegistrarGesto("subir");
                return;
            }

            if (Activo("empujar") && EmpujonListo())
            {
                RegistrarGesto("empujar");
                return;
            }

            if (Activo("pellizco") && PellizcoSoltado())
            {
                RegistrarGesto("pellizco");
                return;
            }

            if (Activo("puno") && PunoAbierto())
            {
                RegistrarGesto("puno");
            }
        }

        /// <summary>Gesto 1: subir la mano de golpe (el de siempre, ahora más permisivo).</summary>
        private bool SubidaLista(float rise)
        {
            Vector2 p = handTracker.Position;

            // Rearme: hace falta que la mano BAJE antes de contar otro lanzamiento.
            if (p.y < _riseArmedY - riseDistance * 0.6f || p.y < 0.25f)
            {
                _riseArmedY = 0f;
            }

            if (p.y < _riseArmedY)
            {
                return false;   // gesto ya usado; se espera a que baje la mano
            }

            if (rise < riseDistance)
            {
                return false;
            }

            // Una subida lenta y pequeña no es un lanzamiento intencionado.
            if (handTracker.Velocity.y < handThrowSpeed && rise < riseDistance * 1.6f)
            {
                return false;
            }

            // La mano tiene que estar más o menos delante, no en un borde raro.
            if (Mathf.Abs(p.x - 0.5f) > 0.5f || p.y > 0.98f)
            {
                return false;
            }

            _riseArmedY = p.y;
            return true;
        }

        /// <summary>Cuánto ha crecido la mano en la ventana (empujar hacia la cámara).</summary>
        private float MedirEmpujon()
        {
            if (_riseCount < 3)
            {
                return 0f;
            }

            float ahora = _muestraTamano[( _riseHead - 1 + RiseSlots) % RiseSlots];
            if (ahora <= 0.0001f)
            {
                return 0f;
            }

            float minimo = 0f;

            for (int i = 0; i < _riseCount; i++)
            {
                int idx = (_riseHead - 1 - i + RiseSlots * 2) % RiseSlots;

                if (Time.time - _riseT[idx] > pushWindow)
                {
                    break;
                }

                float valor = _muestraTamano[idx];

                if (valor > 0.0001f && (minimo <= 0f || valor < minimo))
                {
                    minimo = valor;
                }
            }

            return minimo > 0.0001f ? ahora / minimo : 0f;
        }

        /// <summary>Gesto 2: empujar la mano hacia la cámara (se ve más grande de golpe).</summary>
        private bool EmpujonListo()
        {
            if (Push < pushRatio * 0.85f)
            {
                _empujonArmado = true;   // se alejó: listo para el siguiente empujón
                return false;
            }

            if (Push < pushRatio || !_empujonArmado)
            {
                return false;
            }

            Vector2 p = handTracker.Position;
            if (Mathf.Abs(p.x - 0.5f) > 0.5f)
            {
                return false;
            }

            _empujonArmado = false;
            return true;
        }

        /// <summary>Gesto 3: cerrar el puño y abrir la mano (soltar el balón).</summary>
        private bool PunoAbierto()
        {
            float apertura = handTracker.Apertura;

            if (apertura < fistClosed)
            {
                _punoCerrado = true;
                _punoTiempo = Time.time;
                return false;
            }

            if (!_punoCerrado)
            {
                return false;
            }

            if (apertura > fistOpen && Time.time - _punoTiempo <= fistWindow)
            {
                _punoCerrado = false;
                return true;
            }

            if (Time.time - _punoTiempo > fistWindow)
            {
                _punoCerrado = false;   // se hizo un puño y no lo abrió: se olvida
            }

            return false;
        }

        /// <summary>Gesto 4: pellizcar con el pulgar y el índice y soltar.</summary>
        private bool PellizcoSoltado()
        {
            // El pellizco necesita los puntos de los dedos (MediaPipe).
            if (!handTracker.TienePuntos)
            {
                return false;
            }

            float pinza = handTracker.Pinza;

            if (pinza < pinchClosed)
            {
                _pellizcoCerrado = true;
                _pellizcoTiempo = Time.time;
                return false;
            }

            if (!_pellizcoCerrado)
            {
                return false;
            }

            if (pinza > pinchOpen && Time.time - _pellizcoTiempo <= pinchWindow)
            {
                _pellizcoCerrado = false;
                return true;
            }

            if (Time.time - _pellizcoTiempo > pinchWindow)
            {
                _pellizcoCerrado = false;
            }

            return false;
        }

        /// <summary>
        /// Apunta el gesto (para el diagnóstico y los avisos) y lanza si el balón está
        /// listo. Si no lo está, el gesto se recuerda un momento y sale en cuanto vuelve.
        /// </summary>
        private void RegistrarGesto(string gesto)
        {
            ultimoGesto = gesto;
            handGestures++;
            GestureCharge = 1f;

            if (gesto == "empujar") gestosEmpujar++;
            else if (gesto == "puno") gestosPuno++;
            else if (gesto == "pellizco") gestosPellizco++;
            else gestosSubir++;

            if (!CanThrow)
            {
                // Se vio el gesto pero el balón está en el aire o volviendo: se guarda
                // y sale en cuanto el balón vuelve, para no ignorar al jugador.
                _blockedTimer = 0.9f;
                _pendingThrow = pendingThrowWindow;
                _pendingGesto = gesto;
                return;
            }

            _flickBoost = 0.04f;
            _flickWindow = 0.5f;

            if (TryThrow(gesto))
            {
                handThrows++;
            }
        }

        /// <summary>
        /// Segunda forma de lanzar con la mano: si la mano sale del encuadre por arriba
        /// mientras sube (lo normal en un teléfono, que ve poco campo), cuenta como tiro.
        /// </summary>
        private void DetectHandRelease()
        {
            if (!handControl || handTracker == null || !_handWasVisible)
            {
                return;
            }

            _lostHandTimer += Time.deltaTime;

            if (_lostHandTimer > 0.7f)
            {
                _handWasVisible = false;
                return;
            }

            bool salioPorArriba = _lastHandY < 0.42f && _lastHandUpSpeed > 0.25f;
            if (!salioPorArriba)
            {
                return;
            }

            _handWasVisible = false;
            _riseArmedY = 1f;

            if (!Activo("subir"))
            {
                return;
            }

            RegistrarGesto("subir");
        }

        /// <summary>
        /// Lanza en cuanto se puede si había un gesto guardado: el jugador ya hizo el
        /// movimiento, así que no se le puede ignorar por estar el balón en el aire.
        /// </summary>
        private void FlushPendingThrow()
        {
            if (_pendingThrow <= 0f)
            {
                return;
            }

            _pendingThrow -= Time.deltaTime;

            if (!CanThrow)
            {
                return;
            }

            _pendingThrow = 0f;
            _blockedTimer = 0f;
            _flickBoost = 0.04f;
            _flickWindow = 0.5f;

            if (TryThrow(_pendingGesto))
            {
                handThrows++;
            }
        }

        public bool TryThrow()
        {
            return TryThrow("boton");
        }

        public bool TryThrow(string source)
        {
            if (!CanThrow)
            {
                return false;
            }

            if (ball == null || head == null)
            {
                return false;
            }

            HandProfile p = _profile ?? CalibrationStore.Current;

            Vector3 origin = ball.transform.position;
            Vector3 aimDirection = AimDirection();
            lastAim = aimDirection;

            float elevation = ElevationFromAim(aimDirection);

            // Primera puntería: al aro tal cual está ahora.
            Vector3 target = ComputeTarget(origin, aimDirection, p, Vector3.zero);
            Vector3 velocity = SolveVelocity(origin, target, elevation, p);

            // Segunda puntería: el aro se mueve mientras el balón vuela, así que se
            // apunta un poco DELANTE de donde está. Es lo que hace que un tiro mal
            // apuntado contra un aro en movimiento tienda a entrar igual.
            if (hoop != null && hoopLead > 0.01f)
            {
                float recorrido = new Vector2(target.x - origin.x, target.z - origin.z).magnitude;
                float rapidez = new Vector2(velocity.x, velocity.z).magnitude;

                if (rapidez > 0.2f)
                {
                    float vuelo = Mathf.Clamp(recorrido / rapidez, 0.1f, 2f);
                    Vector3 adelanto = hoop.RingVelocity * vuelo * hoopLead;

                    Vector3 targetConAdelanto = ComputeTarget(origin, aimDirection, p, adelanto);
                    velocity = SolveVelocity(origin, targetConAdelanto, elevation, p);
                }
            }

            if (logTiros)
            {
                Vector3 aro = hoop != null ? hoop.AimTarget : Vector3.zero;
                Vector3 haciaAro = aro - origin;
                Debug.Log(string.Format(
                    "[TIRO] origen=({0:0.00},{1:0.00},{2:0.00}) aro=({3:0.00},{4:0.00},{5:0.00}) objetivo=({6:0.00},{7:0.00},{8:0.00}) anguloAPunteria={9:0.0} gradosElevacion={10:0.0} vel={11:0.0}",
                    origin.x, origin.y, origin.z,
                    aro.x, aro.y, aro.z,
                    target.x, target.y, target.z,
                    Vector3.Angle(aimDirection, haciaAro),
                    elevation,
                    velocity.magnitude));
            }

            ball.Throw(velocity);
            _cooldown = cooldown;
            _flickBoost = 0f;
            SinceLastThrow = 0f;
            GestureCharge = 0f;

            if (source == "boton")
            {
                tapThrows++;
            }

            if (AudioSynth.I != null)
            {
                AudioSynth.I.Throw();
            }

            if (OnThrow != null)
            {
                OnThrow();
            }

            return true;
        }

        /// <summary>
        /// Dirección del tiro: la mirada, girada por la posición de la mano si la cámara
        /// la está viendo. Así se puede apuntar moviendo la mano.
        /// </summary>
        private Vector3 AimDirection()
        {
            Vector3 direction = head.forward;

            if (!HandActive)
            {
                return direction;
            }

            Vector2 p = handTracker.Position;      // 0..1, en espejo

            float yaw = (p.x - 0.5f) * 2f * handYawRange;
            float pitch = (0.5f - p.y) * 2f * handPitchRange;

            if (Mathf.Abs(yaw) > 0.01f)
            {
                direction = Quaternion.AngleAxis(yaw, Vector3.up) * direction;
            }

            if (Mathf.Abs(pitch) > 0.01f)
            {
                // En Unity, girar en positivo alrededor de "right" baja la mirada.
                direction = Quaternion.AngleAxis(-pitch, head.right) * direction;
            }

            return direction.normalized;
        }

        /// <summary>
        /// Punto al que se apunta. Se cruza la mirada con el plano del aro y luego se
        /// tira hacia el aro: cuanto mejor la calibración, más se corrige. Un tiro muy
        /// desviado (fuera del cono) todavía se corrige algo, para que sea difícil fallar.
        /// </summary>
        private Vector3 ComputeTarget(Vector3 origin, Vector3 direction, HandProfile p, Vector3 adelanto)
        {
            if (hoop == null)
            {
                return origin + direction * 8f;
            }

            Vector3 hoopCenter = hoop.AimTarget + adelanto;
            float assist = Mathf.Lerp(minAssist, maxAssist, Mathf.Clamp01(p.stability));

            Vector3 toHoop = hoopCenter - origin;
            float angleToHoop = Vector3.Angle(direction, toHoop);

            // Fuera del cono la ayuda se reduce, pero no se quita: un tiro mal apuntado
            // sigue yendo hacia el aro en vez de perderse.
            if (angleToHoop > assistCone)
            {
                float exceso = Mathf.InverseLerp(assistCone, 120f, angleToHoop);
                assist *= Mathf.Lerp(1f, 0.85f, exceso);
            }

            float planeDistance = Vector3.Dot(toHoop, direction);
            if (planeDistance < 1.5f)
            {
                planeDistance = toHoop.magnitude;
            }

            Vector3 aimPoint = origin + direction * planeDistance;
            return Vector3.Lerp(aimPoint, hoopCenter, assist);
        }

        /// <summary>
        /// Ángulo de salida del balón. Solo se deja que la puntería lo mueva un poco:
        /// así el tiro siempre sale con un arco sano aunque el gesto o la mirada sean
        /// malos, y no se queda corto.
        /// </summary>
        private float ElevationFromAim(Vector3 direction)
        {
            float pitch = Mathf.Asin(Mathf.Clamp(direction.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            return Mathf.Clamp(baseElevation + pitch * elevationFromAim, minElevation, maxElevation);
        }

        /// <summary>Resuelve la velocidad inicial para pasar por el objetivo con un ángulo dado.</summary>
        private Vector3 SolveVelocity(Vector3 origin, Vector3 target, float angleDeg, HandProfile p)
        {
            Vector3 delta = target - origin;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float distance = flat.magnitude;
            float height = delta.y;
            float g = Mathf.Abs(Physics.gravity.y);

            if (distance < 0.4f)
            {
                distance = 0.4f;
                flat = Vector3.forward * 0.4f;
            }

            float angle = angleDeg * Mathf.Deg2Rad;
            float denominator = 2f * Mathf.Cos(angle) * Mathf.Cos(angle) * (distance * Mathf.Tan(angle) - height);

            float speed;
            if (denominator > 0.05f)
            {
                speed = Mathf.Sqrt(g * distance * distance / denominator);
            }
            else
            {
                speed = Mathf.Sqrt(g * distance);
            }

            // La fuerza calibrada ajusta ligeramente la potencia del balón.
            float powerFactor = Mathf.Lerp(0.95f, 1.05f, Mathf.InverseLerp(0.7f, 1.4f, p.power)) + _flickBoost;
            speed *= powerFactor;

            // El tope de arriba es amplio a propósito: si el tiro necesita más fuerza
            // para llegar al aro, se le da. Antes se recortaba y los tiros se quedaban
            // cortos (era la causa de muchos fallos).
            speed = Mathf.Clamp(speed, 3.5f, 30f);

            Vector3 flatDir = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
            Vector3 direction = (flatDir * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)).normalized;

            // Dispersión según la calidad de la calibración.
            float spread = (1f - Mathf.Clamp01(p.stability)) * maxSpread;
            if (spread > 0.01f)
            {
                Vector3 randomAxis = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-0.35f, 0.35f),
                    UnityEngine.Random.Range(-1f, 1f)).normalized;
                direction = Quaternion.AngleAxis(UnityEngine.Random.Range(-spread, spread), randomAxis) * direction;
            }

            return direction * speed;
        }
    }
}
