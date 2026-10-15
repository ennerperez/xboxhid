# Stack

- .NET 10 / C# 14
- Aplicación de consola multiplataforma con overlay de escritorio AvaloniaUI 12
- SDL3 mediante bindings `ppy.SDL3-CS` para entrada de gamepad
- `Microsoft.Extensions.Configuration.Ini` para controles, gramática de mappings, velocidad, inversión de ejes, acciones y teclado virtual
- Serilog + `Serilog.Sinks.File` para diagnóstico condicional con rotación
- P/Invoke del sistema operativo para movimiento, click y escritura global:
  - Windows: `user32!SendInput`
  - macOS: Quartz/CoreGraphics
  - Linux: dispositivo virtual kernel `/dev/uinput`
- xUnit para pruebas unitarias del procesamiento de estados

## Consideraciones

- Linux requiere permisos de lectura/escritura sobre `/dev/uinput`; mouse, teclado y teclas multimedia usan un dispositivo virtual del kernel compatible con Wayland.
- macOS requiere permiso de Accesibilidad para controlar el cursor.
- SDL3 debe poder abrir el control como gamepad. El paquete incluye runtimes nativos soportados.
- Movimiento usa `PrimaryStick`, zona muerta, inversión X/Y, curva de aceleración y tiempo transcurrido.
- Mappings aceptan alternativas `/`, simultáneos `-`, secuencias ordenadas `+` y espera temporal `*`.
- Clicks predeterminados: izquierdo `LeftTrigger`, derecho `RightTrigger`, medio `RightStick`.
- Drag & drop inicia con `DragDropModifier` + `LeftButton`; cualquier comando no relacionado con movimiento ejecuta drop.
- Acciones emiten atajos genéricos como `CTRL+ALT+DEL` mediante backend nativo.
- SDL valida al conectar que todos los botones/ejes usados estén reportados por mando.
- Desconexión, pausa, overlay o cierre siempre liberan botones virtuales; al reanudar se sincroniza estado físico para evitar clicks fantasma.
- `Start` pausa/reanuda procesamiento; durante pausa solo se evalúan `Start` y el cierre prioritario `Back+Start`. Ambos botones están reservados por validación.
- `Back` muestra/oculta overlay. Teclado usa automáticamente stick opuesto a `PrimaryStick` y muestra `LS`/`RS`.
- Overlay no toma foco, reafirma hint topmost tras cada show/desactivación y envía texto hacia aplicación enfocada mediante backend nativo.
- Componentes UI Avalonia mantienen siempre archivos pareados `.axaml` y `.axaml.cs`; code-behind queda para estado y render custom.
- Suite debe mantener cobertura unitaria mayor a 85% sobre lógica determinista; `tests/coverage.runsettings` documenta exclusiones de integración SDL/P/Invoke/UI.

