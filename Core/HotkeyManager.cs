using System.Runtime.InteropServices;

namespace LastFmScrobbler.Core;

public sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int  WM_HOTKEY   = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT     = 0x0001;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 0x9000;

    public HotkeyManager() => CreateHandle(new CreateParams());

    // Ctrl+Alt+key
    public void Register(Keys key, Action action)
    {
        var id = _nextId++;
        if (RegisterHotKey(Handle, id, MOD_CONTROL | MOD_ALT, (uint)key))
            _actions[id] = action;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _actions.TryGetValue(m.WParam.ToInt32(), out var a)) a();
        else base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(Handle, id);
        _actions.Clear();
        DestroyHandle();
    }
}
