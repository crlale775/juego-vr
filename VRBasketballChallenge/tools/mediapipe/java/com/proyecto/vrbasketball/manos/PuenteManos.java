package com.proyecto.vrbasketball.manos;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.ImageFormat;
import android.graphics.SurfaceTexture;
import android.hardware.Camera;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;
import android.view.Display;
import android.view.Surface;
import android.view.WindowManager;

import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.components.containers.Category;
import com.google.mediapipe.tasks.components.containers.Landmark;
import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.vision.core.ImageProcessingOptions;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarker;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarkerResult;

import java.util.List;

/**
 * Puente entre Unity y MediaPipe Hand Landmarker.
 *
 * Unity llama a:
 *   iniciar(contexto, ancho, alto, rotacion, manos)  -> "ok" o "error:..."
 *   datos()                                          -> JSON con la mano detectada
 *   secuencia()                                      -> numero de fotograma analizado
 *   estado()                                         -> JSON de diagnostico
 *   parar()
 *
 * Unity LEE los resultados (Java no llama a Unity): datos() devuelve el ultimo JSON
 * calculado. Asi no hace falta compilar contra unity-classes.jar.
 *
 * La camara se abre con la API clasica (Camera) en su propio hilo y el analisis corre
 * en otro hilo aparte, de modo que un fotograma lento nunca bloquea el juego: si el
 * analizador esta ocupado, el fotograma se descarta.
 */
public final class PuenteManos
{
    private static final String ETIQUETA = "VRBBManos";

    // ---- Datos que lee Unity ----
    private static volatile String _datos = "{\"ok\":0,\"activo\":0,\"listo\":0,\"seq\":0,\"n\":0}";
    private static volatile int _secuencia = 0;
    private static volatile boolean _activo = false;
    private static volatile boolean _listo = false;
    private static volatile String _error = "";
    private static volatile int _cuadrosCamara = 0;
    private static volatile int _cuadrosAnalizados = 0;
    private static volatile int _detecciones = 0;
    private static volatile int _msAnalisis = 0;
    private static volatile int _grados = 0;
    private static volatile int _ancho = 640;
    private static volatile int _alto = 480;
    private static volatile int _rotacionPedida = -1;   // -1 = automatica
    private static volatile int _manosMax = 1;

    // ---- Estado interno ----
    private static final Object CANDADO = new Object();
    private static byte[] _copia;
    private static int _tamanoCuadro = 0;
    private static volatile boolean _ocupado = false;
    private static volatile boolean _corriendo = false;
    private static volatile long _msCuadro = 0;

    private static HandlerThread _hiloCamara;
    private static Handler _manejadorCamara;
    private static Camera _camara;
    private static Thread _hiloAnalisis;
    private static HandLandmarker _detector;
    private static Context _contexto;

    private static Bitmap _mapa;
    private static int[] _pixeles;
    private static ImageProcessingOptions _opcionesImagen;

    // ------------------------------------------------------------------
    // Arranque
    // ------------------------------------------------------------------

