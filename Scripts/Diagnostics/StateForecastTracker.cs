using System;
using System.Collections.Generic;
using System.Numerics;

namespace ImmortalIdle.Diagnostics
{
    /// <summary>
    /// 基于最近窗口采样的状态预测器。
    /// </summary>
    public sealed class StateForecastTracker
    {
        private const double SampleIntervalSeconds = 1.0;
        private const double WindowSeconds = 10.0 * 60.0;

        private readonly Queue<StateSample> _samples = new();
        private double _sampleTimer;

        private readonly struct StateSample
        {
            public double TimeSec { get; init; }
            public BigInteger CurrentCultivation { get; init; }
            public BigInteger TotalCultivationEarned { get; init; }
            public decimal HerbGardenPool { get; init; }
            public decimal SpiritPetPool { get; init; }
            public decimal AlchemyPool { get; init; }
            public decimal CraftPool { get; init; }
            public decimal SpiritPetProgress { get; init; }
            public decimal AlchemyProgress { get; init; }
            public decimal CraftProgress { get; init; }
            public decimal NingqiPill { get; init; }
            public decimal PojingPill { get; init; }
            public decimal NingqiGrass { get; init; }
            public decimal QinglingLeaf { get; init; }
        }

        private readonly struct RateSnapshot
        {
            public double HerbGardenPool { get; init; }
            public double SpiritPetPool { get; init; }
            public double AlchemyPool { get; init; }
            public double CraftPool { get; init; }
            public double SpiritPetProgress { get; init; }
            public double AlchemyProgress { get; init; }
            public double CraftProgress { get; init; }
            public double NingqiPill { get; init; }
            public double PojingPill { get; init; }
            public double NingqiGrass { get; init; }
            public double QinglingLeaf { get; init; }
        }

        public readonly struct ForecastState
        {
            public BigInteger CurrentCultivation { get; init; }
            public decimal HerbGardenPool { get; init; }
            public decimal SpiritPetPool { get; init; }
            public decimal AlchemyPool { get; init; }
            public decimal CraftPool { get; init; }
            public decimal SpiritPetProgress { get; init; }
            public decimal AlchemyProgress { get; init; }
            public decimal CraftProgress { get; init; }
            public decimal NingqiPill { get; init; }
            public decimal PojingPill { get; init; }
            public decimal NingqiGrass { get; init; }
            public decimal QinglingLeaf { get; init; }
        }

        public readonly struct ForecastResult
        {
            public bool Ready { get; init; }
            public int SampleCount { get; init; }
            public double WindowSeconds { get; init; }
            public double CultivationPerSec { get; init; }
            public ForecastState In8Hours { get; init; }
            public ForecastState In24Hours { get; init; }
        }

        public void Update(GameState state, double deltaSeconds)
        {
            if (state == null)
            {
                return;
            }

            _sampleTimer += Math.Max(0.0, deltaSeconds);
            while (_sampleTimer >= SampleIntervalSeconds)
            {
                _sampleTimer -= SampleIntervalSeconds;
                AddSample(state);
            }
        }

        public ForecastResult BuildForecast(GameState state)
        {
            if (state == null || _samples.Count < 2)
            {
                return new ForecastResult
                {
                    Ready = false,
                    SampleCount = _samples.Count,
                    WindowSeconds = 0
                };
            }

            StateSample first = default;
            StateSample last = default;
            bool firstSet = false;
            foreach (StateSample sample in _samples)
            {
                if (!firstSet)
                {
                    first = sample;
                    firstSet = true;
                }

                last = sample;
            }

            double window = Math.Max(0.0, last.TimeSec - first.TimeSec);
            if (window < 30.0)
            {
                return new ForecastResult
                {
                    Ready = false,
                    SampleCount = _samples.Count,
                    WindowSeconds = window
                };
            }

            double cultivationPerSec = (ToDouble(last.TotalCultivationEarned - first.TotalCultivationEarned)) / window;
            RateSnapshot rates = new()
            {
                HerbGardenPool = (double)(last.HerbGardenPool - first.HerbGardenPool) / window,
                SpiritPetPool = (double)(last.SpiritPetPool - first.SpiritPetPool) / window,
                AlchemyPool = (double)(last.AlchemyPool - first.AlchemyPool) / window,
                CraftPool = (double)(last.CraftPool - first.CraftPool) / window,
                SpiritPetProgress = (double)(last.SpiritPetProgress - first.SpiritPetProgress) / window,
                AlchemyProgress = (double)(last.AlchemyProgress - first.AlchemyProgress) / window,
                CraftProgress = (double)(last.CraftProgress - first.CraftProgress) / window,
                NingqiPill = (double)(last.NingqiPill - first.NingqiPill) / window,
                PojingPill = (double)(last.PojingPill - first.PojingPill) / window,
                NingqiGrass = (double)(last.NingqiGrass - first.NingqiGrass) / window,
                QinglingLeaf = (double)(last.QinglingLeaf - first.QinglingLeaf) / window
            };

            return new ForecastResult
            {
                Ready = true,
                SampleCount = _samples.Count,
                WindowSeconds = window,
                CultivationPerSec = cultivationPerSec,
                In8Hours = PredictState(state, rates, cultivationPerSec, 8 * 3600),
                In24Hours = PredictState(state, rates, cultivationPerSec, 24 * 3600)
            };
        }

