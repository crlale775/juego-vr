using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Rig de realidad virtual tipo Cardboard: dos cámaras (un ojo cada una) renderizadas
    /// a una textura intermedia y presentadas en pantalla con corrección de distorsión de lente.
    /// Incluye modo monocular para probar en escritorio sin visor.
    /// </summary>
    public class StereoRig : MonoBehaviour
    {
        [Header("Óptica")]
        [Tooltip("Distancia entre pupilas en metros.")]
        public float ipd = 0.0635f;
        [Tooltip("Campo de visión de cada ojo en grados.")]
        public float eyeFov = 70f;

        [Header("Rendimiento")]
        [Range(0.4f, 1f)]
        [Tooltip("Escala de render. Menos = más fluido en el teléfono.")]
        public float renderScale = 0.8f;

        [Header("Modo")]
        public bool stereo = true;
        [Tooltip("Dibuja los ojos en una textura intermedia y luego la presenta en pantalla.\n"
            + "APAGADO (recomendado): cada ojo dibuja DIRECTO a la pantalla, que es el camino\n"
            + "verificado en todos los móviles. Encendido sólo hace falta para la corrección\n"
            + "de distorsión de lente, y en algunos Android la textura intermedia salía boca\n"
            + "abajo (era el fallo de la imagen invertida).")]
        public bool presentarConTextura = false;
        public bool applyLensDistortion = true;
        public float distortionK1 = 0.10f;
        public float distortionK2 = 0.02f;

        public Camera LeftEye { get; private set; }
        public Camera RightEye { get; private set; }
        public Camera Present { get; private set; }
        public RenderTexture Target { get { return _rt; } }

        private RenderTexture _rt;
        private LensDistortionEffect _effect;
        private int _rtWidth;
        private int _rtHeight;
        private bool _rendering = true;

        private void Awake()
        {
            BuildCameras();
        }

        private void OnDestroy()
        {
            ReleaseTarget();
        }

        private void BuildCameras()
        {
            LeftEye = CreateEye("OjoIzquierdo");
            RightEye = CreateEye("OjoDerecho");

            if (!presentarConTextura)
            {
                // Camino directo: cada ojo se dibuja en su mitad de la pantalla.
                // No hay textura intermedia, así que ninguna versión de Android (Vulkan
                // o GLES) puede dar la vuelta a la imagen al presentarla.
                return;
            }

            GameObject presentGo = new GameObject("CamaraPresentacion");
            presentGo.transform.SetParent(null);
            Present = presentGo.AddComponent<Camera>();
            Present.depth = 100f;
            Present.clearFlags = CameraClearFlags.SolidColor;
            Present.backgroundColor = Color.black;
            Present.cullingMask = 0;
            Present.useOcclusionCulling = false;
            Present.allowHDR = false;
            Present.allowMSAA = false;
            Present.nearClipPlane = 0.03f;
            Present.farClipPlane = 10f;
            Present.fieldOfView = 60f;

            _effect = presentGo.AddComponent<LensDistortionEffect>();
            _effect.rig = this;
        }

        private Camera CreateEye(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            cam.fieldOfView = eyeFov;
            cam.depth = 0f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.useOcclusionCulling = false;
            return cam;
        }

        private void LateUpdate()
        {
            if (presentarConTextura)
            {
                EnsureTarget();
            }
            else if (_rt != null)
            {
                ReleaseTarget();
            }

            ApplyLayout();

            if (!_rendering)
            {
                if (LeftEye != null) LeftEye.enabled = false;
                if (RightEye != null) RightEye.enabled = false;
                if (Present != null) Present.enabled = false;
            }
        }

        private void EnsureTarget()
        {
            int w = Mathf.Max(320, Mathf.RoundToInt(Screen.width * renderScale));
            int h = Mathf.Max(240, Mathf.RoundToInt(Screen.height * renderScale));

            if (_rt != null && _rtWidth == w && _rtHeight == h)
            {
                return;
            }

            ReleaseTarget();
            _rtWidth = w;
            _rtHeight = h;
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.Default);
            _rt.name = "VRBB_Esterero";
            _rt.filterMode = FilterMode.Bilinear;
            _rt.antiAliasing = 1;
            _rt.Create();

            if (LeftEye != null) LeftEye.targetTexture = _rt;
            if (RightEye != null) RightEye.targetTexture = _rt;
        }

        private void ReleaseTarget()
        {
            if (_rt == null)
            {
                return;
            }

            if (LeftEye != null) LeftEye.targetTexture = null;
            if (RightEye != null) RightEye.targetTexture = null;

            _rt.Release();
            Destroy(_rt);
            _rt = null;
            _rtWidth = 0;
            _rtHeight = 0;
        }

        private void ApplyLayout()
        {
            if (LeftEye == null || RightEye == null)
            {
                return;
            }

            LeftEye.fieldOfView = eyeFov;
            RightEye.fieldOfView = eyeFov;

            float half = ipd * 0.5f;

            if (stereo)
            {
                LeftEye.enabled = true;
                RightEye.enabled = true;

                LeftEye.transform.localPosition = new Vector3(-half, 0f, 0f);
                RightEye.transform.localPosition = new Vector3(half, 0f, 0f);

                LeftEye.rect = new Rect(0f, 0f, 0.5f, 1f);
                RightEye.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            }
            else
            {
                LeftEye.enabled = true;
                RightEye.enabled = false;

                LeftEye.transform.localPosition = Vector3.zero;
                LeftEye.rect = new Rect(0f, 0f, 1f, 1f);
            }

            if (_effect != null)
            {
                _effect.enabled = applyLensDistortion;
            }
        }

        /// <summary>Está activa la presentación por textura (con corrección de lente).</summary>
        public bool UsaTexturaIntermedia { get { return presentarConTextura; } }

        /// <summary>Activa o desactiva el render de la vista VR (por ejemplo durante la calibración).</summary>
        public void SetRendering(bool value)
        {
            if (Present != null)
            {
                Present.enabled = value;
            }

            if (LeftEye != null)
            {
                LeftEye.enabled = value;
            }

            if (RightEye != null)
            {
                RightEye.enabled = value && stereo;
            }

            _rendering = value;
        }

        public void SetStereo(bool value)
        {
            stereo = value;
        }

        public void SetRenderScale(float value)
        {
            renderScale = Mathf.Clamp(value, 0.4f, 1f);
        }
    }
}
