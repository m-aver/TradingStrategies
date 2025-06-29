﻿using Fidelity.Components;
using System;
using System.Collections.Concurrent;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Utility;
using WealthLab;
using WealthLab.Visualizers;
using WealthLab.Visualizers.MS123;

namespace TradingStrategies.Backtesting.Optimizers.Scorecards
{
    internal static class ScorecardProviderFactory
    {
        public static IScorecardProvider Create(SettingsManager settingsManager, Optimizer optimizer)
        {
            var provider = ScorecardProvidersContainer.GetScorecard(optimizer.Host);
            provider ??= new ScorecardProvider(settingsManager, optimizer);
            ScorecardProvidersContainer.RegisterScorecard(provider, optimizer.Host);
            return provider;
        }
    }

    internal static class ScorecardProvidersContainer
    {
        private static readonly ConcurrentDictionary<long, IScorecardProvider> _scorecards =
            new ConcurrentDictionary<long, IScorecardProvider>();

        public static void RegisterScorecard(IScorecardProvider provider, IOptimizationHost host)
        {
            var hash = host.GetHashCode();
            _scorecards[hash] = provider;
        }

        public static IScorecardProvider? GetScorecard(IOptimizationHost host)
        {
            var hash = host.GetHashCode();
            return _scorecards.TryGetValue(hash, out var scorecard) ? scorecard : null;
        }
    }

    public interface IScorecardProvider
    {
        StrategyScorecard GetSelectedScorecard();
    }

    internal class ScorecardProvider : IScorecardProvider
    {
        const string ScorecardKey = "Optimization.Scorecard";

        const string BasicScorecard = "Basic Scorecard";
        const string ExtendedScorecard = "Extended Scorecard";
        const string MS123Scorecard = "MS123 Scorecard";
        const string MS123ScorecardLite = "MS123 Scorecard (No closed equity)";
        const string BasicExtendedScorecard = Scorecards.BasicExScorecard.DisplayName;
        const string FilteringScorecard = Scorecards.FilteringScorecard.DisplayName;
        const string LongShortScorecard = Scorecards.LongShortScorecard.DisplayName;

        private StrategyScorecard _current;

        public ScorecardProvider(SettingsManager settingsManager, Optimizer optimizer)
        {
            _current = GetScorecardFromSettings(settingsManager);   //initially selected
            var box = OptimizationFormExtractor.ExtractScorecardBox(optimizer);
            box.SelectedValueChanged += Box_SelectedValueChanged;   //updates from ui
        }

        public StrategyScorecard GetSelectedScorecard()
        {
            return _current;
        }

        private void Box_SelectedValueChanged(object sender, EventArgs args)
        {
            _current = (StrategyScorecard)((ComboBox)sender).SelectedItem;
        }

        private static StrategyScorecard GetScorecardFromSettings(SettingsManager settingsManager)
        {
            if (!settingsManager.Settings.ContainsKey(ScorecardKey))
            {
                throw new InvalidOperationException("scorecard is not defined");
            }

            return settingsManager.Settings[ScorecardKey] switch
            {
                BasicScorecard => new BasicScorecard(),
                ExtendedScorecard => new ExtendedScorecard(),
                MS123Scorecard => new MS123Scorecard(),
                MS123ScorecardLite => new MS123ScorecardLite(),
                BasicExtendedScorecard => new BasicExScorecard(),
                FilteringScorecard => new FilteringScorecard(),
                LongShortScorecard => new LongShortScorecard(),
                _ => new BasicScorecard(),
            };
        }
    }
}
