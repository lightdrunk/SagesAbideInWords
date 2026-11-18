using System.Collections.Generic;
using UnityEngine;

namespace ZhuZiShou
{
    public class Glyph
    {
        public string Char;
        public GlyphKind Kind;
        public Faction Faction;
        public string UnitName;

        public static Glyph FromDef(GlyphDef def)
        {
            return new Glyph
            {
                Char = def.Glyph,
                Kind = def.Kind,
                Faction = def.Faction,
                UnitName = def.UnitName
            };
        }
    }

    public class Cell
    {
        public int Col;
        public int Row;
        public bool IsPath;
        public bool IsCore;
        public Glyph Glyph;
        public Unit Unit;

        public bool IsEmpty { get { return Glyph == null && Unit == null; } }
        public bool Placeable { get { return !IsPath && !IsCore; } }
    }

    public class Unit
    {
        public UnitDef Def;
        public int Tier = 1;
        public Cell Cell;
        public float MaxHp;
        public float Hp;
        public float Shield;
        public float AttackTimer;
        public float HealTimer;
        public float TempAtkTimer;
        public float TempAtkAmount;
        public float AtkDebuffTimer;
        public float AtkDebuffAmount;

        public string DisplayName
        {
            get
            {
                if (Tier == 1) return Def.Name;
                if (Tier == 2) return "大成" + Def.Name;
                return "至圣" + Def.Name;
            }
        }
    }

    public class Enemy
    {
        public EnemyDef Def;
        public float MaxHp;
        public float Hp;
        public float Speed;
        public float PathPos;
        public float AttackTimer;
        public float SlowTimer;
        public float SlowFactor;
        public float StunTimer;
        public float BurnTimer;
        public float BurnDps;
        public float VulnerableTimer;
        public float ArmorBreakTimer;
        public float DebuffTimer;
        public float DebuffAmount;
        public float BossSkillTimer = 3f;

        public bool Dead { get { return Hp <= 0f; } }
        public int CellIndex
        {
            get
            {
                int idx = Mathf.FloorToInt(PathPos);
                if (idx < 0) idx = 0;
                if (idx > GameData.Path.Count - 1) idx = GameData.Path.Count - 1;
                return idx;
            }
        }
    }

    public class GameState
    {
        public int Gold;
        public int Wave = 1;
        public int CoreHp;
        public int CoreMaxHp;
        public int FreeRefreshes = 3;
        public int RevivesLeft = 1;
        public int DoubleRewardLeft = 1;
        public int WavesSurvived;
        public int XinHuoTriggers;

        public Glyph[] Shop = new Glyph[4];
        public readonly List<Glyph> Hand = new List<Glyph>();
        public readonly List<Unit> Units = new List<Unit>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly HashSet<PerkId> Perks = new HashSet<PerkId>();
        public readonly List<PerkId> PickedPerks = new List<PerkId>();
        public readonly HashSet<Faction> Match3FactionBonus = new HashSet<Faction>();

        public Cell[,] Board;
        public readonly System.Random Rng = new System.Random();

        public bool EnableMatch3 = true;

        public void Init()
        {
            Gold = GameData.StartGold;
            Wave = 1;
            CoreMaxHp = GameData.CoreHpMax;
            CoreHp = CoreMaxHp;
            FreeRefreshes = 3;
            RevivesLeft = 1;
            DoubleRewardLeft = 1;
            WavesSurvived = 0;
            Units.Clear();
            Enemies.Clear();
            Hand.Clear();
            Perks.Clear();
            PickedPerks.Clear();
            Match3FactionBonus.Clear();
            BuildBoard();
            RefreshShop(true);
        }

        private void BuildBoard()
        {
            Board = new Cell[GameData.Cols, GameData.Rows];
            for (int c = 0; c < GameData.Cols; c++)
            {
                for (int r = 0; r < GameData.Rows; r++)
                {
                    Board[c, r] = new Cell
                    {
                        Col = c,
                        Row = r,
                        IsPath = GameData.IsPathCell(c, r),
                        IsCore = GameData.IsCoreCell(c, r)
                    };
                }
            }
        }

        public Cell GetCell(int col, int row)
        {
            if (col < 0 || col >= GameData.Cols || row < 0 || row >= GameData.Rows) return null;
            return Board[col, row];
        }

        public int RefreshCost
        {
            get { return Mathf.Max(0, GameData.RefreshCostBase - (Perks.Contains(PerkId.BoWen) ? 1 : 0)); }
        }

        public bool RefreshShop(bool free)
        {
            if (free)
            {
                if (FreeRefreshes <= 0) return false;
                FreeRefreshes--;
            }
            else
            {
                if (Gold < RefreshCost) return false;
                Gold -= RefreshCost;
            }
            for (int i = 0; i < Shop.Length; i++) Shop[i] = Glyph.FromDef(GameData.RollGlyph(Rng));
            return true;
        }

