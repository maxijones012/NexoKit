# NexoKit R9 — Descubrir + Actualizaciones

Versiones: **Desktop 0.9.0 / Android 0.9.0 / R9**.

R9 agrega una capa nueva sobre el gestor de repositorios: **Descubrir**. La idea es separar claramente dos cosas:

- **Actualizaciones**: proyectos concretos que ya elegiste seguir. Cada repo tiene su intervalo y puede descargar una release/ZIP nuevo.
- **Descubrir**: repositorios-catálogo (por ejemplo listas OSINT) que se revisan periódicamente para detectar herramientas/repo nuevos, sin descargarlos automáticamente.

## Fuente incluida

R9 trae como fuente inicial:

`Astrosp/Awesome-OSINT-List`

La primera revisión crea una línea base. En revisiones posteriores, si aparecen links nuevos de GitHub en el README, se marcan como `NUEVO`.

## Desktop · pestaña Descubrir

- agregar/quitar fuentes catálogo;
- intervalo independiente por fuente;
- revisión manual o automática;
- cantidad total de recursos y cantidad nueva;
- categoría tomada del encabezado del README;
- abrir el repositorio descubierto en GitHub;
- mandar un recurso a `Actualizaciones`;
- al agregar desde Descubrir entra en **SOLO AVISA** por seguridad;
- `MARCAR VISTOS` limpia la bandera de novedades sin borrar el historial.

## Android · Descubrir

Android incluye la misma fuente inicial y revisa catálogos junto con el trabajo programado de repositorios. La app muestra hasta 80 recursos por pantalla, priorizando los nuevos.

El catálogo nunca instala ni ejecuta herramientas. Para seguir un proyecto concreto, se agrega a `Actualizaciones`.

## Actualizaciones de repositorios

Se mantiene R8:

- cada repo tiene su propio intervalo;
- detecta GitHub Releases o, si no existen, el último commit de la rama principal;
- descarga el asset adecuado o un ZIP de código;
- **nunca ejecuta ni instala automáticamente** lo descargado.

## Wi‑Fi Sensing

Se conserva el modo Wi‑Fi directo basado en RSSI de R7, con calibración, actividad RF y HUD futurista. Es experimental y no equivale a una imagen ni a una localización exacta de personas.

## Herramienta agregada: Meta Scan

NexoKit incluye ahora `HackUnderway/meta_scan` entre los repositorios seguidos de fábrica.

- categoría: OSINT / Facebook;
- revisión individual: cada 12 horas;
- descarga automática: activada;
- nunca ejecuta ni instala automáticamente lo descargado;
- una migración de una sola vez lo agrega también a instalaciones R9 que ya tenían su lista guardada.


## Compilar

Ejecutar `INICIAR.bat`:

- `2` Compilar Desktop
- `3` Compilar Android
- `4` Compilar todo
- `5` Instalar APK por USB

Si ya tenés `.tools` de una versión anterior, el parche R8→R9 puede reutilizarlo.

## Uso responsable

Las funciones OSINT, de red y sensing deben utilizarse únicamente con fines legales y sobre sistemas, redes, cuentas o ambientes propios o autorizados. Un catálogo puede enlazar proyectos de terceros: NexoKit no garantiza su seguridad ni los ejecuta automáticamente.
