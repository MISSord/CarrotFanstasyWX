using MongoDB.Bson.Serialization.Attributes;
using System.Net;

namespace ETModel
{
    [BsonIgnoreExtraElements]
    public class InnerConfig : AConfigComponent
    {
        [BsonIgnore]
        public IPEndPoint IPEndPoint { get; private set; }

        public string Address { get; set; }

        public override void EndInit()
        {
            this.IPEndPoint = NetworkHelper.ToIPEndPoint(this.Address);
        }
    }
}