# Validación R5 FIX4

## Cambios principales
- Desktop prioriza una interfaz Wi‑Fi activa con gateway antes que Ethernet/adaptadores virtuales.
- Datos básicos obtenidos por .NET: IP/prefijo, gateway, DNS, MAC, tipo/descripcion de adaptador y velocidad.
- Perfil de red complementado con Get-NetConnectionProfile cuando Windows lo permite.
- Datos Wi‑Fi complementados con netsh: SSID, BSSID/AP, señal, canal, radio, autenticación y RX/TX.
- MAC del gateway consultada desde ARP después de un ping corto.
- Monitor RSSI y escaneo visible se inician automáticamente cuando hay datos disponibles.
- Botón directo a Privacidad > Ubicación de Windows para habilitar SSID/RSSI si el sistema los restringe.
- Android carga IP/gateway/DNS antes de pedir permisos de Wi‑Fi; los permisos ya no dejan la pantalla básica vacía.
- Android inicia monitor RSSI y receptor CSI automáticamente cuando corresponde.
- Corregidos strings CSI Kotlin que estaban partidos entre líneas.

## Controles realizados
- XML/XAML parseado correctamente.
- 26 handlers WPF referenciados y encontrados.
- 61 IDs Android referenciados y presentes en layout.
- MainActivity.kt: validación sintáctica con kotlinc sin errores de parser (las referencias Android requieren el SDK real para compilación completa).
- C# revisado con control léxico de strings/comentarios/delimitadores, sin cadenas sin cerrar.

## Validación final pendiente en Windows
El build real WPF/Android debe ejecutarse con INICIAR.bat usando los SDK portátiles del proyecto. Esa compilación es la validación definitiva de APIs/plataforma.
