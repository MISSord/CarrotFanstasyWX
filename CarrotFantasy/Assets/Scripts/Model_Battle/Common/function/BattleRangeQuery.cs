namespace CarrotFantasy
{
    /// <summary>圆–圆接触/射程判定（塔索敌、集火、HitTest 窄相位共用）。</summary>
    public static class BattleRangeQuery
    {
        public static bool CirclesOverlap(
            Fix64 ax, Fix64 ay, Fix64 aRadius,
            Fix64 bx, Fix64 by, Fix64 bRadius)
        {
            Fix64 sumR = aRadius + bRadius;
            if (sumR <= Fix64.Zero)
            {
                return false;
            }

            return Battle_func.PGetDistanceSQ(ax, ay, bx, by) <= sumR * sumR;
        }

        public static bool CirclesOverlap(HitTestShape_Circle a, HitTestShape_Circle b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return CirclesOverlap(a.centerX, a.centerY, a.radius, b.centerX, b.centerY, b.radius);
        }

        public static bool IsInRange(UnitTransformComponent source, UnitTransformComponent target)
        {
            if (source == null || target == null)
            {
                return false;
            }

            Fix64Vector2 sourcePos = source.GetLastPosition();
            Fix64Vector2 targetPos = target.GetLastPosition();
            return CirclesOverlap(
                sourcePos.X, sourcePos.Y, source.GetBodyRadius(),
                targetPos.X, targetPos.Y, target.GetBodyRadius());
        }
    }
}
