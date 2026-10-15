using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Cambia la iluminación y el escenario de golpe. Es la mecánica que sube la
    /// dificultad sin tocar el aro: apagones, luz roja, intermitencia y cambios de tema.
    /// </summary>
    public class LightingDirector : MonoBehaviour
    {
        public enum Mood { Normal, Apagon, Rojo, Intermitente, Caos }

        public CourtBuilder court;
        public Light keyLight;

        [Tooltip("Ambiente de Halloween: niebla morada, luces naranjas y calabazas.")]
        public bool halloween;

        [Tooltip("Decoración de Halloween: se atenúa su luz de luna en los apagones.")]
        public HalloweenDecor decor;

        [Tooltip("Foco del aro: se atenúa en la oscuridad pero mantiene el objetivo visible.")]
        public Light hoopLight;

        public Mood CurrentMood { get; private set; }
        public bool Enabled { get; set; }

        private LevelConfig _config;
        private float _moodTimer;
        private float _nextMoodChange = 6f;
        private float _strobeTimer;
        private bool _strobeOn = true;
        private float _blend = 1f;
        private float _targetIntensity = 1f;

        private float _wantedIntensity = 1f;
        private Color _wantedColor = new Color(1f, 0.97f, 0.9f);
        private float _wantedAmbient = 0.32f;

        private float _currentIntensity = 1f;
        private Color _currentColor = new Color(1f, 0.97f, 0.9f);
        private float _currentAmbient = 0.32f;

        public void Configure(LevelConfig config)
        {
            _config = config;
            Enabled = true;
            _nextMoodChange = Random.Range(5f, 9f);
            _moodTimer = 0f;
            SetMood(Mood.Normal, true);
        }

        public void Stop()
        {
            Enabled = false;
            SetMood(Mood.Normal, true);
        }

        /// <summary>Se llama en cada tiro: puede provocar un cambio brusco.</summary>
        public void OnShot()
        {
            if (!Enabled)
            {
                return;
            }

            float chance = 0.18f * _config.level;

            // A partir del nivel 3 el apagón es parte de la jugada.
            if (_config.level >= 3 && Random.value < chance)
            {
                SetMood(PickMood(), false);
                _nextMoodChange = Random.Range(3.5f, 6.5f);
                _moodTimer = 0f;
            }
        }

        private Mood PickMood()
        {
            int level = _config.level;

            if (level <= 1)
            {
                return Random.value < 0.7f ? Mood.Normal : Mood.Apagon;
            }
            if (level == 2)
            {
                float r = Random.value;
                if (r < 0.55f) return Mood.Normal;
                if (r < 0.8f) return Mood.Apagon;
                return Mood.Intermitente;
            }
            if (level == 3)
            {
                float r = Random.value;
                if (r < 0.35f) return Mood.Normal;
                if (r < 0.62f) return Mood.Apagon;
                if (r < 0.85f) return Mood.Rojo;
                return Mood.Intermitente;
            }

            float q = Random.value;
            if (q < 0.22f) return Mood.Normal;
            if (q < 0.46f) return Mood.Apagon;
            if (q < 0.68f) return Mood.Rojo;
            if (q < 0.86f) return Mood.Intermitente;
            return Mood.Caos;
        }

        public void SetMood(Mood mood, bool immediate)
        {
            CurrentMood = mood;
            _strobeOn = true;

            switch (mood)
            {
                case Mood.Apagon:
                    _wantedIntensity = 0.03f;
                    _wantedColor = new Color(0.12f, 0.14f, 0.25f);
                    _wantedAmbient = 0.02f;
                    if (court != null)
                    {
                        court.SetStandsVisible(false);
                        court.SetTheme(2);
                    }
                    break;

                case Mood.Rojo:
                    _wantedIntensity = 0.55f;
                    _wantedColor = new Color(1f, 0.13f, 0.10f);
                    _wantedAmbient = 0.10f;
                    if (court != null)
                    {
                        court.SetStandsVisible(true);
                        court.SetTheme(3);
                    }
                    break;

                case Mood.Intermitente:
                    _wantedIntensity = 0.95f;
                    _wantedColor = new Color(1f, 1f, 1f);
                    _wantedAmbient = 0.22f;
                    if (court != null)
                    {
                        court.SetStandsVisible(true);
                        court.SetTheme(1);
                    }
                    break;

                case Mood.Caos:
                    _wantedIntensity = 0.6f;
                    _wantedColor = new Color(Random.value, Random.value, Random.value) * 0.5f + new Color(0.5f, 0.5f, 0.5f);
                    _wantedAmbient = 0.06f;
                    if (court != null)
                    {
                        court.SetStandsVisible(Random.value > 0.4f);
                        court.SetTheme(Random.Range(0, 4));
                    }
                    break;

                default:
                    _wantedIntensity = 1.1f;
                    _wantedColor = new Color(1f, 0.98f, 0.92f);
                    _wantedAmbient = 0.36f;
                    if (court != null)
                    {
                        court.SetStandsVisible(true);
                        court.SetTheme(_config.level >= 2 ? 1 : 0);
                    }
                    break;
            }

            if (halloween)
            {
                ApplyHalloweenTint(mood);
            }

            _blend = immediate ? 1f : 0f;
            _targetIntensity = _wantedIntensity;
        }

        /// <summary>
        /// Ajusta el ambiente normal a una paleta de Halloween: naranja de vela,
        /// verde fantasmal, rojo sangre y niebla morada.
        /// </summary>
        private void ApplyHalloweenTint(Mood mood)
        {
            switch (mood)
            {
                case Mood.Apagon:
                    _wantedIntensity = 0.05f;
                    _wantedColor = new Color(0.34f, 0.12f, 0.46f);
                    _wantedAmbient = 0.03f;
                    break;

                case Mood.Rojo:
                    _wantedColor = new Color(0.85f, 0.06f, 0.06f);
                    _wantedAmbient = 0.08f;
                    break;

                case Mood.Intermitente:
                    _wantedColor = new Color(0.55f, 1f, 0.62f);
                    _wantedAmbient = 0.18f;
                    break;

                case Mood.Caos:
                    _wantedColor = new Color(
                        Random.Range(0.4f, 1f),
                        Random.Range(0.1f, 0.5f),
                        Random.Range(0.3f, 0.8f));
                    break;

                default:
                    _wantedIntensity = 0.92f;
                    _wantedColor = new Color(1f, 0.64f, 0.30f);
                    _wantedAmbient = 0.20f;
                    break;
            }

            if (court != null)
            {
                court.SetTheme(CourtBuilder.HalloweenTheme);
            }
        }

        private void Update()
        {
            if (_config.level == 0)
            {
                return;
            }

            _moodTimer += Time.deltaTime;

            if (Enabled && _moodTimer > _nextMoodChange)
            {
                _moodTimer = 0f;
                _nextMoodChange = Random.Range(4.5f, 9f);
                SetMood(PickMood(), false);
            }

            float strobeFactor = 1f;

            if (CurrentMood == Mood.Intermitente || CurrentMood == Mood.Caos)
            {
                _strobeTimer += Time.deltaTime;
                float interval = CurrentMood == Mood.Caos ? Random.Range(0.08f, 0.22f) : 0.13f;
                if (_strobeTimer > interval)
                {
                    _strobeTimer = 0f;
                    _strobeOn = !_strobeOn;
                }
                strobeFactor = _strobeOn ? 1f : 0.05f;
            }

            _blend = Mathf.Min(1f, _blend + Time.deltaTime * 4.5f);

            _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, Time.deltaTime * 6f);
            _currentColor = Color.Lerp(_currentColor, _wantedColor, Time.deltaTime * 5f);
            _currentAmbient = Mathf.Lerp(_currentAmbient, _wantedAmbient, Time.deltaTime * 5f);

            float applied = _currentIntensity * strobeFactor;
            Color appliedColor = _currentColor;

            if (keyLight != null)
            {
                keyLight.intensity = applied * 0.85f;
                keyLight.color = appliedColor;
            }

            if (court != null && court.Lamparas != null)
            {
                Light[] lamps = court.Lamparas;
                for (int i = 0; i < lamps.Length; i++)
                {
                    if (lamps[i] == null)
                    {
                        continue;
                    }

                    float flicker = CurrentMood == Mood.Caos
                        ? Random.Range(0.4f, 1.2f)
                        : 1f;

                    lamps[i].intensity = applied * (i == 0 ? 1.15f : 0.75f) * flicker;
                    lamps[i].color = appliedColor;
                }
            }

            if (hoopLight != null)
            {
                hoopLight.intensity = 0.30f + 0.72f * applied;
                hoopLight.color = Color.Lerp(new Color(1f, 0.96f, 0.88f), appliedColor, 0.35f);
            }

            if (decor != null && decor.MoonLight != null)
            {
                float moonBase = halloween ? 0.3f : 0.22f;
                decor.MoonLight.intensity = moonBase * (0.25f + applied * 0.75f) * (halloween ? 1f : 0f);
            }

            RenderSettings.ambientIntensity = 0.25f + _currentAmbient * 2f;
            RenderSettings.ambientLight = new Color(
                0.45f * appliedColor.r + 0.05f,
                0.47f * appliedColor.g + 0.05f,
                0.5f * appliedColor.b + 0.06f);

            // Halloween: la niebla es parte del ambiente, no solo de los apagones.
            if (halloween)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.16f, 0.05f, 0.22f);
                RenderSettings.fogDensity = CurrentMood == Mood.Apagon ? 0.06f : 0.028f;
            }
            else
            {
                RenderSettings.fog = _currentAmbient < 0.15f;
                RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.04f);
                RenderSettings.fogDensity = 0.035f;
            }
        }
    }
}
