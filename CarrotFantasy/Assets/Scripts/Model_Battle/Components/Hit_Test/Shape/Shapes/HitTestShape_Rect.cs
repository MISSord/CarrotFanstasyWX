namespace CarrotFantasy
{
    public class HitTestShape_Rect : HitTestShape_Base
    {
        public Fix64 x;
        public Fix64 y;
        public Fix64 sizeX;
        public Fix64 sizeY;

        public HitTestShape_Rect(HitShapeType hitType, Fix64 x, Fix64 y, Fix64 sizeX, Fix64 sizeY) : base(hitType)
        {
            this.Reset(x, y, sizeX, sizeY);
            this.resetStrDesc();
        }

        private void Reset(Fix64 x, Fix64 y, Fix64 sizeX, Fix64 sizeY)
        {
            this.x = x;
            this.y = y;
            this.sizeX = sizeX;
            this.sizeY = sizeY;

            this.boundsX = x;
            this.boundsY = y;
            this.boundsSizeX = sizeX;
            this.boundsSizeY = sizeY;
        }
    }
}
