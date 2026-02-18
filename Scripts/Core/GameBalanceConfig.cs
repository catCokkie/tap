namespace ImmortalIdle
{
    /// <summary>
    /// 游戏平衡常量集中配置，避免魔术数字分散在各系统。
    /// </summary>
    public static class GameBalanceConfig
    {
        public const int HerbUnlockRealmId = 1;
        public const int AlchemyUnlockRealmId = 1;
        public const int SpiritPetUnlockRealmId = 2;
        public const int CraftUnlockRealmId = 4;

        public const string DefaultAlchemyRecipeId = "ningqi_pill_recipe";
        public const string DefaultCraftRecipeId = "wind_charm_recipe";
        public const decimal InitialSpiritPetCaptureRequirement = 120m;

        public const double SystemCardUpdateInterval = 0.5;
        public const double HerbTickInterval = 0.2;
        public const double SpiritPetTickInterval = 0.2;
        public const double AlchemyTickInterval = 0.2;
        public const double CraftTickInterval = 0.2;
    }
}
