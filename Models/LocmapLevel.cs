namespace PROXIMAMOP.Models;

public class LocmapLevel
{
    public string Symbol { get; set; } = "";
    public double Price { get; set; }
    public int BuyTicks { get; set; }
    public int SellTicks { get; set; }
    public int TotalTicks { get; set; }
    public double BuyStrength { get; set; }
    public double SellStrength { get; set; }
    public double Delta { get; set; }
    public double Contracts { get; set; }
    public string Zone { get; set; } = "";
    public double LastPrice { get; set; }

    public double BuyPercent => TotalTicks <= 0 ? 0 : Math.Min(100, (BuyStrength / Math.Max(BuyStrength + SellStrength, 1)) * 100);
    public double SellPercent => TotalTicks <= 0 ? 0 : Math.Min(100, (SellStrength / Math.Max(BuyStrength + SellStrength, 1)) * 100);

    public string ZoneText => Zone switch
    {
        "BUY_PRESSURE" => "BUY",
        "SELL_PRESSURE" => "SELL",
        "RANGE_LIQUIDITY" => "RANGE",
        _ => "BALANCED"
    };
}