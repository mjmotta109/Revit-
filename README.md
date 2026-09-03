# MJ Tools para Revit

[![Compilar](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml/badge.svg)](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml)

Add-ins para **Autodesk Revit 2021–2026** que añaden la pestaña de ribbon
**MJ Tools**. Cada herramienta es un add-in independiente: puedes instalar solo
la que te interese, y comparten pestaña sin estorbarse.

| Herramienta | Panel | Qué hace |
|---|---|---|
| [**Cota rápida**](#cota-rápida--acotar-trazando-una-línea) | Acotar | Traza una línea y crea la cota con los elementos arquitectónicos que encuentra |
| [**Link Workset Inspector**](#link-workset-inspector--dónde-está-mi-vínculo) | Vínculos | Dice en qué workset está cada vínculo, para abrir solo ese |

---

# Cota rápida — acotar trazando una línea

Acotar en Revit es repetitivo: hay que ir picando cara por cara. Esta
herramienta invierte el planteamiento — **trazas una línea y ella busca las
referencias**.

## Los dos modos

Al pulsar el botón aparece un diálogo con la decisión principal:

**1. Solo lo que atraviesa la línea**
Las caras de los muros y columnas que la línea cruza. Es la cota de replanteo
clásica: cruzas la planta de lado a lado y obtienes el espesor de cada muro y
la luz entre ellos.

**2. También las aberturas proyectadas**
Añade las **jambas de puertas, ventanas y vanos de muro**, y los **extremos de
los muros**. Es la cota de fachada: trazas la línea *a lo largo* del muro y
obtienes dónde empieza y acaba cada hueco. Opcionalmente puedes acotar al
**eje** de puertas y ventanas en vez de (o además de) a sus bordes.

En ambos modos, **el mobiliario y el equipamiento (FFE) nunca entran**: muebles,
armarios, sanitarios, luminarias y equipos se ignoran aunque la línea los
atraviese. No es una lista de exclusiones que haya que ir ampliando, es una
lista blanca — solo entran muros, columnas arquitectónicas y estructurales,
puertas, ventanas y vanos de muro.

## Cómo trazar la línea

La regla es una sola: **la línea define la dirección en la que se mide**, y
Revit solo sabe acotar referencias perpendiculares a esa dirección. De ahí
salen los dos usos:

- **Cruzando los muros** → obtienes sus caras (son perpendiculares a la línea).
- **A lo largo de un muro** → obtienes sus extremos y las jambas de sus huecos
  (también son perpendiculares a la línea).

Si quieres que la cota quede fuera del edificio en vez de encima del muro,
traza la línea sobre el muro y usa el campo **Desplazar la cota**: la cota se
crea a esa distancia perpendicular, sin perder las referencias.

No hace falta trazar la línea perfectamente recta: la herramienta detecta la
orientación exacta de la geometría que ha encontrado y endereza la cota sola.
Tampoco importa la longitud exacta — la cota se ajusta para abarcar la primera
y la última referencia.

## Uso

1. Abre una vista de **planta, sección, alzado o detalle**.
2. **MJ Tools ▸ Acotar ▸ Cota rápida**.
3. Elige el modo y pulsa **Acotar**.
4. Pica los dos puntos de la línea. La cota se crea al instante.
5. La herramienta sigue pidiendo líneas: acota todas las que quieras y pulsa
   **ESC** para terminar.

Las opciones se recuerdan mientras Revit siga abierto, así que a partir de la
segunda vez basta con pulsar Aceptar. Cada cota es una operación independiente:
`Ctrl+Z` deshace solo la última.

## Opciones

| Opción | Para qué sirve |
|---|---|
| Incluir columnas | Añade columnas arquitectónicas y estructurales atravesadas. Desactívalo si solo te interesan los muros |
| Acotar también al eje de los muros | Añade el eje además de las caras (cota a ejes) |
| Desplazar la cota | Separa la cota de la línea trazada, en metros. Positivo = a la izquierda del sentido de trazado |
| Holgura de búsqueda | Margen alrededor de la línea al buscar elementos. Súbelo si trazas "a ojo" y se te escapa algún muro |

## Límites conocidos

- Un muro **oblicuo** respecto a la línea se ignora: sus caras no son
  perpendiculares a la dirección de medida y Revit no puede acotarlas. Traza
  una línea perpendicular a ese muro.
- Los **muros cortina** no exponen caras laterales al API, así que aportan poco.
- Hacen falta **al menos dos referencias**: si la línea solo toca un elemento,
  la herramienta lo dice en vez de crear una cota inválida.
- Solo se consideran elementos **visibles en la vista activa**, respetando
  rango de vista, filtros y elementos ocultos.

Si Revit rechaza alguna referencia, la herramienta reintenta automáticamente
quedándose con las más fiables (primero descarta ejes y planos de familia,
después las caras obtenidas de la geometría) antes de avisar.

---

# Link Workset Inspector — ¿dónde está mi vínculo?

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

Un botón **"¿Dónde está el vínculo?"** en **MJ Tools ▸ Vínculos** abre una tabla
con una fila por instancia de vínculo (RVT y CAD):

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

---

# Requisitos

- Autodesk Revit **2021 a 2026** (Windows).
- Para compilar: [Visual Studio 2022](https://visualstudio.microsoft.com/es/vs/community/)
  (edición Community es gratuita) con la carga de trabajo **Desarrollo de
  escritorio de .NET**, o el SDK de .NET si prefieres línea de comandos.
  Las referencias a la API de Revit se descargan solas desde NuGet
  (paquetes `Nice3point.Revit.Api.*`); no hace falta copiar ninguna DLL.

> La compilación **debe hacerse en Windows**: los add-ins usan WPF y WinForms, y
> esos SDK de escritorio no existen en Linux ni macOS.

# Instalar sin compilar (recomendado)

Cada cambio en el repositorio se compila automáticamente para Revit 2021–2026
(pestaña **Actions** de GitHub). Para instalar sin abrir Visual Studio:

1. Entra en [Actions ▸ Compilar](https://github.com/mjmotta109/Revit-/actions/workflows/build.yml)
   y abre la ejecución más reciente con ✅.
2. En la sección **Artifacts** descarga `MJTools-Revit<tu versión>`
   (por ejemplo `MJTools-Revit2025`). Incluye las dos herramientas.
3. Descomprime el zip dentro de `%AppData%\Autodesk\Revit\Addins\<versión>\`
   (pega esa ruta en el Explorador de Windows). Debe quedar así:

   ```
   %AppData%\Autodesk\Revit\Addins\2025\QuickDimension.addin
   %AppData%\Autodesk\Revit\Addins\2025\QuickDimension\QuickDimension.dll
   %AppData%\Autodesk\Revit\Addins\2025\LinkWorksetInspector.addin
   %AppData%\Autodesk\Revit\Addins\2025\LinkWorksetInspector\LinkWorksetInspector.dll
   ```

   Si solo quieres una de las dos, copia únicamente su `.addin` y su carpeta.

4. Arranca Revit y acepta el aviso de add-in la primera vez.

> Nota: GitHub pide iniciar sesión (una cuenta gratuita) para descargar artifacts.

# Compilar e instalar

1. Clona o descarga este repositorio.
2. Abre `MjTools.sln` en Visual Studio.
3. Si tu Revit **no** es 2024, compila indicando la versión (ver abajo) o cambia
   la propiedad `RevitVersion` al inicio de cada `.csproj`.
4. Compila en `Release` (Compilar ▸ Compilar solución).

Por línea de comandos, una orden por herramienta (ejemplo para Revit 2025):

```bat
dotnet build -c Release -p:RevitVersion=2025 src\QuickDimension\QuickDimension.csproj
dotnet build -c Release -p:RevitVersion=2025 src\LinkWorksetInspector\LinkWorksetInspector.csproj
```

Al compilar en Windows, cada proyecto **se instala solo**: copia su DLL y su
manifiesto `.addin` a `%AppData%\Autodesk\Revit\Addins\<versión>\`. Arranca
Revit, acepta el aviso de add-in la primera vez, y verás la pestaña **MJ Tools**.

Para compilar sin instalar, añade `-p:SkipAddinDeploy=true`.

### Instalación manual (si compilaste en otra máquina)

Copia a `%AppData%\Autodesk\Revit\Addins\<versión>\`:

```
QuickDimension.addin                             (desde manifest/)
QuickDimension\QuickDimension.dll                (desde src/.../bin/Release/)
LinkWorksetInspector.addin                       (desde manifest/)
LinkWorksetInspector\LinkWorksetInspector.dll    (desde src/.../bin/Release/)
```

# Estructura del código

```
manifest/                                    Manifiestos .addin de cada herramienta

src/QuickDimension/
  App.cs                                     IExternalApplication: ribbon y botón
  RibbonIconFactory.cs                       Icono del botón dibujado en memoria
  Commands/QuickDimensionCommand.cs          IExternalCommand: pide la línea y crea la cota
  Services/QuickDimensionEngine.cs           Busca las referencias y decide dirección y posición
  Services/PlanarGeometry.cs                 Geometría 2D en el plano de la vista
  Services/ArchitecturalCategories.cs        Lista blanca de categorías (deja fuera el FFE)
  Model/QuickDimensionOptions.cs             Opciones del diálogo
  Model/ReferenceCandidate.cs                Una referencia candidata, con su normal y posición
  Model/DimensionPlan.cs                     Cota resuelta: línea, referencias y orden
  UI/QuickDimensionOptionsForm.cs            Diálogo de opciones (WinForms)

src/LinkWorksetInspector/
  App.cs                                     IExternalApplication: ribbon y botón
  RibbonIconFactory.cs                       Icono del botón dibujado en memoria
  Commands/InspectLinkWorksetsCommand.cs     IExternalCommand: orquesta análisis + ventana
  Services/LinkWorksetAnalyzer.cs            Toda la lógica de análisis y diagnóstico
  Models/LinkReportRow.cs                    Fila del informe
  UI/LinkWorksetForm.cs                      Ventana WinForms con la tabla
```
