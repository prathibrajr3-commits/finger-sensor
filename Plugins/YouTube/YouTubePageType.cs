using System;

namespace AirGestureAI.Plugins.YouTube
{
    /// <summary>
    /// Identifies the type of YouTube page currently displayed.
    /// Used to select the appropriate gesture mapping.
    /// </summary>
    public enum YouTubePageType
    {
        /// <summary>Not on YouTube or page type could not be determined.</summary>
        None,
        /// <summary>YouTube home feed (youtube.com or youtube.com/feed/*).</summary>
        Home,
        /// <summary>A standard video watch page (youtube.com/watch?v=...).</summary>
        WatchVideo,
        /// <summary>A YouTube Shorts page (youtube.com/shorts/...).</summary>
        Shorts,
        /// <summary>YouTube search results page (youtube.com/results?...).</summary>
        Search,
        /// <summary>A channel page (youtube.com/@channel or youtube.com/channel/...).</summary>
        Channel,
        /// <summary>A playlist page (youtube.com/playlist?list=...).</summary>
        Playlist
    }
}
