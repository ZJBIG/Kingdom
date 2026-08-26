using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// The narrative archive for the mouse civilization. It is deliberately
/// read-only: gameplay managers remain the authority for progression, while
/// this layer only decides which memories the player may read.
/// </summary>
public sealed class StoryChapter
{
    public string Id { get; }
    public string Title { get; }
    public string EraLabel { get; }
    public string Summary { get; }
    public string Body { get; }
    public TechLevel RequiredEra { get; }
    public string RequiredTutorialStepId { get; }
    public string RequiredResearchId { get; }
    public string RequiredBuildingId { get; }
    public string RequiredWorkshopId { get; }
    public bool RequiresWorkshopPurchase { get; }
    public bool RequiresSectorAccess { get; }
    public bool RequiresCampaignProgress { get; }
    public bool RequiresSectorOccupation { get; }

    public StoryChapter(string id, string title, string eraLabel,
        string summary, string body, TechLevel requiredEra,
        string requiredTutorialStepId = "", bool requiresWorkshopPurchase = false,
        bool requiresSectorAccess = false, bool requiresCampaignProgress = false,
        bool requiresSectorOccupation = false, string requiredResearchId = "",
        string requiredBuildingId = "", string requiredWorkshopId = "")
    {
        Id = id;
        Title = title;
        EraLabel = eraLabel;
        Summary = summary;
        Body = body;
        RequiredEra = requiredEra;
        RequiredTutorialStepId = requiredTutorialStepId ?? string.Empty;
        RequiredResearchId = requiredResearchId ?? string.Empty;
        RequiredBuildingId = requiredBuildingId ?? string.Empty;
        RequiredWorkshopId = requiredWorkshopId ?? string.Empty;
        RequiresWorkshopPurchase = requiresWorkshopPurchase;
        RequiresSectorAccess = requiresSectorAccess;
        RequiresCampaignProgress = requiresCampaignProgress;
        RequiresSectorOccupation = requiresSectorOccupation;
    }
}

