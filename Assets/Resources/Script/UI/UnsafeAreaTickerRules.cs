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
        "河湾鼠群为一颗栗子争论半天",
        "幼鼠在岩缝找到甜草根",
        "老鼠又把远山的故事讲长了",
        "采集队给栗子筐编上草绳记号",
        "窝棚门口挂起一串漂亮石子",
        "一只鼠声称能听懂溪水说话",
        "萤火虫照亮河岸归途",
        "巡夜鼠坚持月亮是圆形奶酪",
        "新编的草帽比主人还要高",
        "采集鼠沿河留下歪歪的脚印",
        "大家正在评选最舒服的苔藓",
        "一片大叶子成了幼鼠的雨伞",
        "老鼠收藏的石头又多了一颗",
        "草编窝棚挂起芦苇风铃",
        "守夜鼠给每颗星星起了名字",
        "小鼠捡到形状像耳朵的树根",
        "鱼群逆流涌入浅滩",
        "鼠群给归途的小路摆上白石",
        "两窝幼鼠争抢暖石",
        "两只鼠为谁先打哈欠争了起来",
        "猎手的故事里猎物又变大了",
        "采药鼠把苦叶子藏进甜果里",
        "河边的青蛙抢走了合唱领唱",
        "最勇敢的鼠也怕冰凉的洗澡水",
        "一顶草帽在风里跑赢了主人",
        "月圆夜里群鼠共舞",
        "石片割开最硬的果壳",
        "幼鼠认真练习大鼠的叹气声",
        "一只蜗牛成了鼠群的新邻居",
        "采药鼠辨出止痛叶片"
    };

    private static readonly string[] StoneAgeEraHeadlines =
    {
        "河渠今日引水入田",
        "陶工在罐底偷偷画了笑脸",
        "织坊晒出新染麻布",
        "铜匠坚持歪斧头更适合左手",
        "赶集鼠把算盘珠当成了糖果",
        "灰鼠家族迁入新石屋",
        "村口石凳坐满了晒太阳的鼠",
        "商旅带来南方彩石",
        "磨坊鼠说风今天有点偏心",
        "陶罐里的回声吓到了小学徒",
        "陶窑烧出首批黑纹罐",
        "学徒把师父的胡须画上泥板",
        "河边洗衣的鼠唱起旧日的歌",
        "赶集路上人人都认得那只羊",
        "织工为染错的布起了新花名",
        "村里的鼓手学会了第二种节拍",
        "一只鸡霸占了最暖和的石墙",
        "新木桥成了幼鼠的赛跑终点",
        "新井水比旧泉更清甜",
        "丰收宴摆满烤根茎",
        "陶工说裂纹也是一种花纹",
        "河畔舂米声响到黄昏",
        "老鼠给谷穗起了一个很长的名",
        "商旅讲的海比村子还要宽",
        "孩子们把脚印留在新泥砖上",
        "草药师熬成驱虫药汤",
        "雨后田埂冒出成群蛙",
        "牧鼠寻回走失山羊",
        "月下鼓声庆祝新田开垦",
        "石碑旁又聚起一圈讲故事的鼠"
    };

    private static readonly string[] MedievalEraHeadlines =
    {
        "铁匠铺的学徒敲出了新节拍",
        "面包师拒绝透露蜂蜜饼的秘方",
        "城镇钟楼的鸽子比钟更准时",
        "行会为学徒名额争执",
        "抄书鼠在页边画了一只小鼠",
        "驿站最受欢迎的是一只慢马",
        "集市说书人的龙越讲越小",
        "裁缝铺流行长尾披风",
        "酒馆老板宣布赊账笑话不打折",
        "桥边的诗人终于写完了第一行",
        "学徒用木剑赢得了扫地比赛",
        "修道院的猫学会了装作没听见",
        "钟楼乌鸦衔走铜钥匙",
        "集市鼠抢购远方胡椒",
        "酒馆传唱失踪骑士歌谣",
        "磨坊的风向旗指向了面包店",
        "书商说最厚的书最能垫桌脚",
        "港口卸下北海羊毛",
        "铁匠家的午饭铃比城钟还响",
        "石匠在旧墙发现密道",
        "吟游诗鼠忘了自己故事的结尾",
        "一把旧椅子成了酒馆名人席",
        "学院辩论月亮是否有洞",
        "守门鼠收集了各地的问候语",
        "木匠给歪桌腿配了歪凳子",
        "城墙上的晚霞引来许多画鼠",
        "小巷里飘出炖豆子与胡椒香",
        "船匠铺接到三艘新订单",
        "公会钟声宣布集市开张",
        "贵族宴席改用银制餐叉"
    };

    private static readonly string[] IndustrialEraHeadlines =
    {
        "城东铁匠铺仍坚持手工打铁",
        "三只学徒鼠追回滚落齿轮",
        "面包铺推出齿轮形麦饼",
        "蒸汽邮车因鼹鼠洞临时改道",
        "北站行李鼠误把奶酪发往南城",
        "工程师终于修好了自己的闹钟",
        "钟楼慢了七分钟引发早市争执",
        "报童鼠率先报道市长掉帽",
        "一位发明家设计了自动挠背机",
        "车站的钟表各有各的意见",
        "工人用废零件拼了一只铁鼠",
        "夜班食堂开始供应热饼",
        "烟囱清洁鼠要求增发洗澡券",
        "老匠人教机器师傅磨刀",
        "街灯下的棋局延长了半小时",
        "运河驳船因鸭群封航半刻",
        "蒸汽管旁成了冬天聊天的好地方",
        "工程图纸背面画着午饭菜单",
        "港口起重机吊起一窝睡鼠",
        "新雨衣让报童看起来像蘑菇",
        "钟表匠把自己的表又调快了",
        "一只风筝飞过了高高的烟囱",
        "图书馆把打鼾区改名为静思区",
        "城南药房免费测量鼠须",
        "汽笛声里有人听出了生日歌",
        "小报悬赏寻找神秘汽笛手",
        "学校举办纸桥承重比赛",
        "食堂厨师坚称配方比图纸复杂",
        "博物馆展出首台木制机床",
        "市议会批准增设夜间电车"
    };

    private static readonly string[] SpacerEraHeadlines =
    {
        "月面温室里的薄荷长得很精神",
        "轨道厨房试种无重力香葱",
        "火星货船捎来红沙纪念瓶",
        "空间站鼠须静电投诉增加",
        "维修鼠说胶带也该拥有勋章",
        "补给舱送错三百双左脚靴",
        "远航鼠收到一段家乡的雨声",
        "轨道婚礼因日出延长三分钟",
        "小乘客问月亮上有没有月亮",
        "空间站举办最圆饭团比赛",
        "厨师成功追上逃走的胡椒粉",
        "新宇航服的口袋装满了家书",
        "小行星矿工发现冰晶洞",
        "月面学校开设陨石辨认课",
        "太空农场南瓜撞上舱顶",
        "休息舱开始播放溪水与蛙声",
        "通信鼠把晚安发向了两颗星球",
        "星港旅店推出观月房",
        "失重棋局需要先找回棋子",
        "机库里有人画了巨大的向日葵",
        "一颗螺丝经历了漫长的寻回之旅",
        "月港出租车开始按圈计费",
        "货运鼠为归程留了一格行李位",
        "天文课的作业是画故乡的月亮",
        "舷窗前的小椅子总是很抢手",
        "食堂推出故乡味道的试验菜单",
        "舱壁涂鸦成为新景点",
        "外环居民抱怨人造黎明太早",
        "值班员为每次日出记了一笔",
        "远航的歌谣多了一段返乡曲"
    };

    private static readonly string[] UltraEraHeadlines =
    {
        "合成鼠给今天的日出打了满分",
        "折叠走廊让迷路有了新借口",
        "量子茶壶仍需耐心等水烧开",
        "记忆花园里保留着童年的秋千",
        "星港的面包香依然让鼠驻足",
        "自动厨房学会了奶奶的口头禅",
        "全息宠物坚持睡在真实的毯子上",
        "仿生花在窗边投下柔软的影子",
        "艺术鼠用光子画笔临摹旧石斧",
        "跨星球棋局等待对手吃完午饭",
        "机器管家学会了故意讲冷笑话",
        "跨星快递送来一张手写菜单",
        "新式枕头引发了传统派的抗议",
        "记忆馆征集旧时代的摇篮曲",
        "文明档案收录了一张食堂菜单",
        "翻译器说这次笑话难在语气",
        "光子乐器演奏了一首乡间小调",
        "鼠群讨论永远不会迟到的钟",
        "花园里真正的蜗牛依然很慢",
        "博物馆里的石斧依旧朴素",
        "工程师开始修理祖传的木椅",
        "全息雨景引来了真的雨伞",
        "星际邮票保留了手写的邮戳",
        "一场虚拟野餐忘了带虚拟杯子",
        "合成甜点的配方仍然是秘密",
        "记忆管理员为旧笑话留了位置",
        "星图再大也画得下故乡的河湾",
        "穿越群星的鼠仍想念灶边小凳",
        "休息日的天空被调成了晴蓝色",
        "老摇篮曲被编成三百声部合唱"
    };

    private static readonly string[] ArchotechEraHeadlines =
    {
        "超越者仍为茶太烫轻轻吹气",
        "星海花园为一株野草挂上名牌",
        "时间档案收录了一次漫长午睡",
        "群星之间有人在练习旧木笛",
        "永恒图书馆新添了一本笑话集",
        "鼠群为一颗小石头开了展览",
        "记忆馆重现第一次搬栗子的争吵",
        "今天的议题是云朵像什么",
        "一场跨越纪元的棋局尚未结束",
        "古老窝棚的影像有了新访客",
        "无限画布上的第一笔是涂歪的鼠须",
        "遥远文明寄来一本奶酪食谱",
        "群星合唱团因一声喷嚏重新排练",
        "穿越时间的家书提醒记得添衣",
        "小鼠问群星之外还有没有家",
        "文明的第一口锅被擦得很亮",
        "最聪明的鼠也找不到那只袜子",
        "宇宙很大而饭桌刚好坐满",
        "一场流星雨没有打断晚安故事",
        "超越者回到老窝棚量童年身高",
        "旧时代的蒲公英在花园里落种",
        "超越者为一幅儿童画让出展位",
        "永恒日历圈出了奶奶的生日",
        "星海的尽头有人在煮一壶茶",
        "一只纸船漂过记忆中的小河",
        "全知档案仍答不出午饭吃什么",
        "幼鼠为最亮的星取了朴素的名",
        "古老的笑话依旧有人笑出声",
        "故乡的夜空被轻轻收进相册",
        "超越者用星光修好老窝棚的油灯"
    };

    public static IReadOnlyList<string> BuildStatusNotices(GameState state, ResearchState research)
    {
        List<string> notices = new(MaximumNoticeCount);
        if (state == null)
            return notices;

        WorldEventState events = state.Events;
        if (events.ActiveKind == WorldEventKind.Fire)
            notices.Add(events.PopulationLost > 0
                ? "失火：损失1人，产出-20%" : "失火：资源粮食产出-20%");
        else if (events.ActiveKind == WorldEventKind.ProductiveDay)
            notices.Add("生产顺利：资源粮食产出+15%");
        else if (events.LastKind == WorldEventKind.Aurora &&
                 WorldEventState.EventIntervalSeconds - events.SecondsUntilNextEvent < WorldEventState.EffectDurationSeconds)
            notices.Add("极光夜：鼠群赏夜空，无数值影响");

        if (state.FoodNetRate < ExpantaNum.Zero)
            notices.Add("粮食持续减少：增产或暂停耗粮建筑");
        if (notices.Count < MaximumNoticeCount && state.PowerSatisfaction < ExpantaNum.One)
            notices.Add("供电不足：扩建发电或暂停耗电建筑");
        if (notices.Count < MaximumNoticeCount && state.LogisticsSatisfaction < ExpantaNum.One)
            notices.Add("物流不足：扩建物流或暂停耗物流建筑");
        if (notices.Count < MaximumNoticeCount && research != null)
        {
            string suffix = !research.CostPaid
                ? "：待支付物资"
                : "：研究进行中";
            string label = string.IsNullOrWhiteSpace(research.Definition.Label)
                ? "当前研究" : research.Definition.Label;
            int labelLimit = MaximumHeadlineCharacters - suffix.Length;
            if (label.Length > labelLimit)
                label = label.Substring(0, labelLimit - 1) + "…";
            notices.Add(label + suffix);
        }
        return notices;
    }

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
        // Dates advance during scrolling; reshuffling here repeatedly restarts the visible feed.
        string[] headlines = GetEraHeadlines(era);
        int count = notices == null ? 0 : notices.Count;
        int notice = Mathf.Max(0, count - MaximumNoticeCount);
        for (int i = 0; i < headlines.Length; i++)
        {
            AppendHeadline(builder, headlines[i]);
            // Everyday life owns the feed; dispatches are occasional inserts.
            if ((i + 1) % 6 == 2 && notice < count)
                AppendHeadline(builder, notices[notice++]);
        }
        if (headlines.Length == 0)
            for (; notice < count; notice++) AppendHeadline(builder, notices[notice]);
        return builder.ToString();
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
            TechLevel.Ultra => UltraEraHeadlines,
            TechLevel.Archotech => ArchotechEraHeadlines,
            _ => Array.Empty<string>()
        };
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
