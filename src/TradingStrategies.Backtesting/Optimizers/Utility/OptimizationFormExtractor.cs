﻿using System;
using System.Windows.Forms;
using WealthLab;

//вычленяет контролы из формы оптимизации

namespace TradingStrategies.Backtesting.Optimizers.Utility;

internal static class OptimizationFormExtractor
{
    public static Button ExtractCancellationButton(Optimizer optimizer)
    {
        return (Button)((TabControl)((UserControl)optimizer.Host).Controls[0]).TabPages[0].Controls[0].Controls[6];
    }

    public static ComboBox ExtractScorecardBox(Optimizer optimizer)
    {
        return (ComboBox)((TabControl)((UserControl)optimizer.Host).Controls[0]).TabPages[0].Controls[0].Controls[1];
    }

    public static ListView ExtractOptimizationResultListView(Optimizer optimizer)
    {
        return (ListView)((TabControl)((UserControl)optimizer.Host).Controls[0]).TabPages[1].Controls[0];
    }
}
