---
name: publicar-version
description: Publica una versión nueva de Worknotch (vX.Y.Z) siguiendo el proceso del repo: cambios a main por PR con la CI en verde, merge con rebase, etiqueta anotada en main y comprobación de que la versión sale con su instalador. Úsala cuando pidan «despliega», «publica», «saca la versión», «nueva versión», «release» o «sube la vX.Y.Z».
---

# Publicar una versión de Worknotch

Lo que publica es **la etiqueta `vX.Y.Z` en `main`**: el workflow `Compilar` (`.github/workflows/compilar.yml`) pasa las pruebas del núcleo, compila la app en Windows, pinta la interfaz en los dos temas y comprueba sus criterios, compila el instalador, lo instala encima de la versión anterior, arranca la app instalada y la desinstala. Si todo pasa, crea la versión en este mismo repo, que es donde la app mira si hay actualizaciones. Las notas se generan solas en la CI; no hay que escribirlas.

Publicar es irreversible para quien la tiene instalada (la app avisa y se actualiza con un botón), así que **no etiquetes nada que no esté en `main` con la CI en verde**.

## Beta (vX.Y.Z-beta.N)

Una etiqueta con guion sale como versión previa y no pasa a ser «la última», así que la app instalada no la ve como actualización: se descarga a mano desde la página de versiones. Usa la misma carpeta y los mismos datos que la estable (no conviven). Se etiqueta sobre la rama que se quiera probar, sin pasar por `main`. El resto de esta guía es para la estable.

## 1. Ver qué hay que publicar

```bash
git fetch --tags origin
git tag --sort=-v:refname | head -3          # última versión
git log --oneline origin/main..HEAD           # lo que aún no está en main
git log --oneline <última-etiqueta>..origin/main   # lo que ya está en main sin publicar
```

- Si los cambios están en una rama vieja cuyos commits ya entraron en `main` con otros hashes (las PR se mergean con rebase), no la uses tal cual: crea una rama nueva desde `origin/main` y aplica solo los commits nuevos con `git cherry-pick`.
- Si no hay nada nuevo respecto a la última etiqueta, dilo y para.

## 2. Elegir el número

- **Menor** (`v1.1.0` → `v1.2.0`): funciones o tarjetas nuevas.
- **Parche** (`v1.1.0` → `v1.1.1`): arreglos y retoques sin funciones nuevas.
- Mayor: solo si lo pide el usuario.

Si dudas entre menor y parche, propón uno y confírmalo con el usuario.

## 3. CONTEXTO.md

Las entradas nuevas de `CONTEXTO.md` llevan «(DD-MM-AAAA, sin publicar)». Antes de publicar, en la misma rama de la PR, cámbialas por la versión, por ejemplo «(10-11-2026, v1.0.0)», y haz un commit aparte:

```
CONTEXTO: lo de <tema> sale en la vX.Y.Z
```

Si no hay entradas «sin publicar», no hace falta este commit.

## 4. PR a main

- Títulos y cuerpos en español, como los commits del repo: qué cambia, cómo se probó y «Sale en la vX.Y.Z.».
- Espera a que **todas** las comprobaciones de la PR estén en verde (núcleo y Windows; tardan varios minutos). Si alguna falla, arréglala en la rama; no mergees en rojo.
- Si el cambio es visible, descarga el artefacto «capturas» de la ejecución y mira las vistas afectadas en Noche y en Día antes de mergear.

## 5. Merge con rebase

El historial de `main` es lineal: siempre rebase, nunca squash ni commit de merge.

## 6. Etiqueta anotada en main

La etiqueta va sobre `origin/main` recién actualizado, con un mensaje que resume la versión en una línea (es lo que se ve en `git tag -n1`):

```bash
git fetch origin
git tag -a vX.Y.Z origin/main -m "vX.Y.Z: <lo nuevo, en una línea>"
git push origin vX.Y.Z
```

## 7. Comprobar que sale

- La ejecución de la etiqueta tarda bastante (instala la versión anterior en Windows y comprueba que la nueva la actualiza). Espera a que termine; no la sondees en bucle.
- Al terminar, comprueba que la versión existe con `Worknotch-setup.exe` y `SHA256SUMS.txt`.
- Si falla el job `publicar` u otro, lee el log y cuéntaselo al usuario antes de tocar nada. No borres ni muevas una etiqueta ya publicada sin que lo pida.

## 8. Al acabar

Dile al usuario la versión, el enlace a la versión y que la Worknotch instalada enseñará «Actualizar a la X.Y.Z» la próxima vez que se abra.
