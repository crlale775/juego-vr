using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Resultado que manda el puente de MediaPipe (PuenteManos.java) en formato JSON.
    /// Los nombres tienen que coincidir EXACTAMENTE con las claves del JSON.
    /// </summary>
    [System.Serializable]
    public class DatosMediaPipe
    {
        public int ok;
        public int activo;
        public int listo;
        public int seq;
        public int ms;
        public int cuadros;
        public int analizados;
        public int detecciones;
        public int rot;
        public int w;
        public int h;
        public string error;

        public int n;            // manos vistas (0 o mas)
        public float conf;       // confianza de la mano elegida
        public string mano;      // "Left" / "Right"
        public float x;          // centro de la palma, 0..1 (y hacia ABAJO, como MediaPipe)
        public float y;
        public float ancho;      // tamano de la mano en el encuadre, 0..1
        public float alto;
        public float pinza;      // pulgar-indice medido en palmos (1 = abierta)
        public float[] puntos;   // 21 puntos normalizados (x,y,z) -> 63 numeros
        public float[] mundo;    // 21 puntos en metros (x,y,z) -> 63 numeros
    }

    /// <summary>
    /// Mano detectada con MediaPipe Hand Landmarker (la IA de Google), ejecutada en
    /// Android con la camara TRASERA del telefono.
    ///
    /// Unity pregunta por el numero de fotograma analizado y solo lee el JSON completo
    /// cuando cambia: asi no se copian cadenas grandes en cada fotograma.
    ///
    /// Como Java no puede llamar a Unity (no se compila contra unity-classes.jar), aqui
    /// se lee el resultado por sondeo. Todo el puente es opcional: si el movil no lo
    /// carga, <see cref="Disponible"/> queda en false y el juego sigue con el detector
    /// de tono de piel de siempre.
    /// </summary>
    public class MediaPipeHands
    {
        public const string ClasePuente = "com.proyecto.vrbasketball.manos.PuenteManos";

        /// <summary>La clase Java del puente existe en esta compilacion.</summary>
        public bool Disponible { get; private set; }
        /// <summary>MediaPipe arranco y esta analizando fotogramas.</summary>
        public bool Activo { get; private set; }
        /// <summary>El modelo de mano se cargo y la camara esta abierta.</summary>
        public bool Listo { get; private set; }
        public string Error { get; private set; }

        public int Secuencia { get; private set; }
        public int Milisegundos { get; private set; }
        public int Cuadros { get; private set; }
        public int Analizados { get; private set; }
        public int Detecciones { get; private set; }
        public int Grados { get; private set; }
        public int AnchoCamara { get; private set; }
        public int AltoCamara { get; private set; }

        /// <summary>Hay una mano delante de la camara ahora mismo.</summary>
        public bool Detectado { get; private set; }
        /// <summary>Centro de la palma en el encuadre (0..1, y hacia ARRIBA).</summary>
        public Vector2 Posicion { get; private set; }
        /// <summary>Tamano de la mano en el encuadre (ancho x alto del recuadro).</summary>
        public float Cobertura { get; private set; }
        /// <summary>Ancho del recuadro de la mano en el encuadre (0..1).</summary>
        public float Ancho { get; private set; }
        /// <summary>Alto del recuadro de la mano en el encuadre (0..1).</summary>
        public float Alto { get; private set; }
        /// <summary>Confianza de MediaPipe (0..1).</summary>
        public float Confianza { get; private set; }
        /// <summary>Distancia pulgar-indice medida en palmos (1 = mano abierta).</summary>
        public float Pinza { get; private set; }
        /// <summary>"Left" o "Right" segun MediaPipe.</summary>
        public string Lado { get; private set; }

        /// <summary>21 puntos normalizados (x,y hacia ARRIBA,z) de la ultima mano vista.</summary>
        public Vector3[] Puntos = new Vector3[21];
        /// <summary>21 puntos en metros (espacio real de la mano) si el movil los da.</summary>
        public Vector3[] PuntosMundo = new Vector3[21];

        public int Dedos { get; private set; }       // 0..5 dedos estirados
        public float Apertura { get; private set; }  // 0 = puno, 1 = mano abierta
        /// <summary>Cuánto está estirado cada dedo (0 cerrado .. 1 estirado).</summary>
        public float[] Estirados = new float[5];     // indice, medio, anular, menique, pulgar
        public bool Puno { get { return Apertura < 0.35f; } }
        public bool Pellizcando { get { return Pinza < 0.55f; } }

        /// <summary>Milisegundos desde el ultimo resultado nuevo (para saber si esta vivo).</summary>
        public float SinDatos { get { return Time.unscaledTime - _ultiimaLectura; } }

        private AndroidJavaClass _clase;
        private int _ultimaSecuencia = -1;
        private float _ultiimaLectura;
        private bool _iniciado;
        private string _fuenteError = "";

        public MediaPipeHands()
        {
            Posicion = new Vector2(0.5f, 0.5f);
            Error = "";
            Lado = "";
            _ultiimaLectura = Time.unscaledTime;

            Disponible = Preparar();

            if (!Disponible && Error.Length > 0)
            {
                Debug.LogWarning("VR Basketball: MediaPipe no disponible (" + Error + "). Se usa el detector por tono de piel.");
            }
        }

        private bool Preparar()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _clase = new AndroidJavaClass(ClasePuente);
                return _clase != null;
            }
            catch (System.Exception e)
            {
                Error = "clase Java: " + e.Message;
                _clase = null;
                return false;
            }
