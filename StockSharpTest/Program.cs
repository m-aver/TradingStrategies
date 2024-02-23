using Ecng.Common;
using StockSharp.Algo;
using StockSharp.Algo.Candles;
using StockSharp.Algo.Candles.Compression;
using StockSharp.Algo.Storages;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.Transaq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StockSharpTest
{
    class Program
    {
        private static Connector _connector;
        private static Security _siSec;
        private static CandleSeries _siCandles;
        private static SecurityId _siSecId = new SecurityId()
        {
            BoardCode = "FORTS",
            SecurityCode = "SiM2",
            SecurityType = SecurityTypes.Future
        };

        private readonly CandleBuilderProvider _builderProvider = new CandleBuilderProvider(new InMemoryExchangeInfoProvider());
        private ICandleBuilder _candleBuilder;
        private readonly ICandleBuilderValueTransform _candleTransform = new TickCandleBuilderValueTransform();
        private readonly CandlesHolder _holder = new CandlesHolder();
        private readonly IdGenerator _transactionIdGenerator = new IncrementalIdGenerator();

        static void Main(string[] args)
        {
            Console.WriteLine("start");

            _connector = new TransaqTrader()
            {
                Address = TransaqAddresses.FinamReal1,
                Login = "FZTC18884A",
                Password = "N8y6hV2U",
                DllPath = "resources/txmlconnector.dll",    // S# сам создает dll по указанному пути в папке bin/Debug                
            };
            InitCallbacks();
            _connector.Connect();       //подключается асинхронно

            _siSec = new Security()
            {
                Code = "SiM2",
                Board = ExchangeBoard.Forts,
            };
            //_connector.RegisterSecurity(_siSec);
            Console.WriteLine("close: " + _siSec.ClosePrice);                

            //siSec = _connector.LookupSecurity(siSecId);

            MarketDataMessage message = new MarketDataMessage()
            {
                SecurityId = _siSecId,
            };
            _connector.SubscribeMarketData(_siSec, message);

            var testOrder = new Order()
            {
                BrokerCode = "MC0061900000",  //firm code
                ClientCode = "7683q1j",
                Comment = "test order",
                Currency = CurrencyTypes.RUB,
                Direction = Sides.Buy,
                Price = 1m,
                Type = OrderTypes.Market,
                Volume = 1,
            };
            //Console.WriteLine("send order " + testOrder.Id);
            //_connector.RegisterOrder(testOrder);        

            //WorkEver();
            Thread.Sleep(10000);

            //_siSec = _connector.LookupSecurity(_siSecId);
            //var typ = _siSec.GetType();    //security
            //var op = _connector.GetSecurityValue<decimal?>(_siSec, Level1Fields.OpenPrice);

            Thread.Sleep(2000);

            RequestCandles();

            Thread.Sleep(1000);

            Console.ReadKey();
        }

        private static void WorkEver()
        {
            bool isRun = true;

            while (isRun)
            {
                Thread.Sleep(200);
            }
        }

        private static void RequestCandles()
        {
            _siCandles = new CandleSeries(typeof(TimeFrameCandle), _siSec, new TimeSpan(0, 1, 0));

            _connector.CandleSeriesProcessing += ProcessingCandleHandler;
            _connector.Error += (exc) =>
            { };

            //_connector.SubscribeCandles(_siCandles,
            //    from: new DateTimeOffset(new DateTime(2022, 5, 24)),
            //    to: DateTimeOffset.Now,
            //    count: 20);

            //_siCandles.From = new DateTimeOffset(new DateTime(2022, 5, 24));
            //_siCandles.To = DateTimeOffset.Now;
            //_siCandles.Count = 20;
            //_siCandles.BuildCandlesFrom = MarketDataTypes.CandleTimeFrame;
            //_siCandles.BuildCandlesMode = MarketDataBuildModes.Load;

            _connector.SubscribeCandles(_siCandles);

            Thread.Sleep(1000);

            var b = _connector.IsCandlesRegistered<TimeFrameCandle>(_siSec, new TimeSpan(1, 0, 0));
            var c = _connector.GetCurrentCandle<TimeFrameCandle>(_siCandles);
            var c1 = _connector.GetCandles<TimeFrameCandle>(_siCandles, 5);
            var c2 = _connector.GetTimeFrameCandle(_siCandles, new DateTimeOffset(new DateTime(2022, 5, 25, 15, 0, 0)));

            //CandleHelper.
            CandleManagerContainer cnt = new CandleManagerContainer();
            CandleManager mng = new CandleManager(_connector);
            mng.Processing += ProcessingCandleHandler;
            mng.Error += (exc) =>
            { };
            mng.Stopped += (s) =>
            { };

            mng.Start(_siCandles);

            var count = mng.GetCandleCount(_siCandles);
            var tfc = _siSec.TimeFrame(new TimeSpan(0, 1, 0));
            //_connector.SubscribeCandles(tfc);
            //mng.Start(tfc);
        }

        private static void InitCallbacks()
        {
            _connector.Connected += () =>
            {
                Console.WriteLine("connected | state: " + _connector.ConnectionState);
                _connector.RegisterSecurity(_siSec);

                _siSec = _connector.LookupSecurity(_siSecId);
                //_connector.RegisterSecurity(_siSec);

                RequestCandles();
            };                
            _connector.ConnectionError += (e) => Console.WriteLine("connection failed | error: " + e.Message);

            _connector.NewOrder += (o) => Console.WriteLine("order sent | sec, vol, dir: " + o.Security + ", " + o.Volume + ", " + o.Direction);
            _connector.OrderRegisterFailed += (of) => Console.WriteLine("order failed | id, error: " + of.Order.Id + ", " + of.Error.Message);

            _connector.NewSecurity += (s) =>
            {
                if (s.Code.ToLower() == "sim2")
                {
                    Console.WriteLine("new sec: " + s.ToString());
                }
            };

            _connector.MarketDataSubscriptionSucceeded += (s, md) =>
            {                
            };

            _connector.SecurityChanged += (s) =>
            {
                if (s.Code.ToLower() == "sim2")
                {
                    Console.WriteLine("sec changed: " + s.ToString() + " " + s.MarginBuy);
                }
            };

            _connector.NewMessage += (message) =>
            {
                if (message is CandleMessage)
                {
                    var cm = message as CandleMessage;
                }
            };
        }

        private static void ProcessingCandleHandler(CandleSeries series, Candle candle)
        {
        }
    }
}
