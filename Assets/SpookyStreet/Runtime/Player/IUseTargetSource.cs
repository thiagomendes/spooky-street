namespace SpookyStreet.Player
{
    /// <summary>
    /// Extension point for contextual interactions (S001/S002): whatever finds the nearest interactable implements
    /// this and is assigned to <see cref="PipMotor.UseTargets"/>. While <see cref="HasTarget"/> is true, Use goes
    /// to <see cref="Interact"/> instead of rolling or diving, and the Use disc shows <see cref="VerbKey"/>.
    /// </summary>
    public interface IUseTargetSource
    {
        /// <summary>True while an interaction is in range.</summary>
        bool HasTarget { get; }

        /// <summary>Localization key of the verb on the Use disc (e.g. Examine, Talk); null shows plain "Use".</summary>
        string VerbKey { get; }

        /// <summary>UI sprite name (UiSpriteCatalog) for the Use disc glyph; null shows the examine glyph.</summary>
        string IconName { get; }

        /// <summary>Called once per Use press while <see cref="HasTarget"/> is true.</summary>
        void Interact();
    }
}
