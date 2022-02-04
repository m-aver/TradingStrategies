using System;
using WealthLab;

#region INFO
//changed assembly settings
//Application - Target framework - 4.6.2
//Build - Output - Output path - WealthLab root
//Debug - Start external program - WealthLab

//с помощью точек останова или приостановки дебагинга
//можно менять код на лету, не закрывая WealthLab и не пересобирая сборку
//но для сохранения изменений в конце работы все же будет нужно забилдить сборку
#endregion

namespace TradingStrategies.Backtesting.Core
{
    public class MyStrategyHelper : StrategyHelper
    {
        public override string Author { get; } = "Misha";

        public override DateTime CreationDate { get; } = new DateTime();

        public override string Description { get; } = 
            "A strategy from visual studio" +
            " (current strategy: " + StrategyFactory.StrategyName + ")";

        public override Guid ID { get; } = new Guid("32a0b579-d2d8-466d-bc2a-32b88f305d4b");

        public override DateTime LastModifiedDate { get; } = new DateTime();

        public override string Name { get; } = "VS_Strategy";

        public override Type WealthScriptType { get; } = typeof(WealthScriptWrapper);
    }
}
