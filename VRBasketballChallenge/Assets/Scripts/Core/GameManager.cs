using System.Collections;
using UnityEngine;

namespace VRBasketball
{
    public enum GameState { Menu, Jugando, FinPartida }

    /// <summary>
    /// Orquesta la partida: calibración, menú, ronda, puntuación y aumento de dificultad.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Header("Referencias")]
        public HUD hud;
        public StereoRig rig;
        public HeadTracker tracker;
        public HoopController hoop;
        public BallController ball;
        public ThrowController thrower;
        public DifficultyManager difficulty;
        public LightingDirector lighting;
        public Transform playerHead;
        public Transform hand;
        public Transform station;

        [Header("Ambiente")]
        [Tooltip("Decoración de Halloween (se enciende con el botón del menú).")]
        public HalloweenDecor halloween;

        [Header("Control por mano")]
        [Tooltip("Rastreador de la mano con la cámara trasera: es el único 'mando' del juego.")]
        public HandTracker handTracker;

        [Header("Manos virtuales")]
        [Tooltip("Dibuja las manos con los puntos que ve MediaPipe (HUD y escena).")]
        public ManosVirtuales manos;

        [Header("Escenario")]
        [Tooltip("Marcador físico de la pared del fondo.")]
        public Scoreboard scoreboard;

        [Header("Partida")]
        public float roundTime = 75f;

        [Header("Modo demo")]
        [Tooltip("Lanza solo. Sirve para mostrar el juego sin visor y para pruebas automaticas.")]
        public bool autoDemo;
        [Tooltip("Segundos entre tiros automaticos.")]
        public float demoInterval = 2.2f;
        private float _demoTimer;

        public GameState State { get; private set; }

        // Lecturas usadas por el HUD y por el modo diagnóstico.
        public int currentScore { get { return _score; } }
        public float currentTime { get { return _timeLeft; } }
        public int madeCount { get { return _made; } }
        public int shotCount { get { return _shots; } }

        private float _timeLeft;
        private int _score;
        private int _made;
        private int _shots;
        private int _streak;
        private int _bestStreak;
        private int _record;
        private bool _awaitingResult;
        private Coroutine _respawn;

        private const string RecordKey = "VRBB_Record_v1";
        private const string HalloweenKey = "VRBB_Halloween_v1";

        private bool _halloweenOn;
        private float _ambienceTimer;
        private bool _handWasReady;

        public bool HalloweenOn { get { return _halloweenOn; } }

        private void Awake()
        {
            I = this;
            _record = PlayerPrefs.GetInt(RecordKey, 0);
        }

        private void Start()
        {
            if (hoop != null)
            {
                hoop.Scored += HandleScore;
            }

            if (lighting != null && hoop != null)
            {
                lighting.Configure(DifficultyManager.Get(1));
            }

            // El modo elegido se recuerda entre partidas. ?halloween=1 lo fuerza
            // (útil para pruebas y demostraciones).
            SetHalloween(HasUrlFlag("halloween=1") || PlayerPrefs.GetInt(HalloweenKey, 0) == 1, false);

            // Parametros de URL utiles para pruebas y demostraciones:
            //   ?demo=1     -> el juego lanza solo
            //   ?start=1    -> empieza la partida sin modo demo (útil para pruebas)
            if (HasUrlFlag("demo=1"))
            {
                autoDemo = true;
            }

            //   ?gesto=puno|empujar|subir|pellizco|toque  -> elige la forma de lanzar
            AplicarGestoDeUrl();

            //   ?simular=1&guion=puno|pellizco|subir|empujar  -> mano inventada para
            //   probar el juego (y el esqueleto) sin cámara ni móvil.
            AplicarManoSimuladaDeUrl();

            bool startNow = autoDemo || HasUrlFlag("start=1");

            // YA NO SE ESCANEA LA MANO AL PRINCIPIO: no hay pantalla previa que medir.
            // La mano la detecta MediaPipe Hand Landmarker con la camara trasera
            // mientras se juega, asi que se entra directo al menu (o a la partida).
            HandProfile profile = CalibrationStore.Current;

            if (!profile.calibrated)
            {
                profile = CalibrationStore.Neutro("mediapipe");
                CalibrationStore.Save(profile);
            }

            ApplyProfileToBall(profile);

            if (thrower != null)
            {
                thrower.RefreshProfile();
            }

            EnterMenu();

            if (startNow)
            {
                StartRound();
            }
        }

