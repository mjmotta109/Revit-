# Link Workset Inspector — ¿En qué workset está mi vínculo?

[![Compilar](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml/badge.svg)](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml)

Add-in para **Autodesk Revit** que responde una pregunta muy concreta:

> *"Tengo un vínculo que no aparece en el modelo, pero sí está registrado en
> Administrar vínculos. ¿En qué workset está, para abrir **solo ese** workset
> en lugar de abrirlos todos y sobrecargar el proyecto?"*

## El problema que resuelve

En modelos de trabajo compartido (worksharing), cuando un vínculo RVT o CAD está
en un **workset cerrado**, Revit ni siquiera lo carga: no se ve en ninguna vista,
aunque sigue apareciendo registrado en *Administrar vínculos*. La "solución"
habitual —abrir todos los worksets— penaliza mucho el rendimiento en proyectos
grandes.

Esta herramienta recorre **todos los tipos de vínculo del documento, estén
cargados o no** (los elementos `RevitLinkType` / `CADLinkType` siempre están en
la base de datos del modelo, aunque su workset esté cerrado), y te dice
exactamente qué workset abrir.

## Qué muestra

Un botón **"¿Dónde está el vínculo?"** en la pestaña de ribbon **MJ Tools ▸
Vínculos** abre una tabla con una fila por instancia de vínculo (RVT y CAD):

| Columna | Contenido |
|---|---|
| Vínculo | Nombre del archivo vinculado |
| Formato | RVT o CAD (DWG, DXF, DGN…) |
| Estado de carga | Cargado / Descargado / Descargado (solo local) / No encontrado / **En workset cerrado** / Anidado |
| Workset del vínculo (tipo) | Workset del *tipo* de vínculo, con su estado `[abierto]` / `[CERRADO]`. Este es el que decide si Revit carga el archivo |
| Workset de la instancia | Workset de la *instancia* colocada, con su estado. Este es el que decide si la ves una vez cargado |
| ¿Visible en vista activa? | Sí / No, revisando workset oculto en la vista, visibilidad global del workset, categoría oculta, elemento oculto manualmente, opciones de diseño y vínculos CAD de "solo vista actual" |
| Diagnóstico — qué hacer | Explicación en claro del motivo y el paso exacto para solucionarlo |

Además:

- **Resumen superior**: "Worksets CERRADOS con vínculos afectados — abre solo
  estos: …" → la respuesta directa, sin buscar en la tabla.
- Filas con problema resaltadas en rojo y ordenadas primero.
- Casilla "Mostrar solo vínculos con problemas".
- **Doble clic** en una fila: detalle con la ruta completa del archivo.
- Botón **Copiar tabla**: copia todo en formato tabulado (pegable en Excel).

La herramienta es **solo lectura**: no abre transacciones ni modifica el modelo.

> Nota: la API de Revit no permite abrir un workset cerrado en un documento ya
> abierto, así que el último paso es manual (y rápido): **Colaborar ▸ Worksets ▸
> seleccionar el workset indicado ▸ Abrir**. Al abrir el modelo también puedes
> usar el desplegable del botón *Abrir ▸ Worksets ▸ Especificar…* para abrir
> solo los worksets que necesitas.

## Requisitos

