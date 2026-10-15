#!/usr/bin/env bash
#
# Compila el puente de MediaPipe (tools/mediapipe/java) y deja el .jar dentro de
# Assets/Plugins/Android, que es donde Unity recoge los plugins de Android.
#
#   bash tools/mediapipe/build_jar.sh
#
# Solo hay que repetirlo si se cambia PuenteManos.java. Las librerias de MediaPipe
# (AAR) se descargan la primera vez y se guardan en tools/mediapipe/libs.
#
# Lo que hace falta en el equipo:
#   - JDK (javac y jar).   - Android SDK con una plataforma (android.jar).   - curl.
#
# Todo se compila en una carpeta temporal sin espacios: la ruta de este proyecto
# ("Nueva carpeta (21)") rompe el paso de -classpath a javac en Windows.

set -e

AQUI="$(cd "$(dirname "$0")" && pwd)"
PROYECTO="$(cd "$AQUI/../.." && pwd)"
LIBS="$AQUI/libs"
CODIGO="$AQUI/java"
SALIDA_JAR="$PROYECTO/Assets/Plugins/Android/manos-mediapipe.jar"

VERSION_MEDIAPIPE="0.10.14"
URL_BASE="https://dl.google.com/dl/android/maven2/com/google/mediapipe"

mkdir -p "$LIBS"

# ---- 0) Carpeta de trabajo sin espacios --------------------------------------
TRABAJO="$(mktemp -d)"
trap 'rm -rf "$TRABAJO"' EXIT
mkdir -p "$TRABAJO/lib" "$TRABAJO/src" "$TRABAJO/classes"

# ---- 1) Android SDK: android.jar ---------------------------------------------
SDK="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-$LOCALAPPDATA/Android/Sdk}}"
ANDROID_JAR=""

for version in android-36 android-35 android-34; do
  if [ -f "$SDK/platforms/$version/android.jar" ]; then
    ANDROID_JAR="$SDK/platforms/$version/android.jar"
    break
  fi
done

if [ -z "$ANDROID_JAR" ]; then
  echo "No encuentro android.jar en $SDK/platforms" >&2
  exit 1
fi

echo "android.jar: $ANDROID_JAR"
cp "$ANDROID_JAR" "$TRABAJO/lib/android.jar"

# ---- 2) AAR de MediaPipe ------------------------------------------------------
descargar() {
  local url="$1"
  local destino="$2"

  if [ -f "$destino" ]; then
    return
  fi

  echo "descargando $(basename "$destino")..."
  curl -sL --max-time 600 -o "$destino" "$url"
}

for artefacto in tasks-vision tasks-core; do
  aar="$LIBS/$artefacto-$VERSION_MEDIAPIPE.aar"
  descargar "$URL_BASE/$artefacto/$VERSION_MEDIAPIPE/$artefacto-$VERSION_MEDIAPIPE.aar" "$aar"

  if [ ! -s "$aar" ]; then
    echo "El AAR $artefacto no se descargo bien" >&2
    exit 1
  fi

  rm -rf "$LIBS/$artefacto"
  mkdir -p "$LIBS/$artefacto"
  unzip -o -q "$aar" classes.jar -d "$LIBS/$artefacto"
  echo "clases de $artefacto: $(ls -la "$LIBS/$artefacto/classes.jar" | awk '{print $5}') bytes"

  cp "$LIBS/$artefacto/classes.jar" "$TRABAJO/lib/$artefacto.jar"
done

# Las fuentes se copian a la carpeta temporal para compilar sin espacios.
while IFS= read -r archivo; do
  destino="$TRABAJO/src/${archivo#$CODIGO/}"
  mkdir -p "$(dirname "$destino")"
  cp "$archivo" "$destino"
done < <(find "$CODIGO" -name '*.java')

# ---- 3) Compilar -------------------------------------------------------------
FUENTES=()
while IFS= read -r archivo; do
  FUENTES+=("${archivo#$TRABAJO/}")
done < <(find "$TRABAJO/src" -name '*.java')

echo "compilando ${#FUENTES[@]} archivo(s) java..."

( cd "$TRABAJO" && javac -encoding UTF-8 -nowarn -Xlint:none --release 11 \
    -classpath "lib/android.jar;lib/tasks-vision.jar;lib/tasks-core.jar" \
    -d classes \
    "${FUENTES[@]}" )

# ---- 4) Empaquetar -----------------------------------------------------------
mkdir -p "$(dirname "$SALIDA_JAR")"
rm -f "$SALIDA_JAR"

( cd "$TRABAJO/classes" && jar cf "$SALIDA_JAR" . )

echo "jar listo: $SALIDA_JAR"
unzip -l "$SALIDA_JAR" | tail -8
