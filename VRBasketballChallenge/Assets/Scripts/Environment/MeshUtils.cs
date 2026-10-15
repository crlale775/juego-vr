using UnityEngine;

namespace VRBasketball
{
    /// <summary>Mallas y texturas generadas por código, sin archivos externos.</summary>
    public static class MeshUtils
    {
        public static Mesh CreateTorus(float radius, float tube, int segments, int tubeSegments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "ToroAro";

            int vertexCount = (segments + 1) * (tubeSegments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];

            int index = 0;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float ang = u * Mathf.PI * 2f;

                Vector3 centerDir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 center = centerDir * radius;
                Vector3 up = Vector3.up;

                for (int j = 0; j <= tubeSegments; j++)
                {
                    float v = (float)j / tubeSegments;
                    float tubeAng = v * Mathf.PI * 2f;
                    Vector3 normal = centerDir * Mathf.Cos(tubeAng) + up * Mathf.Sin(tubeAng);

                    vertices[index] = center + normal * tube;
                    normals[index] = normal;
                    uv[index] = new Vector2(u * 4f, v);
                    index++;
                }
            }

            int[] triangles = new int[segments * tubeSegments * 6];
            int t = 0;
            int ring = tubeSegments + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * ring + j;
                    int b = a + ring;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Esfera simple (el balón) generada por código.</summary>
        public static Mesh CreateSphere(float radius, int segments, int rings)
        {
            Mesh mesh = new Mesh();
            mesh.name = "EsferaBalon";

            int vertexCount = (rings + 1) * (segments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];

            int index = 0;
            for (int ring = 0; ring <= rings; ring++)
            {
                float v = (float)ring / rings;
                float phi = v * Mathf.PI;

                for (int seg = 0; seg <= segments; seg++)
                {
                    float u = (float)seg / segments;
                    float theta = u * Mathf.PI * 2f;

                    Vector3 normal = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));

                    vertices[index] = normal * radius;
                    normals[index] = normal;
                    uv[index] = new Vector2(u, 1f - v);
                    index++;
                }
            }

            int[] triangles = new int[rings * segments * 6];
            int t = 0;
            int stride = segments + 1;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int seg = 0; seg < segments; seg++)
                {
                    int a = ring * stride + seg;
                    int b = a + stride;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Duela de la cancha: tablas de madera con veta, juntas y un poco de suciedad.
        /// Se repite por el piso, así que el dibujo es continuo de borde a borde.
        /// </summary>
        public static Texture2D CreateCourtTexture(int size = 512, float baseR = 0.74f, float baseG = 0.53f, float baseB = 0.30f)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "TexturaCancha";

            const int planks = 8;
            float plankHeight = (float)size / planks;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Veta de la madera: ruido estirado a lo largo de la tabla.
                    float grain = Mathf.PerlinNoise(u * 6f + 0.5f, v * 42f) * 0.10f
                                  + Mathf.PerlinNoise(u * 22f, v * 150f) * 0.05f;

                    // Junta entre tablas y finales de tabla desfasados.
                    float row = Mathf.Floor(v * planks);
                    float withinRow = (v * planks) - row;
                    float joint = Mathf.Min(withinRow, 1f - withinRow);
                    float dark = joint < 0.02f ? 0.45f : 1f;

                    float offset = Mathf.Repeat(row * 0.37f, 1f);
                    float endU = Mathf.Repeat(u + offset, 1f);
                    if (endU < 0.008f)
                    {
                        dark *= 0.7f;
                    }

                    float shade = 1f + grain - (row % 2f) * 0.035f;

                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseR * shade * dark),
                        Mathf.Clamp01(baseG * shade * dark),
                        Mathf.Clamp01(baseB * shade * dark)));
                }
            }

            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply(true);
            return tex;
        }

        /// <summary>Lona de publicidad para las paredes: bandas de color y un emblema.</summary>
        public static Texture2D CreateBannerTexture(Color a, Color b, int width = 256, int height = 128)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            tex.name = "TexturaLona";

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    Color c = ((x + y) / 12) % 2 == 0 ? a : b;

                    // Borde oscuro arriba y abajo, como una lona tensada.
                    float edge = Mathf.Min(v, 1f - v);
                    if (edge < 0.06f)
                    {
                        c *= 0.55f;
                    }

                    // Emblema central: un círculo claro.
                    float dx = (u - 0.5f) * 2.6f;
                    float dy = (v - 0.5f) * 2.6f;
                    if (Mathf.Sqrt(dx * dx + dy * dy) < 0.34f)
                    {
                        c = Color.Lerp(c, Color.white, 0.75f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(true);
            return tex;
        }

        /// <summary>Tablero del aro: blanco, con el cuadro de tiro y el borde reforzado.</summary>
        public static Texture2D CreateBackboardTexture(int size = 256)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "TexturaTablero";

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    Color c = new Color(0.94f, 0.95f, 0.97f);

                    // Marco exterior.
                    if (u < 0.045f || u > 0.955f || v < 0.05f || v > 0.95f)
                    {
                        c = new Color(0.72f, 0.75f, 0.80f);
                    }

                    // Cuadro interior (el que se usa para apuntar).
                    bool inside = u > 0.36f && u < 0.64f && v > 0.22f && v < 0.52f;
                    bool border = (u > 0.35f && u < 0.37f) || (u > 0.63f && u < 0.65f)
                                  || (v > 0.21f && v < 0.23f) || (v > 0.51f && v < 0.53f);
                    if (border && u > 0.33f && u < 0.67f && v > 0.19f && v < 0.55f)
                    {
                        c = new Color(0.90f, 0.35f, 0.12f);
                    }
                    else if (inside)
                    {
                        c = new Color(0.98f, 0.98f, 1f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(true);
            return tex;
        }

        /// <summary>Textura de balón de básquetbol generada por código.</summary>
        public static Texture2D CreateBasketballTexture(int width = 256, int height = 128)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            tex.name = "TexturaBalon";

            Color baseColor = new Color(0.86f, 0.42f, 0.14f);
            Color lineColor = new Color(0.10f, 0.07f, 0.05f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;
                    Color c = baseColor;

                    // Líneas verticales y el ecuador del balón.
                    bool verticalLine = Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f) < 0.022f;
                    bool equator = Mathf.Abs(v - 0.5f) < 0.030f;
                    bool sideArcs = Mathf.Abs(Mathf.Abs(v - 0.5f) - 0.28f) < 0.022f;

                    if (verticalLine || equator || sideArcs)
                    {
                        c = lineColor;
                    }

                    // Grano sutil para que no se vea plano.
                    float grain = Mathf.PerlinNoise(u * 90f, v * 60f) * 0.06f - 0.03f;
                    c.r = Mathf.Clamp01(c.r + grain);
                    c.g = Mathf.Clamp01(c.g + grain);
                    c.b = Mathf.Clamp01(c.b + grain);

                    tex.SetPixel(x, y, c);
                }
            }

            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply(true);
            return tex;
        }
    }
}
