using UnityEngine;

namespace VRBasketball
{
    /// <summary>Medidas obtenidas de un fotograma de la cámara trasera.</summary>
    public struct HandMetrics
    {
        public bool detected;
        public float confidence;   // 0..1
        public float skinRatio;    // % del encuadre ocupado por la mano
        public Vector2 centroid;   // normalizado 0..1
        public Rect bounds;        // normalizado 0..1 (xMin, yMin, ancho, alto)
        public int fingerRuns;     // nº de "dedos" separados que ve el perfil superior
        public float openness;     // 0..1 apertura de la mano
        public float compactness;  // area / area del rectangulo contenedor
    }

    /// <summary>
    /// Detección de mano deliberadamente ligera: segmentación por tono de piel (YCbCr),
    /// centroide, rectángulo contenedor y perfil superior para estimar la apertura.
    /// No es un escaneo 3D: es una estimación pensada para un teléfono.
    /// </summary>
    public static class HandAnalyzer
    {
        public static bool IsSkin(byte r, byte g, byte b)
        {
            // YCbCr: descarta luces muy oscuras y separa la piel del fondo.
            float y  =  0.299f * r + 0.587f * g + 0.114f * b;
            float cb = 128f - 0.168736f * r - 0.331264f * g + 0.5f   * b;
            float cr = 128f + 0.5f   * r - 0.418688f * g - 0.081312f * b;

            // Rango ampliado: cubre tonos claros, medios y oscuros con luz de interior/pantalla
            bool ycbc = y > 25f && cb >= 70f && cb <= 145f && cr >= 125f && cr <= 190f;

            // Regla RGB complementaria (ayuda con luz cálida, fría o de pantalla)
            bool rgb = r > 50 && r > g && g > b * 0.75f && (r - g) > 5 && (r - b) > 8
                       && r < 250 && g < 240;  // evitar sobreexposición total (blanco)

            // Regla extra para tonos oscuros de piel (menos luminancia)
            bool dark = y > 15f && y < 120f && cb >= 68f && cb <= 150f && cr >= 130f && cr <= 195f
                        && r > g && r > b;

            return ycbc || rgb || dark;
        }

