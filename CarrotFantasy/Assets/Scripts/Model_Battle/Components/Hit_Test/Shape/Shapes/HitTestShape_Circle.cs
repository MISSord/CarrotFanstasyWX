namespace CarrotFantasy
{
    public class HitTestShape_Circle : HitTestShape_Base
    {
        public Fix64 centerX;
        public Fix64 centerY;
        public Fix64 radius;

        public HitTestShape_Circle(HitShapeType hitType, Fix64 x, Fix64 y, Fix64 rad) : base(hitType)
        {
            this.Reset(x, y, rad);
            this.resetStrDesc();
        }

        public void Reset(Fix64 x, Fix64 y, Fix64 rad)
        {
            this.centerX = x;
            this.centerY = y;
            this.radius = rad;

            this.boundsX = x - rad;
            this.boundsY = y - rad;
            this.boundsSizeX = rad * Fix64.Two;
            this.boundsSizeY = rad * Fix64.Two;
        }
    }
}
