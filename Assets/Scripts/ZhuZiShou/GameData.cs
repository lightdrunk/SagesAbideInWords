using System.Collections.Generic;
using UnityEngine;

namespace ZhuZiShou
{
    public enum Faction { Ru, Mo, Dao, Fa, Bing, Ming, Nong, Za }
    public enum GlyphKind { Base, Root }
    public enum EnemyKind { Huo, Wang, Luan, Gui, Chan }
    public enum Phase { Prepare, Combat, WaveEnd, GameOver, Victory }

    public enum PerkId
    {
        BoWen, GuangJi, DuXing, ShouDian, MingCha, GuBen,
        RuFeng, MoShou, DaoFa, YanXing, BingGe, QiaoBian, ShunShi, JianRong,
        YiZiQianJin, XinHuo, BaiJiaZhengMing, SiBian, HouJi
    }

    public class GlyphDef
    {
        public string Glyph;
        public GlyphKind Kind;
        public Faction Faction;
        public string UnitName;
    }

    public class UnitDef
    {
        public string Name;
        public Faction Faction;
        public float Hp;
        public float Atk;
        public float Interval;
        public int Range;
        public float SkillCd;
        public string Skill;
        public bool IsSpecial;
    }

    public class EnemyDef
    {
        public EnemyKind Kind;
        public string Name;
        public float Hp;
        public float Damage;
        public float Interval;
        public float Speed;
        public int KillGold;
        public bool Elite;
        public bool Boss;
        public string Trait;
    }

    public struct SpawnGroup
    {
        public EnemyKind Kind;
        public int Count;
        public float Interval;
        public SpawnGroup(EnemyKind kind, int count, float interval)
        {
            Kind = kind; Count = count; Interval = interval;
        }
    }

    public class WaveDef
    {
        public int Index;
        public readonly List<SpawnGroup> Groups = new List<SpawnGroup>();
    }

    public class ComboDef
    {
        public string A;
        public string B;
        public string Unit;
        public ComboDef(string a, string b, string unit) { A = a; B = b; Unit = unit; }
    }

    public class BondTier
    {
        public Faction Faction;
        public int Count;
        public string Desc;
        public BondTier(Faction f, int count, string desc) { Faction = f; Count = count; Desc = desc; }
    }

    public class PerkDef
    {
        public PerkId Id;
        public string Name;
        public string Desc;
        public PerkDef(PerkId id, string name, string desc) { Id = id; Name = name; Desc = desc; }
    }

    public static class FactionInfo
    {
        public static readonly string[] Names = { "儒家", "墨家", "道家", "法家", "兵家", "名家", "农家", "杂家" };
        public static string Name(Faction f) { return Names[(int)f]; }
    }

    public static class GameData
    {
        public const int Cols = 5;
        public const int Rows = 6;
        public const int StartGold = 45;
        public const int RefreshCostBase = 10;
        public const int GlyphCost = 5;
        public const int HandLimit = 10;
        public const int CoreHpMax = 5;
        public const int WaveGoldReward = 18;
        public const int TotalWaves = 10;
        public const float Tier2Mul = 1.6f;
        public const float Tier3Mul = 2.6f;

        public static readonly string[] BaseChars = { "儒", "墨", "道", "法", "兵", "名", "农", "杂" };
        public static readonly string[] BaseUnitNames = { "儒生", "墨徒", "道者", "法吏", "兵士", "辩士", "农者", "杂家" };
        public static readonly string[] RootChars = { "孔", "子", "老", "庄", "周", "商", "鞅", "韩", "非", "孙", "武", "吴", "起", "许", "行", "惠", "施", "吕", "览", "翟" };

        public static readonly List<GlyphDef> Glyphs = new List<GlyphDef>();
        public static readonly List<ComboDef> Combos = new List<ComboDef>();
        public static readonly List<UnitDef> Units = new List<UnitDef>();
        public static readonly List<EnemyDef> Enemies = new List<EnemyDef>();
        public static readonly List<WaveDef> Waves = new List<WaveDef>();
        public static readonly List<BondTier> Bonds = new List<BondTier>();
        public static readonly List<PerkDef> Perks = new List<PerkDef>();
        public static readonly List<Vector2Int> Path = new List<Vector2Int>();

