# Roadmap NexoKit

## R8 — implementado

- Wi‑Fi Sensing directo sin ESP32 obligatorio.
- Interfaz futurista / modo pared experimental.
- Gestor de repositorios independiente.
- Intervalo configurable por repo.
- Auto descarga configurable por repo.
- Releases de GitHub + fallback a commit de rama por defecto.
- Descarga específica por plataforma.
- Android: verificación periódica con JobScheduler.
- Desktop: verificación al inicio + temporizador interno.
- Sin autoejecución de archivos descargados.

## Próximas revisiones

- Renombrar repo temporal `PruebaRepositorio` a `NexoKit`.
- Notificaciones Android cuando una descarga termina.
- SHA-256/firma cuando el repositorio publique checksums.
- Autenticación segura opcional para repos privados, sin embutir tokens en el código.
- Catálogo de herramientas instalables desde repositorios.
- Estado `instalada / descargada / disponible` por herramienta.
