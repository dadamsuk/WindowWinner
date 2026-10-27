// WindowWinner - move every top-level window onto one chosen monitor.
//
// Usage:
//   WindowWinner.exe             show a picker of connected monitors
//   WindowWinner.exe 2           move everything to monitor 2 (no UI)
//   WindowWinner.exe primary     move everything to the primary monitor (no UI)
//
// Build (no SDK needed, uses the .NET Framework compiler that ships with Windows):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /codepage:65001 /target:winexe /optimize /out:WindowWinner.exe WindowWinner.cs

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WindowWinner
{
    class MonitorInfo
    {
        public IntPtr Handle;
        public int Number;          // 1-based, ordered left-to-right then top-to-bottom
        public string DeviceName;   // \\.\DISPLAY1
        public string FriendlyName; // "DELL U2720Q"
        public Rectangle Bounds;    // physical pixels
        public Rectangle WorkArea;  // physical pixels, excludes taskbar
        public bool Primary;

        public string Label
        {
            get
            {
                return string.Format("{0}: {1}  ({2}×{3}){4}", Number, FriendlyName,
                    Bounds.Width, Bounds.Height, Primary ? "  · primary" : "");
            }
        }
    }

    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }
            public static RECT From(Rectangle r) { RECT x; x.Left = r.Left; x.Top = r.Top; x.Right = r.Right; x.Bottom = r.Bottom; return x; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rc, IntPtr data);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX mi);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromRect(ref RECT rc, int flags);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public const int MONITOR_DEFAULTTONEAREST = 2;
        public const int MONITORINFOF_PRIMARY = 1;
        public const int GW_OWNER = 4;
        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const long WS_CHILD = 0x40000000L;
        public const long WS_EX_TOOLWINDOW = 0x80L, WS_EX_APPWINDOW = 0x40000L, WS_EX_NOACTIVATE = 0x08000000L;
        public const int SW_SHOWMINIMIZED = 2, SW_SHOWMAXIMIZED = 3, SW_SHOWNOACTIVATE = 4, SW_SHOWMINNOACTIVE = 7;
        public const int WPF_RESTORETOMAXIMIZED = 2;
        public const int DWMWA_CLOAKED = 14;
        public static readonly IntPtr DPI_SYSTEM_AWARE = new IntPtr(-2);
        public static readonly IntPtr DPI_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        // ---- Display configuration API: used only to get monitors' friendly names ----

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID { public uint Low; public int High; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public LUID srcAdapter; public uint srcId; public uint srcModeIdx; public uint srcFlags;
            public LUID tgtAdapter; public uint tgtId; public uint tgtModeIdx;
            public uint outputTech, rotation, scaling;
            public uint refreshNum, refreshDen;
            public uint scanline; public int targetAvailable; public uint tgtFlags;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType; public uint id; public LUID adapterId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)] public byte[] modeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public int type; public int size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags, outputTechnology;
            public ushort edidManufactureId, edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        public const uint QDC_ONLY_ACTIVE_PATHS = 2;
        public const int GET_SOURCE_NAME = 1, GET_TARGET_NAME = 2;

        [DllImport("user32.dll")] public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
        [DllImport("user32.dll")] public static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] DISPLAYCONFIG_PATH_INFO[] paths, ref uint numModes, [Out] DISPLAYCONFIG_MODE_INFO[] modes, IntPtr topology);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME info);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME info);
    }

    static class Winner
    {
        // Run fn with per-monitor DPI awareness so every coordinate is in real physical pixels,
        // regardless of the scaling set on each monitor.
        static T PerMonitor<T>(Func<T> fn)
        {
            IntPtr old = IntPtr.Zero;
            try { old = Native.SetThreadDpiAwarenessContext(Native.DPI_PER_MONITOR_AWARE_V2); } catch (EntryPointNotFoundException) { }
            try { return fn(); }
            finally { if (old != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(old); }
        }

        public static List<MonitorInfo> GetMonitors()
        {
            return PerMonitor(() =>
            {
                var names = GetFriendlyNames();
                var list = new List<MonitorInfo>();
                Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref Native.RECT rc, IntPtr d) =>
                {
                    var mi = new Native.MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFOEX));
                    if (Native.GetMonitorInfo(h, ref mi))
                    {
                        string friendly;
                        if (!names.TryGetValue(mi.szDevice, out friendly) || string.IsNullOrEmpty(friendly))
                            friendly = mi.szDevice.Replace(@"\\.\", "");
                        list.Add(new MonitorInfo
                        {
                            Handle = h,
                            DeviceName = mi.szDevice,
                            FriendlyName = friendly,
                            Bounds = mi.rcMonitor.ToRectangle(),
                            WorkArea = mi.rcWork.ToRectangle(),
                            Primary = (mi.dwFlags & Native.MONITORINFOF_PRIMARY) != 0
                        });
                    }
                    return true;
                }, IntPtr.Zero);

                list.Sort((a, b) => a.Bounds.Left != b.Bounds.Left ? a.Bounds.Left.CompareTo(b.Bounds.Left) : a.Bounds.Top.CompareTo(b.Bounds.Top));
                for (int i = 0; i < list.Count; i++) list[i].Number = i + 1;
                return list;
            });
        }

        // Maps \\.\DISPLAYn -> monitor model name (e.g. "DELL U2720Q"). Best effort.
        static Dictionary<string, string> GetFriendlyNames()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                uint np, nm;
                if (Native.GetDisplayConfigBufferSizes(Native.QDC_ONLY_ACTIVE_PATHS, out np, out nm) != 0) return result;
                var paths = new Native.DISPLAYCONFIG_PATH_INFO[np];
                var modes = new Native.DISPLAYCONFIG_MODE_INFO[nm];
                if (Native.QueryDisplayConfig(Native.QDC_ONLY_ACTIVE_PATHS, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return result;
                for (int i = 0; i < np; i++)
                {
                    var src = new Native.DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    src.header.type = Native.GET_SOURCE_NAME;
                    src.header.size = Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    src.header.adapterId = paths[i].srcAdapter;
                    src.header.id = paths[i].srcId;
                    if (Native.DisplayConfigGetDeviceInfo(ref src) != 0) continue;

                    var tgt = new Native.DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    tgt.header.type = Native.GET_TARGET_NAME;
                    tgt.header.size = Marshal.SizeOf(typeof(Native.DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    tgt.header.adapterId = paths[i].tgtAdapter;
                    tgt.header.id = paths[i].tgtId;
                    if (Native.DisplayConfigGetDeviceInfo(ref tgt) != 0) continue;

                    string name = tgt.monitorFriendlyDeviceName;
                    if (string.IsNullOrEmpty(name)) name = tgt.outputTechnology == 0x80000000 ? "Built-in display" : null;
                    if (name != null && !result.ContainsKey(src.viewGdiDeviceName)) result[src.viewGdiDeviceName] = name;
                }
            }
            catch (Exception) { }
            return result;
        }

        // The windows a person would think of as "their windows": the ones shown in Alt+Tab.
        static List<IntPtr> GetAppWindows(IntPtr exclude)
        {
            var list = new List<IntPtr>();
            IntPtr shell = Native.GetShellWindow();
            Native.EnumWindows((h, l) =>
            {
                if (h == exclude || h == shell || !Native.IsWindowVisible(h)) return true;
                long style = Native.GetWindowLongPtr(h, Native.GWL_STYLE).ToInt64();
                long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
                if ((style & Native.WS_CHILD) != 0) return true;
                bool appWindow = (ex & Native.WS_EX_APPWINDOW) != 0;
                if (!appWindow && ((ex & Native.WS_EX_TOOLWINDOW) != 0 || (ex & Native.WS_EX_NOACTIVATE) != 0)) return true;
                if (!appWindow && Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero) return true;
                if (Native.GetWindowTextLength(h) == 0) return true;

                int cloaked;
                if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;

                var cls = new StringBuilder(256);
                Native.GetClassName(h, cls, cls.Capacity);
                string c = cls.ToString();
                if (c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd" || c == "Progman" || c == "WorkerW") return true;

                list.Add(h);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        // Moves all app windows to the target monitor. Windows keep their state (normal /
        // maximised / minimised), their relative position, and their size where it fits.
        // Returns (moved, failed). Failures are usually windows of programs running as
        // administrator, which a non-elevated program isn't allowed to move.
        public static int[] MoveAllTo(MonitorInfo target, IntPtr exclude)
        {
            return PerMonitor(() =>
            {
                var monitors = GetMonitors();
                var primary = monitors.Find(m => m.Primary) ?? monitors[0];
                int moved = 0, failed = 0;

                foreach (IntPtr h in GetAppWindows(exclude))
                {
                    var wp = new Native.WINDOWPLACEMENT();
                    wp.length = Marshal.SizeOf(typeof(Native.WINDOWPLACEMENT));
                    if (!Native.GetWindowPlacement(h, ref wp)) { failed++; continue; }

                    // rcNormalPosition is in "workspace" coordinates (relative to the primary
                    // monitor's work area) unless the window is a tool window.
                    long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
                    Point offset = (ex & Native.WS_EX_TOOLWINDOW) != 0 ? Point.Empty
                        : new Point(primary.WorkArea.Left - primary.Bounds.Left, primary.WorkArea.Top - primary.Bounds.Top);
                    Rectangle normal = wp.rcNormalPosition.ToRectangle();
                    normal.Offset(offset);

                    bool minimized = wp.showCmd == Native.SW_SHOWMINIMIZED;
                    IntPtr srcHandle;
                    if (minimized) { var r = Native.RECT.From(normal); srcHandle = Native.MonitorFromRect(ref r, Native.MONITOR_DEFAULTTONEAREST); }
                    else srcHandle = Native.MonitorFromWindow(h, Native.MONITOR_DEFAULTTONEAREST);

                    if (srcHandle == target.Handle) continue;
                    var source = monitors.Find(m => m.Handle == srcHandle);
                    if (source == null) continue;

                    Rectangle dest = MapRect(normal, source.WorkArea, target.WorkArea);
                    dest.Offset(-offset.X, -offset.Y);
                    wp.rcNormalPosition = Native.RECT.From(dest);
                    wp.flags = wp.flags & Native.WPF_RESTORETOMAXIMIZED;

                    if (minimized) wp.showCmd = Native.SW_SHOWMINNOACTIVE;
                    else if (wp.showCmd != Native.SW_SHOWMAXIMIZED) wp.showCmd = Native.SW_SHOWNOACTIVATE;

                    if (Native.SetWindowPlacement(h, ref wp)) moved++; else failed++;
                }
                return new[] { moved, failed };
            });
        }

        // Same relative position on the new monitor; shrink if it won't fit; keep fully on-screen.
        static Rectangle MapRect(Rectangle r, Rectangle from, Rectangle to)
        {
            int w = Math.Min(r.Width, to.Width);
            int h = Math.Min(r.Height, to.Height);
            double fx = from.Width > r.Width ? (double)(r.Left - from.Left) / (from.Width - r.Width) : 0;
            double fy = from.Height > r.Height ? (double)(r.Top - from.Top) / (from.Height - r.Height) : 0;
            fx = Math.Max(0, Math.Min(1, fx));
            fy = Math.Max(0, Math.Min(1, fy));
            int x = to.Left + (int)Math.Round(fx * (to.Width - w));
            int y = to.Top + (int)Math.Round(fy * (to.Height - h));
            return new Rectangle(x, y, w, h);
        }
    }


    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(30, 31, 34);
        public static readonly Color Card = Color.FromArgb(43, 45, 49);
        public static readonly Color CardHover = Color.FromArgb(53, 56, 62);
        public static readonly Color Border = Color.FromArgb(64, 67, 74);
        public static readonly Color Text = Color.FromArgb(240, 241, 243);
        public static readonly Color Muted = Color.FromArgb(158, 162, 170);
        public static readonly Color Accent = Color.FromArgb(76, 154, 255);
        public static readonly Color AccentDeep = Color.FromArgb(34, 98, 198);
        public static readonly Color Success = Color.FromArgb(112, 204, 142);
        public static readonly Color Warn = Color.FromArgb(240, 182, 92);

        static float dpiScale = -1;

        // Pixels at 96 DPI -> pixels at the current system DPI.
        public static int S(int px)
        {
            if (dpiScale < 0)
                using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpiScale = g.DpiX / 96f;
            return (int)Math.Round(px * dpiScale);
        }

        public static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    // A block of text that wraps to its width and sizes its own height to fit.
    class TextBlock : Control
    {
        const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;

        public TextBlock()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        void Fit()
        {
            int w = Math.Max(1, Width - Padding.Horizontal);
            int h = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font, new Size(w, int.MaxValue), Flags).Height;
            int want = h + Padding.Vertical;
            if (Height != want) Height = want;
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Fit(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Fit(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Fit(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            var r = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
            TextRenderer.DrawText(e.Graphics, Text, Font, r, ForeColor, Flags);
        }
    }

    // Draws the monitors in their real arrangement; click one to pick it.
    class LayoutView : Control
    {
        public List<MonitorInfo> Monitors = new List<MonitorInfo>();
        public event Action<MonitorInfo> Picked;
        public event Action<int> HoverChanged;
        public int HotNumber = -1;        // highlighted (hovered here or in the list)
        public int SelectedNumber = -1;   // the monitor windows were last moved to
        readonly List<Rectangle> drawn = new List<Rectangle>();

        public LayoutView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        void ComputeRects()
        {
            drawn.Clear();
            if (Monitors.Count == 0) return;
            Rectangle all = Monitors[0].Bounds;
            foreach (var m in Monitors) all = Rectangle.Union(all, m.Bounds);
            int pad = Theme.S(6);
            double scale = Math.Min((Width - 2.0 * pad) / all.Width, (Height - 2.0 * pad) / all.Height);
            if (scale <= 0) return;
            int ox = (int)((Width - all.Width * scale) / 2), oy = (int)((Height - all.Height * scale) / 2);
            int gap = Theme.S(3);
            foreach (var m in Monitors)
            {
                var b = m.Bounds;
                var r = new Rectangle(ox + (int)((b.Left - all.Left) * scale), oy + (int)((b.Top - all.Top) * scale),
                                      (int)(b.Width * scale), (int)(b.Height * scale));
                r.Inflate(-gap, -gap);
                drawn.Add(r);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ComputeRects();
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            using (var numFont = new Font("Segoe UI Semibold", 20f))
            using (var nameFont = new Font("Segoe UI", 8.5f))
            {
                for (int i = 0; i < drawn.Count; i++)
                {
                    var r = drawn[i];
                    var m = Monitors[i];
                    bool hot = m.Number == HotNumber;
                    bool selected = m.Number == SelectedNumber;

                    Color top = hot ? Theme.Accent : Color.FromArgb(62, 66, 74);
                    Color bottom = hot ? Theme.AccentDeep : Color.FromArgb(44, 47, 53);
                    using (var path = Theme.Rounded(r, Theme.S(8)))
                    using (var fill = new LinearGradientBrush(new Rectangle(r.X, r.Y - 1, r.Width, r.Height + 2), top, bottom, 90f))
                    using (var pen = new Pen(selected ? Theme.Success : hot ? Theme.Mix(Theme.Accent, Color.White, 0.4f) : Theme.Border, Theme.S(selected ? 3 : 1)))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(pen, path);
                    }

                    // Taskbar hint on the primary monitor.
                    if (m.Primary && r.Height > Theme.S(30))
                    {
                        var bar = new Rectangle(r.X + Theme.S(6), r.Bottom - Theme.S(7), r.Width - Theme.S(12), Theme.S(3));
                        using (var b = new SolidBrush(Color.FromArgb(hot ? 140 : 90, 255, 255, 255))) g.FillRectangle(b, bar);
                    }

                    // Number, then the model name underneath if there is room for it.
                    var numSize = TextRenderer.MeasureText(m.Number.ToString(), numFont);
                    bool roomForName = r.Height > numSize.Height + nameFont.Height + Theme.S(12) && r.Width > Theme.S(60);
                    int blockH = numSize.Height + (roomForName ? nameFont.Height : 0);
                    int y = r.Y + (r.Height - blockH) / 2;
                    TextRenderer.DrawText(g, m.Number.ToString(), numFont, new Rectangle(r.X, y, r.Width, numSize.Height), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    if (roomForName)
                        TextRenderer.DrawText(g, m.FriendlyName, nameFont, new Rectangle(r.X + Theme.S(6), y + numSize.Height, r.Width - Theme.S(12), nameFont.Height),
                            hot ? Color.White : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
            }
        }

        int HitTest(Point p) { for (int i = 0; i < drawn.Count; i++) if (drawn[i].Contains(p)) return Monitors[i].Number; return -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int n = HitTest(e.Location);
            if (n != HotNumber && HoverChanged != null) HoverChanged(n);
        }

        protected override void OnMouseLeave(EventArgs e) { if (HoverChanged != null) HoverChanged(-1); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int n = HitTest(e.Location);
            if (n > 0 && Picked != null) Picked(Monitors[n - 1]);
        }
    }

    // One clickable card per monitor: number badge, name, resolution, primary tag.
    class MonitorRow : Control
    {
        public readonly MonitorInfo Monitor;
        public bool Hot, Selected;
        public event Action<MonitorInfo> Picked;
        public event Action<int> HoverChanged;
        public static int Gap { get { return Theme.S(6); } }

        public MonitorRow(MonitorInfo m)
        {
            Monitor = m;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = Theme.S(54) + Gap;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            var card = new Rectangle(0, 0, Width - 1, Height - Gap - 1);
            using (var path = Theme.Rounded(card, Theme.S(8)))
            using (var fill = new SolidBrush(Hot ? Theme.CardHover : Theme.Card))
            using (var pen = new Pen(Selected ? Theme.Success : Hot ? Theme.Accent : Theme.Border, Theme.S(Selected ? 2 : 1)))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            // Number badge.
            int d = card.Height - Theme.S(20);
            var badge = new Rectangle(card.X + Theme.S(12), card.Y + (card.Height - d) / 2, d, d);
            using (var b = new SolidBrush(Hot ? Theme.Accent : Theme.AccentDeep)) g.FillEllipse(b, badge);
            using (var f = new Font("Segoe UI Semibold", 10.5f))
                TextRenderer.DrawText(g, Monitor.Number.ToString(), f, badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            // Primary tag on the right.
            int right = card.Right - Theme.S(12);
            using (var tagFont = new Font("Segoe UI", 8f))
            {
                if (Monitor.Primary)
                {
                    var ts = TextRenderer.MeasureText("Primary", tagFont);
                    var tag = new Rectangle(right - ts.Width - Theme.S(14), card.Y + (card.Height - ts.Height - Theme.S(6)) / 2, ts.Width + Theme.S(14), ts.Height + Theme.S(6));
                    using (var path = Theme.Rounded(tag, tag.Height / 2))
                    using (var pen = new Pen(Theme.Mix(Theme.Accent, Theme.Card, 0.3f)))
                    using (var fill = new SolidBrush(Color.FromArgb(40, Theme.Accent)))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(pen, path);
                    }
                    TextRenderer.DrawText(g, "Primary", tagFont, tag, Theme.Mix(Theme.Accent, Color.White, 0.35f),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    right = tag.Left - Theme.S(10);
                }
            }

            // Name and details.
            int textX = badge.Right + Theme.S(12);
            using (var nameFont = new Font("Segoe UI Semibold", 10f))
            using (var detailFont = new Font("Segoe UI", 8.5f))
            {
                int total = nameFont.Height + detailFont.Height;
                int y = card.Y + (card.Height - total) / 2;
                var tf = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(g, Monitor.FriendlyName, nameFont, new Rectangle(textX, y, right - textX, nameFont.Height), Theme.Text, tf);
                string details = string.Format("{0} × {1}   ·   press {2}", Monitor.Bounds.Width, Monitor.Bounds.Height, Monitor.Number);
                TextRenderer.DrawText(g, details, detailFont, new Rectangle(textX, y + nameFont.Height, right - textX, detailFont.Height), Theme.Muted, tf);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { if (HoverChanged != null) HoverChanged(Monitor.Number); }
        protected override void OnMouseLeave(EventArgs e) { if (HoverChanged != null) HoverChanged(-1); }
        protected override void OnMouseClick(MouseEventArgs e) { if (Picked != null) Picked(Monitor); }
    }

    class PickerForm : Form
    {
        readonly LayoutView view = new LayoutView();
        readonly Panel rows = new Panel();
        readonly TextBlock status = new TextBlock();
        List<MonitorInfo> monitors = new List<MonitorInfo>();
        int selected = -1;

        public PickerForm()
        {
            Text = "Window Winner";
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Padding = new Padding(Theme.S(20), Theme.S(14), Theme.S(20), Theme.S(12));
            ClientSize = new Size(Theme.S(540), Theme.S(560));
            MinimumSize = new Size(Theme.S(380), Theme.S(440));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            var title = new TextBlock
            {
                Dock = DockStyle.Top, Text = "Move all windows to…",
                Font = new Font("Segoe UI Semibold", 15f), ForeColor = Theme.Text, BackColor = Theme.Back
            };
            var subtitle = new TextBlock
            {
                Dock = DockStyle.Top, ForeColor = Theme.Muted, BackColor = Theme.Back, Padding = new Padding(0, Theme.S(2), 0, Theme.S(4)),
                Text = "Pick a monitor below or on the map, or press its number. Every open window moves there, keeping its size and maximised or minimised state. Esc closes."
            };

            view.Dock = DockStyle.Fill;
            view.BackColor = Theme.Back;
            view.Picked += MoveTo;
            view.HoverChanged += SetHot;

            rows.Dock = DockStyle.Bottom;
            rows.BackColor = Theme.Back;

            status.Dock = DockStyle.Bottom;
            status.BackColor = Theme.Back;
            status.ForeColor = Theme.Muted;
            status.Padding = new Padding(0, Theme.S(4), 0, 0);
            status.Font = new Font("Segoe UI", 8.5f);
            status.Text = "Tip: run WindowWinner.exe 2 (or primary) to move everything without this window.";

            // Docking order is the reverse of the add order: status ends up at the very bottom,
            // the title at the very top, and the map fills whatever space is left.
            Controls.Add(view);
            Controls.Add(subtitle);
            Controls.Add(title);
            Controls.Add(rows);
            Controls.Add(status);

            Reload();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        }

        void OnDisplaysChanged(object s, EventArgs e) { if (IsHandleCreated) BeginInvoke(new Action(Reload)); }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
            base.OnFormClosed(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1; // dark title bar to match the window (Windows 10 20H1+ / 11)
            try { Native.DwmSetWindowAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4); } catch (Exception) { }
        }

        void Reload()
        {
            monitors = Winner.GetMonitors();
            view.Monitors = monitors;
            view.Invalidate();

            rows.SuspendLayout();
            rows.Controls.Clear();
            for (int i = monitors.Count - 1; i >= 0; i--) // docked Top, so add bottom-up
            {
                var row = new MonitorRow(monitors[i]) { Dock = DockStyle.Top, BackColor = Theme.Back };
                row.Picked += MoveTo;
                row.HoverChanged += SetHot;
                rows.Controls.Add(row);
            }
            rows.Height = Theme.S(10) + monitors.Count * (Theme.S(54) + MonitorRow.Gap);
            rows.Padding = new Padding(0, Theme.S(10), 0, 0);
            rows.ResumeLayout();
            SetHot(-1);
        }

        void SetHot(int number)
        {
            view.HotNumber = number;
            view.SelectedNumber = selected;
            view.Invalidate();
            foreach (MonitorRow r in rows.Controls)
            {
                r.Hot = r.Monitor.Number == number;
                r.Selected = r.Monitor.Number == selected;
                r.Invalidate();
            }
        }

        void MoveTo(MonitorInfo m)
        {
            Cursor = Cursors.WaitCursor;
            int[] r = Winner.MoveAllTo(m, Handle);
            Cursor = Cursors.Default;
            selected = m.Number;
            SetHot(view.HotNumber);
            status.ForeColor = r[1] > 0 ? Theme.Warn : Theme.Success;
            status.Text = string.Format("✓ Moved {0} window{1} to {2} (monitor {3}).{4}", r[0], r[0] == 1 ? "" : "s", m.FriendlyName, m.Number,
                r[1] > 0 ? string.Format(" {0} couldn't be moved, probably because they're running as administrator.", r[1]) : "");
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); return; }
            int n = -1;
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9) n = e.KeyCode - Keys.D1 + 1;
            if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9) n = e.KeyCode - Keys.NumPad1 + 1;
            if (n > 0 && n <= monitors.Count) MoveTo(monitors[n - 1]);
            base.OnKeyDown(e);
        }
    }


    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try { Native.SetProcessDpiAwarenessContext(Native.DPI_SYSTEM_AWARE); } catch (EntryPointNotFoundException) { }

            if (args.Length > 0)
            {
                var monitors = Winner.GetMonitors();
                string a = args[0].Trim().TrimStart('-', '/').ToLowerInvariant();
                MonitorInfo target = null;
                int n;
                if (a == "primary" || a == "p") target = monitors.Find(m => m.Primary);
                else if (int.TryParse(a, out n) && n >= 1 && n <= monitors.Count) target = monitors[n - 1];

                if (target == null)
                {
                    var sb = new StringBuilder("Usage: WindowWinner.exe [monitor number | primary]\n\nConnected monitors:\n");
                    foreach (var m in monitors) sb.AppendLine("  " + m.Label);
                    MessageBox.Show(sb.ToString(), "Window Winner", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 1;
                }
                Winner.MoveAllTo(target, IntPtr.Zero);
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PickerForm());
            return 0;
        }
    }
}
