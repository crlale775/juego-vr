using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Construye la cancha completa por código: piso de madera, líneas, muros,
    /// grada y las luminarias. También permite cambiar el "escenario" (temas) para
    /// acompañar los cambios de iluminación.
    /// </summary>
    public class CourtBuilder : MonoBehaviour
    {
        public Transform DecorRoot { get; private set; }
        public Material FloorMaterial { get; private set; }
        public Material WallMaterial { get; private set; }
        public Material LineMaterial { get; private set; }
        public Material KeyMaterial { get; private set; }
        public Material CementMaterial { get; private set; }
        public Texture2D BannerTexture { get; private set; }

        private GameObject[] _gradas;
        private Light[] _lamparas;
        private int _theme = -1;

        public Light[] Lamparas { get { return _lamparas; } }

        private static readonly Color[] ThemeFloor =
        {
            new Color(0.72f, 0.52f, 0.30f), // duela clásica
            new Color(0.25f, 0.42f, 0.68f), // azul universitario
            new Color(0.16f, 0.16f, 0.20f), // cancha oscura
            new Color(0.55f, 0.20f, 0.22f), // rojo
            new Color(0.26f, 0.14f, 0.34f)  // Halloween: duela morada
        };

        private static readonly Color[] ThemeWall =
        {
            new Color(0.10f, 0.11f, 0.16f),
            new Color(0.06f, 0.10f, 0.20f),
            new Color(0.02f, 0.02f, 0.03f),
            new Color(0.16f, 0.05f, 0.06f),
            new Color(0.09f, 0.03f, 0.14f)  // Halloween: muro violeta
        };

        private static readonly Color[] ThemeLine =
        {
            new Color(0.95f, 0.95f, 0.95f),
            new Color(0.95f, 0.95f, 0.95f),
            new Color(0.55f, 0.55f, 0.6f),
            new Color(0.95f, 0.85f, 0.85f),
            new Color(1f, 0.62f, 0.15f)     // Halloween: líneas naranjas
        };

        public void Build()
        {
            FloorMaterial = UiFactory.MakeStandardMaterial("Piso", ThemeFloor[0], 0.36f, 0.08f);
            WallMaterial = UiFactory.MakeStandardMaterial("Muro", new Color(0.18f, 0.21f, 0.28f), 0.06f, 0f);
            LineMaterial = UiFactory.MakeStandardMaterial("Linea", new Color(0.98f, 0.98f, 1f), 0.24f, 0f);
            KeyMaterial = UiFactory.MakeStandardMaterial("Zona", new Color(0.35f, 0.42f, 0.62f), 0.15f, 0f);
            CementMaterial = UiFactory.MakeStandardMaterial("Estructura", new Color(0.16f, 0.17f, 0.21f), 0.1f, 0f);

            // Duela de madera generada por código: se repite por todo el piso.
            Texture2D wood = MeshUtils.CreateCourtTexture(512, 0.86f, 0.86f, 0.86f);
            FloorMaterial.mainTexture = wood;
            FloorMaterial.mainTextureScale = new Vector2(6f, 6f);

            BannerTexture = MeshUtils.CreateBannerTexture(new Color(0.15f, 0.32f, 0.62f), new Color(0.09f, 0.18f, 0.38f));

            DecorRoot = new GameObject("Decoracion").transform;
            DecorRoot.SetParent(transform, false);

            BuildFloor();
            BuildLines();
            BuildKeyPaint();
            BuildCenterCircle();
            BuildWalls();
            BuildWallTrim();
            BuildCeiling();
            BuildWallPanels();
            BuildStands();
            BuildBleacherBase();
            BuildLamps();
            BuildLampFixtures();

            SetTheme(0);
        }

        /// <summary>Zona pintada bajo el aro (la "llave"), como en una cancha real.</summary>
        private void BuildKeyPaint()
        {
            GameObject key = GameObject.CreatePrimitive(PrimitiveType.Cube);
            key.name = "ZonaPintada";
            key.transform.SetParent(transform, false);
            key.transform.localPosition = new Vector3(0f, 0.012f, 4.4f);
            key.transform.localScale = new Vector3(4.9f, 0.02f, 5.8f);
            key.GetComponent<Renderer>().sharedMaterial = KeyMaterial;
            Destroy(key.GetComponent<Collider>());
        }

        /// <summary>Círculo central hecho con segmentos finos.</summary>
        private void BuildCenterCircle()
        {
            GameObject group = new GameObject("CirculoCentral");
            group.transform.SetParent(transform, false);

            int segments = 28;
            float radius = 1.9f;
            Vector3 previous = new Vector3(radius, 0.021f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Vector3 current = new Vector3(Mathf.Cos(a) * radius, 0.021f, Mathf.Sin(a) * radius);
                Vector3 mid = (previous + current) * 0.5f;
                Vector3 dir = current - previous;

                GameObject seg = AddLine(group.transform, mid, new Vector3(dir.magnitude, 0.02f, 0.08f));
                seg.transform.rotation = Quaternion.LookRotation(dir);
                previous = current;
            }
        }

        /// <summary>Zócalo oscuro en la base de las paredes.</summary>
        private void BuildWallTrim()
        {
            GameObject trim = new GameObject("Zocalos");
            trim.transform.SetParent(transform, false);

            float half = 15.35f;
            AddWall(trim.transform, new Vector3(0f, 0.6f, half), new Vector3(31.2f, 1.2f, 0.18f));
            AddWall(trim.transform, new Vector3(0f, 0.6f, -half), new Vector3(31.2f, 1.2f, 0.18f));
            AddWall(trim.transform, new Vector3(half, 0.6f, 0f), new Vector3(0.18f, 1.2f, 31.2f));
            AddWall(trim.transform, new Vector3(-half, 0.6f, 0f), new Vector3(0.18f, 1.2f, 31.2f));
        }

        /// <summary>Techop con vigas: cierra el espacio y evita que se vea el vacío.</summary>
        private void BuildCeiling()
        {
            GameObject ceiling = new GameObject("Techo");
            ceiling.transform.SetParent(transform, false);

            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Losa";
            slab.transform.SetParent(ceiling.transform, false);
            slab.transform.localPosition = new Vector3(0f, 9.4f, 0f);
            slab.transform.localScale = new Vector3(31.4f, 0.4f, 31.4f);
            slab.GetComponent<Renderer>().sharedMaterial = CementMaterial;
            Destroy(slab.GetComponent<Collider>());

            for (int i = 0; i < 5; i++)
            {
                float z = -12f + i * 6f;
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "Viga";
                beam.transform.SetParent(ceiling.transform, false);
                beam.transform.localPosition = new Vector3(0f, 9f, z);
                beam.transform.localScale = new Vector3(31.2f, 0.5f, 0.4f);
                beam.GetComponent<Renderer>().sharedMaterial = CementMaterial;
                Destroy(beam.GetComponent<Collider>());
            }

            for (int i = 0; i < 3; i++)
            {
                float x = -8f + i * 8f;
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "VigaCruzada";
                beam.transform.SetParent(ceiling.transform, false);
                beam.transform.localPosition = new Vector3(x, 8.6f, 0f);
                beam.transform.localScale = new Vector3(0.4f, 0.4f, 31.2f);
                beam.GetComponent<Renderer>().sharedMaterial = CementMaterial;
                Destroy(beam.GetComponent<Collider>());
            }
        }

        /// <summary>Base de las gradas, para que los asientos no parezcan flotar.</summary>
        private void BuildBleacherBase()
        {
            GameObject group = new GameObject("BaseGradas");
            group.transform.SetParent(DecorRoot, false);

            AddWall(group.transform, new Vector3(0f, 0.35f, 14.9f), new Vector3(12.6f, 0.7f, 3.4f));
            AddWall(group.transform, new Vector3(0f, 0.35f, -14.9f), new Vector3(12.6f, 0.7f, 3.4f));
            AddWall(group.transform, new Vector3(14.9f, 0.35f, 0f), new Vector3(3.4f, 0.7f, 12.6f));
            AddWall(group.transform, new Vector3(-14.9f, 0.35f, 0f), new Vector3(3.4f, 0.7f, 12.6f));
        }

        /// <summary>Carcasas de las luces: sin ellas las lámparas parecen flotar.</summary>
        private void BuildLampFixtures()
        {
            if (_lamparas == null)
            {
                return;
            }

            GameObject group = new GameObject("Luminarias");
            group.transform.SetParent(transform, false);

            for (int i = 0; i < _lamparas.Length; i++)
            {
                if (_lamparas[i] == null)
                {
                    continue;
                }

                GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                housing.name = "Luminaria";
                housing.transform.SetParent(group.transform, false);
                housing.transform.localPosition = _lamparas[i].transform.localPosition + new Vector3(0f, 0.32f, 0f);
                housing.transform.localScale = new Vector3(1.5f, 0.08f, 0.8f);
                housing.GetComponent<Renderer>().sharedMaterial = CementMaterial;
                Destroy(housing.GetComponent<Collider>());

                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Foco";
                bulb.transform.SetParent(housing.transform, false);
                bulb.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                bulb.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                bulb.GetComponent<Renderer>().sharedMaterial = GlowMaterial();
                Destroy(bulb.GetComponent<Collider>());
            }
        }

        private Material _glowMaterial;

        private Material GlowMaterial()
        {
            if (_glowMaterial != null)
            {
                return _glowMaterial;
            }

            _glowMaterial = UiFactory.MakeStandardMaterial("FocoLuz", new Color(1f, 0.98f, 0.9f), 0.5f, 0f);
            _glowMaterial.EnableKeyword("_EMISSION");
            if (_glowMaterial.HasProperty("_EmissionColor"))
            {
                _glowMaterial.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.82f) * 0.85f);
            }
            _glowMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return _glowMaterial;
        }

        /// <summary>Lonas de publicidad en las cuatro paredes.</summary>
        private void BuildWallPanels()
        {
            GameObject group = new GameObject("Lonas");
            group.transform.SetParent(transform, false);

            Material banner = UiFactory.MakeStandardMaterial("Lona", Color.white, 0.06f, 0f);
            banner.mainTexture = BannerTexture;

            for (int i = 0; i < 4; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 1 && j == 0)
                    {
                        // En la pared del fondo va el marcador, no una lona.
                        continue;
                    }

                    float offset = j * 8.6f;
                    float half = 15.28f;

                    Vector3 position;
                    Vector3 size;

                    if (i == 0) { position = new Vector3(offset, 2.6f, half); size = new Vector3(7.8f, 1.9f, 0.12f); }
                    else if (i == 1) { position = new Vector3(offset, 2.6f, -half); size = new Vector3(7.8f, 1.9f, 0.12f); }
                    else if (i == 2) { position = new Vector3(half, 2.6f, offset); size = new Vector3(0.12f, 1.9f, 7.8f); }
                    else { position = new Vector3(-half, 2.6f, offset); size = new Vector3(0.12f, 1.9f, 7.8f); }

                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Lona";
                    go.transform.SetParent(group.transform, false);
                    go.transform.localPosition = position;
                    go.transform.localScale = size;
                    go.GetComponent<Renderer>().sharedMaterial = banner;
                    Destroy(go.GetComponent<Collider>());
                }
            }
        }

        private void BuildFloor()
        {
            // Se usa una caja en vez de un plano: el plano necesita MeshCollider, que
            // Unity puede eliminar al optimizar la compilación y dejaría el piso sin fisica.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PisoCancha";
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            floor.transform.localScale = new Vector3(32f, 0.4f, 32f);
            floor.GetComponent<Renderer>().sharedMaterial = FloorMaterial;

            Collider collider = floor.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
            }
        }

        private void BuildLines()
        {
            GameObject lines = new GameObject("Lineas");
            lines.transform.SetParent(transform, false);

            // Perímetro y línea de tiros libres
            AddLine(lines.transform, new Vector3(0f, 0.021f, 0f), new Vector3(11.2f, 0.02f, 0.12f));
            AddLine(lines.transform, new Vector3(0f, 0.021f, 0f), new Vector3(0.12f, 0.02f, 11.2f));
            AddLine(lines.transform, new Vector3(0f, 0.021f, 4.9f), new Vector3(3.6f, 0.02f, 0.1f));
            AddLine(lines.transform, new Vector3(0f, 0.021f, -5.6f), new Vector3(3.6f, 0.02f, 0.1f));

            // Arco de tres puntos aproximado con segmentos
            int segments = 26;
            float radius = 6.4f;
            float previousAngle = -Mathf.PI * 0.42f;
            Vector3 previous = new Vector3(Mathf.Sin(previousAngle) * radius, 0.021f, 5.1f + Mathf.Cos(previousAngle) * radius);

            for (int i = 1; i <= segments; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.42f, Mathf.PI * 0.42f, (float)i / segments);
                Vector3 current = new Vector3(Mathf.Sin(a) * radius, 0.021f, 5.1f + Mathf.Cos(a) * radius);
                Vector3 mid = (previous + current) * 0.5f;
                Vector3 dir = current - previous;
                GameObject seg = AddLine(lines.transform, mid, new Vector3(dir.magnitude, 0.02f, 0.09f));
                seg.transform.rotation = Quaternion.LookRotation(dir);
                previous = current;
            }
        }

        private GameObject AddLine(Transform parent, Vector3 position, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Linea";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = LineMaterial;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void BuildWalls()
        {
            GameObject walls = new GameObject("Muros");
            walls.transform.SetParent(transform, false);

            AddWall(walls.transform, new Vector3(0f, 3.5f, 15.6f), new Vector3(31.2f, 7f, 0.4f));
            AddWall(walls.transform, new Vector3(0f, 3.5f, -15.6f), new Vector3(31.2f, 7f, 0.4f));
            AddWall(walls.transform, new Vector3(15.6f, 3.5f, 0f), new Vector3(0.4f, 7f, 31.2f));
            AddWall(walls.transform, new Vector3(-15.6f, 3.5f, 0f), new Vector3(0.4f, 7f, 31.2f));
        }

        private void AddWall(Transform parent, Vector3 position, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Muro";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = WallMaterial;
            Destroy(go.GetComponent<Collider>());
        }

        private void BuildStands()
        {
            // Paleta compartida: unos pocos materiales permiten que Unity agrupe las
            // sillas en lotes y el teléfono no se sature.
            const int paletteSize = 8;
            Material[] palette = new Material[paletteSize];
            for (int i = 0; i < paletteSize; i++)
            {
                Color c = Color.HSVToRGB((float)i / paletteSize, 0.62f, 0.72f);
                palette[i] = UiFactory.MakeStandardMaterial("Asiento" + i, c, 0.1f, 0f);
            }

            _gradas = new GameObject[4];

            for (int side = 0; side < 4; side++)
            {
                GameObject stand = new GameObject("Grada" + side);
                stand.transform.SetParent(DecorRoot, false);
                _gradas[side] = stand;

                for (int step = 0; step < 3; step++)
                {
                    for (int seat = 0; seat < 11; seat++)
                    {
                        GameObject seatGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        seatGo.name = "Asiento";
                        seatGo.transform.SetParent(stand.transform, false);

                        float offset = -5.4f + seat * 1.08f;
                        float height = 0.9f + step * 0.78f;
                        float depth = 13.8f + step * 1.15f;

                        Vector3 pos;
                        Vector3 size = new Vector3(0.88f, 0.55f, 0.88f);
                        if (side == 0) { pos = new Vector3(offset, height, depth); }
                        else if (side == 1) { pos = new Vector3(offset, height, -depth); }
                        else if (side == 2) { pos = new Vector3(depth, height, offset); }
                        else { pos = new Vector3(-depth, height, offset); }

                        seatGo.transform.localPosition = pos;
                        seatGo.transform.localScale = size;

                        int index = (seat + step * 3 + side * 2) % paletteSize;
                        seatGo.GetComponent<Renderer>().sharedMaterial = palette[index];
                        Destroy(seatGo.GetComponent<Collider>());
                    }
                }
            }
        }

        private void BuildLamps()
        {
            _lamparas = new Light[5];

            Vector3[] positions =
            {
                new Vector3(0f, 8.4f, 3f),
                new Vector3(-5f, 8.4f, 6f),
                new Vector3(5f, 8.4f, 6f),
                new Vector3(-5f, 8.4f, -3f),
                new Vector3(5f, 8.4f, -3f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject go = new GameObject("Lampara" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = positions[i];

                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 22f;
                l.intensity = i == 0 ? 1.15f : 0.75f;
                l.color = new Color(1f, 0.97f, 0.9f);
                l.shadows = LightShadows.None;
                _lamparas[i] = l;
            }
        }

        /// <summary>Cambia el aspecto del escenario (0 a 3).</summary>
        public void SetTheme(int index)
        {
            index = Mathf.Clamp(index, 0, ThemeFloor.Length - 1);
            if (index == _theme)
            {
                return;
            }
            _theme = index;

            if (FloorMaterial != null)
            {
                FloorMaterial.SetColor("_Color", ThemeFloor[index]);
            }
            if (WallMaterial != null)
            {
                WallMaterial.SetColor("_Color", ThemeWall[index]);
            }
            if (LineMaterial != null)
            {
                LineMaterial.SetColor("_Color", ThemeLine[index]);
            }
        }

        /// <summary>Tema de Halloween (calabazas, violeta y naranja).</summary>
        public const int HalloweenTheme = 4;

        /// <summary>Muestra u oculta la grada: el escenario "desaparece" en los apagones.</summary>
        public void SetStandsVisible(bool visible)
        {
            if (_gradas == null)
            {
                return;
            }

            for (int i = 0; i < _gradas.Length; i++)
            {
                if (_gradas[i] != null)
                {
                    _gradas[i].SetActive(visible);
                }
            }
        }
    }
}
