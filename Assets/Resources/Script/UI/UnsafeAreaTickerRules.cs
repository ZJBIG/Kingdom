using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class UnsafeAreaTickerRules
{
    public const int MaximumNoticeCount = 3;
    public const int MaximumHeadlineCharacters = 20;
    public const float MinimumTextWidth = 72f;

    private const float LayoutEpsilon = 0.0001f;

    private static readonly string[] AnimalEraHeadlines =
    {
        "河湾鼠群发现新浆果地",
        "夜巡鼠听见猫头鹰长鸣",
        "晨雾压低草原觅食视线",
        "幼鼠在岩缝找到甜草根",
        "松果堆旁留下陌生爪印",
        "采集队带回整筐野栗",
        "暴雨冲开旧獾洞入口",
        "萤火虫照亮河岸归途",
        "老鼠讲起远山巨兽传闻",
        "蛇影掠过南坡草丛",
        "河水上涨淹没低地鼠径",
        "鼠群迁往背风石坡",
        "干雷点燃远处枯草",
        "新火种熬过整夜风雨",
        "石片割开最硬的果壳",
        "草编窝棚挡住寒风",
        "野蜂群占据空心树",
        "采药鼠辨出止痛叶片",
        "两窝幼鼠争抢暖石",
        "星光指引迷路采集队",
        "鱼群逆流涌入浅滩",
        "鹿群踏平北边草甸",
        "鼠群合力搬走倒木",
        "硬壳虫爬进草籽堆",
        "清晨霜冻压弯芦苇",
        "山洞深处传来回声",
        "新磨石刃通过狩猎考验",
        "老鼠爪印遍布泥岸",
        "远方烟柱引发议论",
        "月圆夜里群鼠共舞"
    };

    private static readonly string[] StoneAgeEraHeadlines =
    {
        "河渠今日引水入田",
        "陶窑烧出首批黑纹罐",
        "谷仓鼠发现几袋受潮麦",
        "村口石墙又加高一层",
        "铜匠试成更薄的斧刃",
        "商旅带来南方彩石",
        "织坊晒出新染麻布",
        "灰鼠家族迁入新石屋",
        "丰收宴摆满烤根茎",
        "野猪撞坏东边篱笆",
        "议事圈通过河岸禁猎令",
        "牧鼠寻回走失山羊",
        "陶罐碎片拼出古老图案",
        "河畔舂米声响到黄昏",
        "木桥通向新开垦田地",
        "铁矿洞里发现红色晶石",
        "石碑刻下今年洪水线",
        "祭司称彗星预示丰年",
        "新井水比旧泉更清甜",
        "赶集鼠用盐换回兽皮",
        "炭窑浓烟引来村民抱怨",
        "青铜铃挂上村落大门",
        "孩子们在泥板上学字",
        "粮仓外排起缴谷长队",
        "草药师熬成驱虫药汤",
        "邻村送来和亲的陶杯",
        "雨后田埂冒出成群蛙",
        "守夜鼠抓住偷粮黄鼠狼",
        "村里公牛挣断鼻绳",
        "月下鼓声庆祝新田开垦"
    };

    private static readonly string[] MedievalEraHeadlines =
    {
        "铁匠铺连夜赶制农具",
        "城门税吏查出走私香料",
        "烘焙坊推出蜂蜜硬饼",
        "鼠王下令修缮北城墙",
        "行会为学徒名额争执",
        "修道院抄本缺了三页",
        "驿站换上最快的栗色马",
        "集市鼠抢购远方胡椒",
        "钟楼乌鸦衔走铜钥匙",
        "护城河里漂来陌生木箱",
        "裁缝铺流行长尾披风",
        "酒馆传唱失踪骑士歌谣",
        "面包师因缺盐提前打烊",
        "城堡厨房丢失奶酪轮",
        "商队平安穿过盗匪岭",
        "药草商高价收购银叶草",
        "领主赦免三户欠租鼠民",
        "港口卸下北海羊毛",
        "石匠在旧墙发现密道",
        "骑士团招募新的扈从",
        "公会钟声宣布集市开张",
        "学院辩论月亮是否有洞",
        "教堂彩窗映出奇怪鼠影",
        "法庭判决磨坊归属东村",
        "城中井水暂禁直接饮用",
        "乡间麦田遭遇乌鸦群",
        "王家猎场出现白鹿",
        "船匠铺接到三艘新订单",
        "贵族宴席改用银制餐叉",
        "城墙猫影引发全城戒严"
    };

    private static readonly string[] IndustrialEraHeadlines =
    {
        "城东铁匠铺夜炉失火",
        "三只学徒鼠追回滚落齿轮",
        "蒸汽邮车因鼹鼠洞临时改道",
        "河港工鼠打捞出百年铁钟",
        "面包铺推出齿轮形麦饼",
        "纺织女工发现会唱歌的线轴",
        "北站行李鼠误把奶酪发往南城",
        "钟楼慢了七分钟引发早市争执",
        "铁路餐车新增胡椒炖豆套餐",
        "烟囱清洁鼠要求增发洗澡券",
        "夜班巡警查获假冒机油",
        "旧城区煤价连续三日回落",
        "运河驳船因鸭群封航半刻",
        "报童鼠率先报道市长掉帽",
        "城西锅炉房招募听力好的工鼠",
        "铜币铸厂辟谣硬币缩水",
        "城际电报误传全城放假",
        "雨夜路灯照亮失踪车牌",
        "铁匠铺为消防队免费磨斧",
        "百货商场举办最香奶酪评选",
        "工会讨论缩短冬季夜班",
        "港口起重机吊起一窝睡鼠",
        "新式雨靴在泥街供不应求",
        "城南药房免费测量鼠须",
        "废铁市场发现军用水壶",
        "第三纺织厂提前发放薪资",
        "小报悬赏寻找神秘汽笛手",
        "消防鼠从烟道救出邮差",
        "博物馆展出首台木制机床",
        "市议会批准增设夜间电车"
    };

    private static readonly string[] SpacerEraHeadlines =
    {
        "月面温室收获首篮草莓",
        "轨道厨房试种无重力香葱",
        "火星货船捎来红沙纪念瓶",
        "空间站鼠须静电投诉增加",
        "三号舱窗外飘过一只扳手",
        "货运飞船为躲流星晚点",
        "月港海关查获走私奶酪",
        "轨道婚礼因日出延长三分钟",
        "舱外维修鼠捡回旧卫星铭牌",
        "深空邮局启用延迟回信章",
        "太阳风导致广播出现杂音",
        "小行星矿工发现冰晶洞",
        "环形城举办低重力跳高赛",
        "补给舱送错三百双左脚靴",
        "月面学校开设陨石辨认课",
        "太空农场南瓜撞上舱顶",
        "星港旅店推出观月房",
        "返航艇带回失联探测球",
        "航道气象台发布尘暴预警",
        "新移民排队领取舱室钥匙",
        "维修队修好会漏气的咖啡壶",
        "通讯员收到十年前的问候",
        "观测鼠发现彗星拖着双尾",
        "月港出租车开始按圈计费",
        "冷冻仓库一夜丢失九块奶酪",
        "轨道乐团排练时惊动警报",
        "深空货轮救起漂流信标",
        "氧气税调整引发茶馆热议",
        "舱壁涂鸦成为新景点",
        "外环居民抱怨人造黎明太早"
    };

    public static bool TryCalculateLayout(
        Vector2 safeAnchorMin,
        Vector2 safeAnchorMax,
        float canvasWidth,
        float horizontalPadding,
        out UnsafeAreaTickerEdge edge,
        out Vector2 anchorMin,
        out Vector2 anchorMax,
        out bool canShowText)
    {
        edge = UnsafeAreaTickerEdge.None;
        anchorMin = Vector2.zero;
        anchorMax = Vector2.zero;
        canShowText = false;

        if (canvasWidth <= 0f)
            return false;

        float leftInset = Mathf.Clamp01(safeAnchorMin.x);
        float rightInset = 1f - Mathf.Clamp01(safeAnchorMax.x);
        float normalizedWidth = Mathf.Max(leftInset, rightInset);
        if (normalizedWidth <= LayoutEpsilon)
            return false;

        if (rightInset > leftInset)
        {
            edge = UnsafeAreaTickerEdge.Right;
            anchorMin = new Vector2(1f - rightInset, 0f);
            anchorMax = Vector2.one;
        }
        else
        {
            edge = UnsafeAreaTickerEdge.Left;
            anchorMin = Vector2.zero;
            anchorMax = new Vector2(leftInset, 1f);
        }

        float textWidth = normalizedWidth * canvasWidth - Mathf.Max(0f, horizontalPadding);
        canShowText = textWidth >= MinimumTextWidth;
        return true;
    }

    public static string BuildFeedCycle(
        IReadOnlyList<string> notices,
        TechLevel era,
        string date)
    {
        StringBuilder builder = new();
        int noticeCount = notices == null ? 0 : notices.Count;
        int firstNotice = Mathf.Max(0, noticeCount - MaximumNoticeCount);

        if (noticeCount > 0)
        {
            for (int i = firstNotice; i < noticeCount; i++)
                AppendHeadline(builder, notices[i]);
        }

        AppendEraHeadlines(builder, era, date);
        return builder.ToString();
    }

    private static void AppendEraHeadlines(
        StringBuilder builder,
        TechLevel era,
        string date)
    {
        string[] headlines = GetEraHeadlines(era);
        if (headlines.Length == 0)
            return;

        int seed = StableTextSeed(date) ^ ((int)era * 997);
        int start = seed % headlines.Length;
        for (int i = 0; i < headlines.Length; i++)
            AppendHeadline(builder, headlines[(start + i) % headlines.Length]);
    }

    private static string[] GetEraHeadlines(TechLevel era)
    {
        return era switch
        {
            TechLevel.Animal => AnimalEraHeadlines,
            TechLevel.StoneAge => StoneAgeEraHeadlines,
            TechLevel.Medieval => MedievalEraHeadlines,
            TechLevel.Industrial => IndustrialEraHeadlines,
            TechLevel.Spacer => SpacerEraHeadlines,
            TechLevel.Ultra => Array.Empty<string>(),
            TechLevel.Archotech => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };
    }

    private static int StableTextSeed(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int seed = 17;
        for (int i = 0; i < value.Length; i++)
            seed = unchecked(seed * 31 + value[i]);
        return seed & int.MaxValue;
    }

    private static void AppendHeadline(StringBuilder builder, string headline)
    {
        if (string.IsNullOrWhiteSpace(headline))
            return;

        string normalized = headline.Trim();
        if (normalized.Length > MaximumHeadlineCharacters)
            normalized = normalized.Substring(0, MaximumHeadlineCharacters - 1) + "…";

        if (builder.Length > 0)
            builder.Append('\n');
        builder.Append(normalized);
    }
}
