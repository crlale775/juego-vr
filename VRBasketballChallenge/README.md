# VR Basketball Challenge — beta 0.1

Juego de baloncesto en realidad virtual para **celular dentro de lentes tipo Cardboard**.
El jugador calibra su mano con la cámara, apunta mirando y lanza a un aro que se mueve
mientras la cancha cambia de iluminación de golpe para subir la dificultad.

Motor: **Unity 6000.5.7f1** · Lenguaje: **C#** · Sin plugins externos, sin paquetes de GitHub.

---

## 1. Qué está hecho (y qué no)

### Funciona en esta beta

| Sistema | Estado | Detalle |
|---|---|---|
| Vista VR estéreo | ✅ | Dos cámaras con separación entre pupilas (IPD), escala de render configurable y corrección de distorsión de lente |
| Seguimiento de cabeza | ✅ | Giroscopio del teléfono (Android) · sensores del navegador (WebGL) · arrastre con dedo/ratón (respaldo) |
| Calibración de la mano | ✅ | **Automática**: usa la **cámara trasera** y empieza sola, sin pulsar nada. Mide tamaño, apertura de dedos y estabilidad |
| Modo Halloween | ✅ | Calabazas encendidas, lápidas, murciélagos, telarañas, luces fatuas, niebla morada, truenos y aullidos |
| Personalización del balón | ✅ | La calibración decide tamaño, masa, rebote y precisión del balón |
| Lanzamiento | ✅ | La mano delante de la cámara trasera apunta y el gesto de subirla lanza. El toque en pantalla queda solo de respaldo |
| Física del balón | ✅ | Rigidbody, rebote contra el piso, choque con el aro y el tablero, red |
| Aro móvil por niveles | ✅ | 5 niveles: horizontal, más rápido, vertical, aleatorio y saltos impredecibles |
| Iluminación dinámica | ✅ | Apagones, luz roja, luz intermitente, "caos" y cambio de escenario (tema y gradas) |
| Puntuación y dificultad | ✅ | Puntos por nivel, bonus por racha y por "limpio" (sin tocar el aro), récord guardado |
| Sonido | ✅ | Todos los sonidos se sintetizan en tiempo de ejecución (no hay archivos de audio) |
| Interfaz | ✅ | Marcador en el mundo (se ve en los dos ojos) y menús que se eligen **con la mano** (el cursor va solo sobre el botón) |
| Modo demo | ✅ | El juego lanza solo, para mostrar o probar sin visor |

### Limitaciones honestas

- **No es un escaneo 3D de la mano.** Es una **estimación** a partir de una sola cámara:
  mide el tamaño de la silueta, cuántos dedos se separan y la estabilidad entre fotogramas.
  Es lo máximo que se puede hacer con la cámara de un celular, y es lo que se planteó como
  "calibración / detección de la mano" en lugar de escaneo.
- **La cámara que manda es la TRASERA** (la que apunta hacia delante, la que no está en la
  pantalla). Es la única que puede ver la mano que el jugador tiene delante mientras mira la
  pantalla o lleva el teléfono en los lentes.
- **Puede fallar el permiso de cámara.** El juego pide el permiso de cámara al arrancar; si se
  deniega, sigue funcionando con la mirada y el toque para no quedarse atascado.
- **El giroscopio en el navegador puede salir con la vista girada** en algunos teléfonos.
  Se corrige abriendo la página con `?roll=90` (o `roll=180`, `roll=270`).
- **La versión de navegador necesita HTTPS** para usar la cámara y los sensores.
- La distorsión de lente es una aproximación genérica, no la calibración exacta de tus lentes.

---

## 2. Cómo jugar

### Opción A — En el celular dentro de los lentes (versión WebGL, ya compilada)

1. Conecta el celular a la misma red WiFi que la PC.
2. En la PC, ejecuta:
   ```
   node tools/serve.js
   ```
   Te muestra la dirección, por ejemplo `https://192.168.0.12:8443`.
3. Abre esa dirección en **Chrome** del celular.
4. Acepta la advertencia del certificado (es autofirmado) y permite la cámara.
5. Al abrir el juego, la **calibración de la mano empieza sola** con la cámara trasera:
   solo hay que mantener la palma abierta frente a la cámara unos 3 segundos.
   (Si la cámara no coopera tras varios intentos, el juego continúa igual con un
   perfil neutro, para que nunca se quede atascado.)
6. Pulsa **PONER LOS LENTES Y JUGAR**, mete el teléfono en el visor y **toca la pantalla** para lanzar.

> Para que el navegador permita la cámara, la dirección debe ser **https**.
> El servidor genera un certificado solo la primera vez.

### Opción B — En la PC (sin lentes, para probar)

