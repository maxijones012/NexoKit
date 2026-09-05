# Validación R4

Fecha: 2026-08-31

## Validaciones realizadas
- XML/XAML parseado correctamente:
  - Desktop/App.xaml
  - Desktop/MainWindow.xaml
  - Android AndroidManifest.xml
  - Android activity_main.xml
- 24/24 handlers declarados en XAML tienen método correspondiente en MainWindow.xaml.cs.
- 53/53 referencias `R.id.*` usadas por MainActivity existen en el layout Android.
- Balance estructural de llaves/paréntesis validado en los archivos C# principales.
- `NetworkToolkit.kt` compilado con Kotlin/JVM y probado con casos CIDR /0, /8, /16, /24, /31 y /32.
- Prueba de normalización MAC/OUI superada.
- MainActivity + fuentes Android: sin errores sintácticos Kotlin detectables; las referencias Android no se pueden resolver en esta máquina sin el SDK Android completo.
- Versiones consistentes: Desktop 0.4.0 / Android 0.4.0 / R4.
- Scripts Android generan/instalan `KitHerramientas_Android_0.4.0_R4.apk`.

## Validación pendiente en el equipo Windows del usuario
Ejecutar:

1. `INICIAR.bat`
2. opción 2 — Compilar Desktop
3. opción 3 — Compilar Android

El entorno `.tools` ya existente en la carpeta anterior puede reutilizarse si se aplica el parche R3 FIX2 → R4.

## Notas funcionales
- Descubrimiento LAN limitado al segmento local /24.
- No realiza escaneo de puertos.
- Windows combina respuestas ICMP con vecinos visibles en ARP.
- Android no promete MAC de terceros porque el sistema puede restringirla.
- Traceroute Android es experimental y depende del comando `/system/bin/ping` y del soporte de TTL del fabricante.
