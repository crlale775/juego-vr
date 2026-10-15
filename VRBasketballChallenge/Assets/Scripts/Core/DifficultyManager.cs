using UnityEngine;

namespace VRBasketball
{
    /// <summary>Parámetros de un nivel de dificultad.</summary>
    public struct LevelConfig
    {
        public int level;
        public string nombre;
        public float hoopSpeed;        // velocidad de oscilación del aro
        public float hoopAmplitude;    // metros de recorrido horizontal
        public float verticalAmplitude;// metros de recorrido vertical
        public float hoopDistance;     // distancia desde el jugador
        public float ringRadius;       // radio del aro (se reduce con el nivel)
        public bool randomTeleport;    // saltos impredecibles
        public string luz;             // descripción del comportamiento de la luz
    }

    /// <summary>
    /// Cinco niveles. El aro se mueve más rápido, se aleja, se hace más pequeño
    /// y la iluminación se vuelve más hostil.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        public const int MaxLevel = 5;

        [Tooltip("Encestes necesarios para subir de nivel.")]
        public int basketsPerLevel = 3;

        public int Level { get; private set; }

        private void Awake()
        {
            Level = 1;
        }

        public void Reset()
        {
            Level = 1;
        }

        /// <summary>Avanza de nivel si corresponde. Devuelve true si subió.</summary>
        public bool RegisterBasket(int totalBaskets)
        {
            int target = Mathf.Clamp(totalBaskets / Mathf.Max(1, basketsPerLevel) + 1, 1, MaxLevel);
            if (target > Level)
            {
                Level = target;
                return true;
            }
            return false;
        }

        public LevelConfig Current
        {
            get { return Get(Level); }
        }

        public static LevelConfig Get(int level)
        {
            switch (Mathf.Clamp(level, 1, MaxLevel))
            {
                case 2:
                    return new LevelConfig
                    {
                        level = 2,
                        nombre = "Nivel 2 - Aro en movimiento",
                        hoopSpeed = 0.60f,
                        hoopAmplitude = 1.20f,
                        verticalAmplitude = 0.0f,
                        hoopDistance = 5.6f,
                        ringRadius = 0.230f,
                        randomTeleport = false,
                        luz = "Iluminación normal"
                    };
                case 3:
                    return new LevelConfig
                    {
                        level = 3,
                        nombre = "Nivel 3 - Aro rápido y apagones",
                        hoopSpeed = 0.95f,
                        hoopAmplitude = 1.55f,
                        verticalAmplitude = 0.30f,
                        hoopDistance = 6.0f,
                        ringRadius = 0.225f,
                        randomTeleport = false,
                        luz = "Apagones repentinos"
                    };
                case 4:
                    return new LevelConfig
                    {
                        level = 4,
                        nombre = "Nivel 4 - Horizontal y vertical",
                        hoopSpeed = 1.30f,
                        hoopAmplitude = 1.90f,
                        verticalAmplitude = 0.80f,
                        hoopDistance = 6.4f,
                        ringRadius = 0.220f,
                        randomTeleport = false,
                        luz = "Luz intermitente y escenario cambiante"
                    };
                case 5:
                    return new LevelConfig
                    {
                        level = 5,
                        nombre = "Nivel 5 - Aro impredecible",
                        hoopSpeed = 1.75f,
                        hoopAmplitude = 2.30f,
                        verticalAmplitude = 1.15f,
                        hoopDistance = 6.8f,
                        ringRadius = 0.215f,
                        randomTeleport = true,
                        luz = "Caos total: oscuridad, luz roja y saltos"
                    };
                default:
                    return new LevelConfig
                    {
                        level = 1,
                        nombre = "Nivel 1 - Aro casi fijo",
                        hoopSpeed = 0.25f,
                        hoopAmplitude = 0.55f,
                        verticalAmplitude = 0.0f,
                        hoopDistance = 5.2f,
                        ringRadius = 0.240f,
                        randomTeleport = false,
                        luz = "Iluminación normal"
                    };
            }
        }

        /// <summary>Puntos que vale un enceste en el nivel actual.</summary>
        public int PointsPerBasket(int level)
        {
            return 100 * Mathf.Clamp(level, 1, MaxLevel);
        }
    }
}
