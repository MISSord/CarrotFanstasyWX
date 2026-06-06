
namespace CarrotFantasy {

    public static class ResPath 
    {
        public static string GetRawImagePath(string name)
        {
            if (name == null) return string.Empty;
            return string.Format("ui/rawimages/{0}", name.ToLower());
        }

        public static string GetGameOptionImagePath()
        {
            return "ui/view/gameoption/image_atlas";
        }
    }
}
