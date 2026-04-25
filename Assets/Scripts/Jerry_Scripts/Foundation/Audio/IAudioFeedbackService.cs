namespace JerryScripts.Foundation.Audio
{
    /// <summary>
    /// Contract for the Audio Feedback Service.
    /// Systems post structured events; the service owns all clip selection,
    /// pooling, spatial positioning, mixer routing, and pitch variation.
    ///
    /// <para><b>Dependency direction:</b> Foundation-layer — zero upstream dependencies.
    /// Feature and Core layers may depend on this; this must never depend on them.</para>
    ///
    /// <para><b>Thread safety:</b> Must be called from the Unity main thread only.
    /// Unity AudioSource methods are not thread-safe.</para>
    /// </summary>
    /// <remarks>
    /// S1-006: Audio Feedback service skeleton.
    /// GDD: core-fps-weapon-handling.md §Audio &amp; Feedback.
    /// </remarks>
    public interface IAudioFeedbackService
    {
        /// <summary>
        /// Posts a feedback event to the audio system.
        /// The service looks up the event type in its config, selects a clip via
        /// shuffle-bag, positions and plays a pooled <c>AudioSource</c>, and routes
        /// through the correct mixer group.
        ///
        /// <para>Null-safe on missing clips — a missing clip is a warn-and-skip,
        /// never a thrown exception.</para>
        /// </summary>
        /// <param name="data">
        /// Fully populated event descriptor. Position is ignored for 2D events
        /// (<c>SpatialBlend == 0</c> on the matching config entry).
        /// </param>
        void PostFeedbackEvent(FeedbackEventData data);
    }
}