public static class StoryManager
{
    private static readonly IReadOnlyList<StoryChapter> chapters =
        new List<StoryChapter>
        {
            new StoryChapter(
                "prologue-ashes",
                "序章：灰烬中的耳语",
                "原始时代",
                "鼠族文明没有消失，只是失去了记得自己的方式。",
                "很久以前，天穹曾被灯火照亮，鼠族在巨大的城市阴影下建立过自己的秩序。后来那场灾变夺走了道路、文字与名字。幸存者躲进荒野，把最后一点火种藏在石缝深处。\n\n如今，新的族群从废墟边缘醒来。你要做的不是追逐一座旧城，而是让一个能够持续成长的王国重新出现。每一块木材、每一间居所和每一次研究，都会把失落的文明拉回现实。没有任何先知会告诉他们终点在哪里，只有一代又一代鼠族，用亲手完成的工作证明文明仍然可以重新开始。",
                TechLevel.Animal),
            new StoryChapter(
                "first-fire",
                "第一章：守住火种",
                "原始时代",
                "资源不只是数字，它们是鼠族重新拥有明天的证据。",
                "最早的记录只剩下几道刻痕：食物要被稳定地获得，木材要被持续地收集，族群才不必在下一个寒夜重新迁徙。\n\n当资源开始流动，鼠族第一次意识到，复兴并不从宏伟建筑开始，而从一条可靠的生产循环开始。每一份多出来的储备，都让族人有机会停下来修补工具、照顾幼崽，或把一段经验交给下一位收集者。资源于是有了第二层意义：它们把不可预测的荒野，慢慢变成可以安排的明天。",
                TechLevel.Animal,
                "resources"),
            new StoryChapter(
                "walls-and-shelter",
                "第二章：墙内的名字",
                "原始时代",
                "第一座建筑让临时营地变成了可以留下的地方。",
                "建筑不是静止的装饰。它们把木材、食物和知识变成容量、生产力与安全边界。每当一座建筑完成，鼠族就少依赖一点运气，多拥有一项能够传给下一代的能力。\n\n王国的轮廓，始终从一面能够抵御风雨的墙开始。墙内保存的不只是粮食，还有族谱、火种和对季节的记忆。孩子们在墙下学习辨认材料，老人们则为每一块石头讲述来处；一座建筑因此成为共同生活的承诺。",
                TechLevel.Animal,
                "building"),
            new StoryChapter(
                "the-growing-clan",
                "第三章：会留下的人",
                "原始时代",
                "人口增长意味着火塘旁开始出现不属于同一代人的声音。",
                "食物与幸福度让族群愿意留下，人口容量让更多家庭有了位置。人口不是一条需要填满的数值，而是王国未来所有生产、研究与远行的承担者。\n\n当第一个新生儿被记入族谱，鼠族复兴才真正拥有了时间。新生儿会在还未见过的道路上长大，也会把今天的决定带到更远的时代。族群第一次明白，建设不是为了让数字变大，而是为了让陌生的未来拥有可以继承的人。",
                TechLevel.Animal,
                "population"),
            new StoryChapter(
                "remembered-knowledge",
                "第四章：从石片上读回天空",
                "原始时代",
                "研究让鼠族不再只是重复祖先的动作，而开始理解它们为何有效。",
                "旧文明留下的知识并不完整。研究者只能从残缺符号、反复试验和生产中的失败里，拼回一条可用的道路。\n\n每项研究都连接着一个真实的未来：一座建筑、一段生产链，或通往下一时代的关键思想。知识因此不是菜单上的奖励，而是复兴工程的方向盘。研究者也学会尊重失败，因为每一次错误都替后来者排除了一条危险的路。当第一枚新符号被所有聚落理解，鼠族便重新拥有了跨越距离的声音。",
                TechLevel.Animal,
                "research"),
            new StoryChapter(
                "the-first-chain",
                "第五章：让事物彼此相连",
                "原始时代",
                "原材料经过加工，才会变成能改变王国规模的东西。",
                "当一种建筑的产出成为另一种建筑的输入，鼠族第一次建立了超越单个工匠的协作。原材料、加工与高级用途组成链条，王国也从一堆储藏物变成了有节奏的生产机器。\n\n这条规律会一直伴随鼠族：越遥远的目标，越需要让早期的产业继续发挥作用。工匠开始用同一套尺度交接材料，运输者开始按照生产节奏安排路线，整个王国第一次像一个能够自我修正的整体。每一环都可能成为瓶颈，也都值得被理解，而不是被遗忘。",
                TechLevel.Animal,
                "production-chain"),
            new StoryChapter(
                "neolithic-return",
                "第六章：重新定居",
                "新石器时代",
                "鼠族终于可以把迁徙路线画成村落，把季节记成历法。",
                "进入新石器时代并不意味着忘记荒野，而是学会让荒野成为计划的一部分。灌溉、储粮、陶瓷、纺织与文字治理，把一次次偶然的生存经验变成可传承的制度。\n\n新的时代不是旧时代的替代品。它会继续消耗早期积累，并把火种交给更大的社会。村落第一次拥有了固定的边界，季节第一次被写进公共历法。鼠族开始为尚未出生的人修建仓库，也开始争论应当把哪一段历史刻在石碑上。定居让他们获得土地，也让他们承担守护土地的责任。",
                TechLevel.Neolithic),
            new StoryChapter(
                "medieval-order",
                "第七章：道路与秩序",
                "中世纪",
                "当王国拥有多个聚落，规则本身也必须成为一种基础设施。",
                "贸易、学院、行政与城市住宅让鼠族第一次能够在陌生者之间维持信任。道路连接的不只是资源，也连接了不同族群对未来的想象。\n\n王冠的意义从来不是权力本身，而是让更多人相信，今天投入的劳动会在明天仍然有价值。城市的钟声为不同聚落校准时间，法典则为陌生人划出共同的底线。鼠族仍会争执、交易和失败，但他们开始相信秩序不是束缚，而是让更远的合作成为可能的桥梁。",
                TechLevel.Medieval),
            new StoryChapter(
                "industrial-awakening",
                "第八章：王国开始学习规模",
                "工业时代",
                "进入工业时代不是多了一批建筑，而是王国第一次必须管理一整套相互依赖的系统。",
                "工业化让鼠族第一次同时面对能源、人口、物流与知识。它解决的不是一座旧工厂，而是如何让许多系统一起工作。去时代页查看新的真实条件，再从概览追踪王国的下一步。",
                TechLevel.Industrial,
                requiredResearchId: "Industrialization"),
            new StoryChapter(
                "workshop-memory",
                "第九章：工坊里的第二次发明",
                "工业时代",
                "真正的进步，不只是建造更多机器，而是学会让旧机器继续变得更好。",
                "工坊让一次成功的改造变成可以重复的经验。它连接研究、材料与旧有建筑，解决了工业体系只能依靠单次发明的问题。去 Workshop 页查看真实改良，再观察对应建筑的变化。",
                TechLevel.Industrial, requiresWorkshopPurchase: true,
                requiredResearchId: "PrecisionManufacturing",
                requiredWorkshopId: "PrecisionTooling"),
            new StoryChapter(
                "industrial-scale",
                "第十章：把雷声驯入机器",
                "工业时代",
                "电力与规模化生产让复兴从地方故事变成文明工程。",
                "机器工厂把分散的工艺变成可重复的生产。它解决了单个工匠无法支撑规模的问题，也带来电力、物流和输入材料的新压力。去建筑页查看工厂的真实输入输出，再决定下一项研究。",
                TechLevel.Industrial, "", false, false, false, false,
                "PrecisionManufacturing", "MachineFactory"),
            new StoryChapter(
                "industrial-organization",
                "第十一章：让工厂记住方法",
                "工业时代",
                "规模化生产只有被记录、组织和复用，才不会随着某一代工匠离开而再次失传。",
                "工厂组织把机器、工序与经验连接成可以传承的制度。它让鼠族不再依赖偶然的天才，而是能够把一次成功复制到更多生产线上。去研究页查看真实的组织知识，再观察概览中的工业阻碍。",
                TechLevel.Industrial,
                requiredResearchId: "FactoryOrganization"),
            new StoryChapter(
                "industrial-power",
                "第十二章：让时间一起转动",
                "工业时代",
                "蒸汽与电力第一次让王国共享同一套生产节奏。",
                "蒸汽动力让机器能够持续工作，电力则把这种力量送到更多建筑。它解决了生产因能源不足而停顿的问题，也要求王国管理燃料与供给。去建筑页查看电力变化，再观察工厂是否稳定运行。",
                TechLevel.Industrial, "", false, false, false, false,
                "SteamPower", "SteamPlant"),
            new StoryChapter(
                "industrial-homes",
                "第十三章：给每一代留下位置",
                "工业时代",
                "工业不只是让机器更快，也让更多鼠族拥有可以回来的家。",
                "工业住宅提高人口容量，让更多鼠族能够留下并参与生产。它解决了工厂扩张后缺少居住位置的问题，同时增加食物与幸福度压力。去建筑页查看容量变化，再回到资源页观察人口是否稳定增长。",
                TechLevel.Industrial, "", false, false, false, false,
                "IndustrialHabitationEngineering", "IndustrialHabitationComplex"),
            new StoryChapter(
                "industrial-grid",
                "第十四章：让能源穿过整座王国",
                "工业时代",
                "中央电站与电网让能源从一座工厂的能力，变成所有聚落共享的承诺。",
                "中央电站把电力从单座建筑的能力变成王国共享的供给。它解决了工业建筑不断扩张后的能源协调问题，也需要持续燃料。去建筑页查看供给与消耗，再观察整条生产链的运行状态。",
                TechLevel.Industrial, "", false, false, false, false,
                "PowerGridEngineering", "CentralPowerStation"),
            new StoryChapter(
                "industrial-materials",
                "第十五章：让矿石学会承担重量",
                "工业时代",
                "铁路把原料送到了工厂，但只有标准化材料才能让规模真正可靠。",
                "工业冶炼把多种矿物加工成可交接的标准材料。它解决了原料批次不稳定的问题，也会消耗电力、物流和上游资源；冶炼炉还需要真实的 IntegratedFurnaces 工坊改良。去 Workshop 和建筑页查看前置、输入输出，再检查相关资源的净产出。",
                TechLevel.Industrial, "", false, false, false, false,
                "IndustrialMetalSmelting", "IndustrialMetalSmelter"),
            new StoryChapter(
                "industrial-network",
                "第十六章：把远方接进来",
                "工业时代",
                "铁路和物流让分散的资源第一次成为同一个王国的生产计划。",
                "铁路枢纽把矿山、工厂与聚落接进同一条物流链。它解决了原料无法按时抵达的问题，也增加了物流管理的压力。去建筑页查看物流变化，再观察生产链是否仍有输入短缺。",
                TechLevel.Industrial, "", false, false, false, false,
                "RailwayEngineering", "RailHub"),
            new StoryChapter(
                "industrial-chemistry",
                "第十七章：让旧材料重新组合",
                "工业时代",
                "化学工业让鼠族第一次面对更强大的材料，也面对更复杂的代价。",
                "化学工业把燃料、矿物和早期加工品重新组合成新的材料。它解决了部分高级生产的输入问题，也让供应链更加复杂。去建筑页查看化工厂的真实流量，再回资源页确认上游供给。",
                TechLevel.Industrial, "", false, false, false, false,
                "IndustrialChemistry", "ChemicalPlant"),
            new StoryChapter(
                "industrial-knowledge",
                "第十八章：给机器留下记录",
                "工业时代",
                "现代大学把零散经验变成可以传给下一代的工业知识。",
                "大学把工厂经验、失败记录和实验方法保存成可复用的知识。它解决了工业扩张后研究难以传承的问题，并提供新的研究力。去建筑页查看研究力变化，再在研究页选择下一条真实路径。",
                TechLevel.Industrial, "", false, false, false, false,
                "ModernUniversity", "University"),
            new StoryChapter(
                "industrial-frontier",
                "第十九章：为星际铸造骨架",
                "工业时代",
                "当工业开始服务尚未抵达的地方，鼠族终于拥有了离开故土的准备。",
                "钛合金工艺把工业经验推向星际目标。它解决了未来结构材料的研究门槛，但并不等于星际设施已经建成。去研究页确认工艺状态，再打开时代页查看进入下一时代的真实条件。",
                TechLevel.Industrial, "", false, false, false, false,
                "TitaniumAlloyEngineering"),
            new StoryChapter(
                "frontier-sectors",
                "第二十章：星区边疆",
                "太空时代",
                "当王国拥有了星图，边疆就不再是地图的尽头，而是新的责任。",
                "第一批星区记录来自遥远的探测器：陌生的岩带、沉默的遗迹和可以改变航线的资源脉流。鼠族没有把星区当作无限仓库，因为每一条补给线都要经过漫长的黑暗，每一次开采都可能改变一个尚未理解的环境。\n\n于是星区管理成为生产、领土与探索之间的平衡。前线需要王国持续供应，王国也从前线得到新的原料、遗物和关于旧文明的线索。边疆越远，鼠族越必须学会珍惜每一支远征队带回来的有限消息。",
                TechLevel.Spacer, "", false, true),
            new StoryChapter(
                "war-between-stars",
                "第二十一章：群星之间的守望",
                "太空时代",
                "远行带来发现，也带来必须守护同伴与家园的理由。",
                "并非所有沉默的星区都欢迎来客。某些航道留下了无法解释的警报，某些遗迹周围则盘旋着不属于任何已知族群的信号。战斗因此没有被写成荣耀的终点，而被写进物流、研究和人口共同承担的账簿。\n\n舰队需要材料，前线需要补给，指挥者还必须决定什么时候前进、什么时候撤退。鼠族第一次在星海中面对一个古老问题：文明的力量究竟用来征服未知，还是用来让更多生命拥有选择未来的机会。",
                TechLevel.Spacer, "", false, false, false, true),
            new StoryChapter(
                "beyond-the-sky",
                "第二十二章：越过大气层",
                "太空时代",
                "复兴的王国第一次从自己的星球外观察故乡。",
                "轨道能源、居住设施、量子计算与星际航行，将鼠族带到旧文明曾经凝望的高度。太空并不是逃离王国，而是把王国的生产、研究、人口与战斗能力延伸到更大的地图。\n\n每一次远行都提醒鼠族：真正要寻找的，也许不是旧文明留下的答案，而是证明自己已经能够提出新的问题。第一支远航队从轨道上回望故乡，看见大陆的灯火像一枚被重新点亮的符号。星际时代带来的不只是新领土，还有无法回避的选择：要把旧日的扩张带到群星，还是先学会在更大的黑暗中保持彼此信任。",
                TechLevel.Spacer, "", false, false, false, true),
            new StoryChapter(
                "the-old-boundary",
                "终章：远古边界之外",
                "极致时代与远古科技时代",
                "当文明抵达记忆的边缘，复兴开始变成对自身起源的追问。",
                "极致时代的鼠族已经能够重写许多曾被视为自然法则的限制，但力量越大，失落的历史就越不能被简单复原。远古科技时代留下的边界，可能通向答案，也可能通向灾变最初的原因。\n\n这不是一条替王国写好的终点。它等待鼠族用自己的选择，决定文明复兴究竟意味着回到过去，还是创造一个过去从未拥有过的未来。档案馆保存着两种相互矛盾的记录：一种说祖先因傲慢而毁灭，另一种说他们曾为保护后来者主动沉默。真相必须由新的鼠族承担，而不是由一块远古石片替他们决定。",
                TechLevel.Archotech)
        }.AsReadOnly();