    public static synchronized String iniciar(Context contexto, int ancho, int alto, int rotacion, int manos)
    {
        if (_activo)
        {
            return "ya-activo";
        }

        _contexto = contexto.getApplicationContext();
        _ancho = ancho;
        _alto = alto;
        _rotacionPedida = rotacion;
        _manosMax = Math.max(1, Math.min(2, manos));
        _error = "";
        _listo = false;
        _activo = true;

        // El modelo se busca en las dos rutas posibles dentro de los assets del APK:
        // Unity deja las "StreamingAssets" en assets/ o en assets/StreamingAssets/ según
        // la versión, así que se prueban las dos en vez de dar por hecho una.
        String[] rutasModelo = { "hand_landmarker.task", "StreamingAssets/hand_landmarker.task" };
        Throwable ultimoFallo = null;

        for (int i = 0; i < rutasModelo.length && _detector == null; i++)
        {
            try
            {
                BaseOptions opcionesBase = BaseOptions.builder()
                        .setModelAssetPath(rutasModelo[i])
                        .build();

                HandLandmarker.HandLandmarkerOptions opciones = HandLandmarker.HandLandmarkerOptions.builder()
                        .setBaseOptions(opcionesBase)
                        .setRunningMode(RunningMode.IMAGE)
                        .setNumHands(Integer.valueOf(_manosMax))
                        .setMinHandDetectionConfidence(Float.valueOf(0.35f))
                        .setMinHandPresenceConfidence(Float.valueOf(0.35f))
                        .setMinTrackingConfidence(Float.valueOf(0.35f))
                        .build();

                _detector = HandLandmarker.createFromOptions(_contexto, opciones);
                _listo = true;
                Log.i(ETIQUETA, "MediaPipe HandLandmarker listo con el modelo '" + rutasModelo[i] + "'");
            }
            catch (Throwable t)
            {
                ultimoFallo = t;
                Log.w(ETIQUETA, "No se pudo cargar el modelo en '" + rutasModelo[i] + "': " + resumir(t));
            }
        }

        if (_detector == null)
        {
            _error = "mediapipe:" + resumir(ultimoFallo);
            _listo = false;
            _activo = false;
            Log.e(ETIQUETA, "No se pudo crear el HandLandmarker", ultimoFallo);
            publicarSinMano(0);
            return "error:" + resumir(ultimoFallo);
        }

        iniciarCamara();
        return "ok";
    }

    private static void iniciarCamara()
    {
        _corriendo = true;

        _hiloAnalisis = new Thread(new Runnable()
        {
            public void run()
            {
                bucleAnalisis();
            }
        }, "VRBBManosAnalisis");
        _hiloAnalisis.setDaemon(true);
        _hiloAnalisis.start();

        _hiloCamara = new HandlerThread("VRBBManosCamara");
        _hiloCamara.start();
        _manejadorCamara = new Handler(_hiloCamara.getLooper());
        _manejadorCamara.post(new Runnable()
        {
            public void run()
            {
                abrirCamara();
            }
        });
    }

    private static void abrirCamara()
    {
        try
        {
            Camera.CameraInfo info = new Camera.CameraInfo();
            int elegida = -1;

            for (int i = 0; i < Camera.getNumberOfCameras(); i++)
            {
                Camera.getCameraInfo(i, info);
                if (info.facing == Camera.CameraInfo.CAMERA_FACING_BACK)
                {
                    elegida = i;
                    break;
                }
            }

            if (elegida < 0)
            {
                elegida = 0;
            }

            Camera.getCameraInfo(elegida, info);
            _camara = Camera.open(elegida);

            Camera.Parameters parametros = _camara.getParameters();
            Camera.Size tamano = elegirTamano(parametros.getSupportedPreviewSizes(), _ancho, _alto);
            _ancho = tamano.width;
            _alto = tamano.height;
            _tamanoCuadro = _ancho * _alto * 3 / 2;
            _copia = new byte[_tamanoCuadro];

            parametros.setPreviewSize(_ancho, _alto);
            parametros.setPreviewFormat(ImageFormat.NV21);

            try
            {
                List<String> enfoques = parametros.getSupportedFocusModes();
                if (enfoques != null && enfoques.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO))
                {
                    parametros.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO);
                }
            }
            catch (Throwable ignorado)
            {
            }

            _camara.setParameters(parametros);

            // Giro automatico de la imagen para que la mano salga derecha. Se puede
            // forzar desde el juego (?mpgrados=0|90|180|270) si el movil lo mide mal.
            int automatico = (info.orientation - gradosPantalla() + 360) % 360;
            _grados = _rotacionPedida >= 0 ? _rotacionPedida : automatico;

            _opcionesImagen = ImageProcessingOptions.builder()
                    .setRotationDegrees(_grados)
                    .build();

