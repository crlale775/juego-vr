using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace VRBasketball
{
    /// <summary>
    /// Puente con el evento deviceorientation del navegador (solo en compilaciones WebGL).
    /// El navegador exige HTTPS para poder leer los sensores de orientación.
    /// </summary>
    public static class WebGLDeviceOrientation
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int VRBB_OrientationSupported();
        [DllImport("__Internal")] private static extern int VRBB_HasData();
        [DllImport("__Internal")] private static extern void VRBB_RequestOrientationPermission();
        [DllImport("__Internal")] private static extern float VRBB_GetAlpha();
        [DllImport("__Internal")] private static extern float VRBB_GetBeta();
        [DllImport("__Internal")] private static extern float VRBB_GetGamma();
        [DllImport("__Internal")] private static extern float VRBB_GetScreenAngle();

        private static bool _requested;

        public static bool IsSupported()
        {
            try
            {
                return VRBB_OrientationSupported() == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Verdadero cuando el navegador ya entregó alguna lectura del sensor.</summary>
        public static bool HasData()
        {
            try
            {
                return VRBB_HasData() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static void RequestPermissionIfNeeded()
        {
            if (_requested)
            {
                return;
            }
            _requested = true;
            try
            {
                VRBB_RequestOrientationPermission();
            }
            catch
            {
                // sin permisos: se juega con arrastre
            }
        }

        /// <summary>Rotación de la cabeza según el sensor del teléfono.</summary>
        public static Quaternion GetRotation()
        {
            float alpha = VRBB_GetAlpha();
            float beta = VRBB_GetBeta();
            float gamma = VRBB_GetGamma();
            float screenAngle = VRBB_GetScreenAngle();

            // Misma construcción que la especificación W3C, adaptada a Unity:
            // R = Ry(alpha) * Rx(beta) * Rz(-gamma), luego el arreglo de cámara y de pantalla.
            Quaternion q = Quaternion.Euler(beta, alpha, -gamma);
            q = q * Quaternion.Euler(90f, 0f, 0f);
            q = q * Quaternion.Euler(0f, 0f, -screenAngle);
            return q;
        }
#else
        public static bool IsSupported()
        {
            return false;
        }

        public static bool HasData()
        {
            return false;
        }

        public static void RequestPermissionIfNeeded()
        {
        }

        public static Quaternion GetRotation()
        {
            return Quaternion.identity;
        }
#endif
    }
}