        /// <summary>
        /// Analiza un fotograma. <paramref name="mask"/> debe tener tamaño w*h (se rellena con 1/0).
        /// </summary>
        public static HandMetrics Analyze(Color32[] pixels, int w, int h, byte[] mask)
        {
            HandMetrics m = default(HandMetrics);
            if (pixels == null || w <= 0 || h <= 0 || pixels.Length < w * h)
            {
                return m;
            }

            int count = 0;
            long sx = 0;
            long sy = 0;
            int minX = w, minY = h, maxX = -1, maxY = -1;

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x;
                    Color32 c = pixels[i];
                    bool skin = IsSkin(c.r, c.g, c.b);
                    mask[i] = skin ? (byte)1 : (byte)0;

                    if (skin)
                    {
                        count++;
                        sx += x;
                        sy += y;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (count < w * h / 300 || maxX < 0)
            {
                return m;
            }

            int bw = Mathf.Max(1, maxX - minX + 1);
            int bh = Mathf.Max(1, maxY - minY + 1);

            m.detected = true;
            m.skinRatio = (float)count / (w * h);
            m.centroid = new Vector2((float)sx / count / w, (float)sy / count / h);
            m.bounds = new Rect((float)minX / w, (float)minY / h, (float)bw / w, (float)bh / h);
            m.compactness = (float)count / (bw * bh);
            m.fingerRuns = CountFingerRuns(mask, w, h, minX, minY, maxX, maxY);

            // Apertura: dedos separados en la parte alta + silueta poco compacta.
            float fingerScore = Mathf.Clamp01((m.fingerRuns - 1) / 4f);
            float shapeScore = Mathf.Clamp01((0.95f - m.compactness) / 0.5f);
            m.openness = Mathf.Clamp01(0.7f * fingerScore + 0.3f * shapeScore);

            // Confianza: mano que ocupa entre el 3% y el 45% del encuadre y está razonablemente centrada.
            float sizeOk = Mathf.Clamp01(Mathf.Min(m.skinRatio / 0.06f, 0.45f / Mathf.Max(m.skinRatio, 0.0001f)));
            float centered = 1f - Mathf.Clamp01(Vector2.Distance(m.centroid, new Vector2(0.5f, 0.5f)) / 0.6f);
            m.confidence = Mathf.Clamp01(0.6f * sizeOk + 0.4f * centered);

            return m;
        }

        /// <summary>Cuenta "dedos" mirando cuántas franjas de piel hay en la banda superior del rectángulo.</summary>
        private static int CountFingerRuns(byte[] mask, int w, int h, int minX, int minY, int maxX, int maxY)
        {
            int bh = Mathf.Max(1, maxY - minY + 1);
            int bandStart = minY + Mathf.RoundToInt(bh * 0.35f);
            int bandEnd = minY + Mathf.RoundToInt(bh * 0.95f);
            int best = 1;

            for (int y = bandStart; y <= bandEnd && y < h; y++)
            {
                int runs = 0;
                bool inRun = false;
                int runLen = 0;
                int row = y * w;

                for (int x = minX; x <= maxX && x < w; x++)
                {
                    if (mask[row + x] == 1)
                    {
                        if (!inRun)
                        {
                            inRun = true;
                            runLen = 0;
                        }
                        runLen++;
                    }
                    else if (inRun)
                    {
                        if (runLen >= Mathf.Max(2, w / 120))
                        {
                            runs++;
                        }
                        inRun = false;
                    }
                }

                if (inRun && runLen >= Mathf.Max(2, w / 120))
                {
                    runs++;
                }

                if (runs > best)
                {
                    best = runs;
                }
            }

            return Mathf.Clamp(best, 1, 5);
        }

        /// <summary>Convierte las medidas detectadas en el perfil de juego del jugador.</summary>
        public static HandProfile BuildProfile(HandMetrics final, float stability01, string mode)
        {
            // Escala de la mano: alto del rectángulo contenedor respecto a un 45% de referencia.
            float sizeFactor = Mathf.Clamp(final.bounds.height / 0.45f, 0.8f, 1.25f);

            // Agarres: mano abierta = más agarre sobre el balón.
            float grip = Mathf.Clamp01(final.openness);

            // Fuerza del balón: mezcla de agarre y tamaño, penalizada por la inestabilidad.
            float power = 0.85f
                          + 0.30f * grip
                          + 0.25f * (sizeFactor - 0.8f) / 0.45f;
            power *= 0.92f + 0.08f * Mathf.Clamp01(stability01);
            power = Mathf.Clamp(power, 0.7f, 1.4f);

            HandProfile p = new HandProfile
            {
                calibrated = true,
                sizeFactor = sizeFactor,
                grip = grip,
                power = power,
                stability = Mathf.Clamp01(stability01),
                confidence = Mathf.Clamp01(final.confidence),
                mode = mode
            };

            p.quality = Describe(p, final);
            return p;
        }

        private static string Describe(HandProfile p, HandMetrics m)
        {
            string size;
            if (p.sizeFactor < 0.93f) size = "mano compacta";
            else if (p.sizeFactor > 1.08f) size = "mano amplia";
            else size = "mano media";

            string gripTxt;
            if (p.grip < 0.3f) gripTxt = "agarre cerrado";
            else if (p.grip < 0.6f) gripTxt = "agarre medio";
            else gripTxt = "agarre abierto";

            string st = p.stability > 0.7f ? "estable" : (p.stability > 0.4f ? "aceptable" : "inestable");

            return string.Format("{0}, {1}, {2} ({3:0}% confianza, {4} dedos)",
                size, gripTxt, st, p.confidence * 100f, m.fingerRuns);
        }
    }
}
