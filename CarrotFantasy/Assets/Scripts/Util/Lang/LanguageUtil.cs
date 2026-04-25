using System;
using System.Collections.Generic;

namespace CarrotFantasy
{
    public enum LanguageType
    {
        zh_cn,
        zh_us,
    }

    public class LanguageUtil
    {
        private static LanguageUtil languageUtil;
        private Zh_language curLanguageBag;
        private LanguageType curType;

        private Dictionary<UnityEngine.SystemLanguage, Zh_language> systemLanguageList = new Dictionary<UnityEngine.SystemLanguage, Zh_language>();

        public static LanguageUtil Instance
        {
            get
            {
                if (languageUtil == null)
                {
                    languageUtil = new LanguageUtil();
                    languageUtil.LoadLanguageBag();
                }
                return languageUtil;
            }
        }

        private void LoadLanguageBag()
        {
            systemLanguageList.Add(UnityEngine.SystemLanguage.Chinese, new zh_cn());
            systemLanguageList.Add(UnityEngine.SystemLanguage.English, new zh_cn());
            //systemLanguageList.Add(UnityEngine.SystemLanguage.ChineseSimplified, new zh_cn());
            //systemLanguageList.Add(UnityEngine.SystemLanguage.ChineseTraditional, new zh_cn());

            UnityEngine.SystemLanguage sys = UnityEngine.Application.systemLanguage;
            if (sys == UnityEngine.SystemLanguage.Chinese) //暂时这样写
            {
                curType = LanguageType.zh_cn;
                curLanguageBag = systemLanguageList[UnityEngine.SystemLanguage.Chinese];
            }
            else
            {
                curType = LanguageType.zh_us;
                curLanguageBag = systemLanguageList[UnityEngine.SystemLanguage.Chinese];
            }
        }

        public LanguageType GetCurLanguageType()
        {
            return curType;
        }

        public String GetString(int id)
        {
            return curLanguageBag.GetString(id);
        }

        public String GetFormatString(int id, string one)
        {
            return String.Format(curLanguageBag.GetString(id), one);
        }

        public String GetFormatString(int id, string one, string two)
        {
            return String.Format(curLanguageBag.GetString(id), one, two);
        }
        public String GetFormatString(int id, string one, string two, string three)
        {
            return String.Format(curLanguageBag.GetString(id), one, two, three);
        }
        public String GetFormatString(int id, string[] list)
        {
            return String.Format(curLanguageBag.GetString(id), list);
        }
    }
}