        static GameData()
        {
            BuildGlyphs();
            BuildUnits();
            BuildCombos();
            BuildEnemies();
            BuildWaves();
            BuildBonds();
            BuildPerks();
            BuildPath();
        }

        private static void BuildGlyphs()
        {
            for (int i = 0; i < BaseChars.Length; i++)
            {
                Glyphs.Add(new GlyphDef
                {
                    Glyph = BaseChars[i],
                    Kind = GlyphKind.Base,
                    Faction = (Faction)i,
                    UnitName = BaseUnitNames[i]
                });
            }
            for (int i = 0; i < RootChars.Length; i++)
            {
                Glyphs.Add(new GlyphDef { Glyph = RootChars[i], Kind = GlyphKind.Root });
            }
        }

        private static void BuildCombos()
        {
            Combos.Add(new ComboDef("孔", "子", "孔子"));
            Combos.Add(new ComboDef("墨", "翟", "墨子"));
            Combos.Add(new ComboDef("老", "子", "老子"));
            Combos.Add(new ComboDef("庄", "周", "庄子"));
            Combos.Add(new ComboDef("商", "鞅", "商鞅"));
            Combos.Add(new ComboDef("韩", "非", "韩非"));
            Combos.Add(new ComboDef("孙", "武", "孙武"));
            Combos.Add(new ComboDef("吴", "起", "吴起"));
            Combos.Add(new ComboDef("许", "行", "许行"));
            Combos.Add(new ComboDef("惠", "施", "惠施"));
            Combos.Add(new ComboDef("吕", "览", "吕不韦"));
        }

        private static void BuildUnits()
        {
            AddUnit("儒生", Faction.Ru, 60, 12, 1.2f, 2, 6, "光环：范围内敌人攻速降低 15%", false);
            AddUnit("墨徒", Faction.Mo, 90, 10, 1.3f, 1, 5, "放置获得 18 点护盾", false);
            AddUnit("道者", Faction.Dao, 45, 14, 1.1f, 3, 7, "命中减速 25%，持续 1.5 秒", false);
            AddUnit("法吏", Faction.Fa, 55, 16, 1.2f, 2, 5, "攻击附带 10% 破甲，持续 3 秒", false);
            AddUnit("兵士", Faction.Bing, 50, 18, 1.4f, 1, 4, "无主动技能，高攻速输出", false);
            AddUnit("辩士", Faction.Ming, 40, 11, 1.0f, 2, 8, "使目标停止移动 1.2 秒", false);
            AddUnit("农者", Faction.Nong, 45, 8, 1.0f, 2, 7, "每秒为最近受伤单位回复 4 点生命", false);
            AddUnit("杂家", Faction.Za, 55, 13, 1.2f, 2, 6, "相邻羁绊增益额外提升 5%", false);

            AddUnit("孔子", Faction.Ru, 120, 22, 1.1f, 3, 8, "3 格光环，敌人攻速降低 25%", true);
            AddUnit("墨子", Faction.Mo, 190, 18, 1.2f, 1, 6, "放置生成 40 点护盾，破碎时眩晕周围敌人", true);
            AddUnit("老子", Faction.Dao, 110, 24, 1.0f, 3, 8, "范围减速并造成持续伤害", true);
            AddUnit("庄子", Faction.Dao, 100, 28, 1.1f, 3, 9, "范围爆发伤害，20% 闪避", true);
            AddUnit("商鞅", Faction.Fa, 115, 25, 1.2f, 2, 6, "目标生命越低伤害越高，最高 +40%", true);
            AddUnit("韩非", Faction.Fa, 120, 26, 1.1f, 2, 5, "20% 暴击并附加破甲", true);
            AddUnit("孙武", Faction.Bing, 130, 24, 1.3f, 3, 7, "全队攻击提升 15%", true);
            AddUnit("吴起", Faction.Bing, 125, 32, 1.2f, 1, 6, "目标生命低于 18% 直接斩杀", true);
            AddUnit("许行", Faction.Nong, 95, 14, 1.0f, 2, 8, "持续为范围内单位恢复生命", true);
            AddUnit("惠施", Faction.Ming, 90, 19, 1.0f, 3, 9, "区域敌人停止移动 1.5 秒", true);
            AddUnit("吕不韦", Faction.Za, 110, 21, 1.2f, 3, 7, "已激活羁绊效果再提升 10%", true);
        }

