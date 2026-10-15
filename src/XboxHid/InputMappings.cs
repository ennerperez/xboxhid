namespace XboxHid;

public readonly record struct GamepadControl(GamepadButton? Button, GamepadAxis? Axis)
{
    public bool IsPressed(GamepadSnapshot snapshot, float threshold) =>
        Button is { } button ? snapshot.IsPressed(button) : snapshot.GetAxis(Axis!.Value) >= threshold;

    public static GamepadControl Parse(string value, string key)
    {
        string normalized = value.Trim();
        if (Enum.TryParse(normalized, true, out GamepadButton button))
        {
            if (button is GamepadButton.Back or GamepadButton.Guide or GamepadButton.Start)
                throw new InvalidOperationException($"Valor inválido para {key}: {button} está reservado.");
            return new(button, null);
        }

        if (Enum.TryParse(normalized, true, out GamepadAxis axis) &&
            axis is GamepadAxis.LeftTrigger or GamepadAxis.RightTrigger)
            return new(null, axis);

        throw new InvalidOperationException($"Control inválido para {key}: '{value}'.");
    }

    public override string ToString() => Button?.ToString() ?? Axis?.ToString() ?? string.Empty;
}

public enum BindingGestureKind
{
    Single,
    Simultaneous,
    Ordered,
    HoldThenPress
}

public sealed record BindingAlternative(
    BindingGestureKind Kind,
    IReadOnlyList<GamepadControl> Controls,
    TimeSpan RequiredHold);

public sealed record GamepadBinding(string Expression, IReadOnlyList<BindingAlternative> Alternatives)
{
    public IEnumerable<GamepadControl> Controls => Alternatives.SelectMany(alternative => alternative.Controls).Distinct();

    public GamepadBindingMatcher CreateMatcher(float axisThreshold) => new(this, axisThreshold);

    public static GamepadBinding Parse(string expression, string key, float defaultThreshold)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new InvalidOperationException($"{key} no puede estar vacío.");

        // Operators intentionally do not share precedence inside one alternative: '/' splits
        // independent alternatives, while '+', '-' or '*' defines exactly one gesture kind.
        string[] rawAlternatives = expression.Split('/', StringSplitOptions.TrimEntries);
        if (rawAlternatives.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"Mapping inválido para {key}: '{expression}'.");

        List<BindingAlternative> alternatives = [];
        foreach (string rawAlternative in rawAlternatives)
        {
            bool hasPlus = rawAlternative.Contains('+');
            bool hasDash = rawAlternative.Contains('-');
            bool hasStar = rawAlternative.Contains('*');
            if ((hasPlus ? 1 : 0) + (hasDash ? 1 : 0) + (hasStar ? 1 : 0) > 1)
                throw new InvalidOperationException($"Mapping inválido para {key}: no mezcles '+', '-' y '*' en una alternativa.");

            if (hasStar)
            {
                int first = rawAlternative.IndexOf('*');
                int last = rawAlternative.LastIndexOf('*');
                string left = rawAlternative[..first].Trim();
                string right = rawAlternative[(last + 1)..].Trim();
                if (left.Length == 0 || right.Length == 0 || rawAlternative[first..(last + 1)].Any(character => character != '*'))
                    throw new InvalidOperationException($"Mapping temporal inválido para {key}: '{rawAlternative}'.");
                int multiplier = last - first + 1;
                alternatives.Add(new(
                    BindingGestureKind.HoldThenPress,
                    [GamepadControl.Parse(left, key), GamepadControl.Parse(right, key)],
                    TimeSpan.FromSeconds(defaultThreshold * multiplier)));
                continue;
            }

            char? separator = hasPlus ? '+' : hasDash ? '-' : null;
            string[] parts = separator is null
                ? [rawAlternative.Trim()]
                : rawAlternative.Split(separator.Value, StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace) ||
                parts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parts.Length)
                throw new InvalidOperationException($"Mapping inválido para {key}: '{rawAlternative}'.");
            GamepadControl[] controls = parts.Select(part => GamepadControl.Parse(part, key)).ToArray();
            alternatives.Add(new(
                separator == '+' ? BindingGestureKind.Ordered : separator == '-' ? BindingGestureKind.Simultaneous : BindingGestureKind.Single,
                controls,
                TimeSpan.Zero));
        }

        if (alternatives.Count == 0)
            throw new InvalidOperationException($"{key} no contiene alternativas válidas.");
        return new(expression.Trim(), alternatives);
    }

    public override string ToString() => Expression;
}

public sealed class GamepadBindingMatcher
{
    private readonly GamepadBinding binding;
    private readonly float axisThreshold;
    private readonly AlternativeState[] states;

    public GamepadBindingMatcher(GamepadBinding binding, float axisThreshold)
    {
        this.binding = binding;
        this.axisThreshold = axisThreshold;
        states = binding.Alternatives.Select(alternative => new AlternativeState(alternative.Controls.Count)).ToArray();
    }

    public bool Update(GamepadSnapshot snapshot, TimeSpan elapsed)
    {
        bool active = false;
        for (int index = 0; index < binding.Alternatives.Count; index++)
            active |= UpdateAlternative(binding.Alternatives[index], states[index], snapshot, elapsed);
        return active;
    }

