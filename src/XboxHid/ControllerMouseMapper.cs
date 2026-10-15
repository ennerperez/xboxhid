namespace XboxHid;

public sealed class ControllerMouseMapper
{
    private readonly IMouseController mouse;
    private readonly GamepadBindingMatcher leftMatcher;
    private readonly GamepadBindingMatcher rightMatcher;
    private readonly GamepadBindingMatcher middleMatcher;
    private readonly GamepadBindingMatcher dragModifierMatcher;
    private readonly GamepadBindingMatcher boostMatcher;
    private readonly GamepadBindingMatcher[] actionMatchers;
    private readonly bool[] actionWasActive;
    private double remainderX;
    private double remainderY;
    private bool leftWasActive;
    private bool rightWasActive;
    private bool middleWasActive;
    private bool leftIsDown;
    private bool rightIsDown;
    private bool middleIsDown;
    private bool suppressLeftUntilRelease;
    private bool suppressRightUntilRelease;
    private bool suppressMiddleUntilRelease;

    public ControllerMouseMapper(IMouseController mouse, ControllerOptions options)
    {
        ArgumentNullException.ThrowIfNull(mouse);
        ArgumentNullException.ThrowIfNull(options);
        this.mouse = mouse;
        Options = options;
        float axisThreshold = Math.Clamp(options.DefaultThreshold, 0.05f, 1);
        leftMatcher = options.LeftButton.CreateMatcher(axisThreshold);
        rightMatcher = options.RightButton.CreateMatcher(axisThreshold);
        middleMatcher = options.MiddleButton.CreateMatcher(axisThreshold);
        dragModifierMatcher = options.DragDropModifier.CreateMatcher(axisThreshold);
        boostMatcher = options.BoostButton.CreateMatcher(axisThreshold);
        actionMatchers = options.Actions.Select(action => action.Binding.CreateMatcher(axisThreshold)).ToArray();
        actionWasActive = new bool[actionMatchers.Length];
    }

    public ControllerOptions Options { get; }
    public bool IsDragDropActive { get; private set; }
    public GamepadBinding DragDropModifier => Options.DragDropModifier;

    public void Apply(GamepadSnapshot snapshot, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        TimeSpan delta = TimeSpan.FromSeconds(Math.Clamp(elapsed.TotalSeconds, 0, 0.1));
        bool boostActive = boostMatcher.Update(snapshot, delta);
        Move(snapshot, delta, boostActive);

        bool dragModifierActive = dragModifierMatcher.Update(snapshot, delta);
        bool leftActive = leftMatcher.Update(snapshot, delta);
        bool rightActive = rightMatcher.Update(snapshot, delta);
        bool middleActive = middleMatcher.Update(snapshot, delta);
        bool leftRising = leftActive && !leftWasActive;
        bool rightRising = rightActive && !rightWasActive;
        bool middleRising = middleActive && !middleWasActive;

        bool[] actionsActive = new bool[actionMatchers.Length];
        bool actionRising = false;
        for (int index = 0; index < actionMatchers.Length; index++)
        {
            actionsActive[index] = actionMatchers[index].Update(snapshot, delta);
            actionRising |= actionsActive[index] && !actionWasActive[index];
        }

        bool consumedLeft = false;
        // Drag lock keeps BTN_LEFT down without requiring the physical button to remain held.
        // Any mapped non-movement command drops first; a repeated LeftButton is consumed so it
        // cannot immediately begin another click while that physical control is still held.
        if (IsDragDropActive && (leftRising || rightRising || middleRising || actionRising))
        {
            StopDragDrop();
            consumedLeft = leftRising;
            suppressLeftUntilRelease = leftRising;
        }
        else if (!IsDragDropActive && leftRising && dragModifierActive)
        {
            SetLeft(true);
            IsDragDropActive = true;
            consumedLeft = true;
        }

        if (!IsDragDropActive)
        {
            if (!leftActive) suppressLeftUntilRelease = false;
            if (!rightActive) suppressRightUntilRelease = false;
            if (!middleActive) suppressMiddleUntilRelease = false;
            if (!consumedLeft && !suppressLeftUntilRelease) SetMappedButton(leftActive, ref leftIsDown, mouse.SetLeftButton);
            if (!suppressRightUntilRelease) SetMappedButton(rightActive, ref rightIsDown, mouse.SetRightButton);
            if (!suppressMiddleUntilRelease) SetMappedButton(middleActive, ref middleIsDown, mouse.SetMiddleButton);
        }

        for (int index = 0; index < actionsActive.Length; index++)
        {
            if (actionsActive[index] && !actionWasActive[index])
                mouse.PerformShortcut(Options.Actions[index].Shortcut);
            actionWasActive[index] = actionsActive[index];
        }

        leftWasActive = leftActive;
        rightWasActive = rightActive;
        middleWasActive = middleActive;
    }