        private static void AddUnit(string name, Faction f, float hp, float atk, float interval, int range, float cd, string skill, bool special)
        {
            Units.Add(new UnitDef
            {
                Name = name, Faction = f, Hp = hp, Atk = atk, Interval = interval,
                Range = range, SkillCd = cd, Skill = skill, IsSpecial = special
            });
        }

        private static void BuildEnemies()
        {
            Enemies.Add(new EnemyDef { Kind = EnemyKind.Huo, Name = "惑", Hp = 35, Damage = 8, Interval = 1.5f, Speed = 0.8f, KillGold = 3, Trait = "普通单位，速度与生命均衡" });
            Enemies.Add(new EnemyDef { Kind = EnemyKind.Wang, Name = "妄", Hp = 22, Damage = 6, Interval = 1.2f, Speed = 1.4f, KillGold = 2, Trait = "高速低血，拉扯防线" });
            Enemies.Add(new EnemyDef { Kind = EnemyKind.Luan, Name = "乱", Hp = 90, Damage = 15, Interval = 1.8f, Speed = 0.4f, KillGold = 6, Trait = "肉盾，移动慢生命高" });
            Enemies.Add(new EnemyDef { Kind = EnemyKind.Gui, Name = "诡", Hp = 50, Damage = 12, Interval = 1.5f, Speed = 0.7f, KillGold = 10, Elite = true, Trait = "精英，受击后短暂隐身" });
            Enemies.Add(new EnemyDef { Kind = EnemyKind.Chan, Name = "谗", Hp = 220, Damage = 25, Interval = 1.5f, Speed = 0.5f, KillGold = 35, Boss = true, Trait = "首领，周期性降低全场攻速" });
        }

        private static void BuildWaves()
        {
            AddWave(1, new SpawnGroup(EnemyKind.Huo, 6, 1.3f));
            AddWave(2, new SpawnGroup(EnemyKind.Huo, 8, 1.1f));
            AddWave(3, new SpawnGroup(EnemyKind.Huo, 8, 1.1f), new SpawnGroup(EnemyKind.Wang, 3, 1.0f));
            AddWave(4, new SpawnGroup(EnemyKind.Huo, 6, 1.0f), new SpawnGroup(EnemyKind.Wang, 4, 0.9f), new SpawnGroup(EnemyKind.Luan, 2, 1.4f));
            AddWave(5, new SpawnGroup(EnemyKind.Huo, 8, 0.9f), new SpawnGroup(EnemyKind.Wang, 6, 0.8f), new SpawnGroup(EnemyKind.Luan, 3, 1.3f));
            AddWave(6, new SpawnGroup(EnemyKind.Huo, 10, 0.8f), new SpawnGroup(EnemyKind.Wang, 8, 0.7f), new SpawnGroup(EnemyKind.Luan, 4, 1.2f));
            AddWave(7, new SpawnGroup(EnemyKind.Huo, 10, 0.8f), new SpawnGroup(EnemyKind.Wang, 8, 0.7f), new SpawnGroup(EnemyKind.Luan, 4, 1.2f), new SpawnGroup(EnemyKind.Gui, 2, 1.6f));
            AddWave(8, new SpawnGroup(EnemyKind.Huo, 12, 0.7f), new SpawnGroup(EnemyKind.Wang, 10, 0.6f), new SpawnGroup(EnemyKind.Luan, 5, 1.1f), new SpawnGroup(EnemyKind.Gui, 4, 1.4f));
            AddWave(9, new SpawnGroup(EnemyKind.Huo, 14, 0.6f), new SpawnGroup(EnemyKind.Wang, 12, 0.55f), new SpawnGroup(EnemyKind.Luan, 6, 1.0f), new SpawnGroup(EnemyKind.Gui, 6, 1.2f));
            AddWave(10, new SpawnGroup(EnemyKind.Chan, 1, 1.0f), new SpawnGroup(EnemyKind.Huo, 8, 0.7f), new SpawnGroup(EnemyKind.Wang, 6, 0.6f), new SpawnGroup(EnemyKind.Luan, 4, 1.1f), new SpawnGroup(EnemyKind.Gui, 4, 1.3f));
        }