- Autodesk Revit **2021 a 2026** (Windows).
- Para compilar: [Visual Studio 2022](https://visualstudio.microsoft.com/es/vs/community/)
  (edición Community es gratuita) con la carga de trabajo **Desarrollo de
  escritorio de .NET**, o el SDK de .NET si prefieres línea de comandos.
  Las referencias a la API de Revit se descargan solas desde NuGet
  (paquetes `Nice3point.Revit.Api.*`); no hace falta copiar ninguna DLL.

## Instalar sin compilar (recomendado)

Cada cambio en el repositorio se compila automáticamente para Revit 2021–2026
(pestaña **Actions** de GitHub). Para instalar sin abrir Visual Studio:

1. Entra en [Actions ▸ Compilar](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml)
   y abre la ejecución más reciente con ✅.
2. En la sección **Artifacts** descarga `LinkWorksetInspector-Revit<tu versión>`
   (por ejemplo `LinkWorksetInspector-Revit2025`).
3. Descomprime el zip dentro de `%AppData%\Autodesk\Revit\Addins\<versión>\`
   (pega esa ruta en el Explorador de Windows). Debe quedar así:

   ```
   %AppData%\Autodesk\Revit\Addins\2025\LinkWorksetInspector.addin
   %AppData%\Autodesk\Revit\Addins\2025\LinkWorksetInspector\LinkWorksetInspector.dll
   ```

4. Arranca Revit y acepta el aviso de add-in la primera vez.

> Nota: GitHub pide iniciar sesión (una cuenta gratuita) para descargar artifacts.

## Compilar e instalar

1. Clona o descarga este repositorio.
2. Abre `LinkWorksetInspector.sln` en Visual Studio.
3. Si tu Revit **no** es 2024, cambia la propiedad `RevitVersion` al inicio de
   `src/LinkWorksetInspector/LinkWorksetInspector.csproj` (por ejemplo `2023` o `2025`).
4. Compila en `Release` (Compilar ▸ Compilar solución).

Por línea de comandos es una sola orden (ejemplo para Revit 2025):

```bat
dotnet build -c Release -p:RevitVersion=2025
```

Al compilar en Windows, el proyecto **se instala solo**: copia la DLL y el
manifiesto `.addin` a `%AppData%\Autodesk\Revit\Addins\<versión>\`. Arranca
Revit, acepta el aviso de add-in la primera vez, y verás la pestaña **MJ Tools**.

### Instalación manual (si compilaste en otra máquina)

Copia a `%AppData%\Autodesk\Revit\Addins\<versión>\`:

```
LinkWorksetInspector.addin              (desde manifest/)
LinkWorksetInspector\LinkWorksetInspector.dll   (desde src/.../bin/Release/)
```

## Uso

1. Abre el modelo (con tu selección habitual de worksets, no hace falta abrirlos todos).
2. **MJ Tools ▸ Vínculos ▸ ¿Dónde está el vínculo?**
3. Mira el resumen superior: ahí están los worksets cerrados que bloquean vínculos.
4. **Colaborar ▸ Worksets**, abre solo ese workset, y el vínculo aparecerá
   (si además estaba descargado, recárgalo en *Administrar vínculos*).

## Diagnósticos que detecta

| Situación | Diagnóstico |
|---|---|
| Workset del tipo de vínculo cerrado (`En workset cerrado`) | El vínculo ni se cargó; indica el workset exacto a abrir |
| Workset de la instancia cerrado | El archivo está cargado pero la instancia no se ve; indica el workset a abrir |
| Vínculo descargado / descargado solo local | Recargar desde Administrar vínculos |
| Archivo no encontrado | Ruta rota; corregir en Administrar vínculos |
| Workset oculto en la vista activa (V/G ▸ Worksets) o sin "Visible en todas las vistas" | Indica dónde reactivarlo |
| Categoría oculta, elemento oculto manualmente, opción de diseño no principal, CAD de "solo vista actual" | Indica el motivo y el remedio |
| Tipo registrado sin ninguna instancia colocada | Avisa de que la instancia pudo borrarse aunque el archivo siga en Administrar vínculos |
| Vínculo anidado | Se controla desde el vínculo padre |

## Estructura del código

```
manifest/LinkWorksetInspector.addin        Manifiesto del add-in
src/LinkWorksetInspector/
  App.cs                                   IExternalApplication: ribbon y botón
  RibbonIconFactory.cs                     Icono del botón dibujado en memoria
  Commands/InspectLinkWorksetsCommand.cs   IExternalCommand: orquesta análisis + ventana
  Services/LinkWorksetAnalyzer.cs          Toda la lógica de análisis y diagnóstico
  Models/LinkReportRow.cs                  Fila del informe
  UI/LinkWorksetForm.cs                    Ventana WinForms con la tabla
```