    public void Synchronize(GamepadSnapshot snapshot)
    {
        Reset();
        for (int index = 0; index < binding.Alternatives.Count; index++)
        {
            BindingAlternative alternative = binding.Alternatives[index];
            for (int control = 0; control < alternative.Controls.Count; control++)
                states[index].Previous[control] = alternative.Controls[control].IsPressed(snapshot, axisThreshold);
        }
    }

    public void Reset()
    {
        foreach (AlternativeState state in states) state.Reset();
    }

    private bool UpdateAlternative(BindingAlternative alternative, AlternativeState state, GamepadSnapshot snapshot, TimeSpan elapsed)
    {
        bool[] pressed = alternative.Controls.Select(control => control.IsPressed(snapshot, axisThreshold)).ToArray();
        bool active;
        switch (alternative.Kind)
        {
            case BindingGestureKind.Single:
                active = pressed[0];
                break;
            case BindingGestureKind.Simultaneous:
                active = pressed.All(value => value);
                break;
            case BindingGestureKind.Ordered:
                active = UpdateOrdered(state, pressed);
                break;
            case BindingGestureKind.HoldThenPress:
                active = UpdateHold(state, pressed, elapsed, alternative.RequiredHold);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        pressed.CopyTo(state.Previous, 0);
        return active;
    }

    private static bool UpdateOrdered(AlternativeState state, bool[] pressed)
    {
        // Every next control must rise while all earlier controls remain held. A wrong order
        // resets progress, making '+' deterministic instead of acting as a loose chord.
        if (state.Completed)
        {
            if (pressed.All(value => value)) return true;
            state.ResetProgress();
        }

        if (state.Progress > 0 && !pressed.Take(state.Progress).All(value => value)) state.ResetProgress();
        for (int index = 0; index < pressed.Length; index++)
        {
            bool rising = pressed[index] && !state.Previous[index];
            if (!rising) continue;
            if (index == state.Progress && pressed.Take(index).All(value => value)) state.Progress++;
            else state.Progress = index == 0 ? 1 : 0;
        }

        state.Completed = state.Progress == pressed.Length;
        return state.Completed && pressed.All(value => value);
    }

    private static bool UpdateHold(AlternativeState state, bool[] pressed, TimeSpan elapsed, TimeSpan required)
    {
        // A run of N stars means: hold first control for DefaultThreshold * N, then press second.
        if (!pressed[0]) state.ResetProgress();
        else state.Held += elapsed;

        if (!state.Completed && state.Held >= required && pressed[1] && !state.Previous[1]) state.Completed = true;
        if (state.Completed && pressed[0] && pressed[1]) return true;
        if (state.Completed) state.ResetProgress();
        return false;
    }

    private sealed class AlternativeState(int count)
    {
        public bool[] Previous { get; } = new bool[count];
        public int Progress { get; set; }
        public bool Completed { get; set; }
        public TimeSpan Held { get; set; }

        public void ResetProgress()
        {
            Progress = 0;
            Completed = false;
            Held = TimeSpan.Zero;
        }

        public void Reset()
        {
            ResetProgress();
            Array.Clear(Previous);
        }
    }
}

public enum KeyboardKey
{
    Ctrl, Alt, Shift, Meta, Tab, Delete, Space, Enter, Escape,
    Left, Right, Up, Down, VolumeUp, VolumeDown,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9
}

public sealed record KeyboardShortcut(string Expression, IReadOnlyList<KeyboardKey> Keys)
{
    public static KeyboardShortcut Parse(string expression, string key)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new InvalidOperationException($"{key} no puede estar vacío.");
        string[] parts = expression.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"Atajo inválido para {key}: '{expression}'.");

        List<KeyboardKey> keys = [];
        foreach (string part in parts)
        {
            string normalized = part.ToUpperInvariant();
            normalized = normalized switch { "CONTROL" => "CTRL", "DEL" => "DELETE", "WIN" or "CMD" or "SUPER" => "META", _ => normalized };
            if (normalized.Length == 1 && char.IsDigit(normalized[0])) normalized = $"D{normalized}";
            if (!Enum.TryParse(normalized, true, out KeyboardKey parsed))
                throw new InvalidOperationException($"Tecla inválida para {key}: '{part}'.");
            keys.Add(parsed);
        }
        if (keys.Count == 0 || keys.Distinct().Count() != keys.Count)
            throw new InvalidOperationException($"Atajo inválido para {key}: '{expression}'.");
        return new(expression.Trim().ToUpperInvariant(), keys);
    }

    public override string ToString() => Expression;
}

public sealed record MappedAction(GamepadBinding Binding, KeyboardShortcut Shortcut)
{
    public static IReadOnlyList<MappedAction> CreateDefaults(float threshold) =>
    [
        new(GamepadBinding.Parse("DPadUp", "Actions:DPadUp", threshold), KeyboardShortcut.Parse("VOLUMEUP", "Actions:DPadUp")),
        new(GamepadBinding.Parse("DPadDown", "Actions:DPadDown", threshold), KeyboardShortcut.Parse("VOLUMEDOWN", "Actions:DPadDown")),
        new(GamepadBinding.Parse("DPadLeft", "Actions:DPadLeft", threshold), KeyboardShortcut.Parse("ALT+TAB", "Actions:DPadLeft")),
        new(GamepadBinding.Parse("DPadRight", "Actions:DPadRight", threshold), KeyboardShortcut.Parse("ALT+SHIFT+TAB", "Actions:DPadRight")),
    ];
}