#else
            // En el navegador y en el editor no hay MediaPipe: se avisa y se sigue.
            return false;
#endif
        }

        /// <summary>Abre la camara y arranca la deteccion. <paramref name="grados"/> -1 = automatico.</summary>
        public bool Abrir(int anchoDeseado, int altoDeseado, int grados, int manos)
        {
            if (!Disponible || _iniciado)
            {
                return _iniciado;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaObject actividad = Actividad();
                if (actividad == null)
                {
                    Error = "sin actividad de Unity";
                    return false;
                }

                string resultado = _clase.CallStatic<string>("iniciar", actividad, anchoDeseado, altoDeseado, grados, manos);
                _iniciado = resultado == "ok" || resultado == "ya-activo";

                if (!_iniciado)
                {
                    Error = resultado;
                    Debug.LogWarning("VR Basketball: MediaPipe no arranco: " + resultado);
                }

                return _iniciado;
            }
            catch (System.Exception e)
            {
                Error = "iniciar: " + e.Message;
                Debug.LogWarning("VR Basketball: fallo al arrancar MediaPipe: " + e.Message);
                _iniciado = false;
                return false;
            }
#else
            return false;
#endif
        }

        private static AndroidJavaObject Actividad()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaClass jugador = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                return jugador.GetStatic<AndroidJavaObject>("currentActivity");
            }
            catch (System.Exception)
            {
                return null;
            }
#else
            return null;
#endif
        }

        public void Cerrar()
        {
            if (!Disponible || !_iniciado)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _clase.CallStatic("parar");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("VR Basketball: fallo al parar MediaPipe: " + e.Message);
            }
#endif

            _iniciado = false;
            Activo = false;
            Listo = false;
            Detectado = false;
            Cobertura = 0f;
            Dedos = 0;
            Apertura = 0f;
        }

        /// <summary>Se llama cada fotograma: lee el resultado nuevo si lo hay.</summary>
        public void Actualizar()
        {
            if (!Disponible || !_iniciado)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                int secuencia = _clase.CallStatic<int>("secuencia");

                if (secuencia == _ultimaSecuencia)
                {
                    // La camara sigue dando fotogramas, pero ninguno nuevo analizado.
                    return;
                }

                _ultimaSecuencia = secuencia;

                string json = _clase.CallStatic<string>("datos");
                DatosMediaPipe datos = JsonUtility.FromJson<DatosMediaPipe>(json);

                if (datos == null)
                {
                    return;
                }

                Aplicar(datos);
            }
            catch (System.Exception e)
            {
                // Un fallo suelto de lectura no debe romper el juego.
                if (_fuenteError != e.Message)
                {
                    _fuenteError = e.Message;
                    Debug.LogWarning("VR Basketball: error leyendo MediaPipe: " + e.Message);
                }
            }
#endif
        }

        private void Aplicar(DatosMediaPipe datos)
        {
            Activo = datos.activo == 1;
            Listo = datos.listo == 1;
            Secuencia = datos.seq;
            Milisegundos = datos.ms;
            Cuadros = datos.cuadros;
            Analizados = datos.analizados;
            Detecciones = datos.detecciones;
            Grados = datos.rot;
            AnchoCamara = datos.w;
            AltoCamara = datos.h;

            if (!string.IsNullOrEmpty(datos.error))
            {
                Error = datos.error;
            }

            _ultiimaLectura = Time.unscaledTime;

            if (datos.n <= 0 || (datos.puntos != null && datos.puntos.Length < 63))
            {
                Detectado = false;
                Cobertura = 0f;
                Dedos = 0;
                Apertura = 0f;
                Estirados[0] = Estirados[1] = Estirados[2] = Estirados[3] = Estirados[4] = 0f;
                return;
            }

            // MediaPipe cuenta la "y" desde ARRIBA. El resto del juego (y el detector
            // de tono de piel) la cuenta desde ABAJO: aqui se le da la vuelta una vez.
            Posicion = new Vector2(datos.x, 1f - datos.y);
            Ancho = Mathf.Clamp01(datos.ancho);
            Alto = Mathf.Clamp01(datos.alto);
            Cobertura = Mathf.Clamp01(datos.ancho * datos.alto);
            Confianza = Mathf.Clamp01(datos.conf);
            Lado = datos.mano ?? "";
            Detectado = true;

            for (int i = 0; i < 21; i++)
            {
                int j = i * 3;
                float x = datos.puntos[j];
                float y = 1f - datos.puntos[j + 1];
                float z = datos.puntos[j + 2];
                Puntos[i] = new Vector3(x, y, z);

                if (datos.mundo != null && datos.mundo.Length >= 63)
                {
                    PuntosMundo[i] = new Vector3(datos.mundo[j], datos.mundo[j + 1], datos.mundo[j + 2]);
                }
            }            // Dedos y pellizco: cuentas compartidas con la mano simulada de las pruebas.
            AnalisisMano.MedirDedos(Puntos, Estirados, out float apertura, out int dedos);
            Apertura = apertura;
            Dedos = dedos;
            Pinza = AnalisisMano.MedirPinza(Puntos);
        }
    }
}
