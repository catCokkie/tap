namespace ImmortalIdle
{
    /// <summary>
    /// 运行时功能开关。
    /// </summary>
    public static class GameConfig
    {
        public static bool EnableDebugFeatures { get; set; } =
#if DEBUG
            true;
#else
            false;
#endif
    }
}
