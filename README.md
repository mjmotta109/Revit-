# MJ Tools para Revit — vínculos, asistente IA y alzados por habitación

Add-in para **Autodesk Revit** con tres herramientas en la pestaña **MJ Tools**:

| Panel | Botón | Qué hace |
|---|---|---|
| Vínculos | **¿Dónde está el vínculo?** | Diagnóstico de vínculos y worksets (abajo) |
| Vínculos | **Asistente IA** | Chat con Claude que responde consultando el modelo real ([ver](#asistente-ia--chatea-con-tu-modelo)) |
| Vistas | **Alzados por habitación** | Un alzado interior por muro, recortado al piso y al cielo raso ([ver](#alzados-por-habitación)) |

La primera responde una pregunta muy concreta:

> *"Tengo un vínculo que no aparece en el modelo, pero sí está registrado en
> Gestionar vínculos. ¿En qué workset está, para abrir **solo ese** workset
> en lugar de abrirlos todos y sobrecargar el proyecto?"*

> Nota de terminología: en el Revit en español los *worksets* se llaman
> **Subproyectos** (Colaborar ▸ Gestionar colaboración ▸ Subproyectos) y
> *Manage Links* es **Gestionar vínculos**. Aquí usamos "workset" por ser la
> jerga habitual, y las rutas de menú con sus nombres oficiales en español.

## El problema que resuelve

En modelos de trabajo compartido (worksharing), cuando un vínculo RVT o CAD está
en un **workset cerrado**, Revit ni siquiera lo carga: no se ve en ninguna vista,
aunque sigue apareciendo registrado en *Gestionar vínculos* (con el estado
"En subproyecto cerrado"). La "solución" habitual —abrir todos los worksets—
penaliza mucho el rendimiento en proyectos grandes.

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
> abierto, así que el último paso es manual (y rápido): **Colaborar ▸ Gestionar
> colaboración ▸ Subproyectos ▸ seleccionar el workset indicado ▸ Abrir**. Al
> abrir el modelo también puedes usar el desplegable de subproyectos del botón
> *Abrir ▸ Especificar…* para abrir solo los worksets que necesitas.

## Requisitos

- Autodesk Revit **2021 a 2026** (Windows).
- Para compilar: [Visual Studio 2022](https://visualstudio.microsoft.com/es/vs/community/)
  (edición Community es gratuita) con la carga de trabajo **Desarrollo de
  escritorio de .NET**, o el SDK de .NET si prefieres línea de comandos.
  Las referencias a la API de Revit se descargan solas desde NuGet
  (paquetes `Nice3point.Revit.Api.*`); no hace falta copiar ninguna DLL.

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
4. **Colaborar ▸ Gestionar colaboración ▸ Subproyectos**, abre solo ese workset,
   y el vínculo aparecerá (si además estaba descargado, recárgalo en
   *Gestionar vínculos* con el botón *Volver a cargar*).

## Diagnósticos que detecta

| Situación | Diagnóstico |
|---|---|
| Workset del tipo de vínculo cerrado (`En workset cerrado` / "En subproyecto cerrado") | El vínculo ni se cargó; indica el workset exacto a abrir |
| Workset de la instancia cerrado | El archivo está cargado pero la instancia no se ve; indica el workset a abrir |
| Vínculo descargado / descargado solo local | Recargar desde Gestionar vínculos (Volver a cargar) |
| Archivo no encontrado | Ruta rota; corregir en Gestionar vínculos |
| Workset oculto en la vista activa (V/G ▸ Subproyectos) o sin "Visible en todas las vistas" | Indica dónde reactivarlo |
| Categoría oculta, elemento oculto manualmente, opción de diseño no principal, CAD de "solo vista actual" | Indica el motivo y el remedio |
| Tipo registrado sin ninguna instancia colocada | Avisa de que la instancia pudo borrarse aunque el archivo siga en Gestionar vínculos |
| Vínculo anidado | Se controla desde el vínculo padre |

## Asistente IA — chatea con tu modelo

El botón **MJ Tools ▸ Vínculos ▸ Asistente IA** abre un chat donde escribes en
lenguaje natural y Claude responde consultando el modelo abierto mediante
herramientas. Ejemplos:

- *"¿Cuántas puertas hay por nivel?"*
- *"¿Qué vínculos están descargados y en qué workset están?"*
- *"¿Qué advertencias tiene el modelo y cuáles se repiten más?"*
- *"Busca los muros cuyo tipo contenga 'hormigón' y selecciónalos"*
- *"¿Qué worksets están cerrados?"*

### Cómo funciona

La IA no adivina: invoca herramientas del add-in que leen el modelo con la API
de Revit y le devuelven datos reales — `contar_elementos`, `buscar_elementos`,
`listar_niveles`, `listar_worksets`, `listar_categorias`, `estado_vinculos`
(reutiliza el analizador de la herramienta principal), `listar_advertencias`,
`parametros_elemento`, `info_proyecto` y `seleccionar_elementos`.

Todas son de **solo lectura**: el asistente nunca modifica el modelo (lo único
que puede cambiar es la selección en pantalla, si se lo pides).

### Requisitos y configuración

- Una **API key de Anthropic** (se crea en [platform.claude.com](https://platform.claude.com);
  requiere cuenta con crédito de API — el uso tiene coste por tokens).
- La primera vez que abras el asistente te pedirá la clave y la guardará en
  `%AppData%\LinkWorksetInspector\ai-settings.json` (en texto plano). Si
  prefieres no guardarla, define la variable de entorno `ANTHROPIC_API_KEY`.
- Modelo por defecto: `claude-opus-4-8`. Puedes cambiarlo editando el campo
  `Model` de ese mismo archivo de configuración.
- La integración usa el **SDK oficial de Anthropic para C#** (paquete NuGet
  `Anthropic`), que se restaura al compilar.

> Privacidad: lo que escribas en el chat y los datos que devuelvan las
> herramientas (nombres de elementos, worksets, advertencias…) se envían a la
> API de Anthropic para generar la respuesta. No uses el asistente en modelos
> cuya información no puedas compartir con ese servicio.

## Alzados por habitación

El botón **MJ Tools ▸ Vistas ▸ Alzados por habitación** genera, para cada
habitación, **un alzado interior por cada orientación de muro** — las cuatro
elevaciones clásicas de una estancia rectangular, y las que hagan falta en
plantas irregulares.

Cada vista queda acotada exactamente a la estancia:

- **Abajo**, el piso de la habitación.
- **Arriba**, el **cielo raso o la losa más cercana por encima**. La herramienta
  busca ambos y se queda con el más bajo que esté sobre el piso; si no encuentra
  ninguno usa la altura por defecto que indiques.
- **A los lados**, el ancho de la habitación (más el margen que elijas).
- **En profundidad**, el corte lejano se sitúa justo detrás del muro más lejano,
  para que el alzado no muestre las estancias contiguas.

### Cómo funciona

Para cada habitación se leen sus bordes (`GetBoundarySegments`), se agrupan los
tramos por orientación —los muros paralelos comparten alzado— y se ignoran los
tramos más cortos que la longitud mínima que indiques, para no generar
elevaciones espurias en jambas y quiebres.

Después se coloca un marcador de alzado en el centro de la estancia y se giran
sus vistas hacia cada muro. Un marcador tiene cuatro huecos, así que una
habitación ortogonal usa **un solo marcador** en planta; solo las plantas con
muros no perpendiculares necesitan marcadores adicionales.

### Opciones del diálogo

| Opción | Para qué sirve |
|---|---|
| Habitaciones | Las de la vista activa, todas las del modelo, o solo las seleccionadas |
| Tipo de vista de alzado | El `ViewFamilyType` de alzado que se usará |
| Plantilla de vista | Opcional; se aplica a cada vista creada |
| Escala | Escala de las vistas nuevas |
| Margen horizontal | Aire a izquierda y derecha del recorte |
| Margen vertical | Aire por debajo del piso y por encima del techo (0 para recortar justo) |
| Profundidad tras el muro | Cuánto se prolonga el corte lejano tras la cara del muro |
| Longitud mínima de muro | Los tramos de borde más cortos no generan alzado |
| Altura si no hay techo ni losa | Altura de reserva cuando no se encuentra nada por encima |
| Recortar con cielos rasos / con losas | Qué elementos se consideran como techo |
| Omitir habitaciones que ya tienen alzados | Permite volver a ejecutar la herramienta sin duplicar vistas |
| Nombre de la vista | Patrón con `{numero}`, `{nombre}` y `{direccion}` |

Las vistas se llaman por defecto `101 Sala de reuniones - Muro Norte`, donde la
dirección es el punto cardinal del muro que se está mirando. El diálogo muestra
al terminar qué vistas se crearon en cada habitación, con qué altura se recortó
y de dónde salió esa altura (cielo raso, losa o valor por defecto).

> Nota: si la plantilla de vista que elijas controla la escala o el recorte,
> Revit ignorará esos ajustes; la herramienta lo indica en el informe.

## Estructura del código

```
manifest/LinkWorksetInspector.addin        Manifiesto del add-in
src/LinkWorksetInspector/
  App.cs                                   IExternalApplication: ribbon y botones
  RibbonIconFactory.cs                     Iconos de los botones dibujados en memoria
  Commands/InspectLinkWorksetsCommand.cs   Comando del diagnóstico de vínculos
  Commands/AiAssistantCommand.cs           Comando del asistente IA
  Commands/CreateRoomElevationsCommand.cs  Comando de alzados por habitación
  Services/LinkWorksetAnalyzer.cs          Lógica de análisis y diagnóstico de vínculos
  Services/Ai/ClaudeChatService.cs         Bucle de chat + tool use (SDK de Anthropic)
  Services/Ai/RevitModelTools.cs           Herramientas que la IA ejecuta sobre el modelo
  Services/Ai/AiSettings.cs                API key y modelo (archivo o variable de entorno)
  Services/RoomElevations/                 Generador de alzados y sus opciones
  Models/LinkReportRow.cs                  Fila del informe de vínculos
  UI/LinkWorksetForm.cs                    Ventana de la tabla de vínculos
  UI/AiChatForm.cs                         Ventana de chat del asistente
  UI/ApiKeyForm.cs                         Diálogo de configuración de la API key
  UI/RoomElevationsForm.cs                 Diálogo de alzados por habitación
```
