# NexoKit R8 — caja de herramientas Desktop + Android

Versiones: **Desktop 0.8.0 / Android 0.8.0 / R8**.

R8 mantiene el Wi‑Fi Sensing experimental de R7 y agrega un **gestor de repositorios con actualización independiente por herramienta**.

## Actualizaciones por repositorio

Cada repositorio tiene su propia configuración:

- activo / pausado;
- intervalo en horas (1 a 168);
- descarga automática ON/OFF;
- última versión detectada;
- última revisión;
- estado;
- último archivo descargado.

La comprobación sigue este orden:

1. `releases/latest` de GitHub;
2. si el repo no usa Releases, commit de la rama por defecto;
3. si cambió la versión/commit, descarga el asset apropiado;
4. si no hay asset compatible, descarga el ZIP de código fuente.

### Selección de archivo

**Desktop:** prioriza EXE/MSI/ZIP de Windows.

**Android:** prioriza APK, luego AAB/ZIP de Android.

Las descargas **nunca se ejecutan ni se instalan automáticamente**. Esto evita que una actualización remota ejecute código sin revisión.

## Comprobación periódica

### Desktop

- revisa al iniciar;
- un temporizador interno mira cada 30 minutos qué repositorios ya cumplieron su intervalo individual;
- sólo consulta los repos que están vencidos.

### Android

- usa `JobScheduler` nativo;
- trabajo periódico global aproximadamente cada 1 hora, sólo con red no medida;
- dentro de ese trabajo cada repositorio respeta su propio intervalo;
- el trabajo queda persistido tras reinicios;
- descargas en el almacenamiento específico de la app, dentro de `NexoKitUpdates`.

## Repositorios iniciales

R8 viene con tres entradas editables:

- `maxijones012/PruebaRepositorio` (repo temporal de NexoKit);
- `maxijones012/FACELY-Releases`;
- `maxijones012/IrisTrack_AI`.

Se pueden agregar o quitar repositorios pegando una URL de GitHub o `owner/repo`.

## Repositorios privados

R8 **no guarda tokens ni claves de GitHub**. Los repositorios privados pueden aparecer como no encontrados porque requieren autenticación. Se deja autenticación segura para una revisión posterior.

## Wi‑Fi Sensing

Sigue el modo directo de R7:

- router/AP actual;
- RSSI en vivo;
- calibración;
- variación RF;
- estados `ESTABLE / VARIACIÓN / ACTIVIDAD / MOVIMIENTO PROBABLE`;
- ESP32/CSI sigue como hardware avanzado opcional.

El sensing RSSI es experimental y no identifica ni ubica personas con precisión.

## Compilar

Ejecutá:

`INICIAR.bat`

- `2` Compilar Desktop
- `3` Compilar Android
- `4` Compilar todo
- `5` Instalar APK por USB

Salida Android:

`Releases\Android\KitHerramientas_Android_0.8.0_R8.apk`
