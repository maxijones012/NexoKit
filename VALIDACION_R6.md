# Validación R6

Fecha de preparación: 2026-09-05.

## Estructura

- Desktop: 0.6.0 / R6.
- Android: 0.6.0 / R6.
- Nuevo control WPF: `Desktop/Controls/CsiSceneView.cs`.
- Nuevo control Android: `CsiSceneView.kt`.
- Modo DEMO y REAL separados visualmente.

## Controles realizados

- `MainWindow.xaml` parsea como XML correctamente.
- `App.xaml` parsea como XML correctamente.
- `activity_main.xml` parsea como XML correctamente.
- `AndroidManifest.xml` parsea como XML correctamente.
- 30 handlers declarados en XAML Desktop: todos tienen método en `MainWindow.xaml.cs`.
- 71 referencias `R.id` de `MainActivity.kt`: todas existen en el layout Android.
- No hay `x:Name` duplicados en Desktop.
- No hay IDs Android duplicados.
- Se revisaron cadenas C#/Kotlin para evitar el problema previo de strings cortados por salto de línea.
- Versiones y nombres de salida Android actualizados a `0.6.0_R6`.

## Comportamiento de la escena

- DEMO muestra dos figuras simuladas y métricas simuladas claramente marcadas como DEMO.
- REAL no dibuja figuras si no recibe datos de presencia/vitals.
- CSI RAW se representa como actividad/nube RF y no como persona.
- El receptor UDP 5005 puede seguir escuchando mientras la escena está en DEMO.
- TRACKING y HEATMAP pueden activarse/desactivarse sin detener el receptor.

## Limitación de validación en este entorno

No se ejecutó `dotnet publish` WPF ni `assembleDebug` Android con los SDK completos dentro de este contenedor. La validación final de toolchain se realiza con `INICIAR.bat` en Windows, reutilizando `.tools`. Se hicieron validaciones estructurales y de referencias para reducir errores antes de esa compilación.
