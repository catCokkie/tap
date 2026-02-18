using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ImmortalIdle
{
    /// <summary>
    /// 输入计分系统：将真实键鼠输入转换为修行值。
    /// 在 Windows 下优先使用全局输入采集，支持后台运行。
    /// </summary>
    public partial class InputScoringSystem : Node
    {
        private const double WINDOW_SECONDS = 60.0;

        // 连续相同输入去抖，防止高频抖动重复计分
        private readonly Dictionary<string, double> _lastInputTime = new();

        private double _windowElapsed = 0;
        private decimal _rawPointsInWindow = 0m;
        private decimal _pendingCultivation = 0m;
        private bool _inputStartedLogged = false;
        private decimal _windowStartEffectivePoints = 0m;
        private decimal _windowStartMain = 0m;
        private decimal _windowStartHerb = 0m;
        private decimal _windowStartPet = 0m;
        private decimal _windowStartAlchemy = 0m;
        private decimal _windowStartCraft = 0m;

        private WindowsGlobalInputListener _globalInputListener;
        private bool _useGlobalInput;

        public override void _Ready()
        {
            _globalInputListener = new WindowsGlobalInputListener();
            _useGlobalInput = _globalInputListener.Start();

            // 始终保留 _Input 回调：键鼠在全局采集可用时由全局路径处理，
            // 手柄输入始终通过 Godot 事件路径处理。
            SetProcessInput(true);

            if (_useGlobalInput)
            {
                GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem")
                    ?.AddLog("system", "已启用 Windows 全局输入采集（后台可运行，手柄走窗口输入路径）");
            }
            else
            {
                GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem")
                    ?.AddLog("system", "未启用全局输入采集，当前使用前台窗口输入（含手柄）");
            }

            var state = GameManager.Instance?.CurrentState;
            if (state != null)
            {
                InitializeMetricWindow(state);
            }
        }

        public override void _ExitTree()
        {
            _globalInputListener?.Dispose();
            _globalInputListener = null;
        }

        public override void _Input(InputEvent @event)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            double nowSeconds = Time.GetTicksMsec() / 1000.0;
            decimal rawDelta = 0m;
            if (IsJoypadEvent(@event))
            {
                rawDelta = SampleJoypadPoints(@event, nowSeconds);
            }
            else if (!_useGlobalInput)
            {
                rawDelta = SampleInputPoints(@event, nowSeconds);
            }

            if (rawDelta <= 0m)
            {
                return;
            }

            ApplyRawInputDelta(state, rawDelta);
        }

        public override void _Process(double delta)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            ConsumeGlobalInput(state);

            _windowElapsed += delta;
            while (_windowElapsed >= WINDOW_SECONDS)
            {
                _windowElapsed -= WINDOW_SECONDS;
                EmitWindowMetrics(state);
                _rawPointsInWindow = 0m;
                InitializeMetricWindow(state);
            }

            long gainLong = (long)decimal.Floor(_pendingCultivation);
            if (gainLong <= 0)
            {
                return;
            }

            _pendingCultivation -= gainLong;

            BigInteger gain = new BigInteger(gainLong);
            state.CurrentCultivation += gain;
            state.TotalCultivationEarned += gain;
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, gain);
        }

        private void ConsumeGlobalInput(GameState state)
        {
            if (!_useGlobalInput || _globalInputListener == null)
            {
                return;
            }

            double nowSeconds = Time.GetTicksMsec() / 1000.0;

            while (_globalInputListener.TryDequeue(out GlobalInputSample sample))
            {
                decimal rawDelta = ConvertGlobalSampleToPoints(sample, nowSeconds);
                if (rawDelta <= 0m)
                {
                    continue;
                }

                ApplyRawInputDelta(state, rawDelta);
            }
        }

        private void ApplyRawInputDelta(GameState state, decimal rawDelta)
        {
            decimal oldRaw = _rawPointsInWindow;
            _rawPointsInWindow += rawDelta;

            int effectiveCap = state.GetEffectiveInputMinuteCap();
            decimal oldEffective = CalculateEffectivePoints(oldRaw, effectiveCap);
            decimal newEffective = CalculateEffectivePoints(_rawPointsInWindow, effectiveCap);
            decimal effectiveDelta = newEffective - oldEffective;

            decimal practiceValue = effectiveDelta * state.GetEffectiveInputConversionRate();
            practiceValue *= state.GetEffectiveDebugProgressMultiplier();
            var weights = state.GetInputAllocationWeights();

            decimal mainGain = practiceValue * weights.Main;
            decimal herbGain = practiceValue * weights.Herb;
            decimal petGain = practiceValue * weights.Pet;
            decimal alchemyGain = practiceValue * weights.Alchemy;
            decimal craftGain = practiceValue * weights.Craft;

            _pendingCultivation += mainGain;

            state.HerbGardenPool += herbGain;
            state.SpiritPetPool += petGain;
            state.AlchemyPool += alchemyGain;
            state.CraftPool += craftGain;

            state.TotalAllocatedMain += mainGain;
            state.TotalAllocatedHerb += herbGain;
            state.TotalAllocatedPet += petGain;
            state.TotalAllocatedAlchemy += alchemyGain;
            state.TotalAllocatedCraft += craftGain;

            state.TotalInputEvents++;
            state.TotalInputPointsRaw += rawDelta;
            state.TotalInputPointsEffective += effectiveDelta;

            if (!_inputStartedLogged)
            {
                _inputStartedLogged = true;
                GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem")
                    ?.AddLog("system", "已检测到输入驱动修行，修行值开始自动积累");
            }
        }

        private decimal ConvertGlobalSampleToPoints(GlobalInputSample sample, double nowSeconds)
        {
            switch (sample.Kind)
            {
                case GlobalInputKind.KeyDown:
                {
                    string signature = $"gk:{sample.Data}";
                    if (!PassDebounce(signature, nowSeconds, 0.03))
                    {
                        return 0m;
                    }
                    return 1m;
                }
                case GlobalInputKind.MouseLeftDown:
                {
                    if (!PassDebounce("gm:1", nowSeconds, 0.02))
                    {
                        return 0m;
                    }
                    return 1m;
                }
                case GlobalInputKind.MouseRightDown:
                {
                    if (!PassDebounce("gm:2", nowSeconds, 0.02))
                    {
                        return 0m;
                    }
                    return 1m;
                }
                case GlobalInputKind.MouseWheel:
                {
                    string signature = sample.Data >= 0 ? "gw:up" : "gw:down";
                    if (!PassDebounce(signature, nowSeconds, 0.05))
                    {
                        return 0m;
                    }
                    return 0.5m;
                }
                default:
                    return 0m;
            }
        }

        private decimal SampleInputPoints(InputEvent @event, double nowSeconds)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                bool combo = keyEvent.CtrlPressed || keyEvent.AltPressed || keyEvent.ShiftPressed || keyEvent.MetaPressed;
                string signature = $"k:{(long)keyEvent.Keycode}:{(combo ? 1 : 0)}";
                if (!PassDebounce(signature, nowSeconds, 0.03))
                {
                    return 0m;
                }

                return combo ? 2m : 1m;
            }

            if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
            {
                if (mouseEvent.ButtonIndex == MouseButton.Left || mouseEvent.ButtonIndex == MouseButton.Right)
                {
                    string signature = $"m:{(int)mouseEvent.ButtonIndex}";
                    if (!PassDebounce(signature, nowSeconds, 0.02))
                    {
                        return 0m;
                    }

                    return 1m;
                }

                if (mouseEvent.ButtonIndex == MouseButton.WheelUp || mouseEvent.ButtonIndex == MouseButton.WheelDown)
                {
                    string signature = $"w:{(int)mouseEvent.ButtonIndex}";
                    if (!PassDebounce(signature, nowSeconds, 0.05))
                    {
                        return 0m;
                    }

                    return 0.5m;
                }
            }

            return 0m;
        }

        private static bool IsJoypadEvent(InputEvent @event)
        {
            return @event is InputEventJoypadButton || @event is InputEventJoypadMotion;
        }

        private decimal SampleJoypadPoints(InputEvent @event, double nowSeconds)
        {
            if (@event is InputEventJoypadButton joypadButton && joypadButton.Pressed)
            {
                string signature = $"jb:{joypadButton.Device}:{(int)joypadButton.ButtonIndex}";
                if (!PassDebounce(signature, nowSeconds, 0.05))
                {
                    return 0m;
                }

                return 1m;
            }

            if (@event is InputEventJoypadMotion joypadMotion)
            {
                float axisValue = joypadMotion.AxisValue;
                float abs = Math.Abs(axisValue);
                if (abs < 0.6f)
                {
                    return 0m;
                }

                int direction = axisValue > 0 ? 1 : -1;
                string signature = $"jm:{joypadMotion.Device}:{(int)joypadMotion.Axis}:{direction}";
                if (!PassDebounce(signature, nowSeconds, 0.08))
                {
                    return 0m;
                }

                // 轻推计0.5，重推计1.0
                return abs >= 0.9f ? 1m : 0.5m;
            }

            return 0m;
        }

        private bool PassDebounce(string signature, double nowSeconds, double cooldownSeconds)
        {
            if (_lastInputTime.TryGetValue(signature, out double lastTime))
            {
                if (nowSeconds - lastTime < cooldownSeconds)
                {
                    return false;
                }
            }

            _lastInputTime[signature] = nowSeconds;
            return true;
        }

        private static decimal CalculateEffectivePoints(decimal rawPoints, int minuteCap)
        {
            decimal cap = minuteCap;
            decimal softCap = cap * 1.5m;

            if (rawPoints <= cap)
            {
                return rawPoints;
            }

            if (rawPoints <= softCap)
            {
                return cap + (rawPoints - cap) * 0.5m;
            }

            return cap + (softCap - cap) * 0.5m + (rawPoints - softCap) * 0.2m;
        }

        private void InitializeMetricWindow(GameState state)
        {
            _windowStartEffectivePoints = state.TotalInputPointsEffective;
            _windowStartMain = state.TotalAllocatedMain;
            _windowStartHerb = state.TotalAllocatedHerb;
            _windowStartPet = state.TotalAllocatedPet;
            _windowStartAlchemy = state.TotalAllocatedAlchemy;
            _windowStartCraft = state.TotalAllocatedCraft;
        }

        private void EmitWindowMetrics(GameState state)
        {
            decimal effectivePoints = Math.Max(0m, state.TotalInputPointsEffective - _windowStartEffectivePoints);
            decimal main = Math.Max(0m, state.TotalAllocatedMain - _windowStartMain);
            decimal herb = Math.Max(0m, state.TotalAllocatedHerb - _windowStartHerb);
            decimal pet = Math.Max(0m, state.TotalAllocatedPet - _windowStartPet);
            decimal alchemy = Math.Max(0m, state.TotalAllocatedAlchemy - _windowStartAlchemy);
            decimal craft = Math.Max(0m, state.TotalAllocatedCraft - _windowStartCraft);

            if (_rawPointsInWindow <= 0m
                && effectivePoints <= 0m
                && main <= 0m
                && herb <= 0m
                && pet <= 0m
                && alchemy <= 0m
                && craft <= 0m)
            {
                return;
            }

            LogSystem log = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");
            if (log == null)
            {
                return;
            }

            log.AddMetric("input.raw_per_min", _rawPointsInWindow);
            log.AddMetric("input.effective_per_min", effectivePoints);
            log.AddMetric("output.main_per_min", main);
            log.AddMetric("output.herb_per_min", herb);
            log.AddMetric("output.pet_per_min", pet);
            log.AddMetric("output.alchemy_per_min", alchemy);
            log.AddMetric("output.craft_per_min", craft);
            log.AddMetric("input.minute_cap", state.GetEffectiveInputMinuteCap());
            log.AddMetric("input.conversion_rate", state.GetEffectiveInputConversionRate());
        }
    }
}
