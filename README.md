# XboxHid

Aplicación C# multiplataforma que lee un control compatible con SDL3, muestra cada cambio en consola y permite usar:

- stick izquierdo o derecho configurable: mover cursor;
- botón configurable: mantener/liberar click izquierdo;
- velocidad normal y turbo configurables;
- todos los ejes y botones: registro en consola.
- teclado virtual overlay AvaloniaUI con modos `DaisyWheel`, `CharacterCycling` y `GlyphStick`.

## Requisitos

- .NET 10 SDK
- Control reconocido como gamepad por SDL3
- Linux: acceso de lectura/escritura a `/dev/uinput`
- macOS: permiso de **Accesibilidad** para terminal o ejecutable

SDL3 y sus runtimes soportados llegan mediante paquete NuGet `ppy.SDL3-CS`.

> Un control Xbox 360 inalámbrico original no usa Bluetooth. Necesita receptor Xbox 360. Controles Xbox One/Series sí pueden usar Bluetooth y SDL puede mostrarlos con un mapeo tipo Xbox 360.

## Ejecutar

```bash
dotnet run --project src/XboxHid
```

Solo inspeccionar acciones, sin mover cursor:

```bash
dotnet run --project src/XboxHid -- --log-only
```

Abrir overlay inmediatamente para revisar diseño:

```bash
dotnet run --project src/XboxHid -- --show-overlay --log-only
```

Detener con `Ctrl+C` o presionando `Back + Start` juntos en el control. El segundo método funciona aunque terminal haya perdido foco.

## Configuración

Edita [`xboxhid.ini`](xboxhid.ini). También puedes usar otro archivo:

```bash
dotnet run --project src/XboxHid -- --config /ruta/controles.ini
```

```ini
[Mouse]
PrimaryStick=Right
LeftButton=LeftTrigger
RightButton=RightTrigger
MiddleButton=RightStick
DragDropModifier=RightShoulder
DefaultThreshold=0.5
Speed=1200
DeadZone=0.16
InvertedXAxis=false
InvertedYAxis=false
BoostButton=RightShoulder
BoostMultiplier=2

[Actions]
DPadUp=VOLUMEUP
DPadDown=VOLUMEDOWN
DPadLeft=ALT+TAB
DPadRight=ALT+SHIFT+TAB

[Diagnostics]
Debug=true

[Overlay]
Enabled=true
Mode=DaisyWheel
Opacity=0.94
Scale=1
```

- `PrimaryStick`: `Left` o `Right`; mueve cursor. Predeterminado `Right`.
- `LeftButton`, `RightButton`, `MiddleButton`: mappings de click. Predeterminados `LeftTrigger`, `RightTrigger`, `RightStick`.
- `DragDropModifier`: predeterminado `RightShoulder`. Mantén modificador y activa `LeftButton`. Otro click/acción ejecuta drop.
- `DefaultThreshold`: segundos base para `*`; también umbral analógico de gatillos. Predeterminado `0.5`.
- `Speed`: velocidad normal, `1`-`10000` px/s.
- `InvertedXAxis`, `InvertedYAxis`: inversión independiente; predeterminado `false`.
- `BoostButton`: mapping de turbo; predeterminado `RightShoulder`.
- `BoostMultiplier`: `2` produce x2; `3` produce x3. Rango permitido `1`-`10`.
- Mappings: `/` alternativas (`A/B`), `-` simultáneos (`A-B`), `+` orden manteniendo anteriores (`A+Y+B`), `*` espera (`A**B` = `DefaultThreshold × 2`).
- `[Actions]`: lado izquierdo usa misma gramática; lado derecho es atajo estándar (`CTRL+ALT+DEL`, `CTRL+SPACE`, `ALT+TAB`).
- `Back`, `Guide`, `Start` están reservados en todos los mappings/acciones.
- Al conectar, SDL valida que mando reporte todos los botones/ejes configurados.
- Controles aceptados: `A`, `B`, `X`, `Y`, ambos sticks/shoulders/triggers, D-pad, `Misc1..Misc6`, paddles y `Touchpad`. Consola imprime lista exacta que mando activo reporta.
- Teclas destino aceptadas: modificadores `CTRL`, `ALT`, `SHIFT`, `META`; navegación, volumen, `A-Z` y `0-9` (lista comentada completa en INI).
- `Diagnostics:Debug`: con `true`, Serilog escribe `./logs/xboxhid-YYYYMMDD.log`; con `false`, no crea logs.

En prueba local del 2026-10-14, SDL identificó mando activo como `Xbox Series X Controller` y reportó botones `A/B/X/Y`, `Back/Guide/Start`, ambos sticks, ambos shoulders, D-pad y `Misc1`; también ambos sticks analógicos y triggers. Esto es diagnóstico del dispositivo probado, no garantía para otro mando.

### Teclado virtual

- `Overlay:Enabled`: habilita overlay; valor predeterminado `true`.
- `Overlay:Mode`: `DaisyWheel`, `CharacterCycling` o `GlyphStick`.
- Overlay usa automáticamente stick opuesto a `PrimaryStick` y muestra `LS` o `RS`.
- `Overlay:Opacity`: `0.2`-`1.0`; `Overlay:Scale`: `0.5`-`2.0`.
- D-pad común: izquierda borra, abajo inserta espacio, derecha envía Enter, arriba alterna letras/números.
- Daisywheel: inclina stick hacia grupo y pulsa `X`, `Y`, `B` o `A` para elegir carácter.
- Character Cycling: inclina hacia grupo, alcanza borde exterior para cambiar carácter y vuelve al centro para confirmar.
- Glyph-stick: dibuja secuencia de direcciones cardinales y vuelve al centro para confirmar. En capa numérica usa Character Cycling.

Mientras overlay está visible, mando deja de mover cursor o ejecutar acciones globales. Ventana reafirma `Topmost` al mostrarse o perder activación, pero no toma foco; texto se escribe en aplicación que ya estaba enfocada.

Logs rotan diariamente o al llegar a 10 MB. Se retienen hasta 7 archivos. Incluyen estado de conexión, heartbeat cada 5 segundos, ejes, botones, cierre y excepciones. Puedes monitorearlos con:

```bash
tail -F logs/xboxhid-*.log
```

Con debug activo, archivo conserva cambios detallados. Consola limita ejes a 5 actualizaciones por segundo por eje para evitar que presión de salida bloquee captura o cierre; transiciones de botones siguen siendo inmediatas.

## Compatibilidad

- Windows: `SendInput`
- macOS: Quartz/CoreGraphics
- Linux Wayland: mouse, teclado y controles multimedia virtuales mediante `/dev/uinput`

## Verificar

```bash
dotnet restore XboxHid.slnx
dotnet test tests/XboxHid.Tests/XboxHid.Tests.csproj
dotnet test tests/XboxHid.Tests/XboxHid.Tests.csproj --settings tests/coverage.runsettings --collect:"XPlat Code Coverage"
dotnet build XboxHid.slnx
```

Umbral aceptable de cobertura unitaria: mayor a 85%. Medición cubre lógica determinista; configuración documentada excluye adaptadores SDL/P/Invoke, bucle de integración y superficies Avalonia.
