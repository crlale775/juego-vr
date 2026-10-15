using UnityEngine;
using UnityEngine.EventSystems;

namespace VRBasketball
{
    /// <summary>
    /// Arranque del juego: crea todos los objetos de la escena por código y conecta
    /// los sistemas entre sí. La escena sólo necesita este componente.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;

            // --- Sonido (procedural, sin archivos) ---
            GameObject audioRoot = NewRoot("Audio");
            audioRoot.AddComponent<AudioSynth>();

            // --- Estación del jugador (no gira) ---
            GameObject station = NewRoot("EstacionJugador");
            station.transform.position = new Vector3(0f, 1.6f, 0f);

            // --- Cabeza / ojos (gira con el sensor) ---
            GameObject player = new GameObject("Jugador");
            player.transform.SetParent(station.transform, false);
            HeadTracker tracker = player.AddComponent<HeadTracker>();
            StereoRig rig = player.AddComponent<StereoRig>();

            GameObject hand = new GameObject("Mano");
            hand.transform.SetParent(player.transform, false);
            hand.transform.localPosition = new Vector3(0.30f, -0.42f, 0.42f);

            // --- Escenario ---
            GameObject courtRoot = NewRoot("Cancha");
            CourtBuilder court = courtRoot.AddComponent<CourtBuilder>();
            court.Build();

            GameObject keyLightRoot = NewRoot("LuzPrincipal");
            Light keyLight = keyLightRoot.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 0.8f;
            keyLight.color = new Color(1f, 0.97f, 0.9f);
            keyLight.shadows = LightShadows.None;
            keyLightRoot.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            // --- Ambiente de Halloween (decoración apagada hasta que se active) ---
            GameObject halloweenRoot = NewRoot("AmbienteHalloween");
            HalloweenDecor halloween = halloweenRoot.AddComponent<HalloweenDecor>();
            halloween.Build();

            GameObject lightingRoot = NewRoot("DirectorIluminacion");
            LightingDirector lighting = lightingRoot.AddComponent<LightingDirector>();
            lighting.court = court;
            lighting.keyLight = keyLight;
            lighting.decor = halloween;

            // --- Aro y balón ---
            GameObject hoopRoot = NewRoot("Aro");
            HoopController hoop = hoopRoot.AddComponent<HoopController>();
            hoop.Build();

            // Foco sobre el aro: mantiene el objetivo visible en los apagones.
            GameObject hoopLightGo = new GameObject("LuzAro");
            hoopLightGo.transform.SetParent(hoop.Ring, false);
            hoopLightGo.transform.localPosition = new Vector3(0f, 1.35f, 0.35f);
            hoopLightGo.transform.localRotation = Quaternion.Euler(74f, 180f, 0f);

            Light hoopLight = hoopLightGo.AddComponent<Light>();
            hoopLight.type = LightType.Spot;
            hoopLight.spotAngle = 62f;
            hoopLight.range = 16f;
            hoopLight.intensity = 0.9f;
            hoopLight.color = new Color(1f, 0.96f, 0.88f);
            hoopLight.shadows = LightShadows.None;

            lighting.hoopLight = hoopLight;

            GameObject ballRoot = NewRoot("Balon");
            BallController ball = ballRoot.AddComponent<BallController>();
            ball.Build(0.115f, 0.6f, 0.62f);

            // --- Marcador físico en la pared del fondo ---
            GameObject scoreboardRoot = NewRoot("Marcador");
            scoreboardRoot.transform.position = new Vector3(0f, 7.1f, 2.3f);
            Scoreboard scoreboard = scoreboardRoot.AddComponent<Scoreboard>();
            scoreboard.Build();

            // --- Dificultad ---
            GameObject difficultyRoot = NewRoot("Dificultad");
            DifficultyManager difficulty = difficultyRoot.AddComponent<DifficultyManager>();

            // --- Seguimiento de la mano con la cámara trasera ---
            GameObject handRoot = NewRoot("RastreadorMano");
            HandTracker handTracker = handRoot.AddComponent<HandTracker>();

            // --- Sistema de tiro ---
            GameObject throwRoot = NewRoot("SistemaTiro");
            ThrowController thrower = throwRoot.AddComponent<ThrowController>();
            thrower.head = player.transform;
            thrower.handAnchor = hand.transform;
            thrower.hoop = hoop;
            thrower.ball = ball;
            thrower.handTracker = handTracker;
            thrower.enabled = false;

            // --- Interfaz ---
            HUD hud = station.AddComponent<HUD>();
            hud.head = player.transform;
            hud.station = station.transform;
            hud.rig = rig;   // para poder tocar los botones del menú con el dedo
            hud.Build();

            // --- Manos virtuales: dibujan la mano con los 21 puntos de MediaPipe ---
            GameObject manosRoot = NewRoot("ManosVirtuales");
            ManosVirtuales manos = manosRoot.AddComponent<ManosVirtuales>();
            manos.tracker = handTracker;
            manos.cabeza = player.transform;
            manos.mano = hand.transform;

            EnsureEventSystem();

            // --- Gestor principal ---
            GameManager manager = station.AddComponent<GameManager>();
            manager.hud = hud;
            manager.rig = rig;
            manager.tracker = tracker;
            manager.hoop = hoop;
            manager.ball = ball;
            manager.thrower = thrower;
            manager.difficulty = difficulty;
            manager.lighting = lighting;
            manager.manos = manos;
            manager.playerHead = player.transform;
            manager.hand = hand.transform;
            manager.station = station.transform;
            manager.halloween = halloween;
            manager.handTracker = handTracker;
            manager.scoreboard = scoreboard;

            // --- Conexiones de eventos ---
            hud.OnAction += manager.HandleAction;
            thrower.OnThrow += manager.NotifyThrow;

            rig.SetRenderScale(0.8f);

            // Modo diagnóstico: se activa con ?diag=1 en la URL.
            GameObject diagRoot = NewRoot("Diagnostico");
            Diagnostics diagnostics = diagRoot.AddComponent<Diagnostics>();
            string url = Application.absoluteURL;
            diagnostics.active = !string.IsNullOrEmpty(url) && url.ToLowerInvariant().Contains("diag=1");
        }

        private static GameObject NewRoot(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.position = Vector3.zero;
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
