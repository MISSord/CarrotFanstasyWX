namespace CarrotFantasy
{
    public class Gamer
    {
        public long UserID { get; private set; }
        public bool isReady { get; set; }
        public Gamer(long userId)
        {
            this.UserID = userId;
            this.isReady = false;
        }
    }
}
