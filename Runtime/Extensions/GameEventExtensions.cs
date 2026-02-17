namespace GOC.SOEvents
{
    public static class GameEventExtensions
    {
        public static void SafeRaise(this GameEvent evt)
        {
            if (evt != null) evt.Raise();
        }

        public static void SafeRaise<T>(this GameEvent<T> evt, T value)
        {
            if (evt != null) evt.Raise(value);
        }
    }
}
