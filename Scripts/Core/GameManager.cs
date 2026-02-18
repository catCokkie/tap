using Godot;
using System;
using System.Numerics;

namespace ImmortalIdle
{
    /// <summary>
    /// 游戏全局管理器，负责初始化子系统并协调主流程。
    /// </summary>
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; }

        private SaveSystem _saveSystem;
        private CultivationSystem _cultivationSystem;
        private InputScoringSystem _inputScoringSystem;
        private HerbGardenSystem _herbGardenSystem;
        private SpiritPetSystem _spiritPetSystem;
        private AlchemySystem _alchemySystem;
        private CraftSystem _craftSystem;
        private RealmSystem _realmSystem;
        private LogSystem _logSystem;

        private bool _offlineGainProcessed;

        public override void _Ready()
        {
            if (Instance != null)
            {
                QueueFree();
                return;
            }

            Instance = this;
            ConfigLoader.EnsureLoaded();
            GD.Print("[GameManager] 正在初始化...");

            EnsureEventBus();

            _saveSystem = new SaveSystem();
            AddChild(_saveSystem);
            CurrentState = _saveSystem.LoadGame();
            EnsureBalanceProfileApplied();

            _cultivationSystem = new CultivationSystem();
            AddChild(_cultivationSystem);

            _inputScoringSystem = new InputScoringSystem();
            AddChild(_inputScoringSystem);

            _herbGardenSystem = new HerbGardenSystem();
            AddChild(_herbGardenSystem);

            _spiritPetSystem = new SpiritPetSystem();
            AddChild(_spiritPetSystem);

            _alchemySystem = new AlchemySystem();
            AddChild(_alchemySystem);

            _craftSystem = new CraftSystem();
            AddChild(_craftSystem);

            _realmSystem = new RealmSystem();
            AddChild(_realmSystem);

            _logSystem = new LogSystem();
            AddChild(_logSystem);

            ProcessOfflineGain();

            GD.Print($"[GameManager] 初始化完成，当前境界: {CurrentState.GetCurrentRealmName()}");
            _logSystem.AddLog("system", $"欢迎回来，{CurrentState.PlayerName}。当前修为：{FormatNumber(CurrentState.CurrentCultivation)}");
            if (CurrentState.PrestigeCount > 0)
            {
                _logSystem.AddLog(
                    "system",
                    $"转世增益生效：输入上限 {CurrentState.GetEffectiveInputMinuteCap()}/分钟，转化率 x{CurrentState.GetEffectiveInputConversionRate():F2}");
            }
        }

        public override void _ExitTree()
        {
            _saveSystem?.SaveGame(CurrentState, force: true);
            Instance = null;
        }

        public override void _Process(double delta)
        {
            CurrentState?.UpdateTimedEffects(delta);
        }

        public void OnPlayerClick()
        {
            int clickPower = 1 + CurrentState.CurrentRealmId;
            BigInteger gain = new BigInteger(clickPower);

            CurrentState.CurrentCultivation += gain;
            CurrentState.TotalCultivationEarned += gain;
            CurrentState.TotalClicks++;

            EventBus.Instance.EmitCultivationChanged(CurrentState.CurrentCultivation, gain);
            EventBus.Instance.EmitClickFeedback();

            if (GD.Randf() < 0.05f)
            {
                TriggerRandomEvent();
            }
        }

        public bool TryBreakthrough()
        {
            if (!CurrentState.CanBreakthrough())
            {
                _logSystem.AddLog("breakthrough", "修为不足，无法突破");
                return false;
            }

            if (CurrentState.CanRebirth())
            {
                long rebirthIntervalSeconds = CurrentState.GetSecondsSinceLastRebirth();
                int oldPrestige = CurrentState.PrestigeCount;
                int oldSpiritMarks = CurrentState.RebirthSpiritMarks;
                int oldDestinyShards = CurrentState.RebirthDestinyShards;
                if (CurrentState.TryRebirth())
                {
                    int newPrestige = CurrentState.PrestigeCount;
                    int spiritGain = CurrentState.RebirthSpiritMarks - oldSpiritMarks;
                    int shardGain = CurrentState.RebirthDestinyShards - oldDestinyShards;
                    EventBus.Instance.EmitRealmBreakthrough(CurrentState.CurrentRealmId, CurrentState.CurrentRealmLevel);
                    _logSystem.AddLog("breakthrough", $"转世成功，轮回次数 {oldPrestige} -> {newPrestige}");
                    _logSystem.AddLog("system", $"转世结算：轮回灵印 +{spiritGain}，天命碎片 +{shardGain}");
                    _logSystem.AddLog(
                        "system",
                        $"转世增益：输入上限 {CurrentState.GetEffectiveInputMinuteCap()}/分钟，转化率 x{CurrentState.GetEffectiveInputConversionRate():F2}");
                    string rebirthGuide = CurrentState.GetPostRebirthGuideText();
                    if (!string.IsNullOrWhiteSpace(rebirthGuide))
                    {
                        _logSystem.AddLog("system", rebirthGuide);
                    }

                    _logSystem.AddMetric("breakthrough.rebirth_interval_sec", rebirthIntervalSeconds);
                    GD.Print($"[GameManager] 转世完成: {oldPrestige} -> {newPrestige}");
                    return true;
                }
            }

            string oldRealm = CurrentState.GetCurrentRealmName();
            if (!CurrentState.TryBreakthrough())
            {
                return false;
            }

            string newRealm = CurrentState.GetCurrentRealmName();
            EventBus.Instance.EmitRealmBreakthrough(CurrentState.CurrentRealmId, CurrentState.CurrentRealmLevel);
            _logSystem.AddLog("breakthrough", $"突破成功，从 {oldRealm} 晋升至 {newRealm}");
            string requirementText = CurrentState.GetBreakthroughRequirement().ToString();
            if (requirementText.Length <= 28 && decimal.TryParse(requirementText, out decimal requirementMetric))
            {
                _logSystem.AddMetric("breakthrough.requirement", requirementMetric);
            }

            if (CurrentState.CurrentRealmId >= 1 && !CurrentState.ManualAllocationUnlockHintShown)
            {
                CurrentState.ManualAllocationUnlockHintShown = true;
                _logSystem.AddLog("system", "已解锁输入分配策略：当前为自动分配，可在设置中切换手动权重。");
            }

            GD.Print($"[GameManager] 境界突破: {oldRealm} -> {newRealm}");
            return true;
        }

        /// <summary>
        /// 根据配置触发奇遇，支持 minRealm 过滤和 unlockMethod 奖励。
        /// </summary>
        private void TriggerRandomEvent()
        {
            EventConfig randomEvent = ConfigLoader.PickRandomEvent(CurrentState.CurrentRealmId);
            if (randomEvent == null)
            {
                _logSystem.AddLog("system", "当前境界暂无可触发奇遇配置");
                return;
            }

            BigInteger bonus = new BigInteger(Math.Max(0, randomEvent.Rewards.Cultivation));
            CurrentState.CurrentCultivation += bonus;
            CurrentState.TotalCultivationEarned += bonus;

            if (!CurrentState.CollectedEvents.Contains(randomEvent.Id))
            {
                CurrentState.CollectedEvents.Add(randomEvent.Id);
            }

            string unlockMethod = randomEvent.Rewards.UnlockMethod;
            if (!string.IsNullOrWhiteSpace(unlockMethod) && !CurrentState.UnlockedMethods.Contains(unlockMethod))
            {
                CurrentState.UnlockedMethods.Add(unlockMethod);
                if (!CurrentState.MethodLevels.ContainsKey(unlockMethod))
                {
                    CurrentState.MethodLevels[unlockMethod] = 1;
                }

                _logSystem.AddLog("system", $"领悟新功法：{unlockMethod}");
            }

            EventBus.Instance.EmitCultivationChanged(CurrentState.CurrentCultivation, bonus);
            _logSystem.AddLog("event", $"【奇遇】{randomEvent.Title}：{randomEvent.Description}，获得修为 {bonus}");
        }

        public static string FormatNumber(BigInteger number)
        {
            if (number < 10000)
            {
                return number.ToString();
            }

            string[] units = { "", "万", "亿", "万亿", "京", "垓", "秭", "穰", "沟", "涧" };
            int unitIndex = 0;
            BigInteger divisor = 1;

            for (int i = 0; i < units.Length; i++)
            {
                BigInteger nextDivisor = BigInteger.Pow(10000, i + 1);
                if (number < nextDivisor)
                {
                    unitIndex = i;
                    divisor = BigInteger.Pow(10000, i);
                    break;
                }
            }

            BigInteger displayValue = number / divisor;
            BigInteger remainder = (number % divisor) / (divisor / 100);
            if (remainder > 0)
            {
                return $"{displayValue}.{remainder:D2}{units[unitIndex]}";
            }

            return $"{displayValue}{units[unitIndex]}";
        }

        private void EnsureEventBus()
        {
            EventBus eventBus = GetNodeOrNull<EventBus>("/root/EventBus");
            if (eventBus != null)
            {
                return;
            }

            eventBus = new EventBus { Name = "EventBus" };
            GetTree().Root.AddChild(eventBus);
        }

        private void EnsureBalanceProfileApplied()
        {
            if (CurrentState == null)
            {
                return;
            }

            BalanceProfileConfig profile = ConfigLoader.GetBalanceProfile(CurrentState.ActiveBalanceProfileId)
                ?? ConfigLoader.GetBalanceProfile("default");
            CurrentState.ApplyBalanceProfile(profile);
        }

        private void ProcessOfflineGain()
        {
            if (_offlineGainProcessed)
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long offlineSeconds = now - CurrentState.LastSaveTime;

            if (offlineSeconds > 60)
            {
                _logSystem.AddLog("system", $"你已离开 {FormatTime(offlineSeconds)}，输入驱动模式已恢复在线修行。");
                GD.Print($"[GameManager] 恢复在线模式，离开时长: {offlineSeconds} 秒");
            }

            _offlineGainProcessed = true;
        }

        private static string FormatTime(long seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds}秒";
            }

            if (seconds < 3600)
            {
                return $"{seconds / 60}分钟";
            }

            return $"{seconds / 3600}小时{seconds % 3600 / 60}分钟";
        }
    }
}