    public static IReadOnlyList<StoryChapter> Chapters => chapters;

    public static string GetProgressSignature(TechLevel currentEra,
        TutorialManager tutorial)
    {
        StringBuilder signature = new StringBuilder();
        signature.Append((int)currentEra).Append(':')
            .Append(tutorial == null ? -1 : tutorial.Version);
        if (tutorial != null)
        {
            var tutorialIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < chapters.Count; i++)
            {
                StoryChapter chapter = chapters[i];
                if (chapter == null || string.IsNullOrEmpty(chapter.RequiredTutorialStepId) ||
                    !tutorialIds.Add(chapter.RequiredTutorialStepId))
                    continue;
                bool completed = false;
                foreach (string completedId in tutorial.CompletedStepIds)
                    if (completedId == chapter.RequiredTutorialStepId)
                    {
                        completed = true;
                        break;
                    }
                signature.Append("|t:").Append(chapter.RequiredTutorialStepId)
                    .Append('=').Append(completed ? '1' : '0');
            }
        }
        for (int i = 0; i < chapters.Count; i++)
        {
            StoryChapter chapter = chapters[i];
            if (chapter == null)
                continue;
            if (!string.IsNullOrEmpty(chapter.RequiredResearchId))
                signature.Append("|r:").Append(chapter.RequiredResearchId)
                    .Append('=').Append(GetResearchStatusCode(
                        chapter.RequiredResearchId));
            if (!string.IsNullOrEmpty(chapter.RequiredBuildingId))
                signature.Append("|b:").Append(chapter.RequiredBuildingId)
                    .Append('=').Append(HasOwnedBuilding(
                        chapter.RequiredBuildingId) ? '1' : '0');
            if (!string.IsNullOrEmpty(chapter.RequiredWorkshopId) ||
                chapter.RequiresWorkshopPurchase)
                signature.Append("|w:").Append(chapter.RequiredWorkshopId)
                    .Append('=').Append(HasWorkshopPurchase(
                        chapter.RequiredWorkshopId) ? '1' : '0');
        }

