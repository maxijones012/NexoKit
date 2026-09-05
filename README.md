# Kit Herramientas R7 — Wi‑Fi Sensing directo

Versiones: **Desktop 0.7.0 / Android 0.7.0 / R7**.

R7 cambia la prioridad del proyecto: el modo principal ya no depende de ESP32-S3. Ahora **usa directamente el Wi‑Fi del router al que está conectado el teléfono o la PC**, tomando RSSI como señal experimental de cambios del ambiente.

## Objetivo de R7

Probar presencia/movimiento **de forma experimental** usando solamente:

- router / punto de acceso Wi‑Fi;
- teléfono Android o PC conectado a esa red;
- calibración de línea base;
- variaciones RSSI en el tiempo.

No genera una imagen óptica ni puede identificar o ubicar con precisión a una persona. Paredes, puertas, objetos, interferencia, roaming y cambios del propio enlace pueden producir variaciones.

## Wi‑Fi Sensing · Modo pared

La pantalla futurista de R6 se mantiene, pero el modo real ahora es **WI‑FI DIRECT**.

### Flujo recomendado

1. Conectá teléfono/PC al router que vas a usar.
2. Dejá router y dispositivo quietos.
3. Abrí `Wi‑Fi Sensing · Modo pared`.
4. Tocá `CALIBRAR 25s`.
5. Mantené el ambiente de referencia lo más estable posible.
6. Cuando diga `CALIBRACIÓN LISTA`, tocá `INICIAR`.
7. Probá movimiento entre router y dispositivo, o con una pared en el medio.
8. Mirá `RSSI`, `Δ RSSI`, `Score`, `Confianza RF` y `Estado`.

Estados posibles:

- `AMBIENTE ESTABLE`
- `VARIACIÓN LEVE`
- `ACTIVIDAD RF ALTA`
- `MOVIMIENTO PROBABLE`

`Confianza RF` no es una probabilidad de que exista una persona. Es únicamente una medida de cuánto se aparta la señal de la línea base.

## Visualización

En modo Wi‑Fi real la escena no dibuja un esqueleto falso. Muestra una **nube de actividad RF**, ondas y variación de intensidad.

El `DEMO` sigue disponible solamente para mostrar la estética de la interfaz.

## CSI / ESP32

El receptor CSI anterior sigue incluido como **hardware avanzado opcional**, escondido en la sección correspondiente. No es necesario para usar R7.

## Compilar

Ejecutar:

`INICIAR.bat`

Opciones principales:

- `2` Compilar Desktop
- `3` Compilar Android
- `4` Compilar todo
- `5` Instalar APK por USB

El sistema conserva el entorno portable en `.tools` si ya fue preparado en una versión anterior.

Salida Android:

`Releases\Android\KitHerramientas_Android_0.7.0_R7.apk`

## Uso responsable

Usar las funciones de red y sensing solamente sobre redes y ambientes propios o autorizados. Los estados de R7 son estimaciones experimentales y no deben presentarse como una identificación, localización exacta o evidencia concluyente de presencia humana.
