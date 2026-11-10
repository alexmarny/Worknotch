<p align="center"><img src="build/icono/icono.png" width="128" height="128" alt=""></p>

<h1 align="center">Worknotch</h1>

<p align="center">
Tu día en el borde de arriba de la pantalla: tareas, foco, notas y recortes,<br>
a un gesto desde cualquier programa de Windows.
</p>

---

## Qué hace

- **Vive arriba, en una pastilla.** Pasa el puntero por ella o pulsa `Ctrl+Alt+Espacio` y se abre el panel. Se aparta sola de lo que esté a pantalla completa y sigue al puntero entre pantallas.
- **Tareas por días.** Hoy, los próximos días y una bandeja para lo que aún no tiene día. Lo que no hagas hoy te espera mañana.
- **Escribe como hablas.** «Llamar a Marta mañana 17:30 !alta #Casa» pone el día, el aviso, la prioridad y el proyecto. También entiende «viernes», «20/11», «cada semana», «45m» y «sin día».
- **Foco.** Cuenta atrás o cronómetro, sobre una tarea o sin ninguna, con una pantalla de puntos que también se ve en la pastilla. Al terminar te avisa.
- **Avisos en el propio notch**, con «Empezar foco», «Avisar en 5 min» y «Hecha».
- **Notas** siempre a mano.
- **Recortes.** Lo que copias y tus capturas de pantalla se cuelgan solos; pulsa uno para volver a copiarlo o arrástralo a otro programa.
- **Semana.** La bandeja y los próximos días, una columna cada uno, para repartir la semana de un vistazo.
- **Progreso.** Tareas hechas y minutos de foco de los últimos siete días, tu racha y tu mejor hora.
- **Claude puede planificar por ti.** Conéctalo una vez desde Ajustes y pídele «planifica mi tarde: informe, gimnasio y compra».
- **Enlaces para scripts:** `worknotch://add?title=Comprar%20pan&day=mañana&time=17:30`.

## Instalar

1. Descarga **Worknotch-setup.exe** de la [última versión](https://github.com/alexmarny/Worknotch/releases/latest) y ábrelo.
2. Windows dirá **«Windows protegió su PC»**, porque el programa no está firmado. Pulsa **«Más información»** y después **«Ejecutar de todas formas»**.
3. Pulsa **«Instalar»**. No pide permisos de administrador: se instala solo para tu usuario.

Funciona en Windows 10 y 11 de 64 bits y no necesita instalar nada más. Para quitarla: *Configuración → Aplicaciones → Worknotch → Desinstalar*. Tus tareas y notas se quedan en `%APPDATA%\Worknotch` por si vuelves a instalarla; borra esa carpeta si no las quieres.

## Conectar Claude

En **Ajustes**, pulsa **«Copiar la orden para conectar Claude»** y pégala en una terminal. Es una orden como esta:

```
claude mcp add worknotch -- "%LOCALAPPDATA%\Programs\Worknotch\worknotch-mcp.exe"
```

Después, en una sesión nueva de Claude Code, prueba «¿qué tengo para hoy en Worknotch?» o «empieza 25 minutos de foco con el informe». Lo que cambie Claude aparece en el notch al momento.

## Actualizaciones

Al abrirse mira si hay una versión nueva y, si la hay, arriba del panel aparece **«Actualizar a la X.Y.Z»**. Al pulsarlo la descarga, comprueba que el archivo está íntegro, la instala y se vuelve a abrir sola.

## Privacidad

- Todo se guarda en tu ordenador, en `%APPDATA%\Worknotch`.
- Solo se conecta a internet para preguntar a GitHub si hay una versión nueva. Se puede apagar en Ajustes, y sin internet funciona igual.
- Lo que un gestor de contraseñas marca como privado al copiarlo no se cuelga en Recortes.

## Para desarrollar

`CONTEXTO.md` explica cómo está montado y `docs/design-system/README.md` es la guía de diseño. Las pruebas están descritas en `tests/README.md`.

```
dotnet test                                     # núcleo y criterios de diseño (Windows o Linux)
dotnet run --project src/Worknotch.App          # la app (solo Windows)
```

---

<sub>Proyecto personal e independiente.</sub>
