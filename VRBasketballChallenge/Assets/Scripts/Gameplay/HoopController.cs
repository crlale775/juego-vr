using System;
using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Aro dinámico: se mueve en horizontal, en vertical, se aleja, se hace más
    /// pequeño y en el último nivel da saltos impredecibles.
    /// </summary>
    public class HoopController : MonoBehaviour
    {
        [Header("Medidas")]
        public float height = 3.05f;
        public float ringRadius = 0.24f;
        public float tubeRadius = 0.021f;

        [Header("Movimiento")]
        public float baseDistance = 5.2f;

        public event Action<BallController, bool> Scored;

        private Transform _assembly;
        private Transform _ring;
        private Transform _backboard;
        private Transform _net;
        private Material _ringMaterial;
        private Material _boardMaterial;

        private LevelConfig _config;
        private Vector3 _anchor;
        private float _phase;
        private float _teleportTimer;
        private float _nextTeleport = 4f;
        private Vector3 _lastBallPosition;
        private Vector3 _lastAssemblyPosition;

        /// <summary>
        /// Velocidad del aro en metros por segundo. La usa el sistema de tiro para
        /// apuntar un poco por delante y que el balón llegue donde estará el aro.
        /// </summary>
        public Vector3 RingVelocity { get; private set; }

        /// <summary>Escribe en el registro cada cruce del plano del aro (diagnóstico).</summary>
        [HideInInspector] public bool logTiros;

        public Transform Ring { get { return _ring; } }
        public Vector3 RingCenter { get { return _ring != null ? _ring.position : transform.position; } }
        public LevelConfig Config { get { return _config; } }

        public void Build()
        {
            _ringMaterial = UiFactory.MakeStandardMaterial("Aro", new Color(0.98f, 0.48f, 0.18f), 0.5f, 0.18f);
            _boardMaterial = UiFactory.MakeStandardMaterial("Tablero", new Color(0.96f, 0.97f, 1f), 0.12f, 0f);
            _boardMaterial.mainTexture = MeshUtils.CreateBackboardTexture();

            GameObject existing = GameObject.Find("AroAssembly");
            if (existing != null)
            {
                Destroy(existing);
            }

            _assembly = new GameObject("AroAssembly").transform;
            _assembly.SetParent(transform, false);
            _assembly.position = new Vector3(0f, height, baseDistance);

            Rigidbody rb = _assembly.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            BuildRing();
            BuildRimColliders();
            BuildBackboard();
            BuildNet();
        }

        private void BuildRing()
        {
            GameObject go = new GameObject("Aro");
            go.transform.SetParent(_assembly, false);
            go.transform.localPosition = Vector3.zero;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = MeshUtils.CreateTorus(ringRadius, tubeRadius, 40, 10);

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _ringMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _ring = go.transform;
        }

        private void BuildRimColliders()
        {
            GameObject holder = new GameObject("CollidersAro");
            holder.transform.SetParent(_assembly, false);

            const int count = 32;
            float segment = 2f * Mathf.PI * ringRadius / count;

            for (int i = 0; i < count; i++)
            {
                float a = 2f * Mathf.PI * i / count;
                Vector3 pos = new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius);
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));

                GameObject seg = new GameObject("Borde" + i);
                seg.transform.SetParent(holder.transform, false);
                seg.transform.localPosition = pos;
                seg.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);

                CapsuleCollider cap = seg.AddComponent<CapsuleCollider>();
                cap.direction = 2; // eje Z local
                cap.radius = tubeRadius;
                cap.height = segment + tubeRadius * 2f + 0.01f;
            }
        }

        private void BuildBackboard()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tablero";
            go.transform.SetParent(_assembly, false);
            go.transform.localPosition = new Vector3(0f, 0.16f, ringRadius + 0.10f);
            go.transform.localScale = new Vector3(1.80f, 1.05f, 0.05f);
            go.GetComponent<Renderer>().sharedMaterial = _boardMaterial;
            _backboard = go.transform;

            GameObject square = GameObject.CreatePrimitive(PrimitiveType.Cube);
            square.name = "CuadroTablero";
            square.transform.SetParent(go.transform, false);
            square.transform.localPosition = new Vector3(0f, -0.08f, -0.55f);
            square.transform.localScale = new Vector3(0.60f, 0.46f, 0.09f);
            square.GetComponent<Renderer>().sharedMaterial = UiFactory.MakeStandardMaterial(
                "LineaTablero", new Color(0.95f, 0.35f, 0.10f), 0.1f, 0f);
            Destroy(square.GetComponent<Collider>());
        }

        private void BuildNet()
        {
            GameObject net = new GameObject("Red");
            net.transform.SetParent(_assembly, false);
            _net = net.transform;

            const int strands = 12;
            const int points = 5;
            float drop = 0.40f;

            Material netMat = new Material(Shader.Find("Sprites/Default"));
            netMat.color = new Color(1f, 1f, 1f, 0.55f);

            for (int s = 0; s < strands; s++)
            {
                float a = 2f * Mathf.PI * s / strands;
                GameObject strand = new GameObject("Hilo" + s);
                strand.transform.SetParent(net.transform, false);

                LineRenderer lr = strand.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = points;
                lr.startWidth = 0.010f;
                lr.endWidth = 0.006f;
                lr.material = netMat;
                lr.numCapVertices = 0;

                for (int p = 0; p < points; p++)
                {
                    float t = (float)p / (points - 1);
                    float r = Mathf.Lerp(ringRadius * 0.98f, ringRadius * 0.42f, t);
                    float y = -drop * t;
                    float ang = a + t * 0.55f;
                    lr.SetPosition(p, new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r));
                }
            }
        }

        /// <summary>Aplica los parámetros del nivel actual.</summary>
        public void ApplyLevel(LevelConfig config)
        {
            _config = config;
            baseDistance = config.hoopDistance;
            ringRadius = config.ringRadius;
            height = 3.05f - (config.level - 1) * 0.02f;
            _anchor = new Vector3(0f, height, baseDistance);
            _phase = 0f;
            _teleportTimer = 0f;
            _nextTeleport = 3.5f;
            RingVelocity = Vector3.zero;

            if (_assembly != null)
            {
                _assembly.position = _anchor;
                _lastAssemblyPosition = _anchor;
            }

            if (_ring != null)
            {
                MeshFilter mf = _ring.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    mf.sharedMesh = MeshUtils.CreateTorus(ringRadius, tubeRadius, 32, 8);
                }
            }

            RebuildRim();
        }

        private void RebuildRim()
        {
            Transform holder = _assembly.Find("CollidersAro");
            if (holder != null)
            {
                Destroy(holder.gameObject);
            }
            BuildRimColliders();
        }

        private void Update()
        {
            if (_assembly == null || _config.level == 0)
            {
                return;
            }

            float speed = Mathf.Max(0.05f, _config.hoopSpeed);
            _phase += Time.deltaTime * speed;

            float x = Mathf.Sin(_phase) * _config.hoopAmplitude;
            float y = Mathf.Sin(_phase * 0.73f + 1.1f) * _config.verticalAmplitude;

            if (_config.randomTeleport)
            {
                _teleportTimer += Time.deltaTime;
                if (_teleportTimer > _nextTeleport)
                {
                    _teleportTimer = 0f;
                    _nextTeleport = UnityEngine.Random.Range(2.2f, 4.5f);
                    _phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                }
            }

            _assembly.position = Vector3.Lerp(_assembly.position, _anchor + new Vector3(x, y, 0f), Time.deltaTime * 12f);
            _assembly.rotation = Quaternion.identity;

            // Velocidad del aro, suavizada y con tope, para el adelanto de la puntería.
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 instantanea = (_assembly.position - _lastAssemblyPosition) / dt;
            _lastAssemblyPosition = _assembly.position;

            if (instantanea.magnitude > 8f)
            {
                instantanea = instantanea.normalized * 8f;
            }

            RingVelocity = Vector3.Lerp(RingVelocity, instantanea, 0.35f);
        }

        private void FixedUpdate()
        {
            BallController ball = BallController.Instance;
            if (ball == null || _ring == null)
            {
                return;
            }

            Vector3 now = ball.transform.position;

            // Mientras el balón está en la mano se sigue su posición para no comparar
            // contra un punto viejo. Y se guarda aquí mismo, sin depender del orden en
            // que Unity ejecute los FixedUpdate de otros componentes.
            if (!ball.IsFlying)
            {
                _lastBallPosition = now;
                return;
            }

            Vector3 prev = _lastBallPosition;
            _lastBallPosition = now;

            Vector3 center = _ring.position;                bool logTiros = this.logTiros;

                if (prev.y > center.y && now.y <= center.y)
                {
                // Punto exacto en el que el balón cruza el plano del aro. Es importante
                // medirlo AHI y no donde esta el balon al final del fotograma: a gran
                // velocidad el balon ya ha avanzado medio metro y un tiro bueno parecia
                // fallo (era la causa de que se encestara mucho menos de lo que toca).
                float salto = prev.y - now.y;
                float t = salto > 0.0001f ? (prev.y - center.y) / salto : 0f;
                Vector3 cruce = Vector3.Lerp(prev, now, Mathf.Clamp01(t));

                float dx = cruce.x - center.x;
                float dz = cruce.z - center.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                // Tolerancia generosa: perdona que el balón pase bastante pegado al
                // borde del aro (es un juego, no una simulación exacta).
                float allowed = ringRadius + ball.Radius * 0.55f;

                // Al cruzar el plano de arriba hacia abajo el balón ya baja: no hace
                // falta exigir velocidad vertical negativa (si el balón llegaba justo
                // en lo alto del arco, se rechazaba un tiro que entraba limpio).
                if (logTiros)
                {
                    Debug.Log(string.Format("[CESTA] distanciaAlCentro={0:0.000} permitido={1:0.000}", dist, allowed));
                }

                if (dist <= allowed)
                {
                    if (Scored != null)
                    {
                        Scored(ball, ball.TouchedRim);
                    }
                }
            }
        }

        /// <summary>Punto de mira del aro, usado por la ayuda de puntería.</summary>
        public Vector3 AimTarget
        {
            get
            {
                Vector3 c = RingCenter;
                return c + Vector3.up * 0.06f;
            }
        }
    }
}
