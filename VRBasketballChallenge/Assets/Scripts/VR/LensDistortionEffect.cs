using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Presenta la textura estéreo en pantalla aplicando la corrección de distorsión de la lente.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LensDistortionEffect : MonoBehaviour
    {
        public StereoRig rig;

        private Material _material;

        private void Start()
        {
            Shader shader = Shader.Find("Hidden/VRBasketball/BarrelDistortion");
            if (shader != null && shader.isSupported)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                Debug.LogWarning("VR Basketball: shader de distorsión no disponible, se presenta sin corrección.");
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (rig == null || rig.Target == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (_material == null)
            {
                Graphics.Blit(rig.Target, destination);
                return;
            }

            _material.SetFloat("_K1", rig.distortionK1);
            _material.SetFloat("_K2", rig.distortionK2);
            _material.SetFloat("_StereoSplit", rig.stereo ? 1f : 0f);
            _material.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));

            Graphics.Blit(rig.Target, destination, _material);
        }
    }
}