        private static void AddWave(int index, params SpawnGroup[] groups)
        {
            WaveDef w = new WaveDef { Index = index };
            for (int i = 0; i < groups.Length; i++) w.Groups.Add(groups[i]);
            Waves.Add(w);
        }

        private static void BuildBonds()
        {
            Bonds.Add(new BondTier(Faction.Ru, 2, "儒家单位攻击提升 15%"));
            Bonds.Add(new BondTier(Faction.Ru, 4, "全体友方受到伤害降低 20%，孔子光环范围翻倍"));
            Bonds.Add(new BondTier(Faction.Mo, 2, "墨家单位自带 15% 护盾"));
            Bonds.Add(new BondTier(Faction.Mo, 4, "所有单位每 5 秒生成小型护盾，墨子护盾翻倍"));
            Bonds.Add(new BondTier(Faction.Dao, 2, "道家减速效果提升"));
            Bonds.Add(new BondTier(Faction.Dao, 3, "老子、庄子有概率二次释放"));
            Bonds.Add(new BondTier(Faction.Fa, 2, "破甲效果提升 20%"));
            Bonds.Add(new BondTier(Faction.Fa, 4, "敌人生命低于 30% 时受到伤害提升 35%"));
            Bonds.Add(new BondTier(Faction.Bing, 2, "兵家单位攻速提升 20%"));
            Bonds.Add(new BondTier(Faction.Bing, 4, "全队攻击提升 25%，孙武光环可叠加"));
            Bonds.Add(new BondTier(Faction.Ming, 2, "控制时长提升 40%"));
            Bonds.Add(new BondTier(Faction.Nong, 2, "场上所有回复效果提升 50%"));
            Bonds.Add(new BondTier(Faction.Za, 2, "已激活羁绊效果额外提升 12%"));
        }

        private static void BuildPerks()
        {
            Perks.Add(new PerkDef(PerkId.BoWen, "博闻", "商店刷新费用降低 1 金币"));
            Perks.Add(new PerkDef(PerkId.GuangJi, "广记", "立即获得 15 金币"));
            Perks.Add(new PerkDef(PerkId.DuXing, "笃行", "所有单位攻击速度提升 12%"));
            Perks.Add(new PerkDef(PerkId.ShouDian, "守典", "【典】生命上限增加 2 点"));
            Perks.Add(new PerkDef(PerkId.MingCha, "明察", "所有技能暴击概率提升 10%"));
            Perks.Add(new PerkDef(PerkId.GuBen, "固本", "所有护盾效果提升 25%"));
            Perks.Add(new PerkDef(PerkId.RuFeng, "儒风浩荡", "儒家光环范围扩大，减伤额外提升 10%"));
            Perks.Add(new PerkDef(PerkId.MoShou, "墨守成规", "墨家护盾破碎时对周围敌人造成伤害"));
            Perks.Add(new PerkDef(PerkId.DaoFa, "道法自然", "道家减速附带持续灼烧"));
            Perks.Add(new PerkDef(PerkId.YanXing, "严刑峻法", "法家破甲额外提升 25%"));
            Perks.Add(new PerkDef(PerkId.BingGe, "兵戈不息", "兵家单位击杀敌人后短暂提升攻击力"));
            Perks.Add(new PerkDef(PerkId.QiaoBian, "巧辩夺理", "名家控制结束后目标进入易伤状态"));
            Perks.Add(new PerkDef(PerkId.ShunShi, "顺时农耕", "农家回血时为目标附加少量攻击力"));
            Perks.Add(new PerkDef(PerkId.JianRong, "兼容并蓄", "杂家羁绊加成翻倍"));
            Perks.Add(new PerkDef(PerkId.YiZiQianJin, "一字千金", "合成先贤时返还 4 金币"));
            Perks.Add(new PerkDef(PerkId.XinHuo, "薪火相传", "单位被摧毁时生成一个基础字（每波最多 3 次）"));
            Perks.Add(new PerkDef(PerkId.BaiJiaZhengMing, "百家争鸣", "场上同时存在 3 种流派，全队伤害提升 20%"));
            Perks.Add(new PerkDef(PerkId.SiBian, "思辨", "控制类技能冷却缩短 20%"));
            Perks.Add(new PerkDef(PerkId.HouJi, "厚积", "每存活一波，永久提升 3% 全队伤害（上限 30%）"));
        }

