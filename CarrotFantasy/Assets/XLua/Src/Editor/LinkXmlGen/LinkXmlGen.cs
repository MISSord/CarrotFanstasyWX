using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using XLua;
using System.IO;
using System.Text;
using System.Linq;
using CSObjectWrapEditor;

public class LinkXmlGen : ScriptableObject
{
    public TextAsset Template;

    public static IEnumerable<CustomGenTask> GetTasks(LuaEnv lua_env, UserConfig user_cfg)
    {
        LuaTable data = lua_env.NewTable();
        var assembly_infos = (from type in (user_cfg.ReflectionUse.Concat(user_cfg.LuaCallCSharp))
                              group type by type.Assembly.GetName().Name into assembly_info
                              select new { FullName = assembly_info.Key, Types = assembly_info.ToList()}).ToList();
        data.Set("assembly_infos", assembly_infos);

        yield return new CustomGenTask
        {
            Data = data,
            Output = new StreamWriter(GeneratorConfig.common_path + "/link.xml",
            false, Encoding.UTF8)
        };
    }

    [GenCodeMenu]//加到 Generate Code 菜单里头
    public static void GenLinkXml()
    {
        // 项目里没有 LinkXmlGen.asset，模板直接按路径读。
        TextAsset template = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/XLua/Src/Editor/LinkXmlGen/LinkXmlGen.tpl.txt");
        if (template == null)
        {
            Debug.LogWarning("[LinkXmlGen] 找不到模板 LinkXmlGen.tpl.txt，跳过 link.xml 生成。");
            return;
        }

        Generator.CustomGen(template.text, GetTasks);
    }
}