        private void AddSample(GameState state)
        {
            double now = Godot.Time.GetTicksMsec() / 1000.0;
            _samples.Enqueue(new StateSample
            {
                TimeSec = now,
                CurrentCultivation = state.CurrentCultivation,
                TotalCultivationEarned = state.TotalCultivationEarned,
                HerbGardenPool = state.HerbGardenPool,
                SpiritPetPool = state.SpiritPetPool,
                AlchemyPool = state.AlchemyPool,
                CraftPool = state.CraftPool,
                SpiritPetProgress = state.SpiritPetProgress,
                AlchemyProgress = state.AlchemyProgress,
                CraftProgress = state.CraftProgress,
                NingqiPill = state.GetInventoryQuantity("ningqi_pill"),
                PojingPill = state.GetInventoryQuantity("pojing_pill"),
                NingqiGrass = state.GetInventoryQuantity("ningqi_grass"),
                QinglingLeaf = state.GetInventoryQuantity("qingling_leaf")
            });

            while (_samples.Count > 2 && now - _samples.Peek().TimeSec > WindowSeconds)
            {
                _samples.Dequeue();
            }
        }

        private static ForecastState PredictState(GameState state, RateSnapshot rates, double cultivationPerSec, double horizonSec)
        {
            return new ForecastState
            {
                CurrentCultivation = state.CurrentCultivation + ToBigInteger(cultivationPerSec * horizonSec),
                HerbGardenPool = ClampNonNegative(state.HerbGardenPool + ToDecimal(rates.HerbGardenPool * horizonSec)),
                SpiritPetPool = ClampNonNegative(state.SpiritPetPool + ToDecimal(rates.SpiritPetPool * horizonSec)),
                AlchemyPool = ClampNonNegative(state.AlchemyPool + ToDecimal(rates.AlchemyPool * horizonSec)),
                CraftPool = ClampNonNegative(state.CraftPool + ToDecimal(rates.CraftPool * horizonSec)),
                SpiritPetProgress = ClampNonNegative(state.SpiritPetProgress + ToDecimal(rates.SpiritPetProgress * horizonSec)),
                AlchemyProgress = ClampNonNegative(state.AlchemyProgress + ToDecimal(rates.AlchemyProgress * horizonSec)),
                CraftProgress = ClampNonNegative(state.CraftProgress + ToDecimal(rates.CraftProgress * horizonSec)),
                NingqiPill = ClampNonNegative(state.GetInventoryQuantity("ningqi_pill") + ToDecimal(rates.NingqiPill * horizonSec)),
                PojingPill = ClampNonNegative(state.GetInventoryQuantity("pojing_pill") + ToDecimal(rates.PojingPill * horizonSec)),
                NingqiGrass = ClampNonNegative(state.GetInventoryQuantity("ningqi_grass") + ToDecimal(rates.NingqiGrass * horizonSec)),
                QinglingLeaf = ClampNonNegative(state.GetInventoryQuantity("qingling_leaf") + ToDecimal(rates.QinglingLeaf * horizonSec))
            };
        }

        private static BigInteger ToBigInteger(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return BigInteger.Zero;
            }

            double floored = Math.Floor(value);
            if (floored <= 0)
            {
                return BigInteger.Zero;
            }

            return new BigInteger(floored);
        }

        private static double ToDouble(BigInteger value)
        {
            if (value.IsZero)
            {
                return 0;
            }

            return (double)value;
        }

        private static decimal ToDecimal(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0m;
            }

            if (value > (double)decimal.MaxValue)
            {
                return decimal.MaxValue;
            }

            if (value < (double)decimal.MinValue)
            {
                return decimal.MinValue;
            }

            return (decimal)value;
        }

        private static decimal ClampNonNegative(decimal value)
        {
            return value < 0m ? 0m : value;
        }
    }
}