            _mapa = Bitmap.createBitmap(_ancho, _alto, Bitmap.Config.ARGB_8888);
            _pixeles = new int[_ancho * _alto];

            try
            {
                _camara.setPreviewTexture(new SurfaceTexture(0));
            }
            catch (Throwable t)
            {
                Log.w(ETIQUETA, "Sin textura de vista previa: " + resumir(t));
            }

            for (int i = 0; i < 3; i++)
            {
                _camara.addCallbackBuffer(new byte[_tamanoCuadro]);
            }

            _camara.setPreviewCallbackWithBuffer(new Camera.PreviewCallback()
            {
                public void onPreviewFrame(byte[] datos, Camera camara)
                {
                    if (datos != null)
                    {
                        guardarCuadro(datos);
                    }
                    if (camara != null)
                    {
                        camara.addCallbackBuffer(datos);
                    }
                }
            });

            _camara.startPreview();

            Log.i(ETIQUETA, "Camara trasera lista " + _ancho + "x" + _alto + " giro=" + _grados
                    + " (automatico=" + automatico + ")");
        }
        catch (Throwable t)
        {
            _error = "camara:" + resumir(t);
            Log.e(ETIQUETA, "No se pudo abrir la camara", t);
        }
    }

    private static int gradosPantalla()
    {
        try
        {
            WindowManager wm = (WindowManager) _contexto.getSystemService(Context.WINDOW_SERVICE);
            Display pantalla = wm.getDefaultDisplay();
            int rotacion = pantalla.getRotation();

            if (rotacion == Surface.ROTATION_90) return 90;
            if (rotacion == Surface.ROTATION_180) return 180;
            if (rotacion == Surface.ROTATION_270) return 270;
        }
        catch (Throwable ignorado)
        {
        }

        return 0;
    }

    private static Camera.Size elegirTamano(List<Camera.Size> disponibles, int ancho, int alto)
    {
        if (disponibles == null || disponibles.isEmpty())
        {
            return null;
        }

        Camera.Size mejor = disponibles.get(0);
        long mejorDiferencia = Long.MAX_VALUE;

        for (int i = 0; i < disponibles.size(); i++)
        {
            Camera.Size s = disponibles.get(i);

            // Se evitan tamanos gigantes: en un movil solo encarecen el analisis.
            if (s.width > 1280 || s.height > 960)
            {
                continue;
            }

            long diferencia = Math.abs((long) s.width * alto - (long) ancho * s.height)
                    + Math.abs(s.width - ancho) * 8L;

            if (diferencia < mejorDiferencia)
            {
                mejorDiferencia = diferencia;
                mejor = s;
            }
        }

        return mejor;
    }

    // ------------------------------------------------------------------
    // Fotogramas
    // ------------------------------------------------------------------

    private static void guardarCuadro(byte[] datos)
    {
        _cuadrosCamara++;

        if (_copia == null || datos.length < _tamanoCuadro)
        {
            return;
        }

        synchronized (CANDADO)
        {
            if (_ocupado)
            {
                return;   // el analizador va lleno: se tira este fotograma
            }

            System.arraycopy(datos, 0, _copia, 0, _tamanoCuadro);
            _secuencia++;
            _msCuadro = System.currentTimeMillis();
            _ocupado = true;
        }
    }

    private static void bucleAnalisis()
    {
        while (_corriendo)
        {
            boolean hay;
            synchronized (CANDADO)
            {
                hay = _ocupado;
            }

            if (!hay)
            {
                dormir(5);
                continue;
            }

            long inicio = System.currentTimeMillis();
            int secuencia;

            synchronized (CANDADO)
            {
                secuencia = _secuencia;
            }

            try
            {
                convertirANv21(_copia, _pixeles, _ancho, _alto);
                _mapa.setPixels(_pixeles, 0, _ancho, 0, 0, _ancho, _alto);

                MPImage imagen = new BitmapImageBuilder(_mapa).build();

                HandLandmarkerResult resultado = _detector.detect(imagen, _opcionesImagen);

                _cuadrosAnalizados++;
                _msAnalisis = (int) (System.currentTimeMillis() - inicio);

                if (resultado != null && resultado.landmarks() != null && resultado.landmarks().size() > 0)
                {
                    _detecciones++;
                }

                publicar(resultado, secuencia, _msAnalisis);
            }
            catch (Throwable t)
            {
                _error = "analisis:" + resumir(t);
                Log.w(ETIQUETA, "Fallo analizando el fotograma", t);
            }
            finally
            {
                synchronized (CANDADO)
                {
                    _ocupado = false;
                }
            }
        }
    }

    /** NV21 (Y + VU) a pixeles ARGB. Enteros para que sea rapido en un movil. */
    private static void convertirANv21(byte[] nv21, int[] salida, int ancho, int alto)
    {
        int tamanoY = ancho * alto;
        int indice = 0;

        for (int y = 0; y < alto; y++)
        {
            int filaY = y * ancho;
            int filaUv = tamanoY + (y >> 1) * ancho;

            for (int x = 0; x < ancho; x++)
            {
                int componenteY = nv21[filaY + x] & 0xFF;
                int uv = filaUv + (x & ~1);
                int v = (nv21[uv] & 0xFF) - 128;          // NV21: primero V
                int u = (nv21[uv + 1] & 0xFF) - 128;      // y despues U

                int r = componenteY + ((91881 * v) >> 16);
                int g = componenteY - ((22554 * u + 46802 * v) >> 16);
                int b = componenteY + ((116130 * u) >> 16);

                if (r < 0) r = 0; else if (r > 255) r = 255;
                if (g < 0) g = 0; else if (g > 255) g = 255;
                if (b < 0) b = 0; else if (b > 255) b = 255;

                salida[indice++] = 0xFF000000 | (r << 16) | (g << 8) | b;
            }
        }
    }

    // ------------------------------------------------------------------
    // Resultado -> JSON
    // ------------------------------------------------------------------

    private static void publicar(HandLandmarkerResult resultado, int secuencia, int ms)
    {
        List<List<NormalizedLandmark>> manos = resultado != null ? resultado.landmarks() : null;

        if (manos == null || manos.size() == 0)
        {
            publicarSinMano(secuencia);
            return;
        }

        List<List<Landmark>> mundo = resultado.worldLandmarks();
        List<List<Category>> lados = resultado.handedness();

        // Si aparecen dos manos se queda con la que mas se ve (la mas cercana).
        int elegida = 0;
        float mejorArea = -1f;
        List<NormalizedLandmark> puntos = manos.get(0);

        for (int i = 0; i < manos.size(); i++)
        {
            List<NormalizedLandmark> mano = manos.get(i);
            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;

            for (int j = 0; j < mano.size(); j++)
            {
                NormalizedLandmark p = mano.get(j);
                if (p.x() < minX) minX = p.x();
                if (p.y() < minY) minY = p.y();
                if (p.x() > maxX) maxX = p.x();
                if (p.y() > maxY) maxY = p.y();
            }

            float area = (maxX - minX) * (maxY - minY);

            if (area > mejorArea)
            {
                mejorArea = area;
                elegida = i;
                puntos = mano;
            }
        }

        float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
        float sumaX = 0f, sumaY = 0f;

        for (int j = 0; j < puntos.size(); j++)
        {
            NormalizedLandmark p = puntos.get(j);

            if (p.x() < minX) minX = p.x();
            if (p.y() < minY) minY = p.y();
            if (p.x() > maxX) maxX = p.x();
            if (p.y() > maxY) maxY = p.y();

            // Centro de la palma: muneca + nudillos.
            if (j == 0 || j == 5 || j == 9 || j == 13 || j == 17)
            {
                sumaX += p.x();
                sumaY += p.y();
            }
        }

        float anchoMano = Math.max(0.0001f, maxX - minX);
        float altoMano = Math.max(0.0001f, maxY - minY);

        // Pellizco: pulgar (4) contra indice (8), medido en tamanos de palma.
        float pinza = 1f;

        if (puntos.size() >= 21)
        {
            NormalizedLandmark pulgar = puntos.get(4);
            NormalizedLandmark indice = puntos.get(8);
            NormalizedLandmark muneca = puntos.get(0);
            NormalizedLandmark nudilloMedio = puntos.get(9);

            float dPinza = distancia(pulgar.x(), pulgar.y(), pulgar.z(), indice.x(), indice.y(), indice.z());
            float dPalma = Math.max(0.0001f, distancia(muneca.x(), muneca.y(), muneca.z(),
                    nudilloMedio.x(), nudilloMedio.y(), nudilloMedio.z()));
            pinza = dPinza / dPalma;
        }

        float confianza = 0.5f;
        String lado = "";

        if (lados != null && lados.size() > elegida && lados.get(elegida).size() > 0)
        {
            Category c = lados.get(elegida).get(0);
            confianza = c.score();
            lado = c.categoryName();
        }

        StringBuilder sb = new StringBuilder(1600);

        sb.append("{\"ok\":1,\"activo\":").append(_activo ? 1 : 0)
          .append(",\"listo\":").append(_listo ? 1 : 0)
          .append(",\"seq\":").append(secuencia)
          .append(",\"ms\":").append(ms)
          .append(",\"cuadros\":").append(_cuadrosCamara)
          .append(",\"analizados\":").append(_cuadrosAnalizados)
          .append(",\"detecciones\":").append(_detecciones)
          .append(",\"rot\":").append(_grados)
          .append(",\"w\":").append(_ancho)
          .append(",\"h\":").append(_alto)
          .append(",\"error\":\"").append(escapar(_error)).append('"')
          .append(",\"n\":").append(manos.size())
          .append(",\"conf\":").append(redondear(confianza))
          .append(",\"mano\":\"").append(escapar(lado)).append('"')
          .append(",\"x\":").append(redondear(sumaX / 5f))
          .append(",\"y\":").append(redondear(sumaY / 5f))
          .append(",\"ancho\":").append(redondear(anchoMano))
          .append(",\"alto\":").append(redondear(altoMano))
          .append(",\"pinza\":").append(redondear(pinza))
          .append(",\"puntos\":[");

        for (int j = 0; j < puntos.size(); j++)
        {
            NormalizedLandmark p = puntos.get(j);
            if (j > 0) sb.append(',');
            sb.append(redondear(p.x())).append(',').append(redondear(p.y())).append(',').append(redondear(p.z()));
        }

        sb.append("],\"mundo\":[");

        if (mundo != null && mundo.size() > elegida)
        {
            List<Landmark> m = mundo.get(elegida);

            for (int j = 0; j < m.size(); j++)
            {
                Landmark p = m.get(j);
                if (j > 0) sb.append(',');
                sb.append(redondear(p.x())).append(',').append(redondear(p.y())).append(',').append(redondear(p.z()));
            }
        }

        sb.append("]}");

        _datos = sb.toString();
    }

    private static void publicarSinMano(int secuencia)
    {
        StringBuilder sb = new StringBuilder(320);

        sb.append("{\"ok\":1,\"activo\":").append(_activo ? 1 : 0)
          .append(",\"listo\":").append(_listo ? 1 : 0)
          .append(",\"seq\":").append(secuencia)
          .append(",\"ms\":").append(_msAnalisis)
          .append(",\"cuadros\":").append(_cuadrosCamara)
          .append(",\"analizados\":").append(_cuadrosAnalizados)
          .append(",\"detecciones\":").append(_detecciones)
          .append(",\"rot\":").append(_grados)
          .append(",\"w\":").append(_ancho)
          .append(",\"h\":").append(_alto)
          .append(",\"error\":\"").append(escapar(_error)).append('"')
          .append(",\"n\":0,\"conf\":0,\"mano\":\"\",\"x\":0.5,\"y\":0.5,\"ancho\":0,\"alto\":0,\"pinza\":0,\"puntos\":[],\"mundo\":[]}");

        _datos = sb.toString();
    }

    // ------------------------------------------------------------------
    // API que llama Unity
    // ------------------------------------------------------------------

    /** Ultimo resultado. Unity solo lo pide cuando secuencia() cambia. */
    public static String datos()
    {
        return _datos;
    }

    public static int secuencia()
    {
        return _secuencia;
    }

    public static boolean listo()
    {
        return _listo;
    }

    public static String estado()
    {
        return "{\"activo\":" + (_activo ? 1 : 0)
                + ",\"listo\":" + (_listo ? 1 : 0)
                + ",\"secuencia\":" + _secuencia
                + ",\"cuadros\":" + _cuadrosCamara
                + ",\"analizados\":" + _cuadrosAnalizados
                + ",\"detecciones\":" + _detecciones
                + ",\"ms\":" + _msAnalisis
                + ",\"rot\":" + _grados
                + ",\"w\":" + _ancho
                + ",\"h\":" + _alto
                + ",\"error\":\"" + escapar(_error) + "\"}";
    }

    public static synchronized void parar()
    {
        _corriendo = false;
        _activo = false;

        if (_manejadorCamara != null)
        {
            _manejadorCamara.post(new Runnable()
            {
                public void run()
                {
                    cerrarCamara();
                }
            });
        }
        else
        {
            cerrarCamara();
        }

        if (_hiloAnalisis != null)
        {
            try
            {
                _hiloAnalisis.join(600);
            }
            catch (InterruptedException ignorado)
            {
            }
            _hiloAnalisis = null;
        }

        if (_hiloCamara != null)
        {
            _hiloCamara.quitSafely();
            _hiloCamara = null;
            _manejadorCamara = null;
        }

        if (_detector != null)
        {
            try
            {
                _detector.close();
            }
            catch (Throwable ignorado)
            {
            }
            _detector = null;
        }

        _listo = false;
        publicarSinMano(0);
        Log.i(ETIQUETA, "MediaPipe detenido");
    }

    private static void cerrarCamara()
    {
        if (_camara == null)
        {
            return;
        }

        try
        {
            _camara.setPreviewCallbackWithBuffer(null);
            _camara.stopPreview();
        }
        catch (Throwable ignorado)
        {
        }

        try
        {
            _camara.release();
        }
        catch (Throwable ignorado)
        {
        }

        _camara = null;
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------

    private static void dormir(long ms)
    {
        try
        {
            Thread.sleep(ms);
        }
        catch (InterruptedException ignorado)
        {
        }
    }

    private static float distancia(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        float dz = z1 - z2;
        return (float) Math.sqrt(dx * dx + dy * dy + dz * dz);
    }

    /** Redondea a 3 decimales: suficiente para el juego y el JSON queda corto. */
    private static float redondear(float v)
    {
        if (Float.isNaN(v) || Float.isInfinite(v))
        {
            return 0f;
        }

        return Math.round(v * 1000f) / 1000f;
    }

    private static String resumir(Throwable t)
    {
        if (t == null)
        {
            return "desconocido";
        }

        String mensaje = t.getMessage();
        return t.getClass().getSimpleName() + (mensaje != null ? (": " + mensaje) : "");
    }

    private static String escapar(String texto)
    {
        if (texto == null)
        {
            return "";
        }

        StringBuilder sb = new StringBuilder(texto.length() + 8);

        for (int i = 0; i < texto.length(); i++)
        {
            char c = texto.charAt(i);

            if (c == '"' || c == '\\')
            {
                sb.append('\\').append(c);
            }
            else if (c < ' ')
            {
                sb.append(' ');
            }
            else
            {
                sb.append(c);
            }
        }

        return sb.toString();
    }
}
