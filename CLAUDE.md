# Worknotch

Antes de añadir o cambiar algo visible, lee `docs/design-system/README.md`: sus criterios valen para todo lo nuevo, también para textos, botones y tarjetas. `CONTEXTO.md` explica cómo está montado el proyecto.

Lo que se puede comprobar solo está en `tests/Worknotch.Tests/DisenoTests.cs` (sin pintar nada) y en `Criteria()` de `src/Worknotch.App/Humo.cs` (sobre la interfaz pintada, en Windows), y la CI no publica si fallan. Si algo nuevo no pasa, se arregla lo nuevo: no se añaden excepciones a las pruebas.

Antes de dar por acabado un cambio visible, compruébalo con las capturas de `Worknotch.exe --humo` en Noche y en Día (la CI las deja en el artefacto «capturas») y pasa `dotnet test`.

La app solo se ejecuta en Windows. Desde Linux se puede compilar (`dotnet build`, con el SDK de Microsoft, no el de la distribución) y pasar las pruebas del núcleo, pero no abrirla: lo visible se comprueba en la CI.
