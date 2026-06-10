using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Egl;
using Avalonia.OpenGL.Surfaces;
using Avalonia.Win32.Interop;

namespace Avalonia.Win32.DirectX
{
    internal class DxgiSwapchainWindow : EglGlPlatformSurfaceBase, ICompositionEffectsSurface, IDisposable
    {
        private DxgiConnection _connection;
        private EglGlPlatformSurface.IEglWindowGlPlatformSurfaceInfo _window;
        private DxgiRenderTarget? _renderTarget;

        // Windows 11 22H2+ (build 22621) supports DWMWA_SYSTEMBACKDROP_TYPE for Mica
        private static readonly Version MinMicaVersion = new(10, 0, 22621);

        public DxgiSwapchainWindow(DxgiConnection connection, EglGlPlatformSurface.IEglWindowGlPlatformSurfaceInfo window)
        {
            _connection = connection;
            _window = window;
        }

        public override IGlPlatformSurfaceRenderTarget CreateGlRenderTarget(IGlContext context)
        {
            _renderTarget?.Dispose();

            var eglContext = (EglContext)context;
            using (eglContext.EnsureCurrent())
            {
                _renderTarget = new DxgiRenderTarget(_window, eglContext, _connection, _windowTransparencyLevel);
            }

            return _renderTarget;
        }

        public bool IsBlurSupported(BlurEffect effect)
            => effect switch
            {
                BlurEffect.None => true,
                BlurEffect.Acrylic or BlurEffect.MicaLight or BlurEffect.MicaDark
                    => Win32Platform.WindowsVersion >= MinMicaVersion,
                _ => false
            };

        public unsafe void SetBlur(BlurEffect enable)
        {
            var hwnd = _window.Handle;
            int backdropType;

            switch (enable)
            {
                case BlurEffect.Acrylic:
                    backdropType = (int)UnmanagedMethods.DwmSystemBackdropType.DWMSBT_TABBEDWINDOW;
                    DwmSetWindowAttribute(hwnd, backdropType);
                    break;

                case BlurEffect.MicaLight:
                    backdropType = (int)UnmanagedMethods.DwmSystemBackdropType.DWMSBT_MAINWINDOW;
                    DwmSetWindowAttribute(hwnd, backdropType);
                    var pvLightMode = 0;
                    _ = UnmanagedMethods.DwmSetWindowAttribute(hwnd,
                        (int)UnmanagedMethods.DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE,
                        &pvLightMode, sizeof(int));
                    break;

                case BlurEffect.MicaDark:
                    backdropType = (int)UnmanagedMethods.DwmSystemBackdropType.DWMSBT_MAINWINDOW;
                    DwmSetWindowAttribute(hwnd, backdropType);
                    var pvDarkMode = 1;
                    _ = UnmanagedMethods.DwmSetWindowAttribute(hwnd,
                        (int)UnmanagedMethods.DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE,
                        &pvDarkMode, sizeof(int));
                    break;

                case BlurEffect.None:
                default:
                    backdropType = (int)UnmanagedMethods.DwmSystemBackdropType.DWMSBT_AUTO;
                    DwmSetWindowAttribute(hwnd, backdropType);
                    break;
            }
        }

        private static unsafe void DwmSetWindowAttribute(IntPtr hwnd, int backdropType)
        {
            _ = UnmanagedMethods.DwmSetWindowAttribute(hwnd,
                (int)UnmanagedMethods.DwmWindowAttribute.DWMWA_SYSTEMBACKDROP_TYPE,
                &backdropType, sizeof(int));
        }

        public void SetTransparencyLevel(WindowTransparencyLevel transparencyLevel)
        {
            _windowTransparencyLevel = transparencyLevel;
            _renderTarget?.SetTransparencyLevel(transparencyLevel);
        }

        private WindowTransparencyLevel _windowTransparencyLevel;

        public void Dispose()
        {
            _renderTarget?.Dispose();
            _renderTarget = null;
        }
    }
}
