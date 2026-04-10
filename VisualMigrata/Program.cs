using Avalonia;
using System;

namespace VisualMigrata;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            // -------------------------------------------------------------------------
            // CRITICAL FIX: Force Avalonia to provide a Native Desktop OpenGL context.
            // By default, Avalonia uses ANGLE (OpenGL ES via DirectX) on Windows.
            // 'Wgl' forces native Windows OpenGL, matching your GLFW C++ setup.
            // -------------------------------------------------------------------------
            .With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Wgl, Win32RenderingMode.Software }
            })
            // For Linux compatibility (forces standard GLX Desktop GL over EGL if possible)
            .With(new X11PlatformOptions
            {
                RenderingMode = new[] { X11RenderingMode.Glx, X11RenderingMode.Egl }
            })
            // -------------------------------------------------------------------------
            .LogToTrace();
    }
}