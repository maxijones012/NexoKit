# Validación R9

Fecha: 2026-09-05

## Resultado

- XML/XAML válido en Desktop y Android.
- 36 handlers WPF declarados y encontrados; 0 faltantes.
- 81 IDs Android definidos y 81 referenciados; 0 faltantes.
- `RepositoryUpdateJobService` declarado en Android con `android.permission.BIND_JOB_SERVICE`.
- Versiones coherentes: Desktop 0.9.0 / Android 0.9.0 / R9.
- Motor Desktop presente: `Desktop/Services/RepositoryUpdateService.cs`.
- Motor Android presente: `RepositoryUpdater.kt`.
- Parser Kotlin: sin errores sintácticos. Las referencias Android no se pueden resolver en este contenedor porque no está montado `android.jar`; eso no es un error de sintaxis.

## Comportamiento implementado

### Desktop

- Configuración persistente por repositorio.
- Intervalo independiente de 1 a 168 horas.
- Auto descarga independiente ON/OFF.
- Revisión al iniciar.
- Temporizador de 30 minutos que sólo revisa repositorios cuyo intervalo ya venció.
- Releases de GitHub primero; fallback a commit de rama por defecto.
- Descargas en `%LOCALAPPDATA%\\NexoKit\\RepositoryUpdates\\Downloads`.
- No ejecuta ni instala automáticamente lo descargado.

### Android

- Configuración persistente por repositorio en SharedPreferences.
- Intervalo independiente de 1 a 168 horas.
- Revisión manual de un repo o de todos.
- Pausar/activar y auto descarga por repositorio.
- `JobScheduler` periódico aproximadamente cada 1 hora, únicamente con red no medida; cada repo mantiene su intervalo propio.
- Job persistido tras reinicios.
- Prioridad de descarga: APK > AAB > ZIP Android > ZIP.
- Descargas en el directorio de descargas específico de la app, subcarpeta `NexoKitUpdates`.
- No ejecuta ni instala automáticamente lo descargado.

## Repositorios iniciales

- `maxijones012/PruebaRepositorio`
- `maxijones012/FACELY-Releases`
- `maxijones012/IrisTrack_AI`

Se pueden agregar otros repositorios públicos con URL de GitHub o `owner/repo`.

## Limitación deliberada

Los repositorios privados requieren autenticación. R9 no embebe ni guarda tokens de GitHub para evitar dejar credenciales dentro de la aplicación. Una autenticación segura puede agregarse en una revisión posterior.

## Compilación final

La validación completa del `.exe` WPF y del `.apk` debe ejecutarse con `INICIAR.bat` en el entorno portable del proyecto, porque este contenedor no dispone del SDK Windows Desktop ni del Android SDK completo.