    public void ReleaseButtons()
    {
        if (leftIsDown) mouse.SetLeftButton(false);
        if (rightIsDown) mouse.SetRightButton(false);
        if (middleIsDown) mouse.SetMiddleButton(false);
        leftIsDown = rightIsDown = middleIsDown = false;
        leftWasActive = rightWasActive = middleWasActive = false;
        IsDragDropActive = false;
        suppressLeftUntilRelease = suppressRightUntilRelease = suppressMiddleUntilRelease = false;
        leftMatcher.Reset();
        rightMatcher.Reset();
        middleMatcher.Reset();
        dragModifierMatcher.Reset();
        boostMatcher.Reset();
        foreach (GamepadBindingMatcher matcher in actionMatchers) matcher.Reset();
        Array.Clear(actionWasActive);
    }

    public void SynchronizeButtons(GamepadSnapshot snapshot)
    {
        // A resume/overlay close must adopt the current physical state without manufacturing
        // rising edges for controls which the user already holds.
        leftMatcher.Synchronize(snapshot);
        rightMatcher.Synchronize(snapshot);
        middleMatcher.Synchronize(snapshot);
        dragModifierMatcher.Synchronize(snapshot);
        boostMatcher.Synchronize(snapshot);
        foreach (GamepadBindingMatcher matcher in actionMatchers) matcher.Synchronize(snapshot);
        leftWasActive = leftMatcher.Update(snapshot, TimeSpan.Zero);
        rightWasActive = rightMatcher.Update(snapshot, TimeSpan.Zero);
        middleWasActive = middleMatcher.Update(snapshot, TimeSpan.Zero);
        suppressLeftUntilRelease = leftWasActive;
        suppressRightUntilRelease = rightWasActive;
        suppressMiddleUntilRelease = middleWasActive;
        for (int index = 0; index < actionMatchers.Length; index++)
            actionWasActive[index] = actionMatchers[index].Update(snapshot, TimeSpan.Zero);
    }

    private void Move(GamepadSnapshot snapshot, TimeSpan elapsed, bool boostActive)
    {
        (GamepadAxis xAxis, GamepadAxis yAxis) = Options.PrimaryStick is PrimaryStick.Left
            ? (GamepadAxis.LeftX, GamepadAxis.LeftY)
            : (GamepadAxis.RightX, GamepadAxis.RightY);
        double x = ApplyResponseCurve(snapshot.GetAxis(xAxis));
        double y = ApplyResponseCurve(snapshot.GetAxis(yAxis));
        if (Options.InvertedXAxis) x = -x;
        if (Options.InvertedYAxis) y = -y;
        double speed = Options.Speed * (boostActive ? Options.BoostMultiplier : 1);
        remainderX += x * speed * elapsed.TotalSeconds;
        remainderY += y * speed * elapsed.TotalSeconds;
        int deltaX = (int)Math.Truncate(remainderX);
        int deltaY = (int)Math.Truncate(remainderY);
        remainderX -= deltaX;
        remainderY -= deltaY;
        if (deltaX != 0 || deltaY != 0) mouse.MoveRelative(deltaX, deltaY);
    }

    private double ApplyResponseCurve(float value)
    {
        double magnitude = Math.Abs(value);
        if (magnitude <= Options.DeadZone) return 0;
        double normalized = (magnitude - Options.DeadZone) / (1 - Options.DeadZone);
        return Math.CopySign(normalized * normalized, value);
    }

    private void StopDragDrop()
    {
        SetLeft(false);
        IsDragDropActive = false;
    }

    private void SetLeft(bool pressed)
    {
        if (leftIsDown == pressed) return;
        mouse.SetLeftButton(pressed);
        leftIsDown = pressed;
    }

    private static void SetMappedButton(bool active, ref bool current, Action<bool> setter)
    {
        if (active == current) return;
        setter(active);
        current = active;
    }
}