        private static void BuildPath()
        {
            for (int c = 0; c <= 4; c++) Path.Add(new Vector2Int(c, 3));
            Path.Add(new Vector2Int(4, 2));
            Path.Add(new Vector2Int(4, 1));
            Path.Add(new Vector2Int(4, 0));
        }

        public static UnitDef GetUnit(string name)
        {
            for (int i = 0; i < Units.Count; i++) if (Units[i].Name == name) return Units[i];
            return null;
        }

        public static EnemyDef GetEnemy(EnemyKind kind)
        {
            for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Kind == kind) return Enemies[i];
            return Enemies[0];
        }

        public static string FindComboUnit(string a, string b)
        {
            for (int i = 0; i < Combos.Count; i++)
            {
                ComboDef c = Combos[i];
                if ((c.A == a && c.B == b) || (c.A == b && c.B == a)) return c.Unit;
            }
            return null;
        }

        public static GlyphDef GetBaseGlyph(Faction f)
        {
            for (int i = 0; i < Glyphs.Count; i++)
                if (Glyphs[i].Kind == GlyphKind.Base && Glyphs[i].Faction == f) return Glyphs[i];
            return Glyphs[0];
        }

        public static GlyphDef RollGlyph(System.Random rng)
        {
            bool baseRoll = rng.NextDouble() < 0.7;
            if (baseRoll)
            {
                int i = rng.Next(BaseChars.Length);
                return GetBaseGlyph((Faction)i);
            }
            int r = rng.Next(RootChars.Length);
            for (int i = 0; i < Glyphs.Count; i++)
                if (Glyphs[i].Glyph == RootChars[r]) return Glyphs[i];
            return GetBaseGlyph(Faction.Ru);
        }

        public static bool IsBaseChar(string ch)
        {
            for (int i = 0; i < BaseChars.Length; i++) if (BaseChars[i] == ch) return true;
            return false;
        }

        public static Faction FactionOfBaseChar(string ch)
        {
            for (int i = 0; i < BaseChars.Length; i++) if (BaseChars[i] == ch) return (Faction)i;
            return Faction.Ru;
        }

        public static WaveDef GetWave(int index)
        {
            for (int i = 0; i < Waves.Count; i++) if (Waves[i].Index == index) return Waves[i];
            return Waves[Waves.Count - 1];
        }

        public static PerkDef GetPerk(PerkId id)
        {
            for (int i = 0; i < Perks.Count; i++) if (Perks[i].Id == id) return Perks[i];
            return Perks[0];
        }

        public static bool IsPathCell(int col, int row)
        {
            for (int i = 0; i < Path.Count; i++) if (Path[i].x == col && Path[i].y == row) return true;
            return false;
        }

        public static bool IsCoreCell(int col, int row)
        {
            return col == Path[Path.Count - 1].x && row == Path[Path.Count - 1].y;
        }

        public static bool IsPlaceable(int col, int row)
        {
            if (col < 0 || col >= Cols || row < 0 || row >= Rows) return false;
            return !IsPathCell(col, row) && !IsCoreCell(col, row);
        }
    }
}
