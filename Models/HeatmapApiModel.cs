namespace PROXIMAMOP;

public class HeatmapApiModel
{
    public string Symbol { get; set; } = "";
    public string Timeframe { get; set; } = "";

    public double Price { get; set; }
    public double PriceStep { get; set; }

    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Spread { get; set; }
    public double LastPrice { get; set; }

    public int BuyTicks { get; set; }
    public int SellTicks { get; set; }
    public int NeutralTicks { get; set; }
    public int TotalTicks { get; set; }

    public double BuyStrength { get; set; }
    public double SellStrength { get; set; }
    public double NeutralStrength { get; set; }
    public double TotalStrength { get; set; }

    public double Delta { get; set; }
    public double Contracts { get; set; }

    public double TickSpeed { get; set; }
    public double PriceMove { get; set; }
    public double BurstScore { get; set; }

    public int SweepCount { get; set; }
    public int ReactionCount { get; set; }

    public double Velocity { get; set; }
    public double Acceleration { get; set; }

    public double LifeSeconds { get; set; }
    public int Persistence { get; set; }

    public double Heat { get; set; }
    public double MaxHeat { get; set; }
    public string HeatColor { get; set; } = "";

    public double MaxContracts { get; set; }

    public string Zone { get; set; } = "";

    public bool Absorption { get; set; }
    public bool AggressiveBuy { get; set; }
    public bool AggressiveSell { get; set; }
    public bool TickBurst { get; set; }
    public bool LiquiditySweep { get; set; }
    public bool ReactionZone { get; set; }

    public DateTime FirstSeen { get; set; }
    public DateTime LastUpdate { get; set; }

    public double BuyBarWidth { get; set; }
    public double SellBarWidth { get; set; }

    public string PriceText => Price.ToString("0.00");
    public string BuyText => $"BUY {BuyTicks}";
    public string SellText => $"SELL {SellTicks}";
    public string TotalTicksText => $"{TotalTicks} t";
    public string ContractsText => $"{Contracts:0.0} ctr";
    public string DeltaText => $"Delta {Delta:0.0}";

    public string ZoneText { get; set; } = "BALANCED";

    public void BuildUi()
    {
        var totalStrength = Math.Max(BuyStrength + SellStrength, 1);

        BuyBarWidth = Math.Max(
            18,
            Math.Min(140, (BuyStrength / totalStrength) * 140));

        SellBarWidth = Math.Max(
            18,
            Math.Min(140, (SellStrength / totalStrength) * 140));

        ZoneText = Zone switch
        {
            "BUY_PRESSURE" => "BUY",
            "SELL_PRESSURE" => "SELL",
            "RANGE_LIQUIDITY" => "RANGE",
            "ABSORPTION" => "ABSORB",
            "SWEEP" => "SWEEP",
            "REACTION" => "REACT",
            "BALANCED" => "BALANCED",
            _ => "BALANCED"
        };
    }

    public HeatmapCanvasLevel ToCanvasLevel()
    {
        return new HeatmapCanvasLevel
        {
            Price = Price,
            LastPrice = LastPrice,

            Heat = Heat,
            HeatColor = HeatColor,

            Contracts = Contracts,
            TotalTicks = TotalTicks,

            Delta = Delta,
            Zone = Zone,

            Absorption = Absorption,
            AggressiveBuy = AggressiveBuy,
            AggressiveSell = AggressiveSell,
            TickBurst = TickBurst,
            LiquiditySweep = LiquiditySweep,
            ReactionZone = ReactionZone
        };
    }
}