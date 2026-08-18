using System;
using System.IO;
using System.Media;

namespace D2RSO.Classes
{
    /// <summary>
    /// Plays the "skill cooldown complete" notification sound (Sounds/complete.wav).
    /// </summary>
    internal static class CompletionSoundPlayer
    {
        private static readonly string SoundFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "complete.wav");

        /// <summary>
        /// Plays the completion sound asynchronously (non-blocking). Safe to call even if
        /// the sound file is missing - failures are logged and swallowed so playback issues
        /// never interrupt tracking.
        /// </summary>
        public static void Play()
        {
            try
            {
                if (!File.Exists(SoundFilePath))
                    return;

                // A new SoundPlayer per call is used (instead of a single shared/reused instance)
                // so that multiple skills completing close together can overlap without cutting
                // each other off.
                var player = new SoundPlayer(SoundFilePath);
                player.Play();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}
