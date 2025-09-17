using System;
using Telegram.Common;
using Telegram.Controls;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LibVLCSharp.Platforms.Windows
{
    /// <summary>
    /// VideoView base class for the UWP platform
    /// </summary>
    [TemplatePart(Name = PartSwapChainPanelName, Type = typeof(SwapChainPanel))]
    public class VideoView : ControlEx
    {
        private const string PartSwapChainPanelName = "SwapChainPanel";

        private AsyncMediaPlayerSwapChain _context = new(false);
        private SwapChainPanel _panel;

        /// <summary>
        /// The constructor
        /// </summary>
        public VideoView()
        {
            DefaultStyleKey = typeof(VideoView);

            Connected += OnConnected;
            Disconnected += OnDisconnected;
        }

        public event EventHandler<InitializedEventArgs> Initialized;

        public bool IsUnloadedExpected { get; set; }

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            Application.Current.Suspending += OnSuspending;
            IsUnloadedExpected = false;
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            Application.Current.Suspending -= OnSuspending;

            if (IsUnloadedExpected)
            {
                return;
            }

            _context.Destroy();
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            // TODO: Move to AsyncMediaPlayerSwapChain
            _context.Trim();
        }

        /// <summary>
        /// Invoked whenever application code or internal processes (such as a rebuilding layout pass) call ApplyTemplate. 
        /// In simplest terms, this means the method is called just before a UI element displays in your app.
        /// Override this method to influence the default post-template logic of a class.
        /// </summary>
        protected override void OnApplyTemplate()
        {
            _panel = (SwapChainPanel)GetTemplateChild(PartSwapChainPanelName);

#if !WINUI
            if (DesignMode.DesignModeEnabled)
                return;
#endif
            _context.Destroy();
            _context.Attach(_panel, false);

            _panel.SizeChanged += OnSizeChanged;
            _panel.CompositionScaleChanged += OnCompositionScaleChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsDisconnected && !IsUnloadedExpected)
            {
                _context.Destroy();
            }
            else if (_context.IsLoaded)
            {
                _context.UpdateSize();
            }
            else if (_context.Create(false))
            {
                Initialized?.Invoke(this, new InitializedEventArgs(SwapChainOptions));
            }
        }

        private void OnCompositionScaleChanged(SwapChainPanel sender, object args)
        {
            if (_context.IsLoaded)
            {
                _context.UpdateScale();
            }
        }

        public void Clear()
        {
            _context.Clear();
        }

        /// <summary>
        /// Gets the swapchain parameters to pass to the <see cref="LibVLC"/> constructor.
        /// If you don't pass them to the <see cref="LibVLC"/> constructor, the video won't
        /// be displayed in your application.
        /// Calling this property will throw an <see cref="InvalidOperationException"/> if the VideoView is not yet full Loaded.
        /// </summary>
        /// <returns>The list of arguments to be given to the <see cref="LibVLC"/> constructor.</returns>
        public string[] SwapChainOptions => _context.SwapChainOptions;
    }

#if WINUI
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    internal class ISwapChainPanelNative : SharpDX.DXGI.ISwapChainPanelNative
    {
        public ISwapChainPanelNative(IntPtr nativePtr) : base(nativePtr) { }
    }
#endif
}