        if (RequiresSectorProgressRefresh())
            signature.Append("|s=").Append(GetSectorProgressSignature());
        return signature.ToString();
    }

    public static StoryChapter FindLatestUnlocked(TechLevel currentEra,
        TutorialManager tutorial)
    {
        StoryChapter latest = null;
        for (int i = 0; i < chapters.Count; i++)
            if (IsUnlocked(chapters[i], currentEra, tutorial))
                latest = chapters[i];
        return latest;
    }

    public static StoryChapter FindNextLocked(TechLevel currentEra,
        TutorialManager tutorial)
    {
        for (int i = 0; i < chapters.Count; i++)
            if (!IsUnlocked(chapters[i], currentEra, tutorial))
                return chapters[i];
        return null;
    }

    public static int CountUnlocked(TechLevel currentEra, TutorialManager tutorial)
    {
        int count = 0;
        for (int i = 0; i < chapters.Count; i++)
            if (IsUnlocked(chapters[i], currentEra, tutorial))
                count++;
        return count;
    }

    public static bool IsUnlocked(StoryChapter chapter, TechLevel currentEra,
        TutorialManager tutorial)
    {
        if (chapter == null)
            return false;
        if (chapter.RequiredEra == TechLevel.Industrial &&
            !HasPreviousIndustrialMemory(chapter, currentEra, tutorial))
            return false;
        if (!HasCompletedResearch(chapter.RequiredResearchId))
            return false;
        if (!HasOwnedBuilding(chapter.RequiredBuildingId))
            return false;
        if (chapter.RequiresWorkshopPurchase &&
            !HasWorkshopPurchase(chapter.RequiredWorkshopId))
            return false;
        if (chapter.RequiresSectorAccess && !HasSectorAccess())
            return false;
        if (chapter.RequiresCampaignProgress && !HasCampaignProgress())
            return false;
        if (chapter.RequiresSectorOccupation && !HasSectorOccupation())
            return false;
        if (string.IsNullOrEmpty(chapter.RequiredTutorialStepId))
            return currentEra >= chapter.RequiredEra;
        if (currentEra > chapter.RequiredEra)
            return true;
        if (tutorial == null)
            return false;
        foreach (string completedId in tutorial.CompletedStepIds)
            if (completedId == chapter.RequiredTutorialStepId)
                return true;
        return false;
    }

    private static bool HasPreviousIndustrialMemory(StoryChapter chapter,
        TechLevel currentEra, TutorialManager tutorial)
    {
        int chapterIndex = -1;
        for (int i = 0; i < chapters.Count; i++)
            if (chapters[i] == chapter)
            {
                chapterIndex = i;
                break;
            }

        if (chapterIndex <= 0)
            return true;

        StoryChapter previous = chapters[chapterIndex - 1];
        return previous == null || IsUnlocked(previous, currentEra, tutorial);
    }

    private static bool HasCompletedResearch(string researchId)
    {
        if (string.IsNullOrEmpty(researchId))
            return true;
        ResearchManager manager = ResearchManager.Instance;
        if (!DataBase<Research>.TryFind(researchId, out Research definition))
            return false;
        return manager != null && definition != null &&
            manager.States.TryGetValue(definition, out ResearchState state) &&
            state != null && state.Status == ResearchStatus.Completed;
    }

    private static int GetResearchStatusCode(string researchId)
    {
        if (string.IsNullOrEmpty(researchId) ||
            !DataBase<Research>.TryFind(researchId, out Research definition) ||
            definition == null)
            return -1;
        ResearchManager manager = ResearchManager.Instance;
        if (manager == null || !manager.States.TryGetValue(definition,
            out ResearchState state) || state == null)
            return -1;
        return (int)state.Status;
    }

    private static bool HasOwnedBuilding(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId))
            return true;
        BuildingManager manager = BuildingManager.Instance;
        if (!DataBase<Building>.TryFind(buildingId, out Building definition))
            return false;
        if (manager == null || definition == null ||
            !manager.States.TryGetValue(definition, out BuildingState state) ||
            state == null || state.Amount <= ExpantaNum.Zero)
            return false;

        // Reuse the authoritative read-only prerequisite check so an old or
        // externally restored save cannot unlock a memory for a building whose
        // research or Workshop prerequisites are not actually satisfied.
        if (GameManager.Instance == null || GameManager.Instance.State == null ||
            ResearchManager.Instance == null)
            return false;
        return manager.ArePrerequisitesMet(definition, out _);
    }

    private static bool HasWorkshopPurchase(string requiredWorkshopId = "")
    {
        // Workshop is a later-era dependency. Story evaluates locked future
        // chapters even in Animal/Neolithic, so do not use the strict
        // Singleton accessor before the manager exists.
        WorkshopManager workshopManager =
            UnityEngine.Object.FindObjectOfType<WorkshopManager>();
        if (workshopManager == null)
            return false;
        foreach (WorkshopUpgradeState state in workshopManager.States.Values)
            if (state != null && state.Purchased && state.Definition != null &&
                state.Definition.TechLevel == TechLevel.Industrial &&
                (string.IsNullOrEmpty(requiredWorkshopId) ||
                 state.Definition.Id == requiredWorkshopId))
                return true;
        return false;
    }

    private static bool HasSectorAccess()
    {
        GameManager gameManager = GameManager.Instance;
        SectorManager sectorManager = gameManager == null ? null : gameManager.Sectors;
        if (sectorManager == null)
            return false;
        foreach (SectorState state in sectorManager.OrderedStates)
            if (state != null && state.Unlocked)
                return true;
        return false;
    }

    private static bool HasCampaignProgress()
    {
        GameManager gameManager = GameManager.Instance;
        SectorManager sectorManager = gameManager == null ? null : gameManager.Sectors;
        if (sectorManager == null)
            return false;
        foreach (SectorState state in sectorManager.OrderedStates)
            if (state != null && (state.CampaignActive ||
                state.CampaignProgress > ExpantaNum.Zero))
                return true;
        return false;
    }

    private static bool HasSectorOccupation()
    {
        GameManager gameManager = GameManager.Instance;
        SectorManager sectorManager = gameManager == null ? null : gameManager.Sectors;
        if (sectorManager == null)
            return false;
        foreach (SectorState state in sectorManager.OrderedStates)
            if (state != null && state.Occupied)
                return true;
        return false;
    }

    private static bool RequiresSectorProgressRefresh()
    {
        for (int i = 0; i < chapters.Count; i++)
            if (chapters[i] != null && (chapters[i].RequiresSectorAccess ||
                chapters[i].RequiresCampaignProgress ||
                chapters[i].RequiresSectorOccupation))
                return true;
        return false;
    }

    private static string GetSectorProgressSignature()
    {
        GameManager gameManager = GameManager.Instance;
        SectorManager sectorManager = gameManager == null ? null : gameManager.Sectors;
        if (sectorManager == null)
            return "none";
        StringBuilder signature = new StringBuilder();
        for (int i = 0; i < sectorManager.OrderedStates.Count; i++)
        {
            SectorState state = sectorManager.OrderedStates[i];
            if (state == null)
                continue;
            signature.Append(state.Unlocked ? '1' : '0')
                .Append(state.Occupied ? '1' : '0')
                .Append(state.CampaignActive ? '1' : '0')
                .Append(state.CampaignProgress > ExpantaNum.Zero ? '1' : '0')
                .Append(';');
        }
        return signature.ToString();
    }

    public static string GetUnlockHint(StoryChapter chapter, TechLevel currentEra)
    {
        if (chapter == null)
            return string.Empty;
        if (currentEra < chapter.RequiredEra)
            return "进入“" + chapter.RequiredEra.GetDescription() +
                "”后，这段记忆才会进入王国的现实。";
        if (!string.IsNullOrEmpty(chapter.RequiredTutorialStepId) &&
            !HasCompletedTutorialStep(chapter.RequiredTutorialStepId))
            return "完成当前引导目标后，这段记忆才会被正式唤醒。";
        StoryChapter previousIndustrial = GetPreviousLockedIndustrialChapter(
            chapter, currentEra, TutorialManager.Current);
        if (previousIndustrial != null)
            return "先唤醒上一段工业记忆“" + previousIndustrial.Title + "”：" +
                GetUnlockHint(previousIndustrial, currentEra);
        if (!string.IsNullOrEmpty(chapter.RequiredResearchId) &&
            !HasCompletedResearch(chapter.RequiredResearchId))
            return "完成研究“" + GetResearchLabel(chapter.RequiredResearchId) +
                "”，解锁这段工业能力。";
        if (!string.IsNullOrEmpty(chapter.RequiredBuildingId) &&
            !HasOwnedBuilding(chapter.RequiredBuildingId))
        {
            string workshopHint = GetBuildingWorkshopHint(chapter.RequiredBuildingId);
            if (!string.IsNullOrEmpty(workshopHint))
                return workshopHint;
            return "建成“" + GetBuildingLabel(chapter.RequiredBuildingId) +
                "”，让这段工业记忆从计划变成现实。";
        }
        return GetUnlockHint(chapter);
    }

    public static string GetChapterProgressHint(StoryChapter chapter,
        TechLevel currentEra)
    {
        string baseline = GetUnlockHint(chapter, currentEra);
        if (chapter == null || currentEra < chapter.RequiredEra)
            return baseline;
        if (!string.IsNullOrEmpty(chapter.RequiredTutorialStepId) &&
            !HasCompletedTutorialStep(chapter.RequiredTutorialStepId))
            return baseline;
        StoryChapter previousIndustrial = GetPreviousLockedIndustrialChapter(
            chapter, currentEra, TutorialManager.Current);
        if (previousIndustrial != null)
            return baseline;
        if (!string.IsNullOrEmpty(chapter.RequiredResearchId) &&
            !HasCompletedResearch(chapter.RequiredResearchId))
            return GetResearchProgressHint(chapter.RequiredResearchId);
        if (!string.IsNullOrEmpty(chapter.RequiredBuildingId) &&
            !HasOwnedBuilding(chapter.RequiredBuildingId))
        {
            string workshopHint = GetBuildingWorkshopHint(chapter.RequiredBuildingId);
            if (!string.IsNullOrEmpty(workshopHint))
                return workshopHint;
            return "研究已完成，下一步建造对应建筑，让这段工业记忆成为现实。";
        }
        if (chapter.RequiresWorkshopPurchase &&
            !HasWorkshopPurchase(chapter.RequiredWorkshopId))
            return "研究已完成，下一步购买一项真实工坊改造，让旧有生产体系继续成长。";
        return baseline;
    }

    private static StoryChapter GetPreviousLockedIndustrialChapter(
        StoryChapter chapter, TechLevel currentEra, TutorialManager tutorial)
    {
        if (chapter == null || chapter.RequiredEra != TechLevel.Industrial ||
            currentEra < TechLevel.Industrial)
            return null;

        int index = -1;
        for (int i = 0; i < chapters.Count; i++)
            if (chapters[i] == chapter)
            {
                index = i;
                break;
            }
        if (index <= 0)
            return null;

        StoryChapter previous = chapters[index - 1];
        return previous != null && !IsUnlocked(previous, currentEra, tutorial)
            ? previous
            : null;
    }

    private static bool HasCompletedTutorialStep(string stepId)
    {
        if (string.IsNullOrEmpty(stepId) || TutorialManager.Current == null)
            return string.IsNullOrEmpty(stepId);
        foreach (string completedId in TutorialManager.Current.CompletedStepIds)
            if (completedId == stepId)
                return true;
        return false;
    }

    private static string GetResearchProgressHint(string researchId)
    {
        if (!DataBase<Research>.TryFind(researchId, out Research definition) ||
            definition == null)
            return "完成研究“" + researchId + "”，继续追踪这段工业记忆。";

        ResearchManager manager = ResearchManager.Instance;
        if (manager == null || !manager.States.TryGetValue(definition,
            out ResearchState state) || state == null)
            return "完成研究“" + definition.Label + "”，继续追踪这段工业记忆。";

        switch (state.Status)
        {
            case ResearchStatus.Researching:
                return "研究进行中：“" + definition.Label + "”；等待知识积累完成。";
            case ResearchStatus.Queued:
                return "研究已排队：“" + definition.Label + "”；前面的知识完成后即可推进。";
            case ResearchStatus.WaitingResources:
                return "研究等待资源：“" + definition.Label + "”；先补齐研究所需资源。";
            case ResearchStatus.Available:
                return "研究可开始：“" + definition.Label + "”；用它建立下一段工业能力。";
            default:
                return "完成研究“" + definition.Label + "”，继续追踪这段工业记忆。";
        }
    }

    public static string GetUnlockHint(StoryChapter chapter)
    {
        if (chapter == null)
            return string.Empty;
        if (chapter.RequiresWorkshopPurchase)
            return "完成一项真实工坊改造后，这段记忆才会被正式唤醒。";
        if (chapter.RequiresSectorAccess)
            return "解锁一片真实星区后，这段边疆记忆才会被正式唤醒。";
        if (chapter.RequiresCampaignProgress)
            return "开始一次真实远征并推进星区进度后，这段记忆才会被正式唤醒。";
        if (chapter.RequiresSectorOccupation)
            return "完成一次真实星区占领后，这段记忆才会被正式唤醒。";
        if (!string.IsNullOrEmpty(chapter.RequiredResearchId))
            return "完成研究“" + GetResearchLabel(chapter.RequiredResearchId) +
                "”，再用它建立对应的工业能力。";
        if (!string.IsNullOrEmpty(chapter.RequiredBuildingId))
            return "建成“" + GetBuildingLabel(chapter.RequiredBuildingId) +
                "”，让这段工业记忆从计划变成现实。";
        if (!string.IsNullOrEmpty(chapter.RequiredTutorialStepId) &&
            chapter.RequiredEra == TechLevel.Animal)
            return "完成当前发展行动后，这段记忆会被重新读出。";
        return "进入“" + chapter.RequiredEra.GetDescription() + "”后解锁。";
    }

    private static string GetResearchLabel(string researchId)
    {
        return DataBase<Research>.TryFind(researchId, out Research research) &&
            research != null && !string.IsNullOrEmpty(research.Label)
            ? research.Label
            : researchId;
    }

    private static string GetBuildingLabel(string buildingId)
    {
        return DataBase<Building>.TryFind(buildingId, out Building building) &&
            building != null && !string.IsNullOrEmpty(building.Label)
            ? building.Label
            : buildingId;
    }

    private static string GetBuildingWorkshopHint(string buildingId)
    {
        if (!DataBase<Building>.TryFind(buildingId, out Building building) ||
            building == null)
            return string.Empty;

        if (building.RequiredResearch != null)
            for (int i = 0; i < building.RequiredResearch.Count; i++)
            {
                Research prerequisite = building.RequiredResearch[i];
                if (prerequisite != null && !HasCompletedResearch(prerequisite.Id))
                    return "建造“" + building.Label + "”前还需要研究“" +
                        prerequisite.Label + "”。";
            }

        if (building.RequiredWorkshopUpgrades == null)
            return string.Empty;

        WorkshopManager workshopManager =
            UnityEngine.Object.FindObjectOfType<WorkshopManager>();
        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = building.RequiredWorkshopUpgrades[i];
            if (upgrade == null ||
                (workshopManager != null && workshopManager.IsPurchased(upgrade)))
                continue;
            if (upgrade.RequiredResearch != null)
                for (int r = 0; r < upgrade.RequiredResearch.Count; r++)
                {
                    Research prerequisite = upgrade.RequiredResearch[r];
                    if (prerequisite != null && !HasCompletedResearch(prerequisite.Id))
                        return "建造“" + building.Label + "”前还需要研究“" +
                            prerequisite.Label + "”，再购买 Workshop 改良“" +
                            upgrade.Label + "”。";
                }
            return "研究已完成，但建造“" + building.Label +
                "”前还需要购买 Workshop 改良“" + upgrade.Label + "”。";
        }
        return string.Empty;
    }
}
