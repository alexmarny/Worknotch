Worknotch.Tests: pruebas del núcleo con xUnit (las usa la CI, job «nucleo»). No necesitan Windows.
  dotnet test tests/Worknotch.Tests

  EntradaRapidaTests   lo que se entiende de una línea escrita («mañana 17:30 !alta #Casa»)
  PlanificadorTests    añadir, completar, repetir, mover, avisos y el paso de día
  TemporizadorTests    cuenta atrás, cronómetro, pausas y el foco que se apunta
  EstadisticasTests    semana, racha y hora punta
  AlmacenTests         guardado, deshacer, archivo dañado y dos procesos a la vez
  McpTests             el servidor de Claude: protocolo y cada herramienta
  VariosTests          enlaces worknotch://, actualización (versiones y sumas) y la pantalla de puntos
  DisenoTests          la guía de diseño hecha pruebas: contrastes de los tokens en los dos temas y,
                       sobre el XAML y el C# de la app, colores solo por token, nada por debajo de
                       12 px y campos con ayuda y nombre

Prueba de humo de la interfaz (la usa la CI, job «windows»; solo en Windows):
  Worknotch.exe --humo carpeta
Pinta cada vista en Noche y en Día con datos de ejemplo y la hora fija, deja un PNG por vista y
humo.log, y comprueba sobre lo pintado: tamaño de letra, colores de token, botones con nombre y
tamaño de pulsación, y que ningún texto se salga del notch. Sale con 1 si algo falla.
La CI sube las capturas como artefacto «capturas».

En la CI, además, se instala la app con el instalador, se arranca, se le manda un enlace
worknotch://, se habla con worknotch-mcp.exe por la entrada estándar y se desinstala.

WORKNOTCH_DATOS=carpeta hace que la app y worknotch-mcp usen esa carpeta en vez de %APPDATA%\Worknotch.