```
node tools/serve.js --http
```
y abre `http://localhost:8080`. También funciona la compilación de Windows:
`Builds/Windows/VRBasketballChallenge.exe`.

### Opción C — Modo demo (sin tocar nada)

```
http://localhost:8080/?demo=1
```
Lanza solo, apuntando al aro. Sirve para ver todo el juego en movimiento.

---

## 3. Controles

El "mando" es la **mano delante de la cámara trasera**: no hay mandos ni terceros.

| Acción | Cómo se hace |
|---|---|
| Elegir en el menú | **Tocar el botón en la pantalla** (funciona siempre). También se puede poner la mano delante de la cámara trasera y llevarla hasta el botón (~0,9 s) o mirar fijo 1,1 s |
| Apuntar | Mover la mano delante de la cámara (la posición de la palma gira la puntería) |
| Lanzar | Subir la mano de golpe delante de la cámara. Respaldo: tocar la pantalla o sacudir el teléfono |
| Mirar | Girar la cabeza (giroscopio) · PC: arrastrar con el ratón o el dedo |

---

## 4. Niveles de dificultad

| Nivel | Aro | Velocidad | Iluminación |
|---|---|---|---|
| 1 | Casi fijo | 0,25 | Normal |
| 2 | Movimiento horizontal | 0,60 | Normal |
| 3 | Rápido + algo vertical | 0,95 | Apagones repentinos |
| 4 | Horizontal + vertical | 1,30 | Intermitente + escenario cambiante |
| 5 | Impredecible (salta) | 1,75 | Caos: oscuridad, luz roja y saltos |

Se sube de nivel cada **3 encestes**. Además, con cada tiro hay una probabilidad de que la
luz cambie de golpe (a partir del nivel 3 el apagón es parte de la jugada).
El aro también se aleja y se hace un poco más pequeño en cada nivel.

---

## 5. Cómo la calibración cambia el balón

`HandProfile` (se guarda entre partidas) contiene:

| Medida | De dónde sale | Efecto en el juego |
|---|---|---|
| `sizeFactor` | alto de la silueta de la mano | tamaño del balón (0,8 → 1,25) |
| `grip` | dedos separados + forma de la silueta | rebote del balón (agarre) |
| `power` | mezcla de agarre y tamaño | masa del balón y potencia del tiro |
| `stability` | variación entre fotogramas | ayuda de puntería y dispersión del tiro |

Si la cámara no está disponible, siempre puedes elegir **mano pequeña / media / grande**.
El juego **nunca depende** de que la cámara funcione.

---

## 6. Estructura del proyecto

```
VRBasketballChallenge/
├── Assets/
│   ├── Scenes/VRBasketball.unity      Escena única: sólo contiene GameBootstrap
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── GameBootstrap.cs       Crea toda la jerarquía del juego por código
│   │   │   ├── GameManager.cs         Estados, puntuación, dificultad, récord
│   │   │   ├── DifficultyManager.cs   Los 5 niveles
│   │   │   ├── AudioSynth.cs          Sonidos sintetizados
│   │   │   └── CalibrationStore.cs    Perfil de mano guardado (PlayerPrefs)
│   │   ├── VR/
│   │   │   ├── StereoRig.cs           Vista estéreo + escala de render
│   │   │   ├── HeadTracker.cs         Giroscopio / sensores web / arrastre
│   │   │   ├── LensDistortionEffect.cs y WebGLDeviceOrientation.cs
│   │   ├── Hand/
│   │   │   ├── HandAnalyzer.cs        Detección y medidas de la mano
│   │   │   └── HandCalibrationScreen.cs  Pantalla de calibración
│   │   ├── Gameplay/
│   │   │   ├── BallController.cs      Física y estados del balón
│   │   │   ├── ThrowController.cs     Puntería, tiro y ayuda de puntería
│   │   │   ├── HoopController.cs      Aro móvil + detección de enceste
│   │   │   └── LightingDirector.cs    Apagones y cambios de escenario
│   │   ├── Environment/               Cancha, gradas, mallas y texturas por código
│   │   └── UI/                        HUD, menús y fábrica de interfaz
│   ├── Shaders/BarrelDistortion.shader
│   ├── Plugins/WebGL/DeviceOrientation.jslib
│   └── Editor/                        ProjectSetup.cs y BuildScripts.cs
├── tools/serve.js                     Servidor local HTTP/HTTPS
└── Builds/                            Compilaciones generadas
```

La escena **sólo** contiene un objeto con `GameBootstrap`: todo lo demás se construye por
código en tiempo de ejecución. Es más fácil de revisar y de modificar que decenas de
referencias en el inspector.

---

## 7. Compilar

Desde la carpeta del proyecto:

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Unity.exe"