        private static bool HasUrlFlag(string flag)
        {
            string url = Application.absoluteURL;
            return !string.IsNullOrEmpty(url) && url.ToLowerInvariant().Contains(flag);
        }

        /// <summary>Lee ?simular=1 y ?guion=... (mano inventada para pruebas).</summary>
        private void AplicarManoSimuladaDeUrl()
        {
            if (handTracker == null || !HasUrlFlag("simular=1"))
            {
                return;
            }

            handTracker.simular = true;
            handTracker.guionSimulado = LeerParametro("guion", "subir");
        }

        /// <summary>Valor de un parámetro de la dirección (por ejemplo guion=puno).</summary>
        private static string LeerParametro(string clave, string porDefecto)
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return porDefecto;
            }

            int inicio = url.IndexOf(clave + "=", System.StringComparison.OrdinalIgnoreCase);
            if (inicio < 0)
            {
                return porDefecto;
            }

            inicio += clave.Length + 1;
            int fin = url.IndexOf('&', inicio);
            string valor = fin < 0 ? url.Substring(inicio) : url.Substring(inicio, fin - inicio);

            return string.IsNullOrEmpty(valor) ? porDefecto : valor;
        }

        /// <summary>
        /// Lee ?gesto=... de la dirección para empezar con una forma de lanzar
        /// concreta (útil para probar cada gesto sin tocar el menú).
        /// </summary>
        private void AplicarGestoDeUrl()
        {
            if (thrower == null)
            {
                return;
            }

            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            for (int i = 0; i < ThrowController.Modos.Length; i++)
            {
                if (url.ToLowerInvariant().Contains("gesto=" + ThrowController.Modos[i]))
                {
                    thrower.modoGesto = ThrowController.Modos[i];
                    thrower.GuardarModoGesto();
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            if (hoop != null)
            {
                hoop.Scored -= HandleScore;
            }
        }

        private void ApplyProfileToBall(HandProfile profile)
        {
            if (ball == null)
            {
                return;
            }

            // Personalización del balón a partir de la calibración de la mano:
            // mano más grande -> balón un poco más grande; más fuerza -> más masa.
            float radius = 0.115f * Mathf.Clamp(profile.sizeFactor, 0.8f, 1.25f);
            float mass = Mathf.Clamp(0.58f * profile.power, 0.42f, 0.85f);
            float bounciness = Mathf.Clamp(0.52f + 0.22f * profile.grip, 0.45f, 0.8f);

            ball.ApplyProfile(radius, mass);

            Collider col = ball.GetComponent<Collider>();
            if (col != null && col.material != null)
            {
                col.material.bounciness = bounciness;
            }
        }

        // ------------------------------------------------------------------
        // Estados
        // ------------------------------------------------------------------

        public void EnterMenu()
        {
            State = GameState.Menu;
            _awaitingResult = false;

            // La cámara sigue encendida en el menú: la mano elige los botones.
            StartHandTracking();

            if (rig != null)
            {
                rig.SetRendering(true);
            }
            if (tracker != null)
            {
                tracker.Recenter();
            }

            hud.SetButtonLabel("gesto", "TIRO: " + (thrower != null ? thrower.NombreModo : "NATURAL"));
            hud.SetThrowHint(thrower != null ? thrower.Pista : "");
            hud.ShowHud(false);
            hud.ShowGameOver(false);
            hud.ShowMenu(true);
            hud.SetRecord(_record);
            hud.SetMenuInfo(BuildMenuInfo());

            if (thrower != null)
            {
                thrower.enabled = false;
            }
            if (lighting != null)
            {
                lighting.Configure(DifficultyManager.Get(1));
            }
            if (hoop != null)
            {
                hoop.ApplyLevel(DifficultyManager.Get(1));
            }
            if (ball != null)
            {
                ball.HoldAt(hand);
            }
        }

        private string BuildMenuInfo()
        {
            HandProfile p = CalibrationStore.Current;
            return "Perfil de mano: " + p.quality + "\n"
                   + string.Format("agarre {0:0.00} · fuerza x{1:0.00} · precisión {2:0.00}",
                       p.grip, p.power, p.stability)
                   + "\nRécord: " + _record + " puntos"
                   + (p.calibrated
                       ? (p.mode == "camara" ? "\nMano escaneada con la cámara trasera" : "")
                       : "\nFalta la calibración de la mano");
        }

        public void StartRound()
        {
            State = GameState.Jugando;
            _score = 0;
            _made = 0;
            _shots = 0;
            _streak = 0;
            _bestStreak = 0;
            _awaitingResult = false;
            _timeLeft = roundTime;
            _demoTimer = demoInterval;

            difficulty.Reset();
            LevelConfig config = difficulty.Current;

            hoop.ApplyLevel(config);
            lighting.Configure(config);
            hud.SetLevel(config.level, config.nombre);
            hud.SetScore(0);
            hud.SetStreak(0, 0);
            hud.SetTime(_timeLeft);
            hud.SetRecord(_record);
            hud.ShowMenu(false);
            hud.ShowGameOver(false);
            hud.ShowHud(true);

            rig.SetRendering(true);
            tracker.Recenter();

            thrower.enabled = true;
            thrower.RefreshProfile();

            // La cámara se enciende al empezar a jugar: la mano apunta y lanza.
            StartHandTracking();

            hud.ShowMessage(_handWasReady
                ? "¡A JUGAR! Mueve la mano delante de la cámara para apuntar y " + thrower.Pista
                : "¡A JUGAR! Mira al aro y toca la pantalla para lanzar", 3.4f);

            ball.HoldAt(hand);
        }

        private void StartHandTracking()
        {
            if (handTracker == null)
            {
                return;
            }

            handTracker.Open();
            _handWasReady = handTracker.Running && !handTracker.CameraError;
        }

        private void UpdateHandView()
        {
            if (hud == null)
            {
                return;
            }

            bool detected = handTracker != null && handTracker.Detected;
            Vector2 position = handTracker != null ? handTracker.Position : new Vector2(0.5f, 0.5f);
            float coverage = handTracker != null ? handTracker.Coverage : 0f;
            bool aiming = thrower != null && thrower.HandActive;

            hud.SetHand(detected, position, coverage, aiming);

            // Los 21 puntos de MediaPipe: con ellos se dibuja la mano de verdad
            // (huesos y articulaciones) en vez de la mano de ejemplo.
            hud.SetHandPoints(handTracker != null && handTracker.TienePuntos ? handTracker.Landmarks : null);
            hud.SetThrowHint(thrower != null ? thrower.Pista : "");

            // Estado del gesto: barra que se llena al subir la mano y avisos claros
            // ("SUBE LA MANO" / "BALÓN EN JUEGO · espera").
            if (thrower != null)
            {
                hud.SetThrowState(thrower.GestureCharge, thrower.BallReady,
                    thrower.GestureBlocked, thrower.SinceLastThrow);
            }

            // Mostrar el preview de la c\u00e1mara en el HUD para que el jugador vea su mano
            if (handTracker != null)
            {
                hud.SetCameraPreview(handTracker.CameraTexture);
            }
        }

        private void EndRound()
        {
            State = GameState.FinPartida;
            _awaitingResult = false;

            // La cámara se queda encendida para poder elegir REINTENTAR / VOLVER AL MENÚ
            // con la mano.

            if (thrower != null)
            {
                thrower.enabled = false;
            }
            if (lighting != null)
            {
                lighting.Stop();
            }

            float accuracy = _shots > 0 ? (float)_made / _shots * 100f : 0f;

            if (_score > _record)
            {
                _record = _score;
                PlayerPrefs.SetInt(RecordKey, _record);
                PlayerPrefs.Save();
            }

            hud.ShowHud(false);
            hud.SetGameOverSummary(string.Format(
                "PUNTOS: {0}\n\nAciertos: {1} de {2} ({3:0}% )\n\nMejor racha: {4}\nRécord: {5}",
                _score, _made, _shots, accuracy, _bestStreak, _record));
            hud.ShowGameOver(true);
        }

        // ------------------------------------------------------------------
        // Acciones del menú
        // ------------------------------------------------------------------

        public void HandleAction(string id)
        {
            switch (id)
            {
                case "start":
                    StartRound();
                    break;

                case "gesto":
                    CambiarGesto();
                    break;

                case "retry":
                    StartRound();
                    break;

                case "menu":
                    EnterMenu();
                    break;

                case "recenter":
                    tracker.Recenter();
                    hud.ShowMessage("Vista recentrada", 1.2f);
                    break;

                case "halloween":
                    SetHalloween(!_halloweenOn, true);
                    break;

                case "stereo":
                    rig.SetStereo(!rig.stereo);
                    hud.SetButtonLabel("stereo", rig.stereo ? "VISTA: ESTÉREO" : "VISTA: UN OJO");
                    hud.ShowMessage(rig.stereo ? "Modo estéreo: usa los lentes" : "Modo un ojo: sirve para probar sin lentes", 1.6f);
                    break;
            }
        }

        /// <summary>
        /// Cambia la forma de lanzar (subir la mano, empujar, puño->abrir, pellizco...).
        /// Se recuerda entre partidas para no tener que elegirlo cada vez.
        /// </summary>
        private void CambiarGesto()
        {
            if (thrower == null)
            {
                return;
            }

            thrower.CiclarModoGesto();
            hud.SetButtonLabel("gesto", "TIRO: " + thrower.NombreModo);
            hud.SetThrowHint(thrower.Pista);
            hud.ShowMessage("TIRO: " + thrower.NombreModo + " — " + thrower.Pista, 3f);
        }

        /// <summary>Enciende o apaga el ambiente de Halloween y lo recuerda.</summary>
        public void SetHalloween(bool on, bool announce)
        {
            _halloweenOn = on;

            if (halloween != null)
            {
                halloween.SetActive(on);
            }

            if (lighting != null)
            {
                lighting.halloween = on;
                lighting.decor = halloween;
                lighting.Configure(DifficultyManager.Get(State == GameState.Jugando ? difficulty.Current.level : 1));
            }

            if (hud != null)
            {
                hud.SetButtonLabel("halloween", on ? "MODO: HALLOWEEN" : "MODO: CLÁSICO");
                hud.SetHalloween(on);
                hud.SetMenuInfo(BuildMenuInfo());
            }

            if (scoreboard != null)
            {
                scoreboard.SetHalloween(on);
            }

            PlayerPrefs.SetInt(HalloweenKey, on ? 1 : 0);
            PlayerPrefs.Save();

            _ambienceTimer = Random.Range(6f, 12f);

            if (announce && hud != null)
            {
                hud.ShowMessage(on
                    ? "NOCHE DE HALLOWEEN: calabazas encendidas y niebla morada"
                    : "Modo clásico de cancha", 2.4f);
            }
        }

        // ------------------------------------------------------------------
        // Bucle de juego
        // ------------------------------------------------------------------

        private void Update()
        {
            if (State == GameState.Jugando)
            {
                _timeLeft -= Time.deltaTime;
                hud.SetTime(_timeLeft);

                if (_timeLeft <= 0f)
                {
                    EndRound();
                    return;
                }

                HandleBallCycle();
                UpdateAmbience();
                UpdateHandView();

                if (autoDemo)
                {
                    DemoThrow();
                }

                return;
            }

            // En el menú y al terminar la partida la mano es el "mando": hay que seguir
            // leyendo la cámara para que el cursor elija los botones.
            if (State == GameState.Menu || State == GameState.FinPartida)
            {
                UpdateHandView();
            }

            if (_halloweenOn && halloween != null && halloween.Active)
            {
                UpdateAmbience();
            }
        }

        /// <summary>Truenos y aullidos lejanos: solo con Halloween encendido.</summary>
        private void UpdateAmbience()
        {
            if (!_halloweenOn || AudioSynth.I == null)
            {
                return;
            }

            _ambienceTimer -= Time.deltaTime;
            if (_ambienceTimer > 0f)
            {
                return;
            }

            _ambienceTimer = Random.Range(9f, 20f);

            if (Random.value < 0.6f)
            {
                AudioSynth.I.Thunder();
            }
            else
            {
                AudioSynth.I.Howl();
            }
        }

        /// <summary>Modo demo: apunta al aro y lanza solo, para verificar el juego completo.</summary>
        private void DemoThrow()
        {
            if (thrower == null || ball == null || hoop == null || tracker == null)
            {
                return;
            }

            if (ball.IsFlying || !thrower.CanThrow)
            {
                return;
            }

            _demoTimer += Time.deltaTime;
            if (_demoTimer < demoInterval)
            {
                return;
            }
            _demoTimer = 0f;

            Vector3 delta = hoop.AimTarget - playerHead.position;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(delta.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

            tracker.SetLook(yaw, pitch);
            thrower.TryThrow();
        }

        private void HandleBallCycle()
        {
            if (ball == null || ball.IsFlying || _respawn != null)
            {
                return;
            }

            // El balón ya terminó su vuelo: si no encestó, es un fallo.
            if (!_awaitingResult)
            {
                return;
            }

            _awaitingResult = false;
            _streak = 0;
            hud.SetStreak(_made, _shots);

            if (AudioSynth.I != null)
            {
                AudioSynth.I.Fail();
            }
            hud.ShowMessage("FALLO", 0.9f);

            // El balón vuelve antes: esperar 1 s hacía que el gesto "no respondiera".
            _respawn = StartCoroutine(RespawnBall(0.45f));
        }

        private IEnumerator RespawnBall(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (State == GameState.Jugando || State == GameState.Menu)
            {
                ball.HoldAt(hand);
            }

            _respawn = null;
        }

        public void NotifyThrow()
        {
            _awaitingResult = true;
            _shots++;
            hud.SetStreak(_made, _shots);

            if (lighting != null)
            {
                lighting.OnShot();
            }
        }

        private void HandleScore(BallController scoredBall, bool touchedRim)
        {
            if (State != GameState.Jugando)
            {
                return;
            }

            _awaitingResult = false;
            _made++;
            _streak++;
            _bestStreak = Mathf.Max(_bestStreak, _streak);

            LevelConfig config = difficulty.Current;
            int points = difficulty.PointsPerBasket(config.level);
            points += (_streak - 1) * 25;

            if (!touchedRim)
            {
                points += 50;
            }

            _score += points;

            hud.SetScore(_score);
            hud.SetStreak(_made, _shots);

            if (AudioSynth.I != null)
            {
                if (touchedRim)
                {
                    AudioSynth.I.Rim();
                }
                else
                {
                    AudioSynth.I.Swish();
                }
                AudioSynth.I.Cheer();
            }

            hud.ShowMessage(touchedRim
                ? string.Format("¡ENCESTE! +{0}", points)
                : string.Format("¡LIMPIO! +{0}", points), 1.4f);

            if (difficulty.RegisterBasket(_made))
            {
                LevelConfig next = difficulty.Current;
                hoop.ApplyLevel(next);
                lighting.Configure(next);
                hud.SetLevel(next.level, next.nombre);

                if (AudioSynth.I != null)
                {
                    AudioSynth.I.LevelUp();
                }

                hud.ShowMessage("¡NIVEL " + next.level + "! " + next.luz, 2.6f);
            }

            if (ball != null)
            {
                ball.Miss();
            }

            if (_respawn != null)
            {
                StopCoroutine(_respawn);
            }
            _respawn = StartCoroutine(RespawnBall(0.7f));
        }
    }
}
