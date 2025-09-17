using System;
using System.Collections.Generic;

namespace LibVLCSharp.Platforms.Windows
{
    /// <summary>
    /// Provides data for the <see cref="VideoView{TInitializedEventArgs}.Initialized"/> event.
    /// </summary>
    public partial class VideoViewInitializedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of <see cref="VideoViewInitializedEventArgs"/> class
        /// </summary>
        /// <param name="swapChainOptions">swap chain parameters</param>
        public VideoViewInitializedEventArgs(IList<string> swapChainOptions) : base()
        {
            SwapChainOptions = swapChainOptions;
        }

        /// <summary>
        /// Gets the swap chain parameters
        /// </summary>
        public IList<string> SwapChainOptions { get; }
    }
}
