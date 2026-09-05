# Validación R9

## Estructura
- Desktop: 0.9.0
- Android: 0.9.0
- Revisión: R9
- Nueva sección: `Descubrir`
- Fuente inicial: `Astrosp/Awesome-OSINT-List`

## Validaciones realizadas
- `MainWindow.xaml`, `App.xaml`, Manifest y layouts Android: XML/XAML válido.
- 42 handlers `Click=` de Desktop: todos encontrados en `MainWindow.xaml.cs`.
- 89 IDs declarados en Android y 89 referencias `R.id`: sin referencias faltantes.
- Balance básico de llaves C#/Kotlin: correcto.
- Versiones unificadas en `version.json`, Desktop y Android.

## Comportamiento esperado
- La primera revisión de un catálogo crea la línea base y NO marca cientos de recursos como nuevos.
- Revisiones posteriores comparan los enlaces GitHub del README y marcan sólo altas nuevas.
- `Descubrir` no descarga ni ejecuta herramientas automáticamente.
- En Desktop, un recurso descubierto puede agregarse a `Actualizaciones`; entra con `AutoDownload=false` / `SOLO AVISA`.
- En Android, el chequeo de catálogos se ejecuta junto con el trabajo periódico de actualización de repositorios.

## Nota
No se ejecutó en este entorno un build WPF/Android completo con los SDK de Windows/Android. La validación final de compilación se realiza con `INICIAR.bat` en Windows, reutilizando `.tools` si ya existe.
