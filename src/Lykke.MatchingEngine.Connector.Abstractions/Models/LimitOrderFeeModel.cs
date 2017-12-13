﻿namespace Lykke.MatchingEngine.Connector.Abstractions.Models
{
    public class LimitOrderFeeModel
    {
        public int Type { get; set; }
        public double MakerSize { get; set; }
        public double TakerSize { get; set; }
        public string SourceClientId { get; set; }
        public string TargetClientId { get; set; }
    }

    public enum LimitOrderFeeType
    {
        NO_FEE = 0,
        CLIENT_FEE = 1,
        EXTERNAL_FEE = 2
    }
}