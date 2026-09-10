namespace AirGestureAI.Models
{
    /// <summary>
    /// Identifies recognized foreground application types for context-aware adapter routing.
    /// </summary>
    public enum ApplicationType
    {
        /// <summary>Application could not be identified.</summary>
        Unknown,
        /// <summary>Google Chrome browser.</summary>
        Chrome,
        /// <summary>Microsoft Edge browser.</summary>
        Edge,
        /// <summary>Mozilla Firefox browser.</summary>
        Firefox,
        /// <summary>Brave browser.</summary>
        Brave,
        /// <summary>VLC Media Player.</summary>
        Vlc,
        /// <summary>Spotify music client.</summary>
        Spotify,
        /// <summary>Microsoft PowerPoint.</summary>
        PowerPoint,
        /// <summary>Adobe Acrobat Reader or Pro.</summary>
        AdobeAcrobat,
        /// <summary>Windows File Explorer.</summary>
        FileExplorer,
        /// <summary>Windows Desktop (shell).</summary>
        WindowsDesktop
    }
}
