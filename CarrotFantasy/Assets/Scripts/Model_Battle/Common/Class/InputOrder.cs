namespace CarrotFantasy
{
    public class InputOrder
    {
        public int frameId { get; private set; }
        public int x { get; private set; }
        public int y { get; private set; }
        public int order { get; private set; }

        public int towerId { get; private set; }

        public void SetOrder(int frame, int x, int y, int order)
        {
            this.frameId = frame;
            this.x = x;
            this.y = y;
            this.order = order;
        }

        public void SetTowerId(int towerId)
        {
            this.towerId = towerId;
        }
    }
}
