# Worknotch — contexto para continuar

## Qué es

Una app de Windows que vive pegada al borde de arriba de la pantalla, como el notch de un portátil: una pastilla que se abre en un panel con tareas por días, temporizador de foco, bloc de notas, recortes y resumen de la semana. Es una app propia, con nombre, icono, diseño y código originales; la idea de partida fue tener en Windows lo que hacen las apps de notch del Mac.

Uso personal. Repo público `alexmarny/Worknotch`, que también sirve las versiones.

## Estado actual (10-11-2026, sin publicar)

Primera versión completa, escrita en una sesión sin Windows delante: **nadie la ha usado todavía en un PC de verdad**. Lo comprobado es lo que cubren las pruebas del núcleo y la CI en Windows (ver «Cómo probar»). Lo que falta por mirar a mano está en «Pendiente de probar en un PC».

### Funcionalidades

- Pastilla arriba en el centro; se abre al pasar el puntero, con el atajo global (por defecto `Ctrl+Alt+Espacio`, se elige entre cinco) o desde el icono junto al reloj. `Esc` la cierra.
- Tareas: hoy, seis días siguientes, bandeja sin día e historial de días pasados (solo consulta), con pestañas «Por hacer» y «Hechas». Prioridad, proyecto, aviso, repetición, fecha límite, minutos de foco, pasos y notas. Entrada rápida en una línea. Subir, bajar, mover de día, deshacer (`Ctrl+Z`, 30 pasos).
- Paso de día: lo pendiente se trae a hoy y recuerda de qué día viene (se puede apagar).
- Foco: cuenta atrás o cronómetro, con o sin tarea, pausa, «5 min más», y el tiempo queda apuntado en la tarea y en la semana.
- Avisos en el propio notch (recordatorio y final del foco), con sonido opcional.
- Notas (se guardan solas), Recortes (portapapeles, capturas de pantalla, arrastrar y soltar), Progreso (gráfica de siete días, racha, mejor hora).
- Vista Semana: la bandeja y cuatro días, una columna cada uno, para repartir tareas (se mueven con el menú de cada una).
- Ajustes: tema, pantalla, atajo, tarjetas a la vista, arranque con Windows y demás.
- Servidor MCP para Claude (`worknotch-mcp.exe`, 16 herramientas) y enlaces `worknotch://`.
- Actualización desde la app, con suma SHA-256 comprobada.

## Arquitectura

Solución .NET 8 (`Worknotch.sln`), cuatro proyectos:

