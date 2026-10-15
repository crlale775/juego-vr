using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Resultado de la calibración de la mano del jugador.
    /// Se usa para personalizar el agarre y la fuerza del balón.
    /// </summary>
    [System.Serializable]
    public class HandProfile
    {
        public bool calibrated;
        public float sizeFactor = 1f;   // 0.8 = mano pequeña, 1.25 = mano grande (escala del balón)
        public float grip = 0.5f;       // 0 = puño cerrado, 1 = mano muy abierta (agarre / bote)
        public float power = 1f;        // 0.7 .. 1.4 (fuerza -> masa y respuesta del balón)
        public float stability = 0.5f;  // 0..1 calidad de la detección (precisión del tiro)
        public float confidence = 0f;   // 0..1 confianza de la detección
        public string quality = "sin calibrar";
        public string mode = "ninguno";

        public static HandProfile Default()
        {
            return new HandProfile
            {
                calibrated = false,
                sizeFactor = 1f,
                grip = 0.5f,
                power = 1f,
                stability = 0.5f,
                confidence = 0f,
                quality = "sin calibrar",
                mode = "por defecto"
            };
        }
    }

    /// <summary>Guarda y recupera el perfil de calibración entre partidas.</summary>
    public static class CalibrationStore
    {
        /// <summary>
        /// Perfil neutro: el que se usa cuando no hay escaneo de la mano (ya no se hace
        /// ninguno al empezar). La mano la mide MediaPipe durante el juego.
        /// </summary>
        public static HandProfile Neutro(string modo)
        {
            return new HandProfile
            {
                calibrated = true,
                sizeFactor = 1f,
                grip = 0.55f,
                power = 1f,
                stability = 0.7f,
                confidence = 0f,
                quality = "mano detectada con MediaPipe (sin escaneo)",
                mode = modo
            };
        }

        private const string Key = "VRBB_HandProfile_v1";

        private static HandProfile _current;

        public static HandProfile Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Load();
                }
                return _current;
            }
        }

        public static HandProfile Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return HandProfile.Default();
            }

            try
            {
                HandProfile p = JsonUtility.FromJson<HandProfile>(PlayerPrefs.GetString(Key));
                return p ?? HandProfile.Default();
            }
            catch
            {
                return HandProfile.Default();
            }
        }

        public static void Save(HandProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            _current = profile;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            _current = HandProfile.Default();
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        public static string Describe(HandProfile p)
        {
            if (p == null)
            {
                return "sin perfil";
            }

            return string.Format(
                "calidad: {0} | escala mano: x{1:0.00} | agarre: {2:0.00} | fuerza: x{3:0.00} | precision: {4:0.00}",
                p.quality, p.sizeFactor, p.grip, p.power, p.stability);
        }
    }
}
