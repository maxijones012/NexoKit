# Validación R5

- XML Android válido.
- XAML Desktop válido.
- 26 handlers WPF referenciados y encontrados en `MainWindow.xaml.cs`.
- 59 IDs Android usados por Kotlin existen en `activity_main.xml`.
- `CsiUdpReceiver.kt` compilado aisladamente con `kotlinc` sin errores.
- Parser implementado para RuView ADR-018 `0xC5110001` y Vitals `0xC5110002`.
- UDP 5005 disponible en Desktop y Android.
- Versiones: Desktop 0.5.0 / Android 0.5.0 / R5.

## Límite de validación

El contenedor no dispone del SDK .NET/WPF completo ni del Android SDK configurado para ejecutar la compilación final de ambas apps. Esa validación final se realiza con `INICIAR.bat` en Windows. Tampoco se pudo validar detección física porque requiere ESP32-S3 real y un ambiente de prueba.
