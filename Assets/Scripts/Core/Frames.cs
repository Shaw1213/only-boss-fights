namespace DreamLayerJam
{
    /// <summary>
    /// Frame-data helpers. Design numbers are authored in frames at 60 fps (fighting-game style)
    /// and converted to seconds at runtime, so the game behaves the same at any frame rate.
    /// </summary>
    public static class Frames
    {
        public const float PerSecond = 60f;

        public static float ToSeconds(int frames) => frames / PerSecond;
    }
}
