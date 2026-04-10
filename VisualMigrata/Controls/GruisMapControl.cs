using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;     // <--- Added for Brushes and DrawingContext
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace VisualMigrata;

public class GruisMapControl : OpenGlControlBase
{
    private IntPtr _engineHandle = IntPtr.Zero;
    private Stopwatch _stopwatch = new Stopwatch();
    private double _lastTime = 0;

    // Thread-safe sizing & Scaling
    private int _targetWidth = 1280;
    private int _targetHeight = 720;
    private int _currentWidth = 0;
    private int _currentHeight = 0;

    // Thread-safe data queues
    private readonly Lock _dataLock = new();
    private GruisNodeData[]? _pendingNodes;
    private GruisMigrationData[]? _pendingMigrations;
    private string? _pendingJson;

    // --- INTERCEPTOR DELEGATES ---
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlBindFramebufferDelegate(uint target, uint framebuffer);

    private GruisGLLoaderFunc? _glLoaderDelegate;
    private GlBindFramebufferDelegate? _originalBindFramebuffer;
    private GlBindFramebufferDelegate? _hookedBindFramebuffer;
    private IntPtr _hookedBindFbPtr = IntPtr.Zero;
    private int _currentAvaloniaFb = 0;

    public GruisMapControl()
    {
        // CRITICAL: Ensure the control can receive input focus natively
        Focusable = true; 
    }

    /// <summary>
    /// Avalonia-native hit-test fix: Draw an invisible solid rectangle so the 
    /// Avalonia input system knows this OpenGL surface catches the mouse.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        // Draw the transparent hit-box
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        
        // Continue with the underlying OpenGL Custom Draw Operation
        base.Render(context);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        // 1. Intercept glBindFramebuffer to route the engine's final pass to Avalonia
        _glLoaderDelegate = name =>
        {
            if (name == "glBindFramebuffer")
            {
                if (_hookedBindFbPtr == IntPtr.Zero)
                {
                    IntPtr realPtr = gl.GetProcAddress("glBindFramebuffer");
                    if (realPtr != IntPtr.Zero)
                    {
                        _originalBindFramebuffer = Marshal.GetDelegateForFunctionPointer<GlBindFramebufferDelegate>(realPtr);
                        _hookedBindFramebuffer = (target, framebuffer) =>
                        {
                            uint fbToBind = (framebuffer == 0 && _currentAvaloniaFb != 0) ? (uint)_currentAvaloniaFb : framebuffer;
                            _originalBindFramebuffer(target, fbToBind);
                        };
                        _hookedBindFbPtr = Marshal.GetFunctionPointerForDelegate(_hookedBindFramebuffer);
                    }
                }
                return _hookedBindFbPtr != IntPtr.Zero ? _hookedBindFbPtr : gl.GetProcAddress(name);
            }
            return gl.GetProcAddress(name);
        };

        // 2. Initial Setup using Physical Pixels
        double dpiScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        _targetWidth = Math.Max(1, (int)(Bounds.Width * dpiScale));
        _targetHeight = Math.Max(1, (int)(Bounds.Height * dpiScale));

        _engineHandle = GruisInterop.Gruis_Initialize(_targetWidth, _targetHeight, _glLoaderDelegate);

        if (_engineHandle == IntPtr.Zero) throw new Exception("GruisEngine failed to initialize.");
        _stopwatch.Start();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_engineHandle == IntPtr.Zero) return;
        _currentAvaloniaFb = fb;

        // --- Thread-Safe Resize Update ---
        if (_currentWidth != _targetWidth || _currentHeight != _targetHeight)
        {
            _currentWidth = _targetWidth;
            _currentHeight = _targetHeight;
            GruisInterop.Gruis_Resize(_engineHandle, _currentWidth, _currentHeight);
        }

        // --- Thread-Safe Data Ingestion ---
        lock (_dataLock)
        {
            if (_pendingNodes != null && _pendingMigrations != null)
            {
                GruisInterop.Gruis_UpdateData(_engineHandle, _pendingNodes, _pendingNodes.Length, _pendingMigrations, _pendingMigrations.Length);
                _pendingNodes = null;
                _pendingMigrations = null;
            }
            if (_pendingJson != null)
            {
                GruisInterop.Gruis_UpdateLabelsJSON(_engineHandle, _pendingJson);
                _pendingJson = null;
            }
        }

        // --- Render Frame ---
        double currentTime = _stopwatch.Elapsed.TotalSeconds;
        float deltaTime = (float)(currentTime - _lastTime);
        _lastTime = currentTime;

        GruisInterop.Gruis_RenderFrame(_engineHandle, deltaTime, (float)currentTime);

        Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        
        // Calculate new target size using physical pixels
        double dpiScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        _targetWidth = Math.Max(1, (int)(e.NewSize.Width * dpiScale));
        _targetHeight = Math.Max(1, (int)(e.NewSize.Height * dpiScale));
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (_engineHandle != IntPtr.Zero)
        {
            GruisInterop.Gruis_Shutdown(_engineHandle);
            _engineHandle = IntPtr.Zero;
        }
        base.OnOpenGlDeinit(gl);
    }

    // --- DATA BRIDGE --- //

    public void UpdateSimulation(GruisNodeData[] nodes, GruisMigrationData[] migrations)
    {
        lock (_dataLock)
        {
            _pendingNodes = nodes;
            _pendingMigrations = migrations;
        }
    }

    public void UpdateLabelsJSON(string jsonPayload)
    {
        lock (_dataLock)
        {
            _pendingJson = jsonPayload;
        }
    }

    // --- INPUT ROUTING --- //

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(); // Guarantee we have input focus for keyboard/mouse events
        
        if (_engineHandle == IntPtr.Zero) return;

        // Capture pointer so dragging outside the window doesn't drop the move/release events
        e.Pointer.Capture(this); 

        var point = e.GetCurrentPoint(this);
        double dpiScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        int button = MapMouseButton(point.Properties.PointerUpdateKind);
        if (button != -1)
        {
            // Action 1 = PRESS
            GruisInterop.Gruis_HandleMouseClick(_engineHandle, button, 1, point.Position.X * dpiScale, point.Position.Y * dpiScale);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_engineHandle == IntPtr.Zero) return;

        // Release the pointer capture safely
        e.Pointer.Capture(null); 

        var point = e.GetCurrentPoint(this);
        double dpiScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        int button = MapMouseButton(point.Properties.PointerUpdateKind);
        if (button != -1)
        {
            // Action 0 = RELEASE
            GruisInterop.Gruis_HandleMouseClick(_engineHandle, button, 0, point.Position.X * dpiScale, point.Position.Y * dpiScale);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_engineHandle == IntPtr.Zero) return;

        var point = e.GetCurrentPoint(this);
        double dpiScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        GruisInterop.Gruis_HandleMouseMove(_engineHandle, point.Position.X * dpiScale, point.Position.Y * dpiScale);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_engineHandle == IntPtr.Zero) return;

        GruisInterop.Gruis_HandleScroll(_engineHandle, e.Delta.Y);
    }

    /// <summary>
    /// Translates Avalonia's PointerUpdateKind to the C-API engine equivalents.
    /// Left Button: 0, Right Button: 1, Middle Button: 2
    /// </summary>
    private int MapMouseButton(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => 0,
        PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => 1,
        PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => 2,
        _ => -1
    };
}