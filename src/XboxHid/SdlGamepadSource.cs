using SDL;
using static SDL.SDL3;

namespace XboxHid;

public sealed unsafe class SdlGamepadSource : IDisposable
{
    private readonly ControllerOptions? options;
    private SDL_Gamepad* gamepad;
    private bool reportedWaiting;
    private DateTimeOffset nextConnectionAttempt;
    private bool disposed;

    public SdlGamepadSource(ControllerOptions? options = null)
    {
        this.options = options;
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD | SDL_InitFlags.SDL_INIT_EVENTS))
        {
            throw new InvalidOperationException($"SDL3 no pudo iniciar: {SDL_GetError()}");
        }
    }

    public GamepadPollResult Poll()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SDL_UpdateGamepads();

        if (gamepad != null && !SDL_GamepadConnected(gamepad))
        {
            SDL_CloseGamepad(gamepad);
            gamepad = null;
            reportedWaiting = false;
            return new(null, "Control desconectado.");
        }

        if (gamepad == null)
        {
            if (DateTimeOffset.UtcNow >= nextConnectionAttempt)
            {
                nextConnectionAttempt = DateTimeOffset.UtcNow.AddMilliseconds(500);
                string? description = TryOpenFirstGamepad();
                if (gamepad != null)
                {
                    reportedWaiting = false;
                    return new(ReadSnapshot(), $"Control conectado: {description ?? "Gamepad SDL"}.");
                }
            }

            if (!reportedWaiting)
            {
                reportedWaiting = true;
                return new(null, "Esperando control compatible con SDL3...");
            }

            return new(null, null);
        }

        return new(ReadSnapshot(), null);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (gamepad != null)
        {
            SDL_CloseGamepad(gamepad);
            gamepad = null;
        }

        SDL_Quit();
        disposed = true;
    }

    private string? TryOpenFirstGamepad()
    {
        using SDLArray<SDL_JoystickID>? ids = SDL_GetGamepads();
        if (ids is null)
        {
            return null;
        }

        foreach (SDL_JoystickID id in ids)
        {
            SDL_Gamepad* candidate = SDL_OpenGamepad(id);
            if (candidate == null)
            {
                continue;
            }

            gamepad = candidate;
            // Fail fast against the exact capabilities reported by this physical controller.
            ValidateMappedControls();
            return DescribeGamepad();
        }

        return null;
    }

    private void ValidateMappedControls()
    {
        if (options is null) return;
        List<string> unsupported = [];
        foreach (GamepadControl control in options.MappedControls)
        {
            bool available = control.Button is { } button
                ? SDL_GamepadHasButton(gamepad, (SDL_GamepadButton)(int)button)
                : SDL_GamepadHasAxis(gamepad, (SDL_GamepadAxis)(int)control.Axis!.Value);
            if (!available) unsupported.Add(control.ToString());
        }

        GamepadAxis[] stickAxes = options.PrimaryStick is PrimaryStick.Left
            ? [GamepadAxis.LeftX, GamepadAxis.LeftY, GamepadAxis.RightX, GamepadAxis.RightY]
            : [GamepadAxis.RightX, GamepadAxis.RightY, GamepadAxis.LeftX, GamepadAxis.LeftY];
        foreach (GamepadAxis axis in stickAxes)
            if (!SDL_GamepadHasAxis(gamepad, (SDL_GamepadAxis)(int)axis)) unsupported.Add(axis.ToString());
        if (unsupported.Count > 0)
            throw new InvalidOperationException($"El control conectado no reporta mappings requeridos: {string.Join(", ", unsupported.Distinct())}.");
    }

    private string DescribeGamepad()
    {
        string buttons = string.Join(", ", Enum.GetValues<GamepadButton>()
            .Where(button => SDL_GamepadHasButton(gamepad, (SDL_GamepadButton)(int)button)));
        string axes = string.Join(", ", Enum.GetValues<GamepadAxis>()
            .Where(axis => SDL_GamepadHasAxis(gamepad, (SDL_GamepadAxis)(int)axis)));
        return $"{SDL_GetGamepadName(gamepad) ?? "Gamepad SDL"}. Botones reportados=[{buttons}]; ejes reportados=[{axes}]";
    }

    private GamepadSnapshot ReadSnapshot()
    {
        float[] axes = new float[Enum.GetValues<GamepadAxis>().Length];
        bool[] buttons = new bool[Enum.GetValues<GamepadButton>().Length];

        foreach (GamepadAxis axis in Enum.GetValues<GamepadAxis>())
        {
            short raw = SDL_GetGamepadAxis(gamepad, (SDL_GamepadAxis)(int)axis);
            axes[(int)axis] = raw >= 0 ? raw / 32767f : raw / 32768f;
        }

        foreach (GamepadButton button in Enum.GetValues<GamepadButton>())
        {
            buttons[(int)button] = SDL_GetGamepadButton(gamepad, (SDL_GamepadButton)(int)button);
        }

        return new(axes, buttons);
    }
}

public sealed record GamepadPollResult(GamepadSnapshot? Snapshot, string? StatusMessage);
