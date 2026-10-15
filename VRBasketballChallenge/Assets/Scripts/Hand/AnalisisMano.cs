using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Cuentas que se sacan de los 21 puntos de la mano (los que da MediaPipe).
    /// Vive aparte para que las usen tanto la detección real como la mano simulada
    /// de las pruebas.
    /// </summary>
    public static class AnalisisMano
    {
        public const int Puntos = 21;

        /// <summary>Dedos estirados (índice, medio, anular, meñique, pulgar) y apertura 0..1.</summary>
        public static void MedirDedos(Vector3[] puntos, float[] estirados, out float apertura, out int dedos)
        {
            apertura = 0f;
            dedos = 0;

            if (puntos == null || estirados == null || puntos.Length < Puntos || estirados.Length < 5)
            {
                return;
            }

            float palma = Vector3.Distance(puntos[0], puntos[9]);
            if (palma < 0.0001f)
            {
                return;
            }

            int[] bases = { 5, 9, 13, 17 };
            int[] falanges = { 6, 10, 14, 18 };
            int[] puntas = { 8, 12, 16, 20 };

            float suma = 0f;
            int contados = 0;

            for (int i = 0; i < 4; i++)
            {
                float aFalange = Vector3.Distance(puntos[bases[i]], puntos[falanges[i]]);
                float aPunta = Vector3.Distance(puntos[bases[i]], puntos[puntas[i]]);

                // 0 = dedo doblado del todo, 1 = estirado del todo: al cerrar el puño la
                // punta se queda pegada al nudillo y esta medida se hunde.
                float estirado = Mathf.Clamp01((aPunta / Mathf.Max(0.0001f, aFalange) - 0.95f) / 0.75f);
                estirados[i] = estirado;
                suma += estirado;

                if (estirado > 0.5f)
                {
                    contados++;
                }
            }

            // El pulgar se mide aparte: separación de su punta respecto al meñique.
            float pulgar = Mathf.Clamp01((Vector3.Distance(puntos[4], puntos[17]) / palma - 0.55f) / 0.55f);
            estirados[4] = pulgar;
            suma += pulgar;

            if (pulgar > 0.5f)
            {
                contados++;
            }

            apertura = Mathf.Clamp01(suma / 5f);
            dedos = contados;
        }

        /// <summary>Pellizco: distancia pulgar-índice medida en palmos (1 = mano abierta).</summary>
        public static float MedirPinza(Vector3[] puntos)
        {
            if (puntos == null || puntos.Length < Puntos)
            {
                return 1f;
            }

            float palma = Vector3.Distance(puntos[0], puntos[9]);
            if (palma < 0.0001f)
            {
                return 1f;
            }

            return Vector3.Distance(puntos[4], puntos[8]) / palma;
        }

        /// <summary>Tamaño de la mano en el encuadre: ancho x alto de su recuadro.</summary>
        public static float MedirCobertura(Vector3[] puntos, out float ancho, out float alto)
        {
            ancho = 0f;
            alto = 0f;

            if (puntos == null || puntos.Length < Puntos)
            {
                return 0f;
            }

            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;

            for (int i = 0; i < Puntos; i++)
            {
                if (puntos[i].x < minX) minX = puntos[i].x;
                if (puntos[i].y < minY) minY = puntos[i].y;
                if (puntos[i].x > maxX) maxX = puntos[i].x;
                if (puntos[i].y > maxY) maxY = puntos[i].y;
            }

            ancho = Mathf.Max(0f, maxX - minX);
            alto = Mathf.Max(0f, maxY - minY);
            return ancho * alto;
        }

        /// <summary>Centro de la palma: muñeca más los cuatro nudillos.</summary>
        public static Vector2 CentroPalma(Vector3[] puntos)
        {
            if (puntos == null || puntos.Length < Puntos)
            {
                return new Vector2(0.5f, 0.5f);
            }

            Vector2 suma = Vector2.zero;
            int[] indices = { 0, 5, 9, 13, 17 };

            for (int i = 0; i < indices.Length; i++)
            {
                suma += new Vector2(puntos[indices[i]].x, puntos[indices[i]].y);
            }

            return suma / indices.Length;
        }
    }
}
