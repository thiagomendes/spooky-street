using SpookyStreet.Core.Tips;
using UnityEngine;

namespace SpookyStreet.App
{
    /// <summary>
    /// Remembers which first-time tips were shown, in PlayerPrefs (there is no preferences store yet; this sits
    /// behind <see cref="ITipStore"/> so one can replace it). Keys use the stable tip ids, never display text.
    /// </summary>
    public sealed class PlayerPrefsTipStore : ITipStore
    {
        public const string KeyPrefix = "spooky.tips.shown.";

        public bool HasShown(string tipId) => !string.IsNullOrEmpty(tipId) && PlayerPrefs.GetInt(KeyPrefix + tipId, 0) == 1;

        public void MarkShown(string tipId)
        {
            if (string.IsNullOrEmpty(tipId))
            {
                return;
            }

            PlayerPrefs.SetInt(KeyPrefix + tipId, 1);
            PlayerPrefs.Save();
        }
    }
}
