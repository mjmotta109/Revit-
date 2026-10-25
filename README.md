# MJ Tools para Revit — vínculos y alineación

[![Compilar](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml/badge.svg)](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml)

Add-ins para **Autodesk Revit** en la pestaña **MJ Tools**:

| Panel | Botón | Qué hace |
|---|---|---|
| Vínculos | **¿Dónde está el vínculo?** | Diagnóstico de vínculos y worksets (abajo) |
| Alinear | **Alinear objetos** | Panel de alinear y distribuir como el de Illustrator, pensado para mobiliario ([ver](#alinear-objetos--panel-estilo-illustrator)) |

## ¿Dónde está el vínculo? — Link Workset Inspector

Responde una pregunta muy concreta:

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
2. En la sección **Artifacts** descarga `MJTools-Revit<tu versión>`
   (por ejemplo `MJTools-Revit2025`). Trae todas las herramientas.
3. Descomprime el zip dentro de `%AppData%\Autodesk\Revit\Addins\<versión>\`
   (pega esa ruta en el Explorador de Windows). Debe quedar así:

   ```
   %AppData%\Autodesk\Revit\Addins\2025\LinkWorksetInspector.addin
   %AppData%\Autodesk\Revit\Addins\2025\LinkWorksetInspector\LinkWorksetInspector.dll
   %AppData%\Autodesk\Revit\Addins\2025\MJAlignTools.addin
   %AppData%\Autodesk\Revit\Addins\2025\MJAlignTools\MJAlignTools.dll
   ```

   Cada herramienta es un add-in independiente: si solo quieres una, copia
   solo su `.addin` y su carpeta.

4. Arranca Revit y acepta el aviso de add-in la primera vez.

> Nota: GitHub pide iniciar sesión (una cuenta gratuita) para descargar artifacts.

## Compilar e instalar

1. Clona o descarga este repositorio.
2. Abre `MjTools.sln` en Visual Studio.
3. Si tu Revit **no** es 2024, cambia la propiedad `RevitVersion` al inicio de
   cada `.csproj` de `src/` (por ejemplo `2023` o `2025`).
4. Compila en `Release` (Compilar ▸ Compilar solución).

Por línea de comandos es una sola orden por herramienta (ejemplo para Revit 2025):

```bat
dotnet build -c Release -p:RevitVersion=2025 src/LinkWorksetInspector/LinkWorksetInspector.csproj
dotnet build -c Release -p:RevitVersion=2025 src/MJAlignTools/MJAlignTools.csproj
```

Al compilar en Windows, cada proyecto **se instala solo**: copia la DLL y el
manifiesto `.addin` a `%AppData%\Autodesk\Revit\Addins\<versión>\`. Arranca
Revit, acepta el aviso de add-in la primera vez, y verás la pestaña **MJ Tools**.

### Instalación manual (si compilaste en otra máquina)

Copia a `%AppData%\Autodesk\Revit\Addins\<versión>\`:

```
LinkWorksetInspector.addin                       (desde manifest/)
LinkWorksetInspector\LinkWorksetInspector.dll    (desde src/LinkWorksetInspector/bin/Release/)
MJAlignTools.addin                               (desde manifest/)
MJAlignTools\MJAlignTools.dll                    (desde src/MJAlignTools/bin/Release/)
```

## Uso (vínculos)

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

## Alinear objetos — panel estilo Illustrator

**MJ Tools ▸ Alinear ▸ Alinear objetos** abre un panel flotante con la misma
distribución que el panel *Alinear* de Illustrator. Se queda abierto mientras
trabajas: seleccionas elementos en la vista, pulsas un botón y se mueven. Cada
pulsación es una sola operación de Revit, así que **Ctrl+Z** la deshace entera.

```
Alinear
  Alinear objetos:
    [izq] [centro H] [der]      [arriba] [centro V] [abajo]
  Distribuir objetos:
    [sup] [centros V] [inf]     [izq] [centros H] [der]
  Distribuir espaciado:         Alinear a:
    [vert] [horiz] [ Auto ]       [habitación] [selección] [objeto clave]
  Objeto clave: Mesa — 160x80 [12345]                [Elegir…]
  Alinear a la izquierda: 6 elemento(s) movido(s).
```

### Botones

| Grupo | Botón | Qué hace |
|---|---|---|
| Alinear objetos | Izquierda · Centro horizontal · Derecha | Lleva el borde izquierdo / el centro / el borde derecho de cada elemento a la misma línea vertical |
| | Arriba · Centro vertical · Abajo | Igual, en horizontal (bordes superiores, centros, bordes inferiores) |
| Distribuir objetos | Bordes superiores · Centros verticales · Bordes inferiores | Deja el **mismo paso** vertical entre esos bordes o centros |
| | Bordes izquierdos · Centros horizontales · Bordes derechos | Igual, en horizontal |
| Distribuir espaciado | Vertical · Horizontal | Deja el **mismo hueco libre** entre elementos consecutivos (útil con muebles de distinto tamaño) |
| | Campo de distancia | `Auto` (o vacío) = hueco calculado automáticamente. Una distancia (`600 mm`, `0.6 m`, `2' 0"`…, en las unidades del proyecto) = separa exactamente esa medida |

### Alinear a

| Opción | Alinear | Distribuir objetos | Distribuir espaciado |
|---|---|---|---|
| **Habitación** (el equivalente a la mesa de trabajo) | A los bordes o al centro de la habitación en la que está **cada** elemento. Funciona con un solo elemento | Los extremos se apoyan en las paredes y el resto se reparte a paso igual | `Auto`: mismo hueco entre muebles **y** contra las paredes. Distancia fija: la fila queda centrada en la habitación |
| **Selección** (por defecto) | Al rectángulo que envuelve toda la selección | Los elementos de los extremos no se mueven | `Auto`: los extremos no se mueven. Distancia fija: se queda quieto el de la izquierda (horizontal) o el de arriba (vertical) |
| **Objeto clave** | Al objeto clave, que **no se mueve** | Paso igual, manteniendo fija la clave | El resto se coloca a partir de la clave |

- Con **Habitación**, si seleccionas muebles de varias habitaciones cada grupo se
  alinea en la suya (p. ej. empujar todos los escritorios contra la pared
  izquierda de su despacho de una vez). La habitación puede estar en el propio
  modelo o en un **vínculo RVT cargado** (caso habitual cuando el mobiliario va en
  un modelo aparte). Se usa el contorno de la habitación en la cara de acabado;
  en habitaciones no rectangulares (en L, etc.) se toma su rectángulo envolvente.
- El **objeto clave** se elige con **Elegir…** haciendo clic en la vista. Puede
  ser cualquier elemento, esté o no seleccionado: otro mueble, un muro, un
  pilar… Se recuerda hasta que elijas otro.

### Cómo mide y en qué dirección mueve

- **Izquierda, derecha, arriba y abajo son los de la pantalla**, como en
  Illustrator: en una planta con la región de recorte girada, "izquierda" sigue
  siendo la izquierda de la vista.
- En **alzados y secciones** "arriba" es la altura: sirve, por ejemplo, para
  igualar la altura de los muebles altos de una cocina. En **vistas 3D** se
  alinea en planta (ejes X/Y del proyecto).
- Cada elemento se mide por la **geometría que se ve en la vista** (sólidos y
  líneas de planta), no por la caja de Revit, que siempre va alineada a los ejes
  del modelo y puede incluir los controles de giro de la familia. Así una silla
  girada se alinea por lo que realmente ocupa. Textos y etiquetas se miden por
  su caja; las vistas en un plano, por su marco (sin el título).

### Qué se puede alinear

Mobiliario y muebles fijos, modelos genéricos, aparatos, luminarias, grupos de
modelo, textos, etiquetas, componentes de detalle, vistas colocadas en planos…
En general, cualquier elemento que Revit permita desplazar.

Se omiten (y el panel dice cuántos y por qué): elementos **fijados
(pin)**, elementos **dentro de un grupo** (selecciona el grupo entero),
componentes **anidados** en otra familia y, en modo Habitación, los que no están
dentro de ninguna habitación. Si Revit rechaza mover algún elemento (por
ejemplo, por las restricciones de su anfitrión), el resto se mueve igual y el
panel avisa.

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

manifest/MJAlignTools.addin                Manifiesto del add-in de alineación
src/MJAlignTools/
  App.cs                                   Panel "Alinear" del ribbon
  Commands/ShowAlignPanelCommand.cs        Abre (o trae al frente) el panel flotante
  Core/AlignTypes.cs, AlignEngine.cs       Cálculo de alinear/distribuir/espaciar (sin Revit)
  Revit/ViewFrame.cs                       Ejes de la vista activa (derecha/arriba de pantalla)
  Revit/ElementExtents.cs                  Mide la geometría visible de cada elemento
  Revit/RoomLocator.cs                     Habitación de cada elemento (modelo y vínculos)
  Revit/AlignExecutor.cs                   Selección → cálculo → movimiento en una transacción
  Revit/AlignRequestHandler.cs             ExternalEvent: puente entre el panel y la API
  UI/AlignPanelWindow.cs, IconButton.cs    Panel WPF no modal
  UI/AlignIcons.cs                         Iconos vectoriales estilo Illustrator
tests/MJAlignTools.Tests/                  Pruebas del cálculo (dotnet test, sin Revit)
```