        public bool Buy(int index)
        {
            if (index < 0 || index >= Shop.Length) return false;
            if (Shop[index] == null) return false;
            if (Gold < GameData.GlyphCost) return false;
            if (Hand.Count >= GameData.HandLimit) return false;
            Gold -= GameData.GlyphCost;
            Hand.Add(Shop[index]);
            Shop[index] = null;
            return true;
        }

        public bool SellHand(int index)
        {
            if (index < 0 || index >= Hand.Count) return false;
            Hand.RemoveAt(index);
            Gold += 3;
            return true;
        }

        public int SellPrice(Unit u)
        {
            if (u.Tier == 3) return 16;
            if (u.Tier == 2) return 10;
            return u.Def.IsSpecial ? 7 : 5;
        }

        public bool SellUnit(Cell cell)
        {
            if (cell == null || cell.Unit == null) return false;
            Gold += SellPrice(cell.Unit);
            Units.Remove(cell.Unit);
            cell.Unit = null;
            return true;
        }

        public string PlaceGlyph(int handIndex, Cell target)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return "无效的字";
            if (target == null || !target.Placeable) return "该格不可放置";
            if (target.Unit != null) return "该格已有先贤";

            Glyph g = Hand[handIndex];

            if (target.Glyph != null)
            {
                Glyph other = target.Glyph;
                if (other.Char == g.Char && g.Kind == GlyphKind.Base && other.Kind == GlyphKind.Base)
                {
                    Hand.RemoveAt(handIndex);
                    target.Glyph = null;
                    CreateUnit(g.UnitName, target);
                    return "合成 " + g.UnitName;
                }
                string combo = GameData.FindComboUnit(other.Char, g.Char);
                if (combo != null)
                {
                    Hand.RemoveAt(handIndex);
                    target.Glyph = null;
                    CreateUnit(combo, target);
                    return "拼字合成 " + combo;
                }
                return "两个字无法合成";
            }

            target.Glyph = g;
            Hand.RemoveAt(handIndex);
            return "放入 " + g.Char;
        }

        public bool TryMergeUnits(Cell from, Cell to)
        {
            if (from == null || to == null || from == to) return false;
            if (from.Unit == null || to.Unit == null) return false;
            if (from.Unit.Def.Name != to.Unit.Def.Name) return false;
            if (from.Unit.Tier != to.Unit.Tier) return false;
            if (to.Unit.Tier >= 3) return false;

            Unit keep = to.Unit;
            float ratio = keep.MaxHp > 0 ? keep.Hp / keep.MaxHp : 1f;
            keep.Tier++;
            keep.MaxHp = UnitMaxHp(keep.Def, keep.Tier);
            keep.Hp = Mathf.Max(1f, keep.MaxHp * ratio);
            Units.Remove(from.Unit);
            from.Unit = null;
            if (Perks.Contains(PerkId.YiZiQianJin)) Gold += 4;
            return true;
        }

        public void CreateUnit(string name, Cell cell)
        {
            UnitDef def = GameData.GetUnit(name);
            if (def == null) return;
            Unit u = new Unit
            {
                Def = def,
                Tier = 1,
                Cell = cell,
                MaxHp = UnitMaxHp(def, 1),
                Hp = UnitMaxHp(def, 1),
                AttackTimer = 0.2f,
                HealTimer = 0f
            };
            cell.Unit = u;
            Units.Add(u);
            ApplySpawnShield(u);
        }

        private void ApplySpawnShield(Unit u)
        {
            float shield = 0f;
            if (u.Def.Name == "墨徒") shield = 18f;
            else if (u.Def.Name == "墨子") shield = 40f;
            if (shield > 0f) u.Shield += shield * ShieldMul(u);

            if (u.Def.Faction == Faction.Mo && HasBond(Faction.Mo, 2))
                u.Shield += u.MaxHp * 0.15f * ShieldMul(u);
        }

        public float UnitMaxHp(UnitDef def, int tier)
        {
            float mul = tier >= 3 ? GameData.Tier3Mul : (tier == 2 ? GameData.Tier2Mul : 1f);
            return def.Hp * mul;
        }

