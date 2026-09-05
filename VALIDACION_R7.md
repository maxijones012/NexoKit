# Validación R7

Fecha de preparación: 2026-09-05.

## Versiones

- Desktop: **0.7.0 / R7**
- Android: **0.7.0 / R7**
- Modo principal: **Wi‑Fi Sensing directo**
- CSI/ESP32: opcional, no requerido

## Validaciones realizadas

- `MainWindow.xaml`: XML/XAML bien formado.
- `App.xaml`: XML/XAML bien formado.
- `activity_main.xml`: XML bien formado.
- `AndroidManifest.xml`: XML bien formado.
- 30 handlers `Click=` de Desktop encontrados en `MainWindow.xaml.cs`.
- 68 nombres `x:Name` Desktop, sin duplicados.
- 71 IDs Android, sin duplicados.
- Las 71 referencias `R.id.*` de `MainActivity.kt` existen en el layout.
- Balance de llaves/paréntesis/corchetes revisado en los archivos C# y Kotlin modificados.
- No se detectaron strings sin cerrar en los archivos modificados.
- `WifiSensingEngine.kt` compilado aisladamente con `kotlinc` y ejecutado con datos simulados.
- El motor clasifica línea base, variación leve, actividad RF alta y movimiento probable según desvío sostenido.

## Cambios funcionales validados

- R7 arranca en `WI‑FI DIRECT` en lugar de depender de ESP32.
- Calibración de 25 muestras (~25 s a una muestra por segundo).
- Línea base RSSI + ruido estimado.
- Suavizado móvil de 5 muestras.
- Métricas: RSSI actual, línea base, Δ RSSI, score y confianza RF.
- Estados: `AMBIENTE ESTABLE`, `VARIACIÓN LEVE`, `ACTIVIDAD RF ALTA`, `MOVIMIENTO PROBABLE`.
- La escena real representa una nube/actividad RF; no dibuja una persona en base a RSSI.
- El modo DEMO conserva figuras simuladas sólo como demostración visual.
- El receptor CSI UDP 5005 sigue incluido como hardware avanzado opcional y no pisa la interfaz Wi‑Fi directa.

## Limitación de validación

En este entorno no está instalado el SDK completo de .NET/Android usado por el bootstrap del proyecto, por lo que el `publish` WPF y el APK final deben validarse con `INICIAR.bat` en Windows. La estructura, XAML/XML, referencias, motor Kotlin y consistencia interna sí fueron revisados antes de empaquetar.
