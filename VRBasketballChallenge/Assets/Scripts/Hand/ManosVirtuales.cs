using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Manos virtuales: dibuja una mano de verdad con los 21 puntos que ve MediaPipe
    /// Hand Landmarker.
    ///
    /// La mano sigue a la mano real: se coloca en el mundo según dónde esté la palma
    /// en el encuadre, se gira un poco con la inclinación de la palma y cierra los
    /// dedos según cómo los tenga el jugador. El ANCLA de la mano (donde se apoya el
    /// balón) también se mueve con ella, así el balón va literalmente en la mano.
    ///
    /// El esqueleto de los 21 puntos sobre la vista lo dibuja el HUD (SetHandPoints).
    /// </summary>
    public class ManosVirtuales : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Rastreador de la mano (MediaPipe).")]
        public HandTracker tracker;
        [Tooltip("Cabeza del jugador: la mano se coloca respecto a ella.")]
        public Transform cabeza;
        [Tooltip("Ancla de la mano: es donde el juego apoya el balón.")]
        public Transform mano;

        [Header("Posición")]
        [Tooltip("Cuánto se mueve la mano de lado a lado (metros) con la palma en el borde del encuadre.")]
        public float rangoX = 0.40f;
        [Tooltip("Cuánto se mueve de arriba a abajo (metros).")]
        public float rangoY = 0.34f;
        [Tooltip("Distancia de referencia a la cabeza (metros).")]
        public float distancia = 0.46f;
        [Tooltip("Cuánto se sigue a la mano: más alto, más pegada (menos temblor).")]
        public float suavizado = 14f;

        [Header("Mano dibujada")]
        public bool dibujarMano3D = true;

        /// <summary>Este fotograma hay mano dibujada.</summary>
        public bool Visible { get; private set; }
        /// <summary>Dónde está la mano virtual en el mundo (diagnóstico).</summary>
        public Vector3 PosicionMundo { get; private set; }
        /// <summary>Giro de la muñeca aplicado (grados, diagnóstico).</summary>
        public float GiroMuneca { get; private set; }

        private Transform _raiz;
        private Transform[] _pivotes = new Transform[20];   // 4 dedos x 3 falanges + pulgar x 3 ... se usan 15
        private Transform[] _huesos;
        private Transform[] _dedosPivote = new Transform[5];
        private Transform[] _falanges = new Transform[15];
        private Vector3 _baseMano;
        private Vector3 _baseEscalaRoot;
        private Material _piel;
        private Mesh _esfera;
        private float _giro;

        // El orden de los dedos en el array de falanges: indice, medio, anular, menique, pulgar.
        private static readonly float[] _separacionX = { -0.024f, -0.008f, 0.008f, 0.024f };
        private static readonly float[] _alturaBaseY = { 0.058f, 0.062f, 0.055f, 0.046f };
        private static readonly float[][] _largos = new float[][]
        {
            new float[] { 0.030f, 0.024f, 0.018f },
            new float[] { 0.034f, 0.027f, 0.019f },
            new float[] { 0.031f, 0.025f, 0.017f },
            new float[] { 0.026f, 0.019f, 0.015f }
        };

        private void Start()
        {
            _baseMano = mano != null ? mano.localPosition : new Vector3(0.3f, -0.42f, 0.42f);
            Construir();
        }

        private void Construir()
        {
            if (!dibujarMano3D || mano == null)
            {
                return;
            }

            _esfera = MeshUtils.CreateSphere(1f, 12, 8);
            _piel = UiFactory.MakeTransparentMaterial("ManoVirtual", new Color(1f, 0.78f, 0.64f, 0.62f), null);

            _raiz = new GameObject("ManoVirtual3D").transform;
            _raiz.SetParent(mano, false);
            _raiz.localPosition = Vector3.zero;
            _raiz.localRotation = Quaternion.Euler(0f, 180f, 0f);   // la palma mira al jugador

            // Palma y muñeca.
            CrearHueso(_raiz, "Palma", new Vector3(0f, 0.012f, 0f), new Vector3(0.086f, 0.062f, 0.030f), 0f);
            CrearHueso(_raiz, "Muneca", new Vector3(0f, -0.052f, 0f), new Vector3(0.062f, 0.036f, 0.028f), 0f);

            _huesos = new Transform[15];

            // Cuatro dedos largos.
            for (int dedo = 0; dedo < 4; dedo++)
            {
                Transform padre = _raiz;
                Vector3 posicion = new Vector3(_separacionX[dedo], _alturaBaseY[dedo] + 0.012f, 0f);

                for (int falange = 0; falange < 3; falange++)
                {
                    float largo = _largos[dedo][falange];
                    float grosor = 0.0082f - falange * 0.0008f;

                    Transform pivote = new GameObject("Pivote" + dedo + "_" + falange).transform;
                    pivote.SetParent(padre, false);
                    pivote.localPosition = posicion;

                    Transform hueso = CrearHueso(pivote, "Falange" + dedo + "_" + falange,
                        new Vector3(0f, largo * 0.5f, 0f), new Vector3(grosor, largo * 0.5f, grosor), 0f);

                    _huesos[dedo * 3 + falange] = pivote;
                    _falanges[dedo * 3 + falange] = hueso;

                    padre = pivote;
                    posicion = new Vector3(0f, largo, 0f);
                }

                _dedosPivote[dedo] = _huesos[dedo * 3];
            }

            // Pulgar: sale de lado y un poco hacia abajo.
            Transform pulgarPadre = _raiz;
            Vector3 pulgarPosicion = new Vector3(-0.038f, 0.006f, 0.006f);
            float[] pulgarLargos = { 0.026f, 0.023f, 0.019f };

            for (int falange = 0; falange < 3; falange++)
            {
                Transform pivote = new GameObject("PivotePulgar_" + falange).transform;
                pivote.SetParent(pulgarPadre, false);
                pivote.localPosition = pulgarPosicion;
                pivote.localRotation = Quaternion.Euler(0f, 0f, falange == 0 ? 58f : 12f);

                float largo = pulgarLargos[falange];
                float grosor = 0.0095f - falange * 0.0008f;

                Transform hueso = CrearHueso(pivote, "FalangePulgar" + falange,
                    new Vector3(0f, largo * 0.5f, 0f), new Vector3(grosor, largo * 0.5f, grosor), 0f);

                _huesos[12 + falange] = pivote;
                _falanges[12 + falange] = hueso;

                pulgarPadre = pivote;
                pulgarPosicion = new Vector3(0f, largo, 0f);
            }

            _dedosPivote[4] = _huesos[12];

            // Los pivotes del pulgar ya llevan su propio giro: se guarda para volver a él.
            _baseEscalaRoot = Vector3.one;
            _raiz.localScale = Vector3.one;
        }

        private Transform CrearHueso(Transform padre, string nombre, Vector3 posicion, Vector3 escala, float giro)
        {
            GameObject go = new GameObject(nombre, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicion;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, giro);
            go.transform.localScale = escala;

            MeshFilter filtro = go.GetComponent<MeshFilter>();
            filtro.sharedMesh = _esfera;

            MeshRenderer pintor = go.GetComponent<MeshRenderer>();
            pintor.sharedMaterial = _piel;
            pintor.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pintor.receiveShadows = false;

            return go.transform;
        }

        private void Update()
        {
            if (_raiz == null || tracker == null || mano == null)
            {
                return;
            }

            bool ver = tracker.Detected && tracker.Coverage >= 0.008f;

            if (_raiz.gameObject.activeSelf != ver)
            {
                _raiz.gameObject.SetActive(ver);
            }

            Visible = ver;

            if (!ver)
            {
                return;
            }

            Vector2 palma = tracker.Position;
            float tamano = Mathf.Max(0.03f, tracker.Tamano);

            // 1) Dónde se pone la mano: la palma movida de lado/arriba y, cuanto más
            //    grande se ve (más cerca), más cerca de la cara.
            float cerca = Mathf.Clamp(0.17f / tamano, 0.72f, 1.4f);
            Vector3 destino = new Vector3(
                _baseMano.x + (palma.x - 0.5f) * rangoX,
                _baseMano.y + (palma.y - 0.5f) * rangoY,
                distancia * cerca);

            // El ancla se mueve con la mano: así el balón viaja con ella.
            mano.localPosition = Vector3.Lerp(mano.localPosition, destino,
                Mathf.Clamp01(suavizado * Time.deltaTime));

            PosicionMundo = mano.position;

            // 2) Giro de la muñeca a partir de la inclinación real de la palma.
            if (tracker.TienePuntos)
            {
                Vector3 nudilloIndice = tracker.Landmarks[5];
                Vector3 nudilloMenique = tracker.Landmarks[17];
                float roll = Mathf.Atan2(nudilloMenique.y - nudilloIndice.y,
                    nudilloMenique.x - nudilloIndice.x) * Mathf.Rad2Deg;

                // La palma horizontal (roll 0) es la postura normal: solo se corrige la
                // diferencia, limitada para que la mano no acabe de lado.
                float objetivo = Mathf.Clamp(-roll, -55f, 55f);
                _giro = Mathf.Lerp(_giro, objetivo, Mathf.Clamp01(6f * Time.deltaTime));
            }
            else
            {
                _giro = Mathf.Lerp(_giro, 0f, Mathf.Clamp01(4f * Time.deltaTime));
            }

            GiroMuneca = _giro;
            _raiz.localRotation = Quaternion.Euler(0f, 180f, _giro);

            // 3) Escala según el tamaño de la mano en el encuadre.
            float escala = Mathf.Clamp(tamano / 0.16f, 0.75f, 1.5f);
            _raiz.localScale = Vector3.Lerp(_raiz.localScale, new Vector3(escala, escala, escala),
                Mathf.Clamp01(8f * Time.deltaTime));

            // 4) Dedos: se cierran según cómo los tenga el jugador.
            CerrarDedos();
        }

        private void CerrarDedos()
        {
            for (int dedo = 0; dedo < 5; dedo++)
            {
                float estirado;

                if (tracker.TienePuntos && tracker.DedosEstirados != null && tracker.DedosEstirados.Length >= 5)
                {
                    estirado = tracker.DedosEstirados[dedo];
                }
                else
                {
                    // Sin puntos articulados (detector por color): todos los dedos a la vez.
                    estirado = Mathf.Clamp01(tracker.Apertura);
                }

                // 0 = dedo doblado del todo, 1 = estirado: se convierte en giros por falange.
                float curl = (1f - Mathf.Clamp01(estirado));
                float[] giros = { curl * 72f, curl * 88f, curl * 60f };

                for (int falange = 0; falange < 3; falange++)
                {
                    Transform pivote = _huesos[dedo * 3 + falange];
                    if (pivote == null)
                    {
                        continue;
                    }

                    Quaternion baseGiro = Quaternion.Euler(0f, 0f,
                        dedo == 4 && falange == 0 ? 58f : (dedo == 4 ? 12f : 0f));

                    Quaternion dobla = Quaternion.Euler(-giros[falange], 0f, 0f);
                    pivote.localRotation = baseGiro * dobla;
                }
            }
        }
    }
}