        public int BondCount(Faction f)
        {
            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < Units.Count; i++)
                if (Units[i].Def.Faction == f) names.Add(Units[i].Def.Name);
            return names.Count;
        }

        public bool HasBond(Faction f, int tier)
        {
            return BondCount(f) >= tier;
        }

        public List<BondTier> ActiveBonds()
        {
            List<BondTier> result = new List<BondTier>();
            for (int i = 0; i < GameData.Bonds.Count; i++)
            {
                BondTier b = GameData.Bonds[i];
                if (HasBond(b.Faction, b.Count)) result.Add(b);
            }
            return result;
        }

        public int CountUnit(string name)
        {
            int n = 0;
            for (int i = 0; i < Units.Count; i++) if (Units[i].Def.Name == name) n++;
            return n;
        }

        public int ActiveFactionCount()
        {
            HashSet<Faction> set = new HashSet<Faction>();
            for (int i = 0; i < Units.Count; i++) set.Add(Units[i].Def.Faction);
            return set.Count;
        }

        // 杂家 / 吕不韦 / 兼容并蓄对羁绊数值的整体放大
        public float BondBoost
        {
            get
            {
                float boost = 1f;
                if (HasBond(Faction.Za, 2)) boost += Perks.Contains(PerkId.JianRong) ? 0.24f : 0.12f;
                boost += 0.05f * CountUnit("杂家");
                if (CountUnit("吕不韦") > 0) boost += 0.10f;
                return boost;
            }
        }

        public float AttackMul(Unit u)
        {
            float m = 1f;
            if (u.Def.Faction == Faction.Ru && HasBond(Faction.Ru, 2)) m *= 1f + 0.15f * BondBoost;
            if (HasBond(Faction.Bing, 4)) m *= 1f + 0.25f * BondBoost;
            int sun = CountUnit("孙武");
            if (sun > 0) m *= 1f + 0.15f * sun;
            if (Perks.Contains(PerkId.HouJi)) m *= 1f + 0.03f * Mathf.Min(10, WavesSurvived);
            if (Perks.Contains(PerkId.BaiJiaZhengMing) && ActiveFactionCount() >= 3) m *= 1.2f;
            if (Match3FactionBonus.Contains(u.Def.Faction)) m *= 1.08f;
            if (u.TempAtkTimer > 0f) m *= 1f + u.TempAtkAmount;
            return m;
        }

        public float AttackSpeedMul(Unit u)
        {
            float m = 1f;
            if (Perks.Contains(PerkId.DuXing)) m *= 1.12f;
            if (u.Def.Faction == Faction.Bing && HasBond(Faction.Bing, 2)) m *= 1f + 0.20f * BondBoost;
            if (u.AtkDebuffTimer > 0f) m *= 1f - u.AtkDebuffAmount;
            return m;
        }

        public float DamageTakenMul(Unit u)
        {
            float m = 1f;
            if (HasBond(Faction.Ru, 4)) m *= 0.8f;
            if (Perks.Contains(PerkId.RuFeng)) m *= 0.9f;
            return m;
        }

        public float ShieldMul(Unit u)
        {
            return Perks.Contains(PerkId.GuBen) ? 1.25f : 1f;
        }

        public float HealingMul
        {
            get { return HasBond(Faction.Nong, 2) ? 1.5f * BondBoost : 1f; }
        }

        public float IncomingDamageMul(Enemy e)
        {
            float m = 1f;
            if (HasBond(Faction.Fa, 4) && e.MaxHp > 0f && e.Hp / e.MaxHp < 0.3f) m *= 1.35f;
            if (e.ArmorBreakTimer > 0f)
            {
                float ab = 0.10f;
                if (HasBond(Faction.Fa, 2)) ab *= 1.2f * BondBoost;
                if (Perks.Contains(PerkId.YanXing)) ab *= 1.25f;
                m *= 1f + ab;
            }
            if (e.VulnerableTimer > 0f) m *= 1.15f;
            return m;
        }

        public float ControlDurationMul
        {
            get
            {
                float m = 1f;
                if (HasBond(Faction.Ming, 2)) m *= 1.4f * BondBoost;
                if (Perks.Contains(PerkId.SiBian)) m *= 1.2f;
                return m;
            }
        }

        public float SlowMul
        {
            get { return HasBond(Faction.Dao, 2) ? 1.4f * BondBoost : 1f; }
        }

        public float CritChance
        {
            get { return (Perks.Contains(PerkId.MingCha) ? 0.1f : 0f); }
        }

        public bool HasTriple(out string ch, out Faction faction)
        {
            ch = null;
            faction = Faction.Ru;
            for (int i = 0; i < GameData.BaseChars.Length; i++)
            {
                string c = GameData.BaseChars[i];
                int n = 0;
                for (int j = 0; j < Hand.Count; j++)
                    if (Hand[j].Kind == GlyphKind.Base && Hand[j].Char == c) n++;
                if (n >= 3) { ch = c; faction = (Faction)i; return true; }
            }
            return false;
        }

        public void ConsumeTriple(string ch, int count)
        {
            int removed = 0;
            for (int i = Hand.Count - 1; i >= 0 && removed < count; i--)
            {
                if (Hand[i].Kind == GlyphKind.Base && Hand[i].Char == ch)
                {
                    Hand.RemoveAt(i);
                    removed++;
                }
            }
        }

        public Cell FirstEmptyCell()
        {
            for (int r = 0; r < GameData.Rows; r++)
                for (int c = 0; c < GameData.Cols; c++)
                    if (Board[c, r].Placeable && Board[c, r].IsEmpty) return Board[c, r];
            return null;
        }
    }
}