- **`src/Worknotch.Core`** (net8.0, sin Windows): todo lo que no es pintar.
  - `Modelos.cs`: `AppData` (tareas, proyectos, notas, sesiones de foco, temporizador, recortes, ajustes).
  - `EntradaRapida.cs`: `QuickAdd.Parse`, la línea escrita → título, día, hora, prioridad, proyecto, repetición, minutos. Lo que no entiende se queda en el título.
  - `Planificador.cs`: `Planner`, las operaciones sobre tareas (añadir, completar con repetición, mover, avisos, paso de día). `Texts` da los formatos en español.
  - `Temporizador.cs`: `FocusTimer`. No tiene reloj propio: guarda cuándo empezó el tramo y lo acumulado.
  - `Estadisticas.cs`, `Enlace.cs` (worknotch://), `Actualizacion.cs` (versiones y sumas), `MatrizPuntos.cs` (cifras 5 × 7), `Tokens.cs` (colores y contraste).
  - `Almacen.cs`: `Store`, un JSON (`%APPDATA%\Worknotch\datos.json`). Cada cambio es leer-modificar-escribir con un cerrojo entre procesos (`datos.json.lock`), escritura en temporal y renombrado. Un archivo que no se entiende se aparta como `datos.json.dañado-FECHA`.
  - `Mcp/ServidorMcp.cs`: JSON-RPC por entrada y salida estándar.
- **`src/Worknotch.Mcp`**: el ejecutable `worknotch-mcp.exe`, cuatro líneas sobre `McpServer`.
- **`src/Worknotch.App`** (net8.0-windows, WPF): la ventana.
  - `NotchWindow.xaml` y sus partes `.cs`: estado y colocación (`NotchWindow.xaml.cs`), tareas y detalle (`.Tareas.cs`), foco y pastilla (`.Foco.cs`), notas, recortes, semana, ajustes y avisos (`.Otros.cs`).
  - `Estilos.xaml`: los componentes. `Controles/PantallaPuntos.cs`: `DotDisplay`.
  - `Servicios/`: `Nativo.cs` (llamadas a Windows), `Tema.cs` (tokens → recursos), `Atajo.cs`, `Recortes.cs`, `Actualizador.cs`, `Sistema.cs` (arranque con Windows, instancia única).
  - `Humo.cs`: la prueba de humo de la interfaz.
- **`tests/Worknotch.Tests`**: xUnit.

`build/instalador.iss` (Inno Setup) y `build/make_ico.py` (icono). `docs/design-system/` (guía y tokens).

## Decisiones y convenciones

- **WPF y no WinUI 3:** WPF se puede compilar desde Linux (con `EnableWindowsTargeting`), que es donde trabaja Claude en la nube; WinUI no. Ventana sin bordes, transparente y siempre encima, fuera de Alt+Tab.
- **La ventana no cambia de tamaño:** es transparente y fija (1320 × 520); lo que crece es el borde `Shell`. Windows deja pasar los clics por donde no hay nada pintado.
- **Tarjetas de color con tinta:** los tokens se redefinen en los recursos de cada tarjeta (`Theme.CardScope`), así que los estilos no saben si están sobre el notch o sobre color.
- **Un solo latido** de un segundo (`Tick`) para todo lo que depende del tiempo: avisos, final del foco, cambio de día, cambios hechos por Claude, pantalla y pantalla completa.
- **Dos procesos, un archivo:** la app y `worknotch-mcp` no se hablan; comparten `datos.json` con cerrojo, y la app repinta cuando cambia la fecha del archivo. Así Claude funciona aunque la app esté cerrada.
- **Los errores de uso son `UserException`** con un texto que ya se puede enseñar; se muestran junto al campo o vuelven a Claude como resultado con `isError`.
- **.NET va dentro** (publicación autocontenida): el instalador pesa más, pero no hay que instalar nada antes.
- **Los datos no se borran al desinstalar.**
- Identificadores en inglés; comentarios, textos, commits y documentación en español de España.
- Los avisos del compilador son errores y el formato lo vigila `dotnet format` en la CI.
- Historial lineal en `main`: cambios por PR con rebase, versiones con etiqueta anotada (skill `publicar-version`).

## Limitaciones conocidas

- «mañana» siempre es el día siguiente: «Ir al médico por la mañana» se planifica para mañana y pierde esa palabra del título.
- Deshacer no devuelve una imagen copiada que se haya descolgado (su archivo se borra).
- El atajo global se elige de una lista; no se puede grabar uno cualquiera.
- Sin sincronización entre equipos ni con el móvil.
- El instalador no está firmado: Windows avisa al abrirlo.
- Una beta comparte carpeta y datos con la estable.

## Pendiente de probar en un PC (10-11-2026)

La CI comprueba que arranca, que cada vista se pinta y cumple los criterios, y que se instala y desinstala. No puede comprobar lo que depende de una persona delante:

- Que pasar el puntero abre y apartarlo cierra con naturalidad, y que no molesta al ir a las pestañas del navegador.
- El atajo global y que el panel coge el teclado al abrirse con él.
- Varias pantallas con distinta escala, y que se aparta de vídeos y juegos a pantalla completa.
- Recortes con el portapapeles de verdad (texto, imágenes de Win+Mayús+S, archivos) y arrastrar hacia otros programas.
- La carpeta de capturas: se vigilan `Imágenes\Screenshots`, `Imágenes\Capturas de pantalla` y `Vídeos\Captures`; en un Windows en español puede llamarse de otra forma.
- La actualización de una versión a la siguiente desde el botón (necesita dos versiones publicadas).

## Ideas pendientes (propuestas, sin elegir todavía)

- Dictar tareas con el reconocimiento de voz de Windows.
- Eventos del calendario junto a las tareas (Outlook o Google).
- Sincronizar con Microsoft To Do para tener las tareas en el móvil.
- Arrastrar tareas para reordenarlas y soltarlas sobre un día o una columna de Semana.
- Buscar entre tareas hechas; filtro por proyecto.
- Grabar cualquier atajo.

## Cómo probar

- Para ver la interfaz sin Windows: lanzar a mano el workflow `Compilar` sobre la rama (Actions → Compilar → Run workflow). Además del artefacto «capturas», deja los PNG en la rama `ci-capturas`, que se reescribe cada vez.
- Para compilar la app desde Linux hace falta el SDK de .NET de Microsoft (el de Ubuntu no trae `Microsoft.NET.Sdk.WindowsDesktop`): los paquetes `dotnet-sdk-8.0` y compañía de `packages.microsoft.com/ubuntu/22.04/prod`, extraídos con `dpkg -x`, sirven.

- `dotnet test`: núcleo y criterios de diseño que no necesitan pintar (Windows o Linux).
- `Worknotch.exe --humo carpeta`: cada vista en Noche y en Día, a PNG, con sus criterios (solo Windows; la CI sube las capturas).
- `WORKNOTCH_DATOS=carpeta`: usa esa carpeta de datos en vez de la de verdad.
- Detalle en `tests/README.md`.
