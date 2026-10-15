using UnityEngine;

namespace VRBasketball
{
#if UNITY_6000_0_OR_NEWER
    using BounceMaterial = UnityEngine.PhysicsMaterial;
#else
    using BounceMaterial = UnityEngine.PhysicMaterial;
#endif

    /// <summary>
    /// Balón de básquetbol. Su tamaño, masa y rebote se personalizan con la
    /// calibración de la mano del jugador (agarre y fuerza).
    /// </summary>
    public class BallController : MonoBehaviour
    {
        public static BallController Instance { get; private set; }

        public float Radius { get; private set; }
        public bool IsFlying { get; private set; }
        public bool TouchedRim { get; set; }
        public Vector3 PreviousPosition { get; private set; }

        public Vector3 Velocity
        {
            get
            {
                if (_body == null)
                {
                    return Vector3.zero;
                }
#if UNITY_6000_0_OR_NEWER
                return _body.linearVelocity;
#else
                return _body.velocity;
#endif
            }
        }

        private Rigidbody _body;
        private SphereCollider _collider;
        private TrailRenderer _trail;
        private Material _material;
        private Transform _hand;
        private float _heldTimer;
        private float _stopTimer;

        public event System.Action<float> Bounced;

        public void Build(float radius, float mass, float bounciness)
        {
            Instance = this;

            GameObject existing = GameObject.Find("Balon");
            if (existing != null && existing != gameObject)
            {
                Destroy(existing);
            }

            gameObject.name = "Balon";

            _body = gameObject.GetComponent<Rigidbody>();
            if (_body == null)
            {
                _body = gameObject.AddComponent<Rigidbody>();
            }

            _collider = gameObject.GetComponent<SphereCollider>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<SphereCollider>();
            }

            MeshFilter mf = gameObject.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = gameObject.AddComponent<MeshFilter>();
            }
            if (mf.sharedMesh == null)
            {
                // Esfera de radio 1: la escala del objeto la convierte al radio real.
                mf.sharedMesh = MeshUtils.CreateSphere(1f, 24, 16);
            }

            MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = gameObject.AddComponent<MeshRenderer>();
            }

            _material = UiFactory.MakeStandardMaterial("Balon", new Color(0.86f, 0.42f, 0.14f), 0.35f, 0f);
            Texture2D tex = MeshUtils.CreateBasketballTexture();
            _material.mainTexture = tex;
            mr.sharedMaterial = _material;

            BounceMaterial pm = new BounceMaterial("ReboteBalon");
            pm.bounciness = bounciness;
            pm.dynamicFriction = 0.65f;
            pm.staticFriction = 0.7f;
            _collider.material = pm;

            _trail = gameObject.GetComponent<TrailRenderer>();
            if (_trail == null)
            {
                _trail = gameObject.AddComponent<TrailRenderer>();
            }
            _trail.time = 0.45f;
            // Anchos en espacio local: la escala del balón los ajusta.
            _trail.startWidth = 1.9f;
            _trail.endWidth = 0.12f;
            _trail.material = new Material(Shader.Find("Sprites/Default"));
            _trail.material.color = new Color(1f, 0.75f, 0.35f, 0.55f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ApplyProfile(radius, mass);

            IsFlying = false;
            gameObject.SetActive(true);
            PreviousPosition = transform.position;
        }

        /// <summary>Aplica la personalización del balón según la calibración.</summary>
        public void ApplyProfile(float radius, float mass)
        {
            Radius = radius;

            if (_collider != null)
            {
                // Radio local de 1: la escala del objeto lo lleva al radio real.
                // (Si se pone el radio real aqui, se aplicaria dos veces y el balon
                // quedaria con una colision diminuta.)
                _collider.radius = 1f;
            }

            if (_body != null)
            {
                _body.mass = mass;
            }

            transform.localScale = Vector3.one * radius;
        }

        public void HoldAt(Transform hand)
        {
            _hand = hand;
            IsFlying = false;
            TouchedRim = false;
            _stopTimer = 0f;

            if (_body != null)
            {
                _body.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
                _body.linearVelocity = Vector3.zero;
#else
                _body.velocity = Vector3.zero;
#endif
                _body.angularVelocity = Vector3.zero;
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            if (_trail != null)
            {
                _trail.Clear();
                _trail.emitting = false;
            }

            if (hand != null)
            {
                transform.position = hand.position;
                transform.rotation = hand.rotation;
            }

            PreviousPosition = transform.position;
        }

        public void Throw(Vector3 velocity)
        {
            if (_body == null)
            {
                return;
            }

            _body.isKinematic = false;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

#if UNITY_6000_0_OR_NEWER
            _body.linearVelocity = velocity;
#else
            _body.velocity = velocity;
#endif
            // Efecto de retroceso del balón.
            _body.angularVelocity = Random.onUnitSphere * 4f - Vector3.up * 6f;

            if (_collider != null)
            {
                _collider.enabled = true;
            }

            if (_trail != null)
            {
                _trail.emitting = true;
            }

            IsFlying = true;
            TouchedRim = false;
            _stopTimer = 0f;
            PreviousPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (!IsFlying)
            {
                if (_hand != null && _body != null && _body.isKinematic)
                {
                    transform.position = _hand.position;
                    transform.rotation = _hand.rotation;
                }
                PreviousPosition = transform.position;
                return;
            }

            // El balón se consideró perdido si cae de la cancha o se detiene.
            if (transform.position.y < -3f)
            {
                Miss();
                return;
            }

            if (Velocity.sqrMagnitude < 0.35f)
            {
                _stopTimer += Time.fixedDeltaTime;
                if (_stopTimer > 1.2f)
                {
                    Miss();
                }
            }
            else
            {
                _stopTimer = 0f;
            }

            // Se guarda al final: asi la posicion anterior es la del paso de fisica previo.
            PreviousPosition = transform.position;
        }

        /// <summary>El balón terminó su vuelo sin encestar.</summary>
        public void Miss()
        {
            IsFlying = false;
            if (_trail != null)
            {
                _trail.emitting = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsFlying)
            {
                return;
            }

            string name = collision.collider.name;
            float speed = collision.relativeVelocity.magnitude;

            if (name.StartsWith("Borde"))
            {
                TouchedRim = true;
                if (AudioSynth.I != null) AudioSynth.I.Rim();
            }
            else if (name.StartsWith("Tablero") || name.StartsWith("Cuadro"))
            {
                if (AudioSynth.I != null) AudioSynth.I.Rim();
            }
            else if (name.StartsWith("Piso"))
            {
                if (AudioSynth.I != null) AudioSynth.I.Bounce(Mathf.Clamp01(speed / 8f));
                if (Bounced != null) Bounced(speed);
            }
        }
    }
}
