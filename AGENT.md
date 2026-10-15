# Guía para agentes

## Objetivo

XboxHid transforma entrada SDL3 de un gamepad en mouse/teclado nativo y ofrece teclado virtual Avalonia. Mantener funcionamiento Windows, macOS y Linux Wayland (`uinput`).

## Documentación

- `README.md`: uso, configuración y comportamiento visible.
- `docs/STACK.md`: stack, arquitectura y restricciones de plataforma.
- `xboxhid.ini`: esquema ejecutable; conservar comentarios de valores válidos y controles reservados.
- Ignorar contenido `.private` salvo petición explícita.

## Arquitectura clave

- `ControllerOptions.cs`: carga y valida INI.
- `InputMappings.cs`: gramática de controles y atajos.
- `SdlGamepadSource.cs`: lectura SDL y validación contra capacidades del mando activo.
- `ControllerMouseMapper.cs`: cursor, clicks, boost, acciones y drag lock.
- `MouseControllers.cs`: backends nativos SendInput, Quartz y uinput.
- `XboxHid.Overlay`: overlay topmost; usa automáticamente stick opuesto a `PrimaryStick`.
- Toda aplicación, ventana o control Avalonia debe conservar pareja `.axaml` + `.axaml.cs`; no construir vistas completas solo desde C#.

## Contratos que no deben romperse

- `PrimaryStick` solo admite `Left` o `Right`.
- `/` significa alternativas; `-`, acorde simultáneo; `+`, orden exacto manteniendo previos; `*`, espera `DefaultThreshold × cantidad de *`.
- `Back`, `Guide` y `Start` nunca son configurables. `Back` alterna overlay, `Start` pausa/reanuda, `Back+Start` cierra.
- Drag lock empieza con `DragDropModifier` + `LeftButton`; movimiento no lo cancela; cualquier click o acción hace drop.
- Pausa, overlay, desconexión y cierre liberan botones virtuales. Reanudar sincroniza estado para no crear clicks fantasma.
- Capacidades configuradas deben validarse con `SDL_GamepadHasButton/Axis` cada vez que conecta un mando.
- Serilog crea archivo únicamente con `Diagnostics:Debug=true`.

## Desarrollo y verificación

```bash
dotnet restore XboxHid.slnx
dotnet format XboxHid.slnx --verify-no-changes
dotnet test tests/XboxHid.Tests/XboxHid.Tests.csproj
dotnet test tests/XboxHid.Tests/XboxHid.Tests.csproj --settings tests/coverage.runsettings --collect:"XPlat Code Coverage"
dotnet build XboxHid.slnx --no-restore
dotnet run --project src/XboxHid -- --log-only
```

Cobertura unitaria aceptable: mayor a 85% usando `tests/coverage.runsettings`; exclusiones quedan limitadas a integración SDL/P/Invoke/UI y documentadas ahí. Usar comentarios solo para reglas no obvias, estado nativo o decisiones de interacción. Actualizar README, STACK e INI cuando cambie contrato público. No hacer commit/push salvo solicitud explícita.
