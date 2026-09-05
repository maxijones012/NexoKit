# NexoKit R10 — Herramientas integradas

Versiones: **Desktop 1.0.0 / Android 1.0.0 / R10**.

R10 cambia el concepto central de NexoKit: los repositorios ya no sirven solamente para vigilar actualizaciones. Los proyectos compatibles pueden convertirse en **herramientas usables desde la propia aplicación**.

La estructura queda separada en tres capas:

- **🧰 Herramientas**: usar módulos integrados directamente desde NexoKit.
- **🔄 Actualizaciones**: vigilar cada repositorio por separado y descargar nuevas versiones.
- **🔎 Descubrir**: revisar repositorios-catálogo para encontrar nuevas herramientas.

## 🧰 Herramientas integradas

### Meta Scan · Facebook OSINT

NexoKit integra las funciones principales de `HackUnderway/meta_scan` en una interfaz propia.

En Desktop y Android se puede:

- ingresar usuario o URL de Facebook;
- ingresar una API key propia de RapidAPI;
- consultar perfil;
- consultar Business Home;
- consultar About;
- consultar Transparencia;
- visualizar un resumen de datos y JSON crudo;
- en Desktop, copiar y exportar resultados a TXT/JSON.

La clave de RapidAPI **no se guarda en NexoKit**: permanece sólo durante la sesión de la interfaz.

Repositorio de referencia: `HackUnderway/meta_scan` (MIT).

### OSINT Hub

El catálogo de `Descubrir` pasa a ser navegable como una herramienta real.

- buscador por repositorio, categoría o fuente;
- actualización manual de los catálogos;
- abrir una herramienta en GitHub;
- agregar una herramienta seleccionada a `Actualizaciones`;
- las herramientas agregadas desde el Hub entran como **SOLO AVISA** por defecto.

Fuente inicial:

`Astrosp/Awesome-OSINT-List`

El catálogo no descarga ni ejecuta automáticamente todos sus recursos.

### Wi‑Fi Sensing

El módulo router + teléfono/PC sigue integrado en NexoKit y puede abrirse desde el centro de Herramientas.

- RSSI y señal Wi‑Fi;
- calibración del ambiente;
- actividad RF experimental;
- HUD de sensing;
- CSI/RuView queda como referencia avanzada para hardware compatible.

El sensing mediante RSSI es experimental y no equivale a una imagen ni determina una ubicación exacta de una persona.

## Desktop R10

La aplicación agrega automáticamente una pestaña **🧰 Herramientas** al iniciar, sin eliminar las pestañas existentes.

Dentro de esa pestaña hay módulos para:

- Meta Scan;
- OSINT Hub;
- Wi‑Fi Sensing.

El encabezado de la aplicación se actualiza a NexoKit Desktop 1.0.0 · R10.

## Android R10

Android incorpora un acceso **🧰 Herramientas** desde la pantalla principal que abre el centro integrado.

El centro Android incluye:

- Meta Scan directamente desde el teléfono;
- catálogo OSINT filtrable;
- actualización de catálogos;
- abrir repositorios;
- mandar herramientas a Actualizaciones en modo SOLO AVISA;
- acceso al módulo Wi‑Fi Sensing.

## Actualizaciones de repositorios

Se conserva el sistema independiente por repositorio:

- intervalo propio;
- GitHub Releases o último commit de la rama principal;
- descarga de asset compatible o ZIP de código;
- **nunca ejecuta ni instala automáticamente** lo descargado.

## Descubrir

Se mantiene la detección de herramientas nuevas en repositorios-catálogo. La primera revisión genera una línea base y las revisiones siguientes marcan únicamente altas nuevas.

## Compilar

Ejecutar `INICIAR.bat`:

- `2` Compilar Desktop
- `3` Compilar Android
- `4` Compilar todo
- `5` Instalar APK por USB

GitHub Actions genera además los artifacts y el Release de R10.

## Uso responsable

Las funciones OSINT, de red y sensing deben utilizarse únicamente con fines legales y sobre sistemas, redes, cuentas o ambientes propios o autorizados. Los catálogos pueden enlazar proyectos de terceros: NexoKit no garantiza su seguridad y no los ejecuta automáticamente.