# WebGL (celular / navegador)
"$UNITY" -batchmode -quit -nographics -projectPath . \
  -executeMethod VRBasketball.EditorTools.BuildScripts.BatchBuildWebGL -logFile build_webgl.log

# Windows
"$UNITY" -batchmode -quit -nographics -projectPath . \
  -executeMethod VRBasketball.EditorTools.BuildScripts.BatchBuildWindows -logFile build_win.log

# Android (requiere el módulo "Android Build Support")
"$UNITY" -batchmode -quit -nographics -projectPath . \
  -executeMethod VRBasketball.EditorTools.BuildScripts.BatchBuildAndroid -logFile build_apk.log
```

Salidas: `Builds/WebGL`, `Builds/Windows`, `Builds/Android`.

### Para el APK de Android

1. Unity Hub → pestaña **Installs** → engranaje de **6000.5.7f1** → *Add modules* →
   **Android Build Support** (con *OpenJDK* y *Android SDK & NDK Tools*).
2. `Player Settings` → `Other Settings` → **Scripting Backend: IL2CPP** y
   **Target Architectures: ARM64** (Unity 6 no publica APK de 64 bits con Mono).
3. Añadir el permiso de cámara en `Assets/Plugins/Android/AndroidManifest.xml`:
   ```xml
   <uses-permission android:name="android.permission.CAMERA" />
   ```
4. Compilar y copiar el APK al teléfono.

---

## 8. Verificación hecha

- Compilación de todos los scripts **sin errores** (Unity 6000.5.7f1, batchmode).
- Compilación **WebGL** y **APK de Android** correctas.
- Arranque real del juego en Chrome comprobado por capturas de pantalla:
  pantalla de calibración, menú sobre la cancha 3D y partida en curso con marcador,
  aro, balón y física funcionando.
- Escaneo de mano verificado con una **cámara virtual** que muestra una mano sintética
  (el vídeo se genera con `python _verificacion/genera_mano_y4m.py mano.y4m`).
  Resultado real de la prueba: **la calibración avanzó sola, sin pulsar nada**, y creó el perfil
  `mano amplia, agarre abierto, estable (94% confianza, 5 dedos)`.
- Modo Halloween verificado en el juego en marcha: `halloween activo=True calabazas=12
  objetos=170 luces=2`, con el 22% de la imagen en tonos morados (frente al 0,7% del modo clásico).
- Verificación del APK: `com.proyecto.vrbasketball`, v0.1.0-beta, ARM64-v8a, IL2CPP,
  permiso `CAMERA`, firmado (esquema v2) y válido para instalar.

Scripts de verificación en `_verificacion/`:

| Script | Qué comprueba |
|---|---|
| `verify.js` | que el juego carga y arranca |
| `verify_flujo.js` | flujo completo con clics reales |
| `verify_mano.js` | escaneo automático de la mano con cámara virtual |
| `genera_mano_y4m.py` | genera el video de la mano que usa esa prueba |
| `verify_mano_menu.js` | **control total por mano**: cámara trasera, calibración, menú y partida sin ningún toque |
| `genera_mano_menu_y4m.py` | genera el video con la mano justo delante del botón INICIAR PARTIDA |
| `verify_lanzar.js` | el **gesto de subir la mano** lanza el balón de verdad |

### Verificación del lanzamiento con la mano

Con `mano.y4m` (una mano que se queda quieta y de golpe sube) y Chrome con cámara
virtual, en partida:

```
gestos vistos por la cámara = 8      tiros que salieron con la mano = 4
tiros con toque = 0                  errores = 0
```

La diferencia entre gestos y tiros es el tiempo que el balón está en el aire: el juego
ahora **recuerda el gesto** y lo lanza en cuanto el balón vuelve, y mientras tanto la
mano dibujada avisa con "BALÓN EN JUEGO · espera" y la barra se pone naranja.

### Verificación del menú y del escaneo (tras pulirlos)

```
cámara trasera -> cámara lista -> mano detectada -> perfil creado
-> estado=Jugando (el botón lo llenó el cursor de la mano)
cinco comprobaciones: cámara trasera, cámara lista, mano detectada,
                      perfil creado, sin toques  ->  OK
