using System;
using System.Runtime.InteropServices;

namespace VisualMigrata;

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public struct GruisNodeData
{
    public uint Id;
    public float PosX;
    public float PosY;
    public float PosZ;
    public float Population;
    public float ActivityLevel;
    
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Name;
    
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string Status;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GruisMigrationData
{
    public uint SourceId;
    public uint TargetId;
    public float Volume;
}

// Delegate for the OpenGL Loader
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate IntPtr GruisGLLoaderFunc(string name);

public static class GruisInterop
{
    private const string DllName = "GruisEngine";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Gruis_Initialize(int width, int height, GruisGLLoaderFunc glLoaderProc);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_Resize(IntPtr engineHandle, int width, int height);

    // Arrays are automatically pinned by the P/Invoke marshaller during the synchronous call
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_UpdateData(
        IntPtr engineHandle, 
        [In, Out] GruisNodeData[] nodeData, int nodeCount, 
        [In, Out] GruisMigrationData[] migrationData, int migrationCount);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void Gruis_UpdateLabelsJSON(IntPtr engineHandle, string jsonString);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_RenderFrame(IntPtr engineHandle, float deltaTime, float totalTime);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_HandleKey(IntPtr engineHandle, int key, int action);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_HandleMouseClick(IntPtr engineHandle, int button, int action, double x, double y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_HandleMouseMove(IntPtr engineHandle, double x, double y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_HandleScroll(IntPtr engineHandle, double yoffset);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Gruis_Shutdown(IntPtr engineHandle);
}