using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace XboxHid;

public interface IMouseController : IDisposable
{
    void MoveRelative(int deltaX, int deltaY);

    void SetLeftButton(bool pressed);

    void SetRightButton(bool pressed);

    void SetMiddleButton(bool pressed);

    void PerformShortcut(KeyboardShortcut shortcut);

    void SendText(string text);
}

public static class MouseControllerFactory
{
    public static IMouseController Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsMouseController();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsMouseController();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxUInputMouseController();
        }

        throw new PlatformNotSupportedException("Sistema operativo no soportado para control global del mouse.");
    }
}

internal sealed class NullMouseController : IMouseController
{
    public void MoveRelative(int deltaX, int deltaY)
    {
    }

    public void SetLeftButton(bool pressed)
    {
    }

    public void SetRightButton(bool pressed)
    {
    }

    public void SetMiddleButton(bool pressed)
    {
    }

    public void PerformShortcut(KeyboardShortcut shortcut)
    {
    }

    public void SendText(string text)
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class WindowsMouseController : IMouseController
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyTab = 0x09;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyAlt = 0x12;
    private const ushort VirtualKeyVolumeDown = 0xAE;
    private const ushort VirtualKeyVolumeUp = 0xAF;

    public void MoveRelative(int deltaX, int deltaY) =>
        SendInputs(CreateMouseInput(deltaX, deltaY, MouseEventMove));

    public void SetLeftButton(bool pressed) =>
        SendInputs(CreateMouseInput(0, 0, pressed ? MouseEventLeftDown : MouseEventLeftUp));

    public void SetRightButton(bool pressed) =>
        SendInputs(CreateMouseInput(0, 0, pressed ? MouseEventRightDown : MouseEventRightUp));

    public void SetMiddleButton(bool pressed) =>
        SendInputs(CreateMouseInput(0, 0, pressed ? MouseEventMiddleDown : MouseEventMiddleUp));

    public void PerformShortcut(KeyboardShortcut shortcut) =>
        SendInputs(CreateChord(shortcut.Keys.Select(WindowsVirtualKey).ToArray()));

    public void SendText(string text)
    {
        foreach (char character in text)
        {
            if (character is '\b' or '\n')
            {
                SendInputs(CreateKeyPress(character == '\b' ? (ushort)0x08 : (ushort)0x0D));
                continue;
            }

            Input down = CreateUnicodeInput(character, false);
            Input up = CreateUnicodeInput(character, true);
            SendInputs(down, up);
        }
    }

    public void Dispose()
    {
    }