```

### Verificación del menú táctil (2026-10-14)

El menú se puede pulsar con el dedo, además de con la mano y la mirada. Comprobado con
un clic real en Chrome sobre el botón INICIAR PARTIDA:

```
estado antes del toque = Menu     ->     estado después del toque = Jugando
```También se quitó del menú el botón de **CALIDAD** (gráficos): quedan cinco
botones (INICIAR PARTIDA · MODO · TIRO · RECENTRAR VISTA · VISTA). El reparto en pantalla es
2 + 2 + 1 botones. El script de la prueba es `_verificacion/verify_touch.js`.

### Corrección del volteo de la vista en el APK (2026-10-14)

En el APK de Android el juego arrancaba **boca abajo** (180°): el escenario y también el
texto del marcador y del menú. En la web y en Windows se veía bien porque la orientación
se calcula en otro sitio.

Causa: al compensar la postura horizontal del visor se eligía el signo (+90°/-90°) según
`Screen.orientation`, y en Android ese valor puede llegar al revés. Con el signo equivocado
la cámara queda girada media vuelta y, como el marcador y el menú viven dentro del mundo,
también dan la vuelta.

Arreglo en `Assets/Scripts/VR/HeadTracker.cs`: ya no se fía de la orientación que dice el
sistema; prueba las dos opciones y elige la que deja la "arriba" de la vista apuntando
hacia arriba de verdad (el sensor está referenciado a la gravedad de la Tierra). Si el
móvil está plano y las dos opciones empatan, mantiene la última elegida para no girarse
sola, y usa la orientación del sistema solo como respaldo. Como refuerzo se dejó
`Android Blit Type` en **Auto** (recomendado por Unity) en vez de **Never**, que podía
presentar la imagen del revés en algunos móviles.

Además se corrigió el **espejado de la mano**: la cámara trasera ya **no** se refleja
(antes se forzaba espejo y la puntería con la mano salía al lado contrario). Solo la cámara
frontal se refleja.

### Última verificación del control por mano

Con `mano_menu.y4m` (mano sintética colocada en el punto exacto que corresponde al botón
INICIAR PARTIDA) y Chrome con cámara virtual, el flujo real fue:

```
cámara trasera: abierta  ->  cámara lista  ->  mano detectada (0.41, 0.54, 11% del encuadre)
->  calibración automática: 'mano compacta, agarre abierto, estable (79% confianza, 5 dedos)'
->  estado=Menu  ->  boton=start (el cursor de la mano lo llenó)  ->  estado=Jugando

toques de pantalla = 0    tiros con toque = 0    errores = 0
```

Se confirmó además que el rayo de la mano toca el botón de verdad:
`start pos=(-0.198, 1.577, 1.750) tam=(0.374, 0.068)` con el cursor encima.

En esa misma pasada se encontró y corrigió un fallo que impedía jugar: el colisionador de
los botones del menú se multiplicaba dos veces por la escala del lienzo y quedaba en 0,4 mm,
así que **ningún rayo (ni la mirada ni la mano) podía pulsar ningún botón**. Ya está a tamaño
real y el menú se elige con la mano.

Los scripts quedan en el proyecto para poder repetir la verificación; las capturas y los
vídeos de prueba son temporales y se borran después de comprobar.

---

## 9. Parámetros por URL (útil para pruebas)

| Parámetro | Qué hace |
|---|---|
| `?autostart` | Salta la calibración y va al menú |
| `?halloween=1` | Fuerza el modo Halloween |
| `?demo=1` | Modo demo: el juego lanza solo |
| `?look=drag` | Fuerza el control de vista por arrastre |
| `?look=gyro` | Fuerza el control por sensores |
| `?roll=90` | Gira la vista 90° (si se ve torcida) |
| `?yaw=45` | Ajusta el giro horizontal en grados |

---

## 10. Modo Halloween (ambiente extra)

Se enciende y se apaga desde el menú (**MODO: CLÁSICO / MODO: HALLOWEEN**) y la elección
se recuerda entre partidas. Cambia por completo el aspecto de la cancha:

- **Calabazas talladas y encendidas** (12) alrededor de la cancha, con la cara emisiva y
  dos luces reales que titilan como velas.
- **Lápidas** apoyadas en las gradas.
- **Murciélagos** (10) que sobrevuelan la cancha en círculos, batiendo las alas.
- **Telarañas** generadas por código en las cuatro esquinas altas.
- **Luces fatuas verdes** que flotan cerca del aro.
- **Luna** y **niebla morada** permanentes (dentro de los apagones la niebla es más densa).
- La paleta de la cancha cambia a **violeta con líneas naranjas**.
- Ambiente sonoro: **truenos** y **aullidos** lejanos cada tanto.
- La interfaz completa (título, botones, marcador) cambia a la paleta naranja/violeta.

Todo está generado por código en `Assets/Scripts/Environment/HalloweenDecor.cs`:
no hay ni un modelo, ni una textura, ni un sonido en el proyecto.

---

## 11. Ideas para la siguiente versión

- Segunda pasada de calibración del tiro: medir el gesto real de lanzamiento y guardarlo.
- Nubes de partículas en los apagones y público animado.
- Modo entrenamiento con el aro fijo y contador de precisión.
- Tabla de récords local (Firebase queda como opcional, no es necesario para jugar).
- Calibración de distorsión de lente según el visor concreto.
