using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Ambiente de Halloween para la cancha: calabazas encendidas, lápidas,
    /// murciélagos que sobrevuelan, telarañas en las esquinas, luces fatuas y luna.
    /// Todo se genera por código y se puede encender y apagar en caliente.
    /// </summary>
    public class HalloweenDecor : MonoBehaviour
    {
        public bool Active { get; private set; }

        /// <summary>Luz de luna: el director de iluminación la apaga en los apagones.</summary>
        public Light MoonLight { get; private set; }

        public int PumpkinLightCount { get { return _pumpkinLights.Length; } }

        private Transform _root;
        private Material _pumpkinMat;
        private Material _glowMat;
        private Material _stemMat;
        private Material _stoneMat;
        private Material _batMat;
        private Material _webMat;
        private Material _wispMat;

        private Transform[] _bats;
        private float[] _batRadius;
        private float[] _batAngle;
        private float[] _batSpeed;
        private float[] _batHeight;
        private Transform[] _wisps;
        private float[] _wispPhase;
        private Light[] _pumpkinLights;
        private float[] _pumpkinBase;

        private void Awake()
        {
            Active = false;
        }

        // ------------------------------------------------------------------
        // Construcción
        // ------------------------------------------------------------------

        public void Build()
        {
            _root = new GameObject("Halloween").transform;
            _root.SetParent(transform, false);

            MakeMaterials();

            BuildMoon();
            BuildPumpkins();
            BuildTombstones();
            BuildCobwebs();
            BuildWisps();
            BuildBats();

            SetActive(false);
        }

        private void MakeMaterials()
        {
            _pumpkinMat = UiFactory.MakeStandardMaterial("Calabaza", new Color(0.92f, 0.42f, 0.06f), 0.25f, 0f);
            _stemMat = UiFactory.MakeStandardMaterial("Rabo", new Color(0.24f, 0.34f, 0.12f), 0.1f, 0f);
            _stoneMat = UiFactory.MakeStandardMaterial("Lapida", new Color(0.34f, 0.34f, 0.38f), 0.08f, 0f);
            _batMat = UiFactory.MakeStandardMaterial("Murcielago", new Color(0.07f, 0.05f, 0.09f), 0.05f, 0f);

            // Cara encendida: material que emite luz (más barato que añadir luces reales).
            _glowMat = UiFactory.MakeStandardMaterial("Brillo", new Color(1f, 0.55f, 0.12f), 0.4f, 0f);
            _glowMat.EnableKeyword("_EMISSION");
            if (_glowMat.HasProperty("_EmissionColor"))
            {
                _glowMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.08f) * 1.35f);
            }
            _glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            _wispMat = UiFactory.MakeStandardMaterial("Fatuo", new Color(0.45f, 1f, 0.75f), 0.6f, 0f);
            _wispMat.EnableKeyword("_EMISSION");
            if (_wispMat.HasProperty("_EmissionColor"))
            {
                _wispMat.SetColor("_EmissionColor", new Color(0.35f, 1f, 0.7f) * 0.9f);
            }
            _wispMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            _webMat = UiFactory.MakeTransparentMaterial("Telarana", new Color(0.88f, 0.9f, 0.95f, 0.5f), CreateWebTexture());
        }

        private void BuildMoon()
        {
            GameObject go = new GameObject("Luna");
            go.transform.SetParent(_root, false);
            go.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            MoonLight = go.AddComponent<Light>();
            MoonLight.type = LightType.Directional;
            MoonLight.intensity = 0.22f;
            MoonLight.color = new Color(0.54f, 0.62f, 1f);
            MoonLight.shadows = LightShadows.None;
        }

        private void BuildPumpkins()
        {
            // Calabazas grandes en las cuatro esquinas de la cancha.
            Vector3[] corners =
            {
                new Vector3(-8.6f, 0.34f, 9.6f),
                new Vector3(8.6f, 0.34f, 9.6f),
                new Vector3(-8.6f, 0.34f, -9.6f),
                new Vector3(8.6f, 0.34f, -9.6f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                AddPumpkin(corners[i], 1.15f, i < 2);
            }

            // Calabazas pequeñas sobre la base de la grada.
            for (int i = 0; i < 8; i++)
            {
                float t = (i / 7f) * 2f - 1f;
                float x = t * 7.4f;
                float z = -12.6f - Mathf.Abs(t) * 0.8f;
                AddPumpkin(new Vector3(x, 0.27f, z), 0.72f, false);
            }
        }

        private void AddPumpkin(Vector3 position, float scale, bool withLight)
        {
            GameObject pumpkin = new GameObject("Calabaza");
            pumpkin.transform.SetParent(_root, false);
            pumpkin.transform.localPosition = position;
            pumpkin.transform.localRotation = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);
            pumpkin.transform.localScale = Vector3.one * scale;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Cuerpo";
            body.transform.SetParent(pumpkin.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(1f, 0.82f, 1f);
            body.GetComponent<Renderer>().sharedMaterial = _pumpkinMat;
            Strip(body);

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Rabo";
            stem.transform.SetParent(pumpkin.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            stem.transform.localScale = new Vector3(0.11f, 0.09f, 0.11f);
            stem.GetComponent<Renderer>().sharedMaterial = _stemMat;
            Strip(stem);

            // Cara tallada: dos ojos y una boca dentada, apenas por delante de la cáscara.
            AddFacePart(pumpkin.transform, new Vector3(-0.19f, 0.14f, 0.46f), new Vector3(0.15f, 0.15f, 0.06f), 45f);
            AddFacePart(pumpkin.transform, new Vector3(0.19f, 0.14f, 0.46f), new Vector3(0.15f, 0.15f, 0.06f), -45f);
            AddFacePart(pumpkin.transform, new Vector3(0f, -0.14f, 0.47f), new Vector3(0.34f, 0.09f, 0.06f), 0f);
            AddFacePart(pumpkin.transform, new Vector3(-0.11f, -0.21f, 0.45f), new Vector3(0.09f, 0.09f, 0.06f), 0f);
            AddFacePart(pumpkin.transform, new Vector3(0.11f, -0.21f, 0.45f), new Vector3(0.09f, 0.09f, 0.06f), 0f);

            if (!withLight)
            {
                return;
            }

            GameObject lightGo = new GameObject("LuzCalabaza");
            lightGo.transform.SetParent(pumpkin.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.05f, 0.25f);

            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 11f;
            l.intensity = 1.3f;
            l.color = new Color(1f, 0.55f, 0.16f);
            l.shadows = LightShadows.None;

            if (_pumpkinLights == null)
            {
                _pumpkinLights = new Light[0];
            }

            Light[] bigger = new Light[_pumpkinLights.Length + 1];
            System.Array.Copy(_pumpkinLights, bigger, _pumpkinLights.Length);
            bigger[bigger.Length - 1] = l;
            _pumpkinLights = bigger;
        }

        private void AddFacePart(Transform parent, Vector3 position, Vector3 size, float roll)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cara";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            go.GetComponent<Renderer>().sharedMaterial = _glowMat;
            Strip(go);
        }

        private void BuildTombstones()
        {
            for (int i = 0; i < 10; i++)
            {
                float t = (i / 9f) * 2f - 1f;
                float x = -13.4f + (i % 5) * 6.7f;
                float z = i < 5 ? -14.2f - Mathf.Abs(t) * 0.6f : -14.9f - Random.Range(0f, 0.5f);

                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stone.name = "Lapida";
                stone.transform.SetParent(_root, false);
                stone.transform.localPosition = new Vector3(x, 0.62f, z);
                stone.transform.localScale = new Vector3(0.86f, 1.24f, 0.22f);
                stone.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-8f, 8f), Random.Range(-3f, 3f));
                stone.GetComponent<Renderer>().sharedMaterial = _stoneMat;
                Strip(stone);

                GameObject top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                top.name = "Remate";
                top.transform.SetParent(stone.transform, false);
                top.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                top.transform.localScale = new Vector3(0.9f, 0.42f, 1.1f);
                top.GetComponent<Renderer>().sharedMaterial = _stoneMat;
                Strip(top);
            }
        }

        private void BuildCobwebs()
        {
            float half = 15.2f;
            float height = 6.7f;

            AddWeb(new Vector3(-half, height, -half), 165f, 3.4f);
            AddWeb(new Vector3(half, height, -half), -165f, 3.4f);
            AddWeb(new Vector3(-half, height, half), 15f, 3.4f);
            AddWeb(new Vector3(half, height, half), -15f, 3.4f);
        }

        private void AddWeb(Vector3 position, float yaw, float size)
        {
            GameObject web = GameObject.CreatePrimitive(PrimitiveType.Quad);
            web.name = "Telarana";
            web.transform.SetParent(_root, false);
            web.transform.localPosition = position;
            web.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            web.transform.localScale = new Vector3(size, size, 1f);
            web.GetComponent<Renderer>().sharedMaterial = _webMat;
            Strip(web);
        }

        private void BuildWisps()
        {
            _wisps = new Transform[6];
            _wispPhase = new float[6];

            for (int i = 0; i < _wisps.Length; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "LuzFátua";
                go.transform.SetParent(_root, false);
                go.transform.localScale = Vector3.one * 0.09f;

                float angle = (float)i / _wisps.Length * Mathf.PI * 2f;
                go.transform.localPosition = new Vector3(Mathf.Cos(angle) * 6.5f, 2.3f + (i % 3) * 0.7f, 2.5f + Mathf.Sin(angle) * 5.5f);
                go.GetComponent<Renderer>().sharedMaterial = _wispMat;
                Strip(go);

                _wisps[i] = go.transform;
                _wispPhase[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        private void BuildBats()
        {
            _bats = new Transform[10];
            _batRadius = new float[_bats.Length];
            _batAngle = new float[_bats.Length];
            _batSpeed = new float[_bats.Length];
            _batHeight = new float[_bats.Length];

            for (int i = 0; i < _bats.Length; i++)
            {
                GameObject bat = new GameObject("Murcielago");
                bat.transform.SetParent(_root, false);

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Cuerpo";
                body.transform.SetParent(bat.transform, false);
                body.transform.localScale = new Vector3(0.13f, 0.17f, 0.1f);
                body.GetComponent<Renderer>().sharedMaterial = _batMat;
                Strip(body);

                AddWing(bat.transform, -1f);
                AddWing(bat.transform, 1f);

                _bats[i] = bat.transform;
                _batRadius[i] = Random.Range(5.5f, 12.5f);
                _batAngle[i] = Random.Range(0f, Mathf.PI * 2f);
                _batSpeed[i] = Random.Range(0.22f, 0.55f) * (Random.value > 0.5f ? 1f : -1f);
                _batHeight[i] = Random.Range(3.6f, 6.6f);

                bat.transform.localPosition = new Vector3(Mathf.Cos(_batAngle[i]) * _batRadius[i], _batHeight[i], Mathf.Sin(_batAngle[i]) * _batRadius[i]);
            }
        }

        private void AddWing(Transform parent, float side)
        {
            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = "Ala";
            wing.transform.SetParent(parent, false);
            wing.transform.localPosition = new Vector3(0.19f * side, 0.02f, 0f);
            wing.transform.localScale = new Vector3(0.3f, 0.12f, 0.03f);
            wing.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
            wing.GetComponent<Renderer>().sharedMaterial = _batMat;
            Strip(wing);
        }

        private void Strip(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Destroy(c);
            }
        }

        // ------------------------------------------------------------------
        // Encendido y animación
        // ------------------------------------------------------------------

        public void SetActive(bool on)
        {
            Active = on;

            if (_root != null)
            {
                _root.gameObject.SetActive(on);
            }
        }

        private void Update()
        {
            if (!Active)
            {
                return;
            }

            float dt = Time.deltaTime;
            float time = Time.time;

            // Murciélagos: giran alrededor de la cancha y aletean.
            if (_bats != null)
            {
                for (int i = 0; i < _bats.Length; i++)
                {
                    if (_bats[i] == null)
                    {
                        continue;
                    }

                    _batAngle[i] += _batSpeed[i] * dt;
                    float r = _batRadius[i] + Mathf.Sin(time * 0.6f + i) * 0.5f;
                    float y = _batHeight[i] + Mathf.Sin(time * 1.3f + i * 1.7f) * 0.35f;

                    float x = Mathf.Cos(_batAngle[i]) * r;
                    float z = Mathf.Sin(_batAngle[i]) * r;

                    _bats[i].localPosition = new Vector3(x, y, z);
                    _bats[i].localRotation = Quaternion.Euler(Mathf.Sin(time * 0.8f + i) * 8f, -_batAngle[i] * Mathf.Rad2Deg + 90f, 0f);

                    // Aleteo
                    float flap = Mathf.Sin(time * 9f + i) * 42f;
                    for (int w = 0; w < _bats[i].childCount; w++)
                    {
                        Transform wing = _bats[i].GetChild(w);
                        if (wing.name != "Ala")
                        {
                            continue;
                        }

                        float side = wing.localPosition.x > 0f ? 1f : -1f;
                        wing.localRotation = Quaternion.Euler(0f, 0f, side * (12f + flap));
                    }
                }
            }

            // Luces fatuas: flotan y parpadean.
            if (_wisps != null)
            {
                for (int i = 0; i < _wisps.Length; i++)
                {
                    if (_wisps[i] == null)
                    {
                        continue;
                    }

                    Vector3 p = _wisps[i].localPosition;
                    p.x += Mathf.Sin(time * 0.5f + _wispPhase[i]) * 0.25f * dt * 6f;
                    p.y += Mathf.Cos(time * 0.7f + _wispPhase[i]) * 0.2f * dt * 6f;
                    if (p.y < 1.2f) p.y = 1.2f;
                    if (p.y > 4.4f) p.y = 4.4f;
                    _wisps[i].localPosition = p;
                    _wisps[i].localRotation = Quaternion.identity;
                }
            }

            // Las calabazas respiran, como si tuvieran una vela dentro.
            if (_pumpkinLights != null)
            {
                if (_pumpkinBase == null || _pumpkinBase.Length != _pumpkinLights.Length)
                {
                    _pumpkinBase = new float[_pumpkinLights.Length];
                    for (int i = 0; i < _pumpkinBase.Length; i++)
                    {
                        _pumpkinBase[i] = Random.Range(3.5f, 7.5f);
                    }
                }

                for (int i = 0; i < _pumpkinLights.Length; i++)
                {
                    Light l = _pumpkinLights[i];
                    if (l == null)
                    {
                        continue;
                    }

                    float slow = Mathf.Sin(time * 4.2f + _pumpkinBase[i]) * 0.5f + 0.5f;
                    float fast = Mathf.PerlinNoise(time * 6f, _pumpkinBase[i]) * 0.5f;
                    l.intensity = 0.85f + slow * 0.55f + fast * 0.35f;
                }
            }
        }

        // ------------------------------------------------------------------
        // Textura de telaraña generada por código
        // ------------------------------------------------------------------

        private static Texture2D CreateWebTexture(int size = 128)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Telarana";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            const int spokes = 10;
            float spokeWidth = 1.4f;
            float[] rings = { 0.16f, 0.34f, 0.52f, 0.7f, 0.88f, 1.02f };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = ((x + 0.5f) / size) * 2f - 1f;
                    float v = ((y + 0.5f) / size) * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);

                    float alpha = 0f;

                    if (r <= 1.02f)
                    {
                        float angle = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                        if (angle < 0f) angle += 360f;

                        float step = 360f / spokes;
                        float nearest = Mathf.Round(angle / step) * step;
                        float delta = Mathf.Abs(Mathf.DeltaAngle(angle, nearest));

                        // Radios
                        if (delta < spokeWidth)
                        {
                            alpha = Mathf.Clamp01(1f - delta / spokeWidth) * 0.9f;
                        }

                        // Anillos, con el centro más oscuro
                        for (int i = 0; i < rings.Length; i++)
                        {
                            float thickness = 0.012f + r * 0.01f;
                            if (Mathf.Abs(r - rings[i]) < thickness)
                            {
                                alpha = Mathf.Max(alpha, 0.75f);
                            }
                        }
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, false);
            return tex;
        }
    }
}
