namespace CarrotFantasy
{
    public class Battle_func
    {
        public static Fix64Vector2 P(Fix64 x, Fix64 y)
        {
            return new Fix64Vector2(x, y);
        }

        public static Fix64Vector2 PAdd(Fix64Vector2 one, Fix64Vector2 two)
        {
            return one + two;
        }

        public static Fix64Vector2 PSub(Fix64Vector2 one, Fix64Vector2 two)
        {
            return one - two;
        }

        public static Fix64Vector2 PMul(Fix64Vector2 one, Fix64Vector2 two)
        {
            return new Fix64Vector2(one.X * two.X, one.Y * two.Y);
        }

        public static Fix64 PGetLength(Fix64Vector2 pt)
        {
            return Fix64.Sqrt(pt.X * pt.X + pt.Y * pt.Y); //保留修改意见
        }

        public static Fix64 PDistanceSqure(Fix64Vector2 pt1, Fix64Vector2 pt2)
        {
            return (pt1.X - pt2.X) * (pt1.X - pt2.X) + (pt1.Y - pt2.Y) * (pt1.Y - pt2.Y);
        }

        public static Fix64 pi = Fix64.Pi;
        public static Fix64 pi_div_180 = Fix64.DegreeToRad;

        public static Fix64 Angle2radian(Fix64 angle)
        {
            return angle * pi_div_180; //未来定点数
        }

        public static Fix64 p180_div_pi = new Fix64(180) / Fix64.Pi;
        public static Fix64 Radian2angle(Fix64 radian)
        {
            return radian * p180_div_pi;//未来定点数
        }

        public static Fix64 Min(Fix64 min1, Fix64 min2, Fix64 min3, Fix64 min4)
        {
            Fix64 Min = min1;
            if (Min > min2)
            {
                Min = min2;
            }
            if (Min > min3)
            {
                Min = min3;
            }
            if (Min > min4)
            {
                Min = min4;
            }
            return Min;
        }

        public static Fix64 Max(Fix64 min1, Fix64 min2, Fix64 min3, Fix64 min4)
        {
            Fix64 Min = min1;
            if (Min < min2)
            {
                Min = min2;
            }
            if (Min < min3)
            {
                Min = min3;
            }
            if (Min < min4)
            {
                Min = min4;
            }
            return Min;
        }

        public static Fix64 PDot(Fix64Vector2 one, Fix64Vector2 two)
        {
            return one.X * two.X + one.Y * two.Y;
        }

        public static Fix64 PLengthSQ(Fix64Vector2 pt)
        {
            return PDot(pt, pt);
        }

        public static Fix64 PGetDistanceSQ(Fix64 pointX1, Fix64 pointY1, Fix64 pointX2, Fix64 pointY2)
        {
            Fix64 offsetX = pointX1 - pointX2;
            Fix64 offsetY = pointY1 - pointY2;
            Fix64 distanceSQ = offsetX * offsetX + offsetY * offsetY;

            return distanceSQ; //未来定点数
        }

        public static Fix64 PGetDistanceOfPoint2Line(Fix64Vector2 P, Fix64Vector2 plineA, Fix64Vector2 plineB)
        {
            Fix64Vector2 u = PSub(plineA, plineB);
            Fix64 lengthSQ = PLengthSQ(u);
            lengthSQ = lengthSQ != Fix64.Zero ? lengthSQ : Fix64.One;
            Fix64 t = PDot(PSub(P, plineA), u) / lengthSQ;
            t = Fix64.Max(Fix64.Min(t, Fix64.One), Fix64.Zero);

            Fix64 d = PGetLength(PSub(P, PAdd(plineA, new Fix64Vector2(t * u.X, t * u.Y))));
            return d;
        }

        public static bool RectContainsPoint(Fix64 rectX, Fix64 rectY, Fix64 rectWidth, Fix64 rectHeight, Fix64 pointX, Fix64 pointY)
        {
            bool ret = false;
            if ((pointX >= rectX) && (pointX <= rectX + rectWidth)
                && (pointY >= rectY) && (pointY <= rectY + rectHeight))
            {
                ret = true;
            }
            return ret;
        }

        public static bool RectIntersectsRect(Fix64 x1, Fix64 y1, Fix64 width1, Fix64 height1,
            Fix64 x2, Fix64 y2, Fix64 width2, Fix64 height2)
        {
            //未来定点数
            bool intersect = !((x1 > x2 + width2) || (x1 + width1 < x2) || (y1 > y2 + height2)
                || (y1 + height1 < y2));
            return intersect;
        }
    }
}
