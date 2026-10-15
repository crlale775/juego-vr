using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRBasketball.EditorTools
{
    /// <summary>
    /// Prepara el proyecto: escena principal, ajustes de jugador, shaders incluidos
    /// y configuración de compilación. Se ejecuta en modo batch.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/VRBasketball.unity";

        public static void ConfigureAll()
        {
            CreateScene();
            ConfigurePlayer();
            ConfigureShaders();
            ConfigureQuality();
            ConfigureAndroidTools();
            AssetDatabase.SaveAssets();
            Debug.Log("VR Basketball: proyecto configurado correctamente.");
        }

        /// <summary>
        /// Usa el SDK, NDK y JDK que ya esten instalados en la maquina, para no tener
        /// que descargarlos otra vez desde Unity Hub.
        /// </summary>
        public static void ConfigureAndroidTools()
        {
            string local = System.Environment.GetEnvironmentVariable("LOCALAPPDATA");
            string sdk = System.IO.Path.Combine(local ?? "", "Android", "Sdk");
            string ndk = null;
            string jdk = FindJavaHome();

            if (System.IO.Directory.Exists(sdk))
            {
                string ndkFolder = System.IO.Path.Combine(sdk, "ndk");
                if (System.IO.Directory.Exists(ndkFolder))
                {
                    string[] versions = System.IO.Directory.GetDirectories(ndkFolder);
                    System.Array.Sort(versions);

                    // Unity 6000.5 solo acepta el NDK r27c (27.2.x). Si esta instalado se usa
                    // ese; si no, se usa el mas nuevo disponible.
                    ndk = versions.Length > 0 ? versions[versions.Length - 1] : null;
                    for (int i = 0; i < versions.Length; i++)
                    {
                        string name = System.IO.Path.GetFileName(versions[i]);
                        if (name.StartsWith("27."))
                        {
                            ndk = versions[i];
                            break;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("VR Basketball: no se encontro el SDK de Android en " + sdk);
                sdk = null;
            }

            // 1) Ajustes clasicos (se leen al arrancar el editor).
            if (sdk != null) EditorPrefs.SetString("AndroidSdkRoot", sdk);
            if (ndk != null) EditorPrefs.SetString("AndroidNdkRoot", ndk);
            if (jdk != null) EditorPrefs.SetString("JdkPath", jdk);

            // 2) Ajustes actuales, para que se apliquen en esta misma sesion.
            ApplyExternalToolsProperty("sdkRootPath", sdk);
            ApplyExternalToolsProperty("ndkRootPath", ndk);
            ApplyExternalToolsProperty("jdkRootPath", jdk);

            Debug.Log(string.Format("VR Basketball: herramientas Android -> SDK={0} NDK={1} JDK={2}",
                sdk ?? "(no)", ndk ?? "(no)", jdk ?? "(no)"));
        }

        private static void ApplyExternalToolsProperty(string property, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            try
            {
                System.Type t = System.Type.GetType(
                    "UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions", false);

                if (t == null)
                {
                    return;
                }

                System.Reflection.PropertyInfo info = t.GetProperty(property,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (info != null && info.CanWrite)
                {
                    info.SetValue(null, value, null);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("VR Basketball: no se pudo fijar " + property + ": " + e.Message);
            }
        }

        private static string FindJavaHome()
        {
            string javaHome = System.Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome) && System.IO.Directory.Exists(javaHome))
            {
                return javaHome;
            }

            string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
            string microsoft = System.IO.Path.Combine(programFiles, "Microsoft");
            if (System.IO.Directory.Exists(microsoft))
            {
                string[] jdks = System.IO.Directory.GetDirectories(microsoft, "jdk-*");
                if (jdks.Length > 0)
                {
                    System.Array.Sort(jdks);
                    return jdks[jdks.Length - 1];
                }
            }

            return null;
        }

        public static void CreateScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject bootstrap = new GameObject("VRBasketballBootstrap");
            bootstrap.transform.position = Vector3.zero;
            bootstrap.AddComponent<GameBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("VR Basketball: escena creada en " + ScenePath);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Proyecto VR Basketball";
            PlayerSettings.productName = "VR Basketball Challenge";
            PlayerSettings.bundleVersion = "0.1.0-beta";

            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android, "com.proyecto.vrbasketball");
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Standalone, "com.proyecto.vrbasketball");

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            // Presentación en pantalla de Android. "Auto" es el valor recomendado por
            // Unity: deja que el motor copie la imagen sólo cuando la GPU/pantalla lo
            // necesita para corregir la orientación. Con "Never" forzado, en algunos
            // móviles la imagen acababa presentándose del revés (todo boca abajo,
            // incluidos los textos), justo el fallo que se veía en el APK.
            PlayerSettings.Android.blitType = AndroidBlitType.Auto;
            PlayerSettings.Android.useCustomKeystore = false;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // La cámara del teléfono se necesita para calibrar la mano.
            PlayerSettings.SetApiCompatibilityLevel(
                UnityEditor.Build.NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            // Android moderno necesita IL2CPP y ARM64 (los telefonos de 64 bits
            // no instalan paquetes solo de 32 bits).
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            PlayerSettings.colorSpace = ColorSpace.Gamma;

            // Sin esto Unity puede eliminar clases del motor que solo se usan en tiempo
            // de ejecución (ocurrió con MeshCollider) y el juego falla en silencio.
            PlayerSettings.stripEngineCode = false;

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            // Plantilla propia: lienzo a pantalla completa y arranque por toque.
            PlayerSettings.WebGL.template = "PROJECT:VRBasketball";
            PlayerSettings.WebGL.memorySize = 512;
            // Con "None" una excepción dentro de un Update deja de ejecutarse el resto
            // del método SIN decir nada (pasó con un string.Format mal formado), así que
            // se dejan registrar las excepciones: es la única forma de ver el problema.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.emscriptenArgs = "-s ALLOW_MEMORY_GROWTH=1";

            // Plantilla de Gradle propia (Assets/Plugins/Android/mainTemplate.gradle):
            // es lo que añade MediaPipe (com.google.mediapipe:tasks-vision) al APK,
            // y sin ella la mano con IA no existiría en el juego de Android.
            EnableCustomMainGradleTemplate();
        }

        /// <summary>
        /// Activa las casillas "Custom Main Gradle Template" y "Custom Launcher Gradle
        /// Template" de los ajustes del proyecto.
        ///
        /// En Unity 6 este ajuste ya no está en PlayerSettings.Android (se comprobó que
        /// el API desapareció), así que se escribe directamente en
        /// ProjectSettings/ProjectSettings.asset, que es exactamente lo que hace la
        /// casilla de la ventana de ajustes. Es imprescindible: sin la plantilla propia
        /// el APK no lleva MediaPipe y la mano con IA no existe en el móvil.
        /// </summary>
        private static void EnableCustomMainGradleTemplate()
        {
            // 1) Ajuste en memoria: es el que usa el compilador de Android al montar el
            //    proyecto Gradle. La propiedad está en el ensamblado de Android, que no
            //    tiene por qué estar cargado todavía, así que se carga a mano.
            CargarEnsambladoAndroid();

            System.Reflection.Assembly[] ensamblados = System.AppDomain.CurrentDomain.GetAssemblies();

            for (int a = 0; a < ensamblados.Length; a++)
            {
                System.Type[] tipos;

                try
                {
                    tipos = ensamblados[a].GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException parcial)
                {
                    // Casi todos los tipos se cargan aunque algunos fallen: se usan los
                    // que sí están en vez de descartar el ensamblado entero.
                    tipos = parcial.Types;
                }
                catch (System.Exception)
                {
                    continue;
                }

                if (tipos == null)
                {
                    continue;
                }

                for (int t = 0; t < tipos.Length; t++)
                {
                    if (tipos[t] == null)
                    {
                        continue;
                    }

                    System.Reflection.PropertyInfo propiedad;

                    try
                    {
                        propiedad = tipos[t].GetProperty(
                            "UseCustomMainGradleTemplate",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                            | System.Reflection.BindingFlags.IgnoreCase);
                    }
                    catch (System.Exception)
                    {
                        continue;
                    }

                    if (propiedad == null || !propiedad.CanWrite || propiedad.PropertyType != typeof(bool))
                    {
                        continue;
                    }

                    try
                    {
                        propiedad.SetValue(null, true, null);
                        Debug.Log("VR Basketball: plantilla de Gradle propia activada en " + tipos[t].FullName);
                        GuardarEnDisco();
                        return;
                    }
                    catch (System.Exception)
                    {
                    }
                }
            }

            Debug.LogWarning("VR Basketball: no encontré la propiedad de la plantilla de Gradle; "
                + "el APK podría no llevar MediaPipe (se comprueba al acabar la compilación).");

            GuardarEnDisco();
        }

        /// <summary>Carga el ensamblado del módulo Android, que no siempre está cargado.</summary>
        private static void CargarEnsambladoAndroid()
        {
            try
            {
                System.Reflection.Assembly.Load("UnityEditor.Android.Extensions");
                return;
            }
            catch (System.Exception)
            {
            }

            try
            {
                string ruta = System.IO.Path.Combine(
                    EditorApplication.applicationContentsPath,
                    "PlaybackEngines", "AndroidPlayer", "UnityEditor.Android.Extensions.dll");

                if (System.IO.File.Exists(ruta))
                {
                    System.Reflection.Assembly.LoadFrom(ruta);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("VR Basketball: no pude cargar el ensamblado de Android: " + e.Message);
            }
        }

        /// <summary>
        /// Deja también la casilla marcada en ProjectSettings.asset, para que la próxima
        /// vez que se abra el proyecto (o se compile desde el editor) ya esté activada.
        /// </summary>
        private static void GuardarEnDisco()
        {
            try
            {
                string ruta = System.IO.Path.Combine(
                    System.IO.Directory.GetParent(Application.dataPath).FullName,
                    "ProjectSettings", "ProjectSettings.asset");

                if (!System.IO.File.Exists(ruta))
                {
                    return;
                }

                string texto = System.IO.File.ReadAllText(ruta);
                string cambiado = texto.Replace("useCustomMainGradleTemplate: 0", "useCustomMainGradleTemplate: 1");

                if (cambiado != texto)
                {
                    System.IO.File.WriteAllText(ruta, cambiado);
                    Debug.Log("VR Basketball: casilla de la plantilla de Gradle guardada en ProjectSettings.asset.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("VR Basketball: no pude escribir ProjectSettings.asset: " + e.Message);
            }
        }

        private static void ConfigureShaders()
        {
            string[] wanted =
            {
                "Standard",
                "Legacy Shaders/Diffuse",
                "Sprites/Default",
                "UI/Default",
                "Hidden/VRBasketball/BarrelDistortion"
            };

            Object[] graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null || graphicsSettings.Length == 0)
            {
                Debug.LogWarning("VR Basketball: no se pudo abrir GraphicsSettings.asset");
                return;
            }

            SerializedObject so = new SerializedObject(graphicsSettings[0]);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");

            if (list == null)
            {
                Debug.LogWarning("VR Basketball: no se encontró m_AlwaysIncludedShaders");
                return;
            }

            List<Object> shaders = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                shaders.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            for (int i = 0; i < wanted.Length; i++)
            {
                Shader shader = Shader.Find(wanted[i]);
                if (shader == null)
                {
                    Debug.LogWarning("VR Basketball: shader no encontrado -> " + wanted[i]);
                    continue;
                }

                if (!shaders.Contains(shader))
                {
                    shaders.Add(shader);
                }
            }

            list.arraySize = shaders.Count;
            for (int i = 0; i < shaders.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = shaders[i];
            }

            so.ApplyModifiedProperties();
            Debug.Log("VR Basketball: shaders incluidos en la compilación.");
        }

        private static void ConfigureQuality()
        {
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.pixelLightCount = 2;
        }
    }
}
