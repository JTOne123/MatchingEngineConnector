﻿using Common;
using ProtoBuf;

namespace Lykke.MatchingEngine.Connector.Models.Me
{
    [ProtoContract]
    public class MeNewCashInOutModel
    {
        [ProtoMember(1, IsRequired = true)]
        public string Id { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string ClientId { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public long DateTime { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public string AssetId { get; set; }

        [ProtoMember(5, IsRequired = true)]
        public double Amount { get; set; }

        [ProtoMember(6, IsRequired = false)]
        public FeeContract Fee { get; set; }
        
        public static MeNewCashInOutModel Create(
            string id,
            string clientId,
            string assetId,
            double amount,
            FeeContract feeContract = null)
        {
            return new MeNewCashInOutModel
            {
                Id = id,
                ClientId = clientId,
                DateTime = (long)System.DateTime.UtcNow.ToUnixTime(),
                AssetId = assetId,
                Amount = amount,
                Fee = feeContract
            };
        }
    }
}
