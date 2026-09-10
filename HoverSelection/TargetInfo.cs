using System.Windows;

namespace AirGestureAI.HoverSelection
{
    /// <summary>
    /// Describes a selectable target region on the screen.
    ///
    /// <para>
    /// <see cref="TargetInfo"/> is intentionally application-agnostic. A target can
    /// represent anything — a browser button, a desktop icon, a video card, a media
    /// control — as long as it has a rectangular screen region and an identifier.
    /// Application-specific plugins supply <see cref="TargetInfo"/> instances to the
    /// <see cref="IHoverEngine"/> via <c>RegisterTarget</c> / <c>ClearTargets</c>;
    /// the engine itself never knows what type of application it is serving.
    /// </para>
    /// </summary>
    public sealed class TargetInfo
    {
        /// <summary>
        /// Gets a unique identifier for this target, used to detect target changes.
        /// Must be unique among all currently registered targets.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets a human-readable display name for logging and UI feedback.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the bounding rectangle of the target in WPF device-independent pixels
        /// (screen coordinates).
        /// </summary>
        public Rect BoundingRect { get; }

        /// <summary>
        /// Gets an optional application-defined tag that callers can use to attach
        /// arbitrary metadata (e.g. a URL, an action callback, a DOM element reference).
        /// The Hover Engine never reads or modifies this field.
        /// </summary>
        public object? Tag { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="TargetInfo"/>.
        /// </summary>
        /// <param name="id">Unique identifier string.</param>
        /// <param name="displayName">Human-readable label.</param>
        /// <param name="boundingRect">Screen bounding box in DIPs.</param>
        /// <param name="tag">Optional application metadata.</param>
        public TargetInfo(string id, string displayName, Rect boundingRect, object? tag = null)
        {
            Id           = id;
            DisplayName  = displayName;
            BoundingRect = boundingRect;
            Tag          = tag;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="point"/> lies within
        /// <see cref="BoundingRect"/>.
        /// </summary>
        public bool Contains(Point point) => BoundingRect.Contains(point);

        /// <inheritdoc/>
        public override string ToString() => $"[Target '{DisplayName}' id={Id} rect={BoundingRect}]";
    }
}
