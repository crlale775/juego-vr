using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Mano simulada: fabrica los 21 puntos de una mano y los va moviendo siguiendo un
    /// guion (subir, empujar, puño, pellizco). Sirve para probar TODO lo que depende de
    /// los puntos de la mano (el esqueleto dibujado, el cierre de dedos y el gesto de
    /// pellizco) sin cámara y sin móvil.
    ///
    /// Se activa con ?simular=1 y el guion con ?guion=subir|empujar|puno|pellizco.
    /// </summary>
    public class ManoSimulada
    {
        public string guion = "subir";

        /// <summary>Los 21 puntos (x derecha, y HACIA ARRIBA, como el resto del juego).</summary>
        public readonly Vector3[] Puntos = new Vector3[21];

        public Vector2 Centro { get; private set; }
        public float Cobertura { get; private set; }
        public float Ancho { get; private set; }
        public float Alto { get; private set; }

        private float _tiempo;

        public ManoSimulada(string guion)
        {
            this.guion = string.IsNullOrEmpty(guion) ? "subir" : guion;
        }

        /// <summary>Avanza el guion y recalcula los 21 puntos. Devuelve la pose.</summary>
        public void Actualizar(float deltaTiempo, float tiempo)
        {
            _tiempo = tiempo;

            float ciclo = Mathf.Repeat(tiempo, 3f);          // el guion se repite cada 3 s
            float escala = 0.20f;
            float centroX = 0.5f;
            float centroY = 0.52f;
            float apertura = 0f;                             // 0 = puño, 1 = abierto
            float pinza = 0f;                                // 1 = pellizcando

            switch (guion)
            {
                case "empujar":
                    // La mano se acerca a la cámara: crece de golpe a mitad de ciclo.
                    escala = 0.20f + Mathf.Clamp01((ciclo - 0.4f) / 0.5f) * 0.14f;
                    apertura = 1f;
                    break;

                case "puno":
                    // Puño cerrado y, de golpe, la mano abierta.
                    apertura = ciclo < 1f ? 0f : 1f;
                    break;

                case "pellizco":
                    // Pellizca y suelta.
                    pinza = ciclo < 1f ? 1f : 0f;
                    apertura = ciclo < 1f ? 0.25f : 1f;
                    break;

                default:
                    // Sube la mano de golpe (el gesto de siempre).
                    apertura = 1f;
                    centroY = 0.30f + Mathf.Clamp01(ciclo / 1f) * 0.42f;
                    break;
            }

            Construir(centroX, centroY, escala, apertura, pinza);

            Centro = AnalisisMano.CentroPalma(Puntos);
            Cobertura = AnalisisMano.MedirCobertura(Puntos, out float ancho, out float alto);
            Ancho = ancho;
            Alto = alto;
        }

        /// <summary>
        /// Coloca los 21 puntos: muñeca, palma y las cadenas de dedos. Con
        /// <paramref name="cierre"/> los dedos se doblan hacia la palma y con
        /// <paramref name="pinza"/> la punta del pulgar toca la del índice.
        /// </summary>
        private void Construir(float cx, float cy, float escala, float apertura, float pinza)
        {
            float cierre = 1f - Mathf.Clamp01(apertura);
            float L = escala;

            Puntos[0] = new Vector3(cx, cy - L * 0.55f, 0f);                  // muñeca

            Puntos[5] = new Vector3(cx - L * 0.30f, cy + L * 0.15f, 0f);      // nudillos
            Puntos[9] = new Vector3(cx - L * 0.10f, cy + L * 0.18f, 0f);
            Puntos[13] = new Vector3(cx + L * 0.10f, cy + L * 0.17f, 0f);
            Puntos[17] = new Vector3(cx + L * 0.28f, cy + L * 0.14f, 0f);

            // Índice, medio, anular y meñique: cada uno con su largo.
            Dedo(5, 6, 7, 8, cx - L * 0.30f, cy + L * 0.15f, L * 0.60f, cierre);
            Dedo(9, 10, 11, 12, cx - L * 0.10f, cy + L * 0.18f, L * 0.70f, cierre);
            Dedo(13, 14, 15, 16, cx + L * 0.10f, cy + L * 0.17f, L * 0.62f, cierre);
            Dedo(17, 18, 19, 20, cx + L * 0.28f, cy + L * 0.14f, L * 0.50f, cierre);

            // Pulgar: sale de lado. Al pellizcar, su punta se junta con la del índice.
            float pulgarX = cx - L * 0.42f;
            float pulgarY = cy - L * 0.30f;

            Puntos[1] = new Vector3(pulgarX, pulgarY, 0f);
            Puntos[2] = new Vector3(pulgarX - L * 0.20f, pulgarY + L * 0.18f, 0f);
            Puntos[3] = new Vector3(pulgarX - L * 0.26f, pulgarY + L * 0.34f, 0f);
            Puntos[4] = new Vector3(pulgarX - L * 0.28f, pulgarY + L * 0.48f, 0f);

            if (pinza > 0f)
            {
                Puntos[4] = Vector3.Lerp(Puntos[4], Puntos[8], pinza);

                // Al pellizcar el índice se dobla un poco para tocar el pulgar.
                Puntos[7] = Vector3.Lerp(Puntos[7], Puntos[6], pinza * 0.4f);
                Puntos[8] = Vector3.Lerp(Puntos[8], Puntos[6], pinza * 0.35f);
            }
        }

        /// <summary>Falanges de un dedo: al cerrar, la punta se dobla hacia el nudillo.</summary>
        private void Dedo(int mcp, int pip, int dip, int tip, float baseX, float baseY, float largo, float cierre)
        {
            Vector3 basePunto = new Vector3(baseX, baseY, 0f);

            Vector3 extendido1 = new Vector3(baseX, baseY + largo * 0.45f, 0f);
            Vector3 extendido2 = new Vector3(baseX, baseY + largo * 0.75f, 0f);
            Vector3 extendido3 = new Vector3(baseX, baseY + largo, 0f);

            Vector3 doblado1 = Vector3.Lerp(basePunto, extendido1, 0.55f);
            Vector3 doblado2 = Vector3.Lerp(basePunto, extendido2, 0.28f);
            Vector3 doblado3 = Vector3.Lerp(basePunto, extendido3, 0.10f);

            Puntos[pip] = Vector3.Lerp(extendido1, doblado1, cierre);
            Puntos[dip] = Vector3.Lerp(extendido2, doblado2, cierre);
            Puntos[tip] = Vector3.Lerp(extendido3, doblado3, cierre);
        }
    }
}
