using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Todos los sonidos se generan por síntesis en tiempo de ejecución:
    /// el proyecto no necesita archivos de audio.
    /// </summary>
    public class AudioSynth : MonoBehaviour
    {
        public static AudioSynth I { get; private set; }

        private const int SampleRate = 44100;

        private AudioSource _source;

        private AudioClip _bounce;
        private AudioClip _rim;
        private AudioClip _swish;
        private AudioClip _fail;
        private AudioClip _click;
        private AudioClip _levelUp;
        private AudioClip _whoosh;
        private AudioClip _cheer;
        private AudioClip _thunder;
        private AudioClip _howl;

        private void Awake()
        {
            I = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.85f;

            _bounce = MakeBounce();
            _rim = MakeRim();
            _swish = MakeSwish();
            _fail = MakeFail();
            _click = MakeClick();
            _levelUp = MakeLevelUp();
            _whoosh = MakeWhoosh();
            _cheer = MakeCheer();
            _thunder = MakeThunder();
            _howl = MakeHowl();
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _source == null)
            {
                return;
            }

            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume);
        }

        public void Bounce(float intensity)
        {
            Play(_bounce, Mathf.Clamp01(0.25f + intensity), Random.Range(0.9f, 1.1f));
        }

        public void Rim()
        {
            Play(_rim, 0.7f, Random.Range(0.95f, 1.05f));
        }

        public void Swish()
        {
            Play(_swish, 0.9f, 1f);
        }

        public void Fail()
        {
            Play(_fail, 0.55f, 1f);
        }

        public void Click()
        {
            Play(_click, 0.6f, 1f);
        }

        public void LevelUp()
        {
            Play(_levelUp, 0.9f, 1f);
        }

        public void Throw()
        {
            Play(_whoosh, 0.5f, Random.Range(0.95f, 1.08f));
        }

        public void Cheer()
        {
            Play(_cheer, 0.55f, 1f);
        }

        /// <summary>Trueno lejano para el ambiente de Halloween.</summary>
        public void Thunder()
        {
            Play(_thunder, 0.65f, Random.Range(0.92f, 1.06f));
        }

        /// <summary>Aullido lejano para el ambiente de Halloween.</summary>
        public void Howl()
        {
            Play(_howl, 0.4f, Random.Range(0.94f, 1.08f));
        }

        // ---------------- Síntesis ----------------

        private static AudioClip FromSamples(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeBounce()
        {
            int n = (int)(SampleRate * 0.28f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 22f);
                float body = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.8f
                             + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.25f;
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 120f) * 0.35f;
                d[i] = (body * env + noise) * 0.9f;
            }
            return FromSamples("bote", d);
        }

        private static AudioClip MakeRim()
        {
            int n = (int)(SampleRate * 0.55f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 9f);
                float metal = Mathf.Sin(2f * Mathf.PI * 1780f * t) * 0.5f
                              + Mathf.Sin(2f * Mathf.PI * 2410f * t) * 0.3f
                              + Mathf.Sin(2f * Mathf.PI * 3190f * t) * 0.2f
                              + Mathf.Sin(2f * Mathf.PI * 4020f * t) * 0.12f;
                float strike = (Random.value * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.3f;
                d[i] = (metal * env + strike) * 0.85f;
            }
            return FromSamples("aro", d);
        }

        private static AudioClip MakeSwish()
        {
            int n = (int)(SampleRate * 0.45f);
            float[] d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.42f) * Mathf.PI);
                float noise = Random.value * 2f - 1f;
                lp += (noise - lp) * 0.28f;
                d[i] = lp * env * 0.9f;
            }
            return FromSamples("red", d);
        }

        private static AudioClip MakeFail()
        {
            int n = (int)(SampleRate * 0.35f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float f = Mathf.Lerp(220f, 110f, t / 0.35f);
                float env = Mathf.Exp(-t * 7f);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.8f;
            }
            return FromSamples("fallo", d);
        }

        private static AudioClip MakeClick()
        {
            int n = (int)(SampleRate * 0.09f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 45f);
                d[i] = Mathf.Sin(2f * Mathf.PI * 1350f * t) * env * 0.7f;
            }
            return FromSamples("clic", d);
        }

        private static AudioClip MakeLevelUp()
        {
            int n = (int)(SampleRate * 0.7f);
            float[] d = new float[n];
            float[] notes = { 523f, 659f, 784f, 1046f };
            int per = n / notes.Length;
            for (int i = 0; i < n; i++)
            {
                int k = Mathf.Min(notes.Length - 1, i / per);
                float t = (float)(i - k * per) / SampleRate;
                float env = Mathf.Exp(-t * 6f);
                float tone = Mathf.Sin(2f * Mathf.PI * notes[k] * t) * 0.7f
                             + Mathf.Sin(2f * Mathf.PI * notes[k] * 2f * t) * 0.2f;
                d[i] = tone * env;
            }
            return FromSamples("nivel", d);
        }

        private static AudioClip MakeWhoosh()
        {
            int n = (int)(SampleRate * 0.3f);
            float[] d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI);
                float noise = Random.value * 2f - 1f;
                lp += (noise - lp) * 0.14f;
                d[i] = lp * env * 0.8f + Mathf.Sin(2f * Mathf.PI * 160f * t) * env * 0.15f;
            }
            return FromSamples("latigazo", d);
        }

        private static AudioClip MakeThunder()
        {
            int n = (int)(SampleRate * 2.1f);
            float[] d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                // Golpe inicial y cola larga, como un trueno que se aleja.
                float env = Mathf.Exp(-t * 1.1f) * (1f - Mathf.Exp(-t * 55f));
                lp += ((Random.value * 2f - 1f) - lp) * 0.035f;
                float rumble = Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.5f
                               + Mathf.Sin(2f * Mathf.PI * 63f * t) * 0.25f;
                d[i] = (lp * 1.6f + rumble) * env * 0.85f;
            }
            return FromSamples("trueno", d);
        }

        private static AudioClip MakeHowl()
        {
            int n = (int)(SampleRate * 1.5f);
            float[] d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float u = t / 1.5f;
                float env = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

                // Sube, se mantiene y cae, con vibrato.
                float f = Mathf.Lerp(320f, 520f, Mathf.Clamp01(u * 2f));
                f *= 1f - 0.35f * Mathf.Clamp01((u - 0.6f) * 2.5f);
                float vibrato = 1f + Mathf.Sin(2f * Mathf.PI * 6.5f * t) * 0.03f;

                lp += ((Random.value * 2f - 1f) - lp) * 0.06f;
                float tone = Mathf.Sin(2f * Mathf.PI * f * vibrato * t) * 0.7f
                             + Mathf.Sin(2f * Mathf.PI * f * 2f * vibrato * t) * 0.18f;

                d[i] = (tone + lp * 0.35f) * env * 0.6f;
            }
            return FromSamples("aullido", d);
        }

        private static AudioClip MakeCheer()
        {
            int n = (int)(SampleRate * 0.9f);
            float[] d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI);
                lp += ((Random.value * 2f - 1f) - lp) * 0.09f;
                float swell = Mathf.Sin(2f * Mathf.PI * 2.2f * t) * 0.3f + 0.7f;
                d[i] = lp * env * swell;
            }
            return FromSamples("publico", d);
        }
    }
}