    private static Input CreateMouseInput(int deltaX, int deltaY, uint flags) =>
        new()
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput { DeltaX = deltaX, DeltaY = deltaY, Flags = flags },
            },
        };

    private static Input[] CreateKeyPress(ushort key) =>
        [CreateKeyboardInput(key, false), CreateKeyboardInput(key, true)];

    private static Input[] CreateChord(params ushort[] keys)
    {
        Input[] inputs = new Input[keys.Length * 2];
        for (int index = 0; index < keys.Length; index++)
        {
            inputs[index] = CreateKeyboardInput(keys[index], false);
            inputs[inputs.Length - index - 1] = CreateKeyboardInput(keys[index], true);
        }

        return inputs;
    }

    private static ushort WindowsVirtualKey(KeyboardKey key) => key switch
    {
        KeyboardKey.Ctrl => 0x11,
        KeyboardKey.Alt => VirtualKeyAlt,
        KeyboardKey.Shift => VirtualKeyShift,
        KeyboardKey.Meta => 0x5B,
        KeyboardKey.Tab => VirtualKeyTab,
        KeyboardKey.Delete => 0x2E,
        KeyboardKey.Space => 0x20,
        KeyboardKey.Enter => 0x0D,
        KeyboardKey.Escape => 0x1B,
        KeyboardKey.Left => 0x25,
        KeyboardKey.Up => 0x26,
        KeyboardKey.Right => 0x27,
        KeyboardKey.Down => 0x28,
        KeyboardKey.VolumeUp => VirtualKeyVolumeUp,
        KeyboardKey.VolumeDown => VirtualKeyVolumeDown,
        >= KeyboardKey.A and <= KeyboardKey.Z => (ushort)('A' + key - KeyboardKey.A),
        >= KeyboardKey.D0 and <= KeyboardKey.D9 => (ushort)('0' + key - KeyboardKey.D0),
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static Input CreateKeyboardInput(ushort key, bool released) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput { VirtualKey = key, Flags = released ? KeyEventKeyUp : 0 },
            },
        };

    private static Input CreateUnicodeInput(char character, bool released) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    ScanCode = character,
                    Flags = KeyEventUnicode | (released ? KeyEventKeyUp : 0),
                },
            },
        };

    private static void SendInputs(params Input[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rechazó la entrada de mouse.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int DeltaX;
        public int DeltaY;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}

internal sealed unsafe class LinuxUInputMouseController : IMouseController
{
    private const string DevicePath = "/dev/uinput";
    private const int OpenWriteOnly = 0x0001;
    private const int OpenNonBlocking = 0x0800;
    private const int ErrorTryAgain = 11;
    private const ushort BusVirtual = 0x06;
    private const ushort EventSynchronize = 0x00;
    private const ushort EventKey = 0x01;
    private const ushort EventRelative = 0x02;
    private const ushort SynchronizeReport = 0x00;
    private const ushort RelativeX = 0x00;
    private const ushort RelativeY = 0x01;
    private const ushort KeyTab = 15;
    private const ushort KeyLeftShift = 42;
    private const ushort KeyLeftAlt = 56;
    private const ushort KeyVolumeDown = 114;
    private const ushort KeyVolumeUp = 115;
    private const ushort ButtonLeft = 0x110;
    private const ushort ButtonRight = 0x111;
    private const ushort ButtonMiddle = 0x112;
    private const uint UInputIoctlBase = 0x55;
    private const uint IoctlWrite = 1;
    private const int IoctlDirectionShift = 30;
    private const int IoctlSizeShift = 16;
    private const int IoctlTypeShift = 8;
    private static readonly nuint UiDeviceCreate = Io(UInputIoctlBase, 1);
    private static readonly nuint UiDeviceDestroy = Io(UInputIoctlBase, 2);
    private static readonly nuint UiDeviceSetup = Iow(UInputIoctlBase, 3, (uint)sizeof(UInputSetup));
    private static readonly nuint UiSetEventBit = Iow(UInputIoctlBase, 100, sizeof(int));
    private static readonly nuint UiSetKeyBit = Iow(UInputIoctlBase, 101, sizeof(int));
    private static readonly nuint UiSetRelativeBit = Iow(UInputIoctlBase, 102, sizeof(int));
    private int fileDescriptor = -1;
    private static readonly IReadOnlyDictionary<char, (ushort Key, bool Shift)> TextKeys = CreateTextKeys();
    private static readonly IReadOnlyDictionary<KeyboardKey, ushort> ShortcutKeys = CreateShortcutKeys();

    public LinuxUInputMouseController()
    {
        fileDescriptor = Open(DevicePath, OpenWriteOnly | OpenNonBlocking);
        if (fileDescriptor < 0)
        {
            throw CreateNativeException("No se pudo abrir /dev/uinput. Verifica permisos del usuario.");
        }

        try
        {
            ConfigureDevice();
        }
        catch
        {
            Close(fileDescriptor);
            fileDescriptor = -1;
            throw;
        }
    }

    public void MoveRelative(int deltaX, int deltaY)
    {
        InputEvent* events = stackalloc InputEvent[3];
        events[0] = new(EventRelative, RelativeX, deltaX);
        events[1] = new(EventRelative, RelativeY, deltaY);
        events[2] = new(EventSynchronize, SynchronizeReport, 0);
        WriteEvents(events, 3);
    }

    public void SetLeftButton(bool pressed)
    {
        InputEvent* events = stackalloc InputEvent[2];
        events[0] = new(EventKey, ButtonLeft, pressed ? 1 : 0);
        events[1] = new(EventSynchronize, SynchronizeReport, 0);
        WriteEvents(events, 2);
    }

    public void SetRightButton(bool pressed)
    {
        InputEvent* events = stackalloc InputEvent[2];
        events[0] = new(EventKey, ButtonRight, pressed ? 1 : 0);
        events[1] = new(EventSynchronize, SynchronizeReport, 0);
        WriteEvents(events, 2);
    }

    public void SetMiddleButton(bool pressed)
    {
        InputEvent* events = stackalloc InputEvent[2];
        events[0] = new(EventKey, ButtonMiddle, pressed ? 1 : 0);
        events[1] = new(EventSynchronize, SynchronizeReport, 0);
        WriteEvents(events, 2);
    }

    public void PerformShortcut(KeyboardShortcut shortcut) =>
        WriteChord(shortcut.Keys.Select(key => ShortcutKeys[key]).ToArray());

    public void SendText(string text)
    {
        foreach (char character in text)
        {
            if (!TextKeys.TryGetValue(character, out (ushort Key, bool Shift) mapping))
                throw new InvalidOperationException($"Carácter no soportado por uinput: U+{(int)character:X4}.");
            if (mapping.Shift) WriteChord(KeyLeftShift, mapping.Key);
            else WriteKeyPress(mapping.Key);
        }
    }

    public void Dispose()
    {
        if (fileDescriptor < 0)
        {
            return;
        }

        IoctlValue(fileDescriptor, UiDeviceDestroy, 0);
        Close(fileDescriptor);
        fileDescriptor = -1;
    }

    private void ConfigureDevice()
    {
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetEventBit, EventSynchronize), "No se pudo habilitar EV_SYN.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetEventBit, EventKey), "No se pudo habilitar EV_KEY.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetEventBit, EventRelative), "No se pudo habilitar EV_REL.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, ButtonLeft), "No se pudo habilitar BTN_LEFT.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, ButtonRight), "No se pudo habilitar BTN_RIGHT.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, ButtonMiddle), "No se pudo habilitar BTN_MIDDLE.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, KeyTab), "No se pudo habilitar KEY_TAB.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, KeyLeftShift), "No se pudo habilitar KEY_LEFTSHIFT.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, KeyLeftAlt), "No se pudo habilitar KEY_LEFTALT.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, KeyVolumeDown), "No se pudo habilitar KEY_VOLUMEDOWN.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, KeyVolumeUp), "No se pudo habilitar KEY_VOLUMEUP.");
        foreach (ushort key in TextKeys.Values.Select(value => value.Key).Append(KeyLeftShift).Distinct())
        {
            EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, key), $"No se pudo habilitar tecla {key}.");
        }
        foreach (ushort key in ShortcutKeys.Values.Distinct())
            EnsureIoctl(IoctlValue(fileDescriptor, UiSetKeyBit, key), $"No se pudo habilitar tecla {key}.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetRelativeBit, RelativeX), "No se pudo habilitar REL_X.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiSetRelativeBit, RelativeY), "No se pudo habilitar REL_Y.");

        UInputSetup setup = default;
        setup.Id = new InputId
        {
            BusType = BusVirtual,
            Vendor = 0x045E,
            Product = 0x028E,
            Version = 1,
        };

        byte[] deviceName = Encoding.UTF8.GetBytes("XboxHid Virtual Input");
        for (int index = 0; index < deviceName.Length; index++)
        {
            setup.Name[index] = deviceName[index];
        }

        EnsureIoctl(IoctlSetup(fileDescriptor, UiDeviceSetup, ref setup), "No se pudo configurar dispositivo uinput.");
        EnsureIoctl(IoctlValue(fileDescriptor, UiDeviceCreate, 0), "No se pudo crear dispositivo virtual uinput.");
    }

    private void WriteKeyPress(ushort key)
    {
        InputEvent* events = stackalloc InputEvent[4];
        events[0] = new(EventKey, key, 1);
        events[1] = new(EventSynchronize, SynchronizeReport, 0);
        events[2] = new(EventKey, key, 0);
        events[3] = new(EventSynchronize, SynchronizeReport, 0);
        WriteEvents(events, 4);
    }

    private void WriteChord(params ushort[] keys)
    {
        int eventCount = (keys.Length * 2) + 2;
        InputEvent* events = stackalloc InputEvent[eventCount];
        int eventIndex = 0;

        foreach (ushort key in keys)
        {
            events[eventIndex++] = new(EventKey, key, 1);
        }

        events[eventIndex++] = new(EventSynchronize, SynchronizeReport, 0);
        for (int keyIndex = keys.Length - 1; keyIndex >= 0; keyIndex--)
        {
            events[eventIndex++] = new(EventKey, keys[keyIndex], 0);
        }

        events[eventIndex] = new(EventSynchronize, SynchronizeReport, 0);
        WriteEvents(events, eventCount);
    }

    private void WriteEvents(InputEvent* events, int count)
    {
        nuint expectedBytes = (nuint)(sizeof(InputEvent) * count);
        nint writtenBytes = Write(fileDescriptor, events, expectedBytes);
        if ((nuint)writtenBytes == expectedBytes)
        {
            return;
        }

        int error = Marshal.GetLastWin32Error();
        if (writtenBytes < 0 && error == ErrorTryAgain)
        {
            return;
        }

        throw new Win32Exception(error, "No se pudo escribir evento en /dev/uinput.");
    }

    private static IReadOnlyDictionary<char, (ushort Key, bool Shift)> CreateTextKeys()
    {
        Dictionary<char, (ushort, bool)> keys = new()
        {
            ['1'] = (2, false),
            ['2'] = (3, false),
            ['3'] = (4, false),
            ['4'] = (5, false),
            ['5'] = (6, false),
            ['6'] = (7, false),
            ['7'] = (8, false),
            ['8'] = (9, false),
            ['9'] = (10, false),
            ['0'] = (11, false),
            ['-'] = (12, false),
            ['='] = (13, false),
            ['\b'] = (14, false),
            ['q'] = (16, false),
            ['w'] = (17, false),
            ['e'] = (18, false),
            ['r'] = (19, false),
            ['t'] = (20, false),
            ['y'] = (21, false),
            ['u'] = (22, false),
            ['i'] = (23, false),
            ['o'] = (24, false),
            ['p'] = (25, false),
            ['['] = (26, false),
            [']'] = (27, false),
            ['\n'] = (28, false),
            ['a'] = (30, false),
            ['s'] = (31, false),
            ['d'] = (32, false),
            ['f'] = (33, false),
            ['g'] = (34, false),
            ['h'] = (35, false),
            ['j'] = (36, false),
            ['k'] = (37, false),
            ['l'] = (38, false),
            [';'] = (39, false),
            ['\''] = (40, false),
            ['z'] = (44, false),
            ['x'] = (45, false),
            ['c'] = (46, false),
            ['v'] = (47, false),
            ['b'] = (48, false),
            ['n'] = (49, false),
            ['m'] = (50, false),
            [','] = (51, false),
            ['.'] = (52, false),
            ['/'] = (53, false),
            [' '] = (57, false),
            ['_'] = (12, true),
            ['+'] = (13, true),
            ['?'] = (53, true),
            ['*'] = (9, true),
            ['('] = (10, true),
            [')'] = (11, true),
            ['@'] = (3, true),
            ['#'] = (4, true),
            ['$'] = (5, true),
            ['%'] = (6, true),
            ['!'] = (2, true),
            [':'] = (39, true),
            ['\"'] = (40, true),
        };
        return keys;
    }

    private static IReadOnlyDictionary<KeyboardKey, ushort> CreateShortcutKeys()
    {
        Dictionary<KeyboardKey, ushort> keys = new()
        {
            [KeyboardKey.Ctrl] = 29,
            [KeyboardKey.Alt] = 56,
            [KeyboardKey.Shift] = 42,
            [KeyboardKey.Meta] = 125,
            [KeyboardKey.Tab] = 15,
            [KeyboardKey.Delete] = 111,
            [KeyboardKey.Space] = 57,
            [KeyboardKey.Enter] = 28,
            [KeyboardKey.Escape] = 1,
            [KeyboardKey.Left] = 105,
            [KeyboardKey.Right] = 106,
            [KeyboardKey.Up] = 103,
            [KeyboardKey.Down] = 108,
            [KeyboardKey.VolumeUp] = 115,
            [KeyboardKey.VolumeDown] = 114
        };
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        for (int index = 0; index < letters.Length; index++) keys[KeyboardKey.A + index] = TextKeys[letters[index]].Key;
        for (int index = 0; index <= 9; index++) keys[KeyboardKey.D0 + index] = TextKeys[(char)('0' + index)].Key;
        return keys;
    }

    private static void EnsureIoctl(int result, string message)
    {
        if (result < 0)
        {
            throw CreateNativeException(message);
        }
    }

    private static Win32Exception CreateNativeException(string message) =>
        new(Marshal.GetLastWin32Error(), message);

    private static nuint Io(uint type, uint number) => (nuint)((type << IoctlTypeShift) | number);

    private static nuint Iow(uint type, uint number, uint size) =>
        (nuint)((IoctlWrite << IoctlDirectionShift) | (size << IoctlSizeShift) | (type << IoctlTypeShift) | number);

    [DllImport("libc", SetLastError = true, EntryPoint = "open")]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", SetLastError = true, EntryPoint = "close")]
    private static extern int Close(int fileDescriptor);

    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern int IoctlValue(int fileDescriptor, nuint request, int value);

    [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static extern int IoctlSetup(int fileDescriptor, nuint request, ref UInputSetup setup);

    [DllImport("libc", SetLastError = true, EntryPoint = "write")]
    private static extern nint Write(int fileDescriptor, InputEvent* events, nuint count);

    [StructLayout(LayoutKind.Sequential)]
    private struct InputId
    {
        public ushort BusType;
        public ushort Vendor;
        public ushort Product;
        public ushort Version;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UInputSetup
    {
        public InputId Id;
        public fixed byte Name[80];
        public uint ForceFeedbackEffectsMaximum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct InputEvent(ushort type, ushort code, int value)
    {
        private readonly nint seconds = 0;
        private readonly nint microseconds = 0;
        public readonly ushort Type = type;
        public readonly ushort Code = code;
        public readonly int Value = value;
    }
}

internal sealed class MacOsMouseController : IMouseController
{
    private const string ApplicationServices =
        "/System/Library/Frameworks/ApplicationServices.framework/Versions/Current/ApplicationServices";
    private const uint LeftMouseDown = 1;
    private const uint LeftMouseUp = 2;
    private const uint RightMouseDown = 3;
    private const uint RightMouseUp = 4;
    private const uint OtherMouseDown = 25;
    private const uint OtherMouseUp = 26;
    private const uint MouseMoved = 5;
    private const ushort KeyTab = 0x30;
    private const ushort KeyShift = 0x38;
    private const ushort KeyCommand = 0x37;
    private const ushort KeyVolumeUp = 0x48;
    private const ushort KeyVolumeDown = 0x49;
    private const ushort KeyReturn = 0x24;
    private const ushort KeyDelete = 0x33;

    public void MoveRelative(int deltaX, int deltaY)
    {
        nint probe = CGEventCreate(nint.Zero);
        if (probe == nint.Zero)
        {
            throw new InvalidOperationException("macOS no permitió leer posición del cursor.");
        }

        CGPoint position = CGEventGetLocation(probe);
        CFRelease(probe);
        PostMouseEvent(MouseMoved, new(position.X + deltaX, position.Y + deltaY));
    }

    public void SetLeftButton(bool pressed)
    {
        nint probe = CGEventCreate(nint.Zero);
        if (probe == nint.Zero)
        {
            throw new InvalidOperationException("macOS no permitió leer posición del cursor.");
        }

        CGPoint position = CGEventGetLocation(probe);
        CFRelease(probe);
        PostMouseEvent(pressed ? LeftMouseDown : LeftMouseUp, position, 0);
    }

    public void SetRightButton(bool pressed)
    {
        nint probe = CGEventCreate(nint.Zero);
        if (probe == nint.Zero)
        {
            throw new InvalidOperationException("macOS no permitió leer posición del cursor.");
        }

        CGPoint position = CGEventGetLocation(probe);
        CFRelease(probe);
        PostMouseEvent(pressed ? RightMouseDown : RightMouseUp, position, 1);
    }

    public void SetMiddleButton(bool pressed)
    {
        nint probe = CGEventCreate(nint.Zero);
        if (probe == nint.Zero) throw new InvalidOperationException("macOS no permitió leer posición del cursor.");
        CGPoint position = CGEventGetLocation(probe);
        CFRelease(probe);
        PostMouseEvent(pressed ? OtherMouseDown : OtherMouseUp, position, 2);
    }

    public void PerformShortcut(KeyboardShortcut shortcut) =>
        PostChord(shortcut.Keys.Select(MacVirtualKey).ToArray());

    public void SendText(string text)
    {
        foreach (char character in text)
        {
            if (character is '\b' or '\n')
            {
                PostKeyPress(character == '\b' ? KeyDelete : KeyReturn);
                continue;
            }

            nint down = CGEventCreateKeyboardEvent(nint.Zero, 0, true);
            nint up = CGEventCreateKeyboardEvent(nint.Zero, 0, false);
            if (down == nint.Zero || up == nint.Zero)
                throw new InvalidOperationException("macOS rechazó evento de texto.");
            string value = character.ToString();
            CGEventKeyboardSetUnicodeString(down, 1, value);
            CGEventKeyboardSetUnicodeString(up, 1, value);
            CGEventPost(0, down);
            CGEventPost(0, up);
            CFRelease(down);
            CFRelease(up);
        }
    }

    public void Dispose()
    {
    }

    private static void PostMouseEvent(uint type, CGPoint position, uint button = 0)
    {
        nint mouseEvent = CGEventCreateMouseEvent(nint.Zero, type, position, button);
        if (mouseEvent == nint.Zero)
        {
            throw new InvalidOperationException(
                "macOS rechazó evento de mouse. Concede permiso de Accesibilidad a la terminal.");
        }

        CGEventPost(0, mouseEvent);
        CFRelease(mouseEvent);
    }

    private static void PostKeyPress(ushort key)
    {
        PostKeyboardEvent(key, true);
        PostKeyboardEvent(key, false);
    }

    private static void PostChord(params ushort[] keys)
    {
        foreach (ushort key in keys)
        {
            PostKeyboardEvent(key, true);
        }

        for (int index = keys.Length - 1; index >= 0; index--)
        {
            PostKeyboardEvent(keys[index], false);
        }
    }

    private static void PostKeyboardEvent(ushort key, bool pressed)
    {
        nint keyboardEvent = CGEventCreateKeyboardEvent(nint.Zero, key, pressed);
        if (keyboardEvent == nint.Zero)
        {
            throw new InvalidOperationException(
                "macOS rechazó evento de teclado. Concede permiso de Accesibilidad a la terminal.");
        }

        CGEventPost(0, keyboardEvent);
        CFRelease(keyboardEvent);
    }

    private static ushort MacVirtualKey(KeyboardKey key) => key switch
    {
        KeyboardKey.Ctrl => 0x3B,
        KeyboardKey.Alt => 0x3A,
        KeyboardKey.Shift => KeyShift,
        KeyboardKey.Meta => KeyCommand,
        KeyboardKey.Tab => KeyTab,
        KeyboardKey.Delete => 0x75,
        KeyboardKey.Space => 0x31,
        KeyboardKey.Enter => KeyReturn,
        KeyboardKey.Escape => 0x35,
        KeyboardKey.Left => 0x7B,
        KeyboardKey.Right => 0x7C,
        KeyboardKey.Down => 0x7D,
        KeyboardKey.Up => 0x7E,
        KeyboardKey.VolumeUp => KeyVolumeUp,
        KeyboardKey.VolumeDown => KeyVolumeDown,
        >= KeyboardKey.A and <= KeyboardKey.Z => MacLetterKeys[(int)(key - KeyboardKey.A)],
        >= KeyboardKey.D0 and <= KeyboardKey.D9 => MacDigitKeys[(int)(key - KeyboardKey.D0)],
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static readonly ushort[] MacLetterKeys =
        [0, 11, 8, 2, 14, 3, 5, 4, 34, 38, 40, 37, 46, 45, 31, 35, 12, 15, 1, 17, 32, 9, 13, 7, 16, 6];
    private static readonly ushort[] MacDigitKeys = [29, 18, 19, 20, 21, 23, 22, 26, 28, 25];

    [DllImport(ApplicationServices)]
    private static extern nint CGEventCreate(nint source);

    [DllImport(ApplicationServices)]
    private static extern CGPoint CGEventGetLocation(nint mouseEvent);

    [DllImport(ApplicationServices)]
    private static extern nint CGEventCreateMouseEvent(nint source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [DllImport(ApplicationServices)]
    private static extern nint CGEventCreateKeyboardEvent(nint source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [DllImport(ApplicationServices, CharSet = CharSet.Unicode)]
    private static extern void CGEventKeyboardSetUnicodeString(nint keyboardEvent, nint stringLength, string unicodeString);

    [DllImport(ApplicationServices)]
    private static extern void CGEventPost(uint tap, nint mouseEvent);

    [DllImport(ApplicationServices)]
    private static extern void CFRelease(nint value);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CGPoint(double X, double Y);
}
