using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VRBasketball
{
    /// <summary>
    /// Modo diagnóstico: se activa con ?diag=1 en la URL (versión WebGL) y escribe en la
    /// consola el estado del juego, las cámaras y la posición real de cada elemento de
    /// interfaz. Sirve para encontrar problemas sin tener el juego delante.
    /// </summary>
    public class Diagnostics : MonoBehaviour
    {
        public bool active;
        private float _timer;
        private int _ticks;

        private void Start()
        {
            if (!active)
            {
                enabled = false;
                return;
            }

            Log("inicio", true);
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < 5f)
            {
                return;
            }

            _timer = 0f;
            _ticks++;
            Log("t" + _ticks * 5 + "s", true);
        }

        private void Log(string cuando, bool detallado)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[DIAG ").Append(cuando).Append("] ");

            GameManager gm = GameManager.I;
            sb.Append("estado=").Append(gm != null ? gm.State.ToString() : "sin GameManager");
            sb.Append(" pantalla=").Append(Screen.width).Append("x").Append(Screen.height);
            sb.Append(" pista=").Append(Application.platform).Append(" url=").Append(Application.absoluteURL);
            Debug.Log(sb.ToString());

            if (gm == null)
            {
                return;
            }

            Debug.Log(string.Format(
                "[DIAG] puntos={0} tiempo={1:0} aciertos={2} tiros={3} demo={4} rendimiento={5}",
                gm.currentScore, gm.currentTime, gm.madeCount, gm.shotCount, gm.autoDemo,
                gm.rig != null ? gm.rig.renderScale.ToString("0.00") + (gm.rig.stereo ? " estereo" : " mono") : "-"));

            UnityEngine.EventSystems.EventSystem es =
                Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();

            Debug.Log(string.Format(
                "[DIAG] entrada: raton={0} boton={1} toques={2} ratonPresente={3} eventSystem={4} modulo={5} seleccionado={6}",
                Input.mousePosition.ToString("0"),
                Input.GetMouseButton(0),
                Input.touchCount,
                Input.mousePresent,
                es != null,
                es != null ? es.currentInputModule != null : false,
                es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "-"));

            if (gm.rig != null)
            {
                Debug.Log(string.Format(
                    "[DIAG] camaras: ojos={0} presentacion={1} textura={2} sombraactivada={3}",
                    gm.rig.LeftEye != null ? gm.rig.LeftEye.enabled.ToString() : "-",
                    gm.rig.Present != null ? gm.rig.Present.enabled.ToString() : "-",
                    gm.rig.Target != null ? (gm.rig.Target.width + "x" + gm.rig.Target.height) : "sin",
                    gm.rig.gameObject.activeInHierarchy));
            }

            if (gm.ball != null)
            {
                Rigidbody rb = gm.ball.GetComponent<Rigidbody>();
                Debug.Log(string.Format(
                    "[DIAG] balon pos={0} volando={1} masa={2:0.00} r={3:0.000} activo={4}",
                    gm.ball.transform.position.ToString("0.00"),
                    gm.ball.IsFlying,
                    rb != null ? rb.mass : -1f,
                    gm.ball.Radius,
                    gm.ball.gameObject.activeInHierarchy));
            }

            if (gm.hoop != null)
            {
                Debug.Log(string.Format(
                    "[DIAG] aro centro={0} radio={1:0.000} nivel={2} activo={3}",
                    gm.hoop.RingCenter.ToString("0.00"),
                    gm.hoop.ringRadius,
                    gm.difficulty != null ? gm.difficulty.Level : -1,
                    gm.hoop.gameObject.activeInHierarchy));
            }

            if (gm.halloween != null)
            {
                Transform root = gm.halloween.transform.Find("Halloween");
                int objetos = 0;
                int calabazas = 0;

                if (root != null)
                {
                    objetos = root.GetComponentsInChildren<Transform>(true).Length;
                    Transform[] all = root.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].name == "Calabaza")
                        {
                            calabazas++;
                        }
                    }
                }

                Debug.Log(string.Format(
                    "[DIAG] halloween activo={0} forzado={1} calabazas={2} objetos={3} luces={4}",
                    gm.halloween.Active,
                    gm.HalloweenOn,
                    calabazas,
                    objetos,
                    gm.halloween.PumpkinLightCount));
            }

            if (gm.handTracker != null)
            {
                Debug.Log(string.Format(
                    "[DIAG] mano camara={0} error={1} detectada={2} pos=({3:0.00},{4:0.00}) cob={5:0.0}% vel=({6:0.00},{7:0.00}) dedos={8} gestos={9} tirosMano={10} tirosBoton={11} apuntando={12} apuntadoY={13:0.00} pico={14:0.00} muestras={15}",
                    gm.handTracker.Running,
                    gm.handTracker.CameraError,
                    gm.handTracker.Detected,
                    gm.handTracker.Position.x,
                    gm.handTracker.Position.y,
                    gm.handTracker.Coverage * 100f,
                    gm.handTracker.Velocity.x,
                    gm.handTracker.Velocity.y,
                    gm.handTracker.Fingers,
                    gm.thrower != null ? gm.thrower.handGestures : -1,
                    gm.thrower != null ? gm.thrower.handThrows : -1,
                    gm.thrower != null ? gm.thrower.tapThrows : -1,
                    gm.thrower != null && gm.thrower.HandActive,
                    gm.thrower != null ? gm.thrower.lastAim.y : 0f,
                    gm.handTracker.PeakUpVelocity,
                    gm.handTracker.Samples));

                gm.handTracker.ResetPeak();

                // Qué fuente está dando la mano y cómo va MediaPipe en este móvil.
                Debug.Log(string.Format(
                    "[DIAG] manofuente fuente={0} puntos={1} apertura={2:0.00} pinza={3:0.00} tamano={4:0.000} conf={5:0.00} dedos={6}",
                    gm.handTracker.Fuente,
                    gm.handTracker.TienePuntos,
                    gm.handTracker.Apertura,
                    gm.handTracker.Pinza,
                    gm.handTracker.Tamano,
                    gm.handTracker.Confidence,
                    gm.handTracker.Dedos));

                Debug.Log(string.Format(
                    "[DIAG] mediapipe giro={0} cuadros={1} analizados={2} detecciones={3} ms={4} error='{5}'",
                    gm.handTracker.MediaPipeGrados,
                    gm.handTracker.MediaPipeCuadrosCamara,
                    gm.handTracker.MediaPipeAnalizados,
                    gm.handTracker.MediaPipeDetecciones,
                    gm.handTracker.MediaPipeMs,
                    gm.handTracker.MediaPipeError));

                Debug.Log(string.Format(
                    "[DIAG] gestos modo={0} ultimo={1} subir={2} empujar={3} puno={4} pellizco={5} vistos={6}",
                    gm.thrower != null ? gm.thrower.modoGesto : "-",
                    gm.thrower != null ? gm.thrower.ultimoGesto : "-",
                    gm.thrower != null ? gm.thrower.gestosSubir : -1,
                    gm.thrower != null ? gm.thrower.gestosEmpujar : -1,
                    gm.thrower != null ? gm.thrower.gestosPuno : -1,
                    gm.thrower != null ? gm.thrower.gestosPellizco : -1,
                    gm.thrower != null ? gm.thrower.handGestures : -1));

                if (gm.manos != null)
                {
                    Debug.Log(string.Format(
                        "[DIAG] manovirtual visible={0} posicion={1} giro={2:0}",
                        gm.manos.Visible,
                        gm.manos.PosicionMundo.ToString("0.00"),
                        gm.manos.GiroMuneca));
                }
            }

            if (gm.hud != null)
            {
                Debug.Log(string.Format(
                    "[DIAG] menu mano={0} cursor={1} boton={2} progreso={3:0.00} posMano=({4:0.00},{5:0.00})",
                    gm.hud.HandDriven,
                    gm.hud.HandCursorVisible,
                    gm.hud.HoveredId,
                    gm.hud.HoverProgress,
                    gm.hud.HandPosition.x,
                    gm.hud.HandPosition.y));
            }

            if (gm.hud != null)
            {
                Debug.Log("[DIAG] botones " + gm.hud.MenuButtonsDump());
            }

            ReportMenuRotation();

            if (detallado)
            {
                DumpCanvases();
            }
        }

        /// <summary>
        /// Informa cómo está girado el panel del menú. Con 180° el texto sale espejado
        /// (fue el error real que se vio en el visor), así que debe estar en 0°.
        /// </summary>
        private void ReportMenuRotation()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                bool panel = canvases[i].name == "Menu" || canvases[i].name == "FinPartida";
                bool marcador = canvases[i].name.StartsWith("Marcador");

                if (!panel && !marcador)
                {
                    continue;
                }

                Vector3 euler = canvases[i].transform.rotation.eulerAngles;
                float y = Mathf.Repeat(euler.y, 360f);
                bool espejado = Mathf.Abs(Mathf.DeltaAngle(y, 180f)) < 45f;

                Debug.Log(string.Format(
                    "[DIAG] panel '{0}' giroY={1:0} espejado={2} posicion={3}",
                    canvases[i].name,
                    y,
                    espejado,
                    canvases[i].transform.position.ToString("0.00")));

                if (panel)
                {
                    ReportPanelRect(canvases[i]);
                }
            }
        }

        /// <summary>
        /// Rectángulo del panel en píxeles de pantalla. Sirve para recortar justo el
        /// panel en una captura y comprobar si su texto está espejado o no.
        /// </summary>
        private void ReportPanelRect(Canvas canvas)
        {
            GameManager gm = GameManager.I;
            if (gm == null || gm.rig == null || gm.rig.LeftEye == null)
            {
                return;
            }

            RectTransform rect = canvas.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            Camera cam = gm.rig.LeftEye;
            Vector2 size = rect.rect.size;
            Vector3 a = cam.WorldToScreenPoint(rect.TransformPoint(new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0f)));
            Vector3 b = cam.WorldToScreenPoint(rect.TransformPoint(new Vector3(size.x * 0.5f, size.y * 0.5f, 0f)));

            if (a.z <= 0f || b.z <= 0f)
            {
                return;
            }

            Debug.Log(string.Format(
                "[DIAG] panelpix '{0}' x={1:0}..{2:0} y={3:0}..{4:0} pantalla={5}x{6}",
                canvas.name,
                Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x),
                Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y),
                Screen.width, Screen.height));
        }

        private void DumpCanvases()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Debug.Log("[DIAG] lienzos encontrados=" + canvases.Length);

            for (int c = 0; c < canvases.Length; c++)
            {
                Canvas canvas = canvases[c];
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();

                Debug.Log(string.Format(
                    "[DIAG] lienzo '{0}' modo={1} activo={2} escala={3:0.000} tamano={4} escalaMundo={5}",
                    canvas.name,
                    canvas.renderMode,
                    canvas.gameObject.activeInHierarchy,
                    canvas.scaleFactor,
                    canvasRect != null ? canvasRect.rect.size.ToString("0") : "-",
                    canvasRect != null ? canvasRect.lossyScale.ToString("0.0000") : "-"));

                Text[] texts = canvas.GetComponentsInChildren<Text>(true);
                Image[] images = canvas.GetComponentsInChildren<Image>(true);

                for (int i = 0; i < texts.Length && i < 8; i++)
                {
                    Text t = texts[i];
                    Vector2 sp = ScreenPoint(t.rectTransform);
                    Debug.Log(string.Format(
                        "[DIAG]    texto '{0}' activo={1} contenido='{2}' pantalla={3} tam={4} fuente={5} color={6}",
                        t.name,
                        t.gameObject.activeInHierarchy,
                        t.text,
                        sp.ToString("0"),
                        t.rectTransform.rect.size.ToString("0"),
                        t.font != null ? t.font.name : "SIN FUENTE",
                        ColorUtility.ToHtmlStringRGB(t.color)));
                }

                for (int i = 0; i < images.Length && i < 8; i++)
                {
                    Image img = images[i];
                    Vector2 sp = ScreenPoint(img.rectTransform);
                    Debug.Log(string.Format(
                        "[DIAG]    imagen '{0}' activo={1} color={2} pantalla={3} tam={4} sprite={5}",
                        img.name,
                        img.gameObject.activeInHierarchy,
                        ColorUtility.ToHtmlStringRGB(img.color),
                        sp.ToString("0"),
                        img.rectTransform.rect.size.ToString("0"),
                        img.sprite != null ? "si" : "no"));
                }
            }
        }

        private Vector2 ScreenPoint(RectTransform rt)
        {
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = canvas.worldCamera;
            }

            return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
        }
    }
}
