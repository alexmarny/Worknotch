# Worknotch Notch

Un notch negro que sale del borde de arriba, tarjetas de grafito y una pantalla de puntos ámbar. Es el diseño de Worknotch, y aquí está lo necesario para seguir construyéndolo sin perder el estilo.

## Contenido y tono

- Español de España, en segunda persona y sin rodeos: «3 por hacer hoy», «Nada para hoy. Escribe abajo tu primera tarea».
- Las cifras son las del día de verdad, nunca de ejemplo. Si un dato no existe, no se enseña su hueco: sin racha no hay línea de racha.
- Sin género en los mensajes dirigidos a quien usa la app.
- Las tareas se llaman como las escribió su dueño: nunca se reescribe un título.

## Textos e interacción

Valen para todo lo nuevo que entre en la app.

- **No expliques la tarjeta.** Su nombre y lo que hay dentro ya lo dicen. Una frase solo se queda si cuenta algo que hace falta y no se puede deducir, como «Pulsa uno para copiarlo. Con doble clic se abre».
- **Escribe para quien usa la app, no para quien programa.** Nada de archivos, procesos, JSON ni «el programa». Los errores no hablan en primera persona y dicen qué hacer después: «Esa hora ya ha pasado. Elige una más tarde».
- **Cada cosa, una vez.** Si un dato ya se ve en la misma vista, no se repite.
- **Lo que se pulsa se nota sin explicarlo y sin pasar el ratón.** Fondo `wn-relieve`, ficha o icono. Un botón dice lo que hace («Usar cronómetro»), no el estado en el que está.
- **Una acción se llama igual en todas partes.** «Hecha» en la lista, en el foco y en el aviso; «Quitar» para tareas y pasos; «Descolgar» para recortes.
- **Lo vacío invita a actuar.** Una lista vacía dice qué escribir o qué pasará, no solo que está vacía.
- **Antes de quitar un texto, comprueba qué enseñaba.** Si sin él alguien ya no encuentra una función, primero se añade la señal.

## Construcción

- **Colores, siempre de un token** y con `DynamicResource`: nunca un color escrito a mano, ni en XAML ni en C#. `Servicios/Tema.cs` los pone desde `tokens.json`.
- **Foco visible con teclado.** Todo lo que se pulsa lleva `FocusVisualStyle` (el estilo `Foco`); los campos cambian su borde a `wn-ambar`.
- **Nombre para lectores de pantalla.** Un botón de solo icono lleva `AutomationProperties.Name` y `ToolTip`.
- **Campos:** estilo `Campo`, con la ayuda en `Tag` acabada en «…» y `AutomationProperties.Name`.
- **Tamaño de pulsación:** nada que se pulse mide menos de 20 × 20; los botones, 28 de alto.
- **Animación:** solo crece el notch (220 ms) y aparece el panel (260 ms). Nada se mueve solo, y con las animaciones de Windows apagadas el cambio es directo.
- **Formatos:** horas y fechas con `Texts` (`Texts.Time`, `Texts.Duration`, `Planner.DayName`), siempre en español aunque Windows esté en otro idioma.
- **La app no pide atención.** No roba el foco al abrirse con el ratón, se aparta de lo que esté a pantalla completa y solo suena cuando llega un aviso.

## Fundamentos visuales

**Dos materiales.** El notch (`wn-notch`) es el borde de la pantalla prolongado: negro puro en Noche, sin degradados ni sombra. El contenido va en tarjetas (`wn-tarjeta`), y dentro de ellas lo que se toca va sobre `wn-relieve`.

**Dos temas.** Noche es el principal y Día es su versión clara. Se elige en Ajustes o se sigue a Windows. Cada token tiene valor en los dos.

**Un solo acento.** `wn-ambar` marca la selección, el botón principal de cada vista y la hora de un aviso. No hay segundo color de marca. `wn-alta` y `wn-media` son solo para la prioridad y lo vencido; `wn-hecho`, solo para lo terminado.

**La pantalla de puntos.** El temporizador es lo único con carácter propio: cifras de 5 × 7 puntos (`DotDisplay`), ámbar sobre negro en los dos temas, con los puntos apagados a la vista. No se usa para nada más que el tiempo de foco (en la tarjeta y, en pequeño, en la pastilla) y el icono.

**Una letra, tres tamaños.** Segoe UI Variable: 12 para datos y ayudas, 14 para lo que se lee y 18 para el día que se está viendo y el título de Ajustes. Nada visible baja de 12. Sin mayúsculas sostenidas.

**Marcos, no sombras.** Las tarjetas se separan por su fondo; el notch lleva un filete (`wn-filete`) para distinguirse de un fondo de pantalla oscuro. Radios: 22 el notch abierto, 14 las tarjetas y las fichas, 8 lo que se pulsa.

**Movimiento.** Una sola transición: el notch crece desde la pastilla y el contenido aparece cuando ya tiene sitio.

## Componentes

Pastilla, Panel, Aviso, Tarjeta (`Tarjeta`), Ficha de día (`Ficha`), Fila de tarea, Casilla (`Casilla`), Campo (`Campo`), Botón (`Boton`, `BotonPrincipal`, `BotonIcono`), Interruptor (`Interruptor`), Pantalla de puntos (`DotDisplay`), Columna de día (la gráfica de Semana). Los estilos están en `src/Worknotch.App/Estilos.xaml`.

Los iconos son de trazo, a 16 px, en el color del texto de su botón (estilo `Icono`).

## Reglas al añadir algo

1. Busca primero el componente en la lista de arriba.
2. Toma los colores de `tokens.json`. Si falta uno, se añade ahí con su uso, para los dos temas, y con su contraste en `contrastes`.
3. Comprueba 4,5:1 de contraste en los dos temas.
4. Que quepa: el panel no pasa del ancho de la pantalla, y lo que no cabe se desplaza dentro de su tarjeta.
5. No quites función por estilo: si algo cambia de forma, debe seguir pudiendo hacerse.
6. Cumple «Textos e interacción» y «Construcción», y pasa `dotnet test` y la prueba de humo.

## En el código

`docs/design-system/tokens.json` es la fuente de los colores: va dentro de `Worknotch.Core` y `Servicios/Tema.cs` lo convierte en recursos de la aplicación. `build/make_ico.py` dibuja el icono con esos mismos colores.
