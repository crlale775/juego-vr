using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Seguimiento de cabeza del jugador. Usa el giroscopio del teléfono cuando existe
    /// y, si no (escritorio / navegador), permite mirar arrastrando con el ratón o el dedo.
    /// </summary>
    public class HeadTracker : MonoBehaviour
    {
        public enum Mode { Automatico, Giroscopio, Arrastre }

        [Header("Modo")]
        public Mode mode = Mode.Automatico;

        [Header("Ajustes de la vista")]
        [Tooltip("Corrección de giro (grados) si la vista aparece inclinada en los lentes.")]
        public float rollFix = 0f;
        [Tooltip("Corrección del giro horizontal (grados).")]
        public float yawFix = 0f;
        public bool invertPitch = false;

        [Header("Arrastre (sin giroscopio)")]
        public float dragSensitivity = 0.12f;
        public float keyboardSpeed = 90f;

        public bool UsingGyro { get; private set; }
        public bool HasGyro { get; private set; }

        /// <summary>
        /// Verdadero cuando el giroscopio realmente puede dar datos AHORA MISMO.
        /// Es importante en el navegador: DeviceOrientation existe en el escritorio
        /// pero nunca entrega lecturas, y usarlo dejaría la vista girada 90 grados.
        /// </summary>
        public bool GyroActive
        {
            get
            {
                if (!UsingGyro)
                {
                    return false;
                }

                if (HasGyro)
                {
                    return true;
                }

                if (mode == Mode.Giroscopio)
                {
                    return true;
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                if (WebGLDeviceOrientation.HasData())
                {
                    return true;
                }
#endif

                return false;
            }
        }

        private float _yawOffset;
        private float _dragYaw;
        private float _dragPitch;
        private bool _dragging;
        private Vector3 _lastPointer;

        private Quaternion _applied = Quaternion.identity;

        // Compensación de horizontal elegida (+90 o -90). Se recuerda para no dar la
        // vuelta a la vista mientras el móvil está casi plano y las dos opciones empatan.
        private int _rollElegido = 0;

        private void Awake()
        {
            ApplyUrlOverrides();

            HasGyro = SystemInfo.supportsGyroscope;

            bool hasWebOrientation = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            hasWebOrientation = WebGLDeviceOrientation.IsSupported();
#endif

            if (mode == Mode.Arrastre)
            {
                UsingGyro = false;
            }
            else if (HasGyro)
            {
                Input.gyro.enabled = true;
                Input.compensateSensors = false;
                UsingGyro = true;
            }
            else if (hasWebOrientation && mode == Mode.Automatico)
            {
                UsingGyro = true;
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGLDeviceOrientation.RequestPermissionIfNeeded();
#endif
            }
            else
            {
                UsingGyro = false;
                if (mode == Mode.Automatico)
                {
                    mode = Mode.Arrastre;
                }
            }

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        /// <summary>
        /// Ajustes por URL, útiles en el navegador:
        ///   ?look=drag | look=gyro | roll=90 | yaw=45
        /// </summary>
        private void ApplyUrlOverrides()
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            url = url.ToLowerInvariant();

            if (url.Contains("look=drag"))
            {
                mode = Mode.Arrastre;
            }
            else if (url.Contains("look=gyro"))
            {
                mode = Mode.Giroscopio;
            }

            rollFix = ParseValue(url, "roll=", rollFix);
            yawFix = ParseValue(url, "yaw=", yawFix);
        }

        private static float ParseValue(string url, string key, float fallback)
        {
            int index = url.IndexOf(key, System.StringComparison.Ordinal);
            if (index < 0)
            {
                return fallback;
            }

            index += key.Length;
            int end = index;
            while (end < url.Length && (char.IsDigit(url[end]) || url[end] == '-' || url[end] == '.'))
            {
                end++;
            }

            float value;
            if (float.TryParse(url.Substring(index, end - index), out value))
            {
                return value;
            }

            return fallback;
        }

        private void Start()
        {
            Recenter();
        }

        /// <summary>Fija la dirección actual como "al frente" (solo el giro horizontal).</summary>
        public void Recenter()
        {
            _dragYaw = 0f;
            _dragPitch = 0f;

            // Se vuelve a decidir el signo del visor por si el móvil cambió de lado.
            _rollElegido = 0;

            if (UsingGyro && SystemInfo.supportsGyroscope)
            {
                Quaternion upright = GetUprightGyroRotation();
                Vector3 fwd = upright * Vector3.forward;
                float currentYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                _yawOffset = -currentYaw;
            }
            else
            {
                _yawOffset = 0f;
            }
        }

        /// <summary>Coloca la mirada en unos ángulos concretos (modo arrastre y modo demo).</summary>
        public void SetLook(float yawDegrees, float pitchDegrees)
        {
            _dragYaw = yawDegrees;
            _dragPitch = Mathf.Clamp(pitchDegrees, -85f, 85f);

            // Se aplica al instante: el modo demo apunta y lanza en el mismo fotograma,
            // y no puede esperar al siguiente Update.
            if (!GyroActive)
            {
                _applied = RawRotation();
                transform.localRotation = _applied;
            }
        }

        /// <summary>Ángulo de mirada actual en modo arrastre.</summary>
        public float DragYaw { get { return _dragYaw; } }
        public float DragPitch { get { return _dragPitch; } }

        /// <summary>Cicla la corrección de inclinación: útil si la imagen sale girada 90 grados.</summary>
        public void CycleRollFix()
        {
            rollFix = Mathf.Repeat(rollFix + 90f, 360f);
            Recenter();
        }

        private Quaternion GetUprightGyroRotation()
        {
            Quaternion g = Input.gyro.attitude;
            // Sensor Android (mano derecha) a Unity (mano izquierda):
            Quaternion gyroUnity = new Quaternion(g.x, g.y, -g.z, -g.w);

            // En un visor el teléfono va horizontal, así que hay que compensar 90° en Z.
            // Hay DOS signos posibles y el que informa el sistema (Screen.orientation)
            // puede venir al revés en Android: con el signo equivocado TODA la vista
            // —el escenario y también el texto del marcador y del menú— salía boca abajo.
            //
            // En vez de fiarse de la orientación que dice el sistema, se elige el signo
            // con la GRAVEDAD REAL (el sensor está referenciado a la Tierra): la opción
            // buena es la que deja la "arriba" de la vista apuntando hacia arriba.
            Quaternion candidatoPositivo = UprightFrom(gyroUnity, 90f);
            Quaternion candidatoNegativo = UprightFrom(gyroUnity, -90f);

            // Las dos opciones se diferencian en 180° de giro, así que la "arriba" de
            // una es exactamente la contraria de la otra: basta con el signo del primero.
            float arribaPositivo = (candidatoPositivo * Vector3.up).y;

            if (_rollElegido == 0)
            {
                // Primera vez: si está claro, se elige; si el móvil está plano (empate)
                // se usa la orientación del sistema como hasta ahora.
                if (Mathf.Abs(arribaPositivo) > 0.15f)
                {
                    _rollElegido = arribaPositivo > 0f ? 1 : -1;
                    Debug.Log("VR Basketball: orientación del visor -> roll "
                        + (_rollElegido > 0 ? "+90" : "-90") + " (arriba=" + arribaPositivo.ToString("0.00") + ")");
                }
            }
            else
            {
                // Margen amplio para cambiar: evita que la vista se gire sola cuando la
                // mano o el móvil pasan cerca de la horizontal.
                if (_rollElegido > 0 && arribaPositivo < -0.25f)
                {
                    _rollElegido = -1;
                    Debug.Log("VR Basketball: orientación del visor corregida -> roll -90");
                }
                else if (_rollElegido < 0 && arribaPositivo > 0.25f)
                {
                    _rollElegido = 1;
                    Debug.Log("VR Basketball: orientación del visor corregida -> roll +90");
                }
            }

            bool positivo = _rollElegido > 0
                || (_rollElegido == 0 && Screen.orientation != ScreenOrientation.LandscapeRight);

            return positivo ? candidatoPositivo : candidatoNegativo;
        }

        /// <summary>
        /// Compensación de visor: primero se pasa el sensor a Unity, luego se gira 90°
        /// en el marco del sensor (horizontal del visor) y por último los 90° en X que
        /// dejan "mirar al frente" cuando la cabeza está recta.
        /// </summary>
        private static Quaternion UprightFrom(Quaternion gyroUnity, float rollAngle)
        {
            Quaternion landscape = gyroUnity * Quaternion.Euler(0f, 0f, rollAngle);
            return Quaternion.Euler(90f, 0f, 0f) * landscape;
        }

        private Quaternion RawRotation()
        {
            if (UsingGyro && SystemInfo.supportsGyroscope)
            {
                Quaternion upright = GetUprightGyroRotation();
                Quaternion fix = Quaternion.Euler(invertPitch ? -180f : 0f, yawFix + _yawOffset, rollFix);
                return fix * upright;
            }

            if (Application.platform == RuntimePlatform.WebGLPlayer && WebGLDeviceOrientation.HasData())
            {
                Quaternion device = WebGLDeviceOrientation.GetRotation();
                Quaternion fix = Quaternion.Euler(invertPitch ? -180f : 0f, yawFix + _yawOffset, rollFix);
                return fix * device;
            }

            return Quaternion.Euler(_dragPitch, _dragYaw, rollFix);
        }

        private void Update()
        {
            if (GyroActive)
            {
                _applied = RawRotation();
            }
            else
            {
                HandlePointerLook();
                _applied = RawRotation();
            }

            transform.localRotation = _applied;
        }

        private void HandlePointerLook()
        {
            bool down = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            bool held = Input.GetMouseButton(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved);
            bool up = Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);

            if (down)
            {
                _dragging = true;
                _lastPointer = Input.mousePosition;
                // Si hay toque, usamos su posición para el arrastre.
                if (Input.touchCount > 0)
                {
                    _lastPointer = Input.GetTouch(0).position;
                }
            }

            if (_dragging && held)
            {
                Vector3 p = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved
                    ? (Vector3)Input.GetTouch(0).position
                    : Input.mousePosition;

                Vector3 delta = p - _lastPointer;
                _lastPointer = p;

                if (delta.sqrMagnitude > 0.01f)
                {
                    _dragYaw += delta.x * dragSensitivity;
                    _dragPitch = Mathf.Clamp(_dragPitch - delta.y * dragSensitivity, -85f, 85f);
                }
            }

            if (up)
            {
                _dragging = false;
            }

            // Flechas del teclado para pruebas en escritorio.
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > 0.01f)
            {
                _dragYaw += h * keyboardSpeed * Time.deltaTime;
            }
            if (Mathf.Abs(v) > 0.01f)
            {
                _dragPitch = Mathf.Clamp(_dragPitch - v * keyboardSpeed * Time.deltaTime, -85f, 85f);
            }
        }
    }
}
