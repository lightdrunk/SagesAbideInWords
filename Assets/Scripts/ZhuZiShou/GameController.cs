using System.Collections.Generic;
using UnityEngine;

namespace ZhuZiShou
{
    public class GameController : MonoBehaviour
    {
        public GameState S { get; private set; }
        public Phase CurrentPhase { get; private set; }
        public int SelectedHand = -1;
        public Cell SelectedCell;

        private GameUI ui;
        private readonly List<EnemyDef> spawnList = new List<EnemyDef>();
        private readonly List<float> spawnDelays = new List<float>();
        private int spawnIndex;
        private float spawnTimer;
        private float uiTimer;
        private float diagTimer = 1f;
        private float waveHpMul = 1f;
        private float waveSpeedBonus;
        private readonly List<PerkId> perkChoices = new List<PerkId>();

        public bool PerkPending { get { return perkChoices.Count > 0; } }

        private void Awake()
        {
            S = new GameState();
            S.Init();
            ui = gameObject.AddComponent<GameUI>();
            ui.Build(this);
            CurrentPhase = Phase.Prepare;
            // StartCoroutine(AutoTest()); // 调试用自动测试，正常玩时注释掉
        }

        private System.Collections.IEnumerator AutoTest()
        {
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log("[ZZS-TEST] === 自动测试开始 ===");

            // 步骤 1: 用免费刷新和金币刷新，尽量找到 2 个以上基础字的组合
            int refreshTry = 0;
            while (S.Shop.Length >= 4 && refreshTry < 10)
            {
                // 尝试直接买商店所有 4 个字
                for (int i = 0; i < 4; i++)
                {
                    if (S.Gold >= GameData.GlyphCost)
                    {
                        OnShopClick(i);
                        yield return new WaitForSecondsRealtime(0.15f);
                    }
                }
                if (S.Hand.Count >= 2) break;
                refreshTry++;
                if (S.FreeRefreshes > 0) OnFreeRefresh();
                else if (S.Gold >= GameData.RefreshCostBase) OnRefresh();
                else break;
                yield return new WaitForSecondsRealtime(0.15f);
            }

            Debug.Log("[ZZS-TEST] 买入后手牌数: " + S.Hand.Count + " 金币: " + S.Gold);

            // 步骤 2: 合成单位 —— 先找所有可放置格，倒序遍历手牌
            // 每选一个字，优先找已有同字的格子放上去触发合成；没有就放空格暂存
            var placeableCells = new List<Cell>();
            for (int col = 0; col < GameData.Cols; col++)
                for (int row = 0; row < GameData.Rows; row++)
                {
                    if (GameData.IsPathCell(col, row) || GameData.IsCoreCell(col, row)) continue;
                    placeableCells.Add(S.GetCell(col, row));
                }

            // 倒序遍历避免 RemoveAt 影响索引
            for (int h = S.Hand.Count - 1; h >= 0; h--)
            {
                Glyph g = S.Hand[h];
                // 优先：找已有同基础字的格子 → 同字合成
                Cell target = null;
                foreach (var c in placeableCells)
                {
                    if (c.Glyph != null && c.Unit == null
                        && c.Glyph.Char == g.Char
                        && g.Kind == GlyphKind.Base && c.Glyph.Kind == GlyphKind.Base)
                    {
                        target = c; break;
                    }
                }
                // 次优：找可拼字组合的格子
                if (target == null)
                {
                    foreach (var c in placeableCells)
                    {
                        if (c.Glyph != null && c.Unit == null)
                        {
                            string combo = GameData.FindComboUnit(c.Glyph.Char, g.Char);
                            if (combo != null) { target = c; break; }
                        }
                    }
                }
                // 兜底：找空格暂存
                if (target == null)
                {
                    foreach (var c in placeableCells)
                        if (c.Glyph == null && c.Unit == null) { target = c; break; }
                }
                if (target == null) continue;
                Debug.Log("[ZZS-TEST] 放字 h=" + h + " char=" + g.Char + " → cell(" + target.Col + "," + target.Row + ") existingGlyph=" + (target.Glyph != null ? target.Glyph.Char : "none"));
                SelectedHand = h;
                OnCellClick(target.Col, target.Row);
                yield return new WaitForSecondsRealtime(0.12f);
            }

            Debug.Log("[ZZS-TEST] 放置后单位数: " + S.Units.Count + " 棋盘有字格: " + CountBoardGlyphs());
            for (int i = 0; i < S.Units.Count; i++)
                Debug.Log("[ZZS-TEST] 单位[" + i + "]: name=" + S.Units[i].Def.Name + " tier=" + S.Units[i].Tier + " cell=" + (S.Units[i].Cell != null ? S.Units[i].Cell.Col + "," + S.Units[i].Cell.Row : "null"));

            // 步骤 3: 开始第 1 波
            Debug.Log("[ZZS-TEST] 开始第 1 波");
            OnStartWave();
            yield return new WaitForSecondsRealtime(0.1f);
            Debug.Log("[ZZS-TEST] phase after start=" + CurrentPhase + " enemies=" + S.Enemies.Count);

            // 步骤 4: 等待战斗结束（最多 40 秒）
            float waitLimit = 40f;
            float waited = 0f;
            while (CurrentPhase == Phase.Combat && waited < waitLimit)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                waited += 0.5f;
                Debug.Log("[ZZS-TEST] 等待结束... phase=" + CurrentPhase + " enemies=" + S.Enemies.Count + " coreHp=" + S.CoreHp);
            }

            Debug.Log("[ZZS-TEST] === 第 1 波结束 ===");
            Debug.Log("[ZZS-TEST] phase=" + CurrentPhase + " wave=" + S.Wave + " gold=" + S.Gold + " coreHp=" + S.CoreHp);
            Debug.Log("[ZZS-TEST] unitCount=" + S.Units.Count + " perkPending=" + PerkPending);

            // 如果还有下一波，继续打完（每波自动放置一些字）
            for (int wave = 2; wave <= 10 && CurrentPhase != Phase.Victory && CurrentPhase != Phase.GameOver; wave++)
            {
                // 等词条选择
                if (PerkPending)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    Debug.Log("[ZZS-TEST] 跳过词条");
                    // 选第一个词条
                    if (perkChoices.Count > 0)
                    {
                        S.Perks.Add(perkChoices[0]);
                        perkChoices.Clear();
                        ui.HideAllOverlays();
                        ui.RefreshAll();
                    }
                }

                // 补点字到商店
                for (int i = 0; i < 4 && S.Gold >= GameData.GlyphCost; i++)
                {
                    if (S.Hand.Count < GameData.HandLimit) OnShopClick(i);
                }
                yield return new WaitForSecondsRealtime(0.2f);

                // 放到棋盘空格
                for (int h = 0; h < S.Hand.Count; h++)
                {
                    Cell target = null;
                    for (int col = 0; col < GameData.Cols && target == null; col++)
                        for (int row = 0; row < GameData.Rows && target == null; row++)
                        {
                            if (GameData.IsPathCell(col, row) || GameData.IsCoreCell(col, row)) continue;
                            Cell cell = S.GetCell(col, row);
                            if (cell != null && cell.Placeable && cell.Unit == null) { target = cell; break; }
                        }
                    if (target == null) break;
                    SelectedHand = h;
                    OnCellClick(target.Col, target.Row);
                    yield return new WaitForSecondsRealtime(0.08f);
                }

                OnStartWave();
                yield return new WaitForSecondsRealtime(0.1f);

                float w = 0f;
                while (CurrentPhase == Phase.Combat && w < 40f)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    w += 0.5f;
                }

                Debug.Log("[ZZS-TEST] 第 " + wave + " 波结束 phase=" + CurrentPhase + " gold=" + S.Gold + " coreHp=" + S.CoreHp + " units=" + S.Units.Count);
            }

            Debug.Log("[ZZS-TEST] === 全部结束 ===");
            Debug.Log("[ZZS-TEST] 最终 phase=" + CurrentPhase + " wave=" + S.Wave + " gold=" + S.Gold + " coreHp=" + S.CoreHp + " units=" + S.Units.Count);
        }

        private int CountBoardGlyphs()
        {
            int n = 0;
            for (int col = 0; col < GameData.Cols; col++)
                for (int row = 0; row < GameData.Rows; row++)
                {
                    Cell c = S.GetCell(col, row);
                    if (c != null && c.Glyph != null) n++;
                }
            return n;
        }

        private float diagEnemyLog = 0f;

        private void Update()
        {
            float dt = Time.deltaTime;
            diagTimer -= dt;
            if (diagTimer <= 0f)
            {
                diagTimer = 1f;
                Debug.Log("[ZZS-DIAG] S_null=" + (S == null)
                    + " units=" + (S == null ? -1 : S.Units.Count)
                    + " nullUnits=" + (S == null ? -1 : CountNullUnits())
                    + " phase=" + CurrentPhase + " ui=" + (ui == null));
            }

            // 战斗中每秒打印一次敌人 vs 单位阻挡诊断
            diagEnemyLog -= dt;
            if (diagEnemyLog <= 0f && CurrentPhase == Phase.Combat && S != null && S.Enemies.Count > 0)
            {
                diagEnemyLog = 1f;
                Debug.Log("[ZZS-ENEMY] === 敌数=" + S.Enemies.Count + " 单位数=" + S.Units.Count + " ===");
                // 打印每个格子的状态：通路/P/Glyph/Unit
                string board = "";
                for (int r = 0; r < GameData.Rows; r++)
                {
                    for (int c = 0; c < GameData.Cols; c++)
                    {
                        Cell cc = S.GetCell(c, r);
                        if (cc == null) { board += "? "; continue; }
                        if (cc.IsCore) board += "■ ";
                        else if (cc.IsPath) board += "· ";
                        else if (cc.Unit != null) board += "U[" + cc.Unit.Def.Name + "] ";
                        else if (cc.Glyph != null) board += "G(" + cc.Glyph.Char + ") ";
                        else board += ". ";
                    }
                    board += "\n";
                }
                Debug.Log("[ZZS-BOARD]\n" + board);

                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    Enemy e = S.Enemies[i];
                    Vector2Int pc = GameData.Path[e.CellIndex];
                    string blockers = "";
                    for (int j = 0; j < S.Units.Count; j++)
                    {
                        Unit u = S.Units[j];
                        if (u == null) { blockers += " null! "; continue; }
                        int d = Mathf.Min(
                            Mathf.Abs(u.Cell.Col - pc.x) + Mathf.Abs(u.Cell.Row - pc.y),
                            Mathf.Abs(u.Cell.Col - GameData.Path[Mathf.Clamp(Mathf.FloorToInt(e.PathPos)+1,0,GameData.Path.Count-1)].x) +
                            Mathf.Abs(u.Cell.Row - GameData.Path[Mathf.Clamp(Mathf.FloorToInt(e.PathPos)+1,0,GameData.Path.Count-1)].y));
                        blockers += u.Def.Name + "@(" + u.Cell.Col + "," + u.Cell.Row + ")d=" + d + " ";
                    }
                    Debug.Log("[ZZS-ENEMY] e[" + i + "] pos=" + e.PathPos.ToString("F1") + " cell=(" + pc.x + "," + pc.y + ") blockers=[" + blockers + "]");
                }
            }

            if (CurrentPhase == Phase.Combat) UpdateCombat(dt);
            UpdateUnitTimers(dt);
            uiTimer -= dt;
            if (uiTimer <= 0f)
            {
                uiTimer = 0.1f;
                ui.RefreshAll();
            }
            ui.UpdateEnemies(S.Enemies, CurrentPhase == Phase.Combat);
        }

        private int CountNullUnits()
        {
            int n = 0;
            for (int i = 0; i < S.Units.Count; i++) if (S.Units[i] == null) n++;
            return n;
        }

        private void UpdateUnitTimers(float dt)
        {
            for (int i = 0; i < S.Units.Count; i++)
            {
                Unit u = S.Units[i];
                if (u == null) continue;
                if (u.AtkDebuffTimer > 0f) u.AtkDebuffTimer -= dt;
                if (u.TempAtkTimer > 0f) u.TempAtkTimer -= dt;
            }
            // 顺便清理掉 null 项，避免后续循环每次都跳过
            S.Units.RemoveAll(x => x == null);
        }

        // ---------------- 玩家操作 ----------------

        public void OnShopClick(int index)
        {
            if (PerkPending) return;
            string ch = ShopChar(index);
            if (S.Buy(index)) ui.Toast("买入 " + ch);
            else if (S.Gold < GameData.GlyphCost) ui.Toast("金币不足");
            else if (S.Hand.Count >= GameData.HandLimit) ui.Toast("待选区已满");

            CheckMatch3();
            ui.RefreshAll();
        }

        private string ShopChar(int index)
        {
            if (index < 0 || index >= S.Shop.Length || S.Shop[index] == null) return "";
            return S.Shop[index].Char;
        }

        public void OnRefresh()
        {
            if (PerkPending) return;
            if (S.RefreshShop(false)) ui.Toast("刷新商店");
            else ui.Toast("金币不足");
            ui.RefreshAll();
        }

        public void OnFreeRefresh()
        {
            if (PerkPending) return;
            if (S.RefreshShop(true)) ui.Toast("广告刷新（剩 " + S.FreeRefreshes + " 次）");
            else ui.Toast("免费刷新次数已用完");
            ui.RefreshAll();
        }

        public void OnHandClick(int index)
        {
            if (PerkPending) return;
            if (index < 0 || index >= S.Hand.Count) return;
            SelectedHand = index;
            SelectedCell = null;
            ui.HideUnitInfo();
            ui.RefreshAll();
        }

        // 拖拽：手牌直接放格子
        public void PlaceGlyphFromHand(int handIndex, int col, int row)
        {
            if (PerkPending) return;
            if (handIndex < 0 || handIndex >= S.Hand.Count) return;
            Cell cell = S.GetCell(col, row);
            if (cell == null || !GameData.IsPlaceable(cell.Col, cell.Row)) { ui.Toast("不能放这里"); return; }
            string msg = S.PlaceGlyph(handIndex, cell);
            ui.Toast(msg);
            if (SelectedHand == handIndex) SelectedHand = -1;
            SelectedCell = null;
            ui.RefreshAll();
        }

        // 拖拽：商店字直接放格子（先买再放）
        public void BuyAndPlace(int shopIndex, int col, int row)
        {
            if (PerkPending) return;
            Cell cell = S.GetCell(col, row);
            if (cell == null || !GameData.IsPlaceable(cell.Col, cell.Row)) { ui.Toast("不能放这里"); return; }
            if (S.Gold < GameData.GlyphCost) { ui.Toast("金币不足"); return; }
            if (S.Hand.Count >= GameData.HandLimit) { ui.Toast("待选区已满"); return; }

            string ch = ShopChar(shopIndex);
            if (!S.Buy(shopIndex)) { ui.Toast("买入失败"); return; }
            // Buy 把新字加到 Hand 末尾，直接 PlaceGlyph 用最后一个 index
            int lastHand = S.Hand.Count - 1;
            string msg = S.PlaceGlyph(lastHand, cell);
            ui.Toast("拖放：" + ch + " → " + msg);
            if (SelectedHand == lastHand) SelectedHand = -1;
            SelectedCell = null;
            CheckMatch3();
            ui.RefreshAll();
        }

        public void OnCellClick(int col, int row)
        {
            if (PerkPending) return;
            Cell cell = S.GetCell(col, row);

            if (cell.Unit != null)
            {
                if (SelectedCell != null && SelectedCell.Unit != null && SelectedCell != cell)
                {
                    if (S.TryMergeUnits(SelectedCell, cell))
                    {
                        ui.Toast("升阶：" + cell.Unit.DisplayName);
                        SelectedCell = null;
                        ui.HideUnitInfo();
                        ui.RefreshAll();
                        return;
                    }
                }
                SelectedCell = cell;
                SelectedHand = -1;
                ui.ShowUnitInfo(cell);
                ui.RefreshAll();
                return;
            }

            if (SelectedHand >= 0)
            {
                string msg = S.PlaceGlyph(SelectedHand, cell);
                ui.Toast(msg);
                SelectedHand = -1;
                SelectedCell = null;
                ui.RefreshAll();
                return;
            }

            if (SelectedCell != null && SelectedCell.Glyph != null)
            {
                if (cell == SelectedCell)
                {
                    SelectedCell = null;
                    ui.RefreshAll();
                    return;
                }
                if (!cell.Placeable)
                {
                    ui.Toast("该格不可放置");
                    return;
                }
                Glyph moving = SelectedCell.Glyph;
                if (cell.Glyph != null)
                {
                    if (cell.Glyph.Char == moving.Char && moving.Kind == GlyphKind.Base)
                    {
                        SelectedCell.Glyph = null;
                        cell.Glyph = null;
                        S.CreateUnit(moving.UnitName, cell);
                        ui.Toast("合成 " + moving.UnitName);
                    }
                    else
                    {
                        string combo = GameData.FindComboUnit(cell.Glyph.Char, moving.Char);
                        if (combo != null)
                        {
                            SelectedCell.Glyph = null;
                            cell.Glyph = null;
                            S.CreateUnit(combo, cell);
                            ui.Toast("拼字合成 " + combo);
                        }
                        else ui.Toast("两个字无法合成");
                    }
                }
                else if (cell.Unit == null)
                {
                    cell.Glyph = moving;
                    SelectedCell.Glyph = null;
                }
                SelectedCell = null;
                ui.RefreshAll();
                return;
            }

            if (cell.Glyph != null)
            {
                SelectedCell = cell;
                SelectedHand = -1;
                ui.RefreshAll();
                return;
            }

            SelectedCell = null;
            ui.RefreshAll();
        }

        public void OnSellSelected()
        {
            if (SelectedCell != null && SelectedCell.Unit != null)
            {
                S.SellUnit(SelectedCell);
                SelectedCell = null;
                ui.HideUnitInfo();
                ui.Toast("出售先贤");
            }
            else if (SelectedHand >= 0 && SelectedHand < S.Hand.Count)
            {
                Glyph g = S.Hand[SelectedHand];
                S.SellHand(SelectedHand);
                SelectedHand = -1;
                ui.HideUnitInfo();
                ui.Toast("出售 " + g.Char);
            }
            ui.RefreshAll();
        }

        public void OnCloseInfo()
        {
            ui.HideUnitInfo();
            SelectedCell = null;
            SelectedHand = -1;
            ui.RefreshAll();
        }

        public void OnStartWave()
        {
            if (PerkPending) return;
            if (CurrentPhase != Phase.Prepare) return;
            StartWave();
        }

        public void OnRestart()
        {
            Debug.Log("[ZZS-DIAG] OnRestart");
            S.Init();
            CurrentPhase = Phase.Prepare;
            perkChoices.Clear();
            spawnList.Clear();
            spawnDelays.Clear();
            spawnIndex = 0;
            SelectedHand = -1;
            SelectedCell = null;
            ui.HideAllOverlays();
            ui.RefreshAll();
            ui.Toast("新的一局");
        }

        public void OnDoubleReward()
        {
            if (S.DoubleRewardLeft <= 0) return;
            S.DoubleRewardLeft--;
            S.Gold += GameData.WaveGoldReward;
            ui.Toast("广告奖励：金币翻倍 +" + GameData.WaveGoldReward);
            ui.RefreshAll();
        }

        public void OnRevive()
        {
            if (CurrentPhase != Phase.GameOver || S.RevivesLeft <= 0) return;
            S.RevivesLeft--;
            S.CoreHp = S.CoreMaxHp;
            S.Enemies.Clear();
            spawnList.Clear();
            spawnDelays.Clear();
            spawnIndex = 0;
            CurrentPhase = Phase.Prepare;
            ui.HideAllOverlays();
            ui.Toast("广告复活，场上单位保留");
            ui.RefreshAll();
        }

        // ---------------- 三消 ----------------

        private void CheckMatch3()
        {
            if (!S.EnableMatch3) return;
            string ch;
            Faction f;
            if (S.HasTriple(out ch, out f))
            {
                ui.ShowMatch3(ch, f);
            }
        }

        public void OnMatch3Chosen(int choice)
        {
            string ch;
            Faction f;
            if (!S.HasTriple(out ch, out f))
            {
                ui.HideMatch3();
                return;
            }
            S.ConsumeTriple(ch, 3);
            if (choice == 0)
            {
                S.Gold += 12;
                ui.Toast("三消奖励：+12 金币");
            }
            else if (choice == 1)
            {
                Cell cell = S.FirstEmptyCell();
                if (cell != null)
                {
                    S.CreateUnit(GameData.BaseUnitNames[(int)f], cell);
                    ui.Toast("三消奖励：" + GameData.BaseUnitNames[(int)f]);
                }
                else
                {
                    S.Gold += 12;
                    ui.Toast("棋盘已满，奖励转为 +12 金币");
                }
            }
            else
            {
                S.Match3FactionBonus.Add(f);
                ui.Toast("三消奖励：" + FactionInfo.Name(f) + " 伤害 +8%");
            }
            ui.HideMatch3();
            ui.RefreshAll();
        }

        // ---------------- 词条 ----------------

        private void OfferPerks()
        {
            perkChoices.Clear();
            List<PerkDef> pool = new List<PerkDef>();
            for (int i = 0; i < GameData.Perks.Count; i++)
                if (!S.Perks.Contains(GameData.Perks[i].Id)) pool.Add(GameData.Perks[i]);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int idx = S.Rng.Next(pool.Count);
                perkChoices.Add(pool[idx].Id);
                pool.RemoveAt(idx);
            }
            if (perkChoices.Count > 0) ui.ShowPerkChoices(perkChoices);
        }

        public void OnPerkChosen(int index)
        {
            if (index < 0 || index >= perkChoices.Count) return;
            PerkId id = perkChoices[index];
            S.Perks.Add(id);
            S.PickedPerks.Add(id);
            ApplyPerk(id);
            perkChoices.Clear();
            ui.HidePerkPanel();
            CurrentPhase = Phase.Prepare;
            ui.Toast("获得词条：" + GameData.GetPerk(id).Name);
            ui.RefreshAll();
        }

        private void ApplyPerk(PerkId id)
        {
            if (id == PerkId.GuangJi) S.Gold += 15;
            else if (id == PerkId.ShouDian) { S.CoreMaxHp += 2; S.CoreHp += 2; }
        }

        // ---------------- 战斗 ----------------

        private void StartWave()
        {
            Debug.Log("[ZZS-DIAG] StartWave " + S.Wave);
            CurrentPhase = Phase.Combat;
            waveHpMul = Mathf.Pow(1.08f, S.Wave - 1);
            waveSpeedBonus = 0.05f * Mathf.Floor((S.Wave - 1) / 3f);
            spawnList.Clear();
            spawnDelays.Clear();
            spawnIndex = 0;
            spawnTimer = 0.5f;
            S.XinHuoTriggers = 0;

            WaveDef wave = GameData.GetWave(S.Wave);
            for (int g = 0; g < wave.Groups.Count; g++)
            {
                SpawnGroup grp = wave.Groups[g];
                for (int i = 0; i < grp.Count; i++)
                {
                    spawnList.Add(GameData.GetEnemy(grp.Kind));
                    spawnDelays.Add(grp.Interval);
                }
            }
            ui.Toast("第 " + S.Wave + " 波 迎敌");
            ui.RefreshAll();
        }

        private void UpdateCombat(float dt)
        {
            SpawnEnemies(dt);
            UpdateUnits(dt);
            UpdateEnemies(dt);

            if (spawnIndex >= spawnList.Count && S.Enemies.Count == 0)
                EndWave();
        }

        private void SpawnEnemies(float dt)
        {
            if (spawnIndex >= spawnList.Count) return;
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            EnemyDef def = spawnList[spawnIndex];
            spawnTimer = spawnDelays[spawnIndex];
            spawnIndex++;

            Enemy e = new Enemy
            {
                Def = def,
                MaxHp = def.Hp * waveHpMul,
                Hp = def.Hp * waveHpMul,
                Speed = Mathf.Min(1.8f, def.Speed + waveSpeedBonus),
                PathPos = 0f,
                AttackTimer = 0.3f
            };
            S.Enemies.Add(e);
        }

        private void UpdateUnits(float dt)
        {
            // 倒序 + null guard，防止遍历时 DestroyUnit 导致索引错位或空引用
            for (int i = S.Units.Count - 1; i >= 0; i--)
            {
                Unit u = S.Units[i];
                if (u == null) { S.Units.RemoveAt(i); continue; }
                u.AttackTimer -= dt;

                string name = u.Def.Name;
                if (name == "农者" || name == "许行")
                {
                    u.HealTimer -= dt;
                    if (u.HealTimer <= 0f)
                    {
                        u.HealTimer = u.Def.SkillCd;
                        DoHeal(u);
                    }
                }

                if (u.AttackTimer <= 0f)
                {
                    Enemy target = FindTarget(u);
                    if (target != null)
                    {
                        float interval = u.Def.Interval / Mathf.Max(0.1f, S.AttackSpeedMul(u));
                        u.AttackTimer = interval;
                        AttackEnemy(u, target);
                    }
                    else
                    {
                        // 每秒最多打一次，避免刷屏
                        float interval = u.Def.Interval / Mathf.Max(0.1f, S.AttackSpeedMul(u));
                        u.AttackTimer = Mathf.Min(0.3f, interval);
                    }
                }
            }
        }

        private Enemy FindTarget(Unit u)
        {
            if (u == null || u.Cell == null) return null;
            Enemy best = null;
            float bestProgress = -1f;

            int range = u.Def.Range;
            if (u.Def.Name == "孔子" && S.HasBond(Faction.Ru, 4)) range *= 2;

            for (int i = 0; i < S.Enemies.Count; i++)
            {
                Enemy e = S.Enemies[i];
                if (e.Dead) continue;

                // 同时检查敌人当前格和下一格，消除帧边界误差
                int idx = Mathf.FloorToInt(e.PathPos);
                Vector2Int pc = GameData.Path[Mathf.Clamp(idx, 0, GameData.Path.Count - 1)];
                Vector2Int pn = GameData.Path[Mathf.Clamp(idx + 1, 0, GameData.Path.Count - 1)];
                int d = Mathf.Min(
                    Mathf.Abs(pc.x - u.Cell.Col) + Mathf.Abs(pc.y - u.Cell.Row),
                    Mathf.Abs(pn.x - u.Cell.Col) + Mathf.Abs(pn.y - u.Cell.Row));

                if (d <= range && e.PathPos > bestProgress)
                {
                    bestProgress = e.PathPos;
                    best = e;
                }
            }
            return best;
        }

        private void AttackEnemy(Unit u, Enemy e)
        {
            float dmg = u.Def.Atk * TierMul(u) * S.AttackMul(u);
            string n = u.Def.Name;

            if (n == "商鞅" && e.MaxHp > 0f)
                dmg *= 1f + 0.4f * (1f - e.Hp / e.MaxHp);

            bool crit = Random.value < (S.CritChance + (n == "韩非" ? 0.2f : 0f));
            if (crit) dmg *= 1.5f;

            if (n == "吴起" && e.MaxHp > 0f && e.Hp / e.MaxHp < 0.18f)
                dmg = e.Hp + 1f;

            DealDamageToEnemy(e, dmg, u);

            if (n == "道者" || n == "老子")
            {
                e.SlowTimer = 1.5f;
                e.SlowFactor = Mathf.Clamp(0.25f * S.SlowMul, 0f, 0.8f);
                if (n == "老子" || S.Perks.Contains(PerkId.DaoFa))
                {
                    e.BurnTimer = 3f;
                    e.BurnDps = n == "老子" ? 6f : 4f;
                }
            }
            else if (n == "法吏" || n == "韩非")
            {
                e.ArmorBreakTimer = 3f;
            }
            else if (n == "辩士")
            {
                e.StunTimer = Mathf.Max(e.StunTimer, 1.2f * S.ControlDurationMul);
                ApplyQiaoBian(e);
            }
            else if (n == "惠施")
            {
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    Enemy other = S.Enemies[i];
                    if (other.Dead) continue;
                    Vector2Int pc = GameData.Path[other.CellIndex];
                    int d = Mathf.Abs(pc.x - u.Cell.Col) + Mathf.Abs(pc.y - u.Cell.Row);
                    if (d <= u.Def.Range)
                    {
                        other.StunTimer = Mathf.Max(other.StunTimer, 1.5f * S.ControlDurationMul);
                        ApplyQiaoBian(other);
                    }
                }
            }
            else if (n == "庄子" && Random.value < 0.25f)
            {
                for (int i = 0; i < S.Enemies.Count; i++)
                {
                    Enemy other = S.Enemies[i];
                    if (other.Dead || other == e) continue;
                    Vector2Int pc = GameData.Path[other.CellIndex];
                    int d = Mathf.Abs(pc.x - u.Cell.Col) + Mathf.Abs(pc.y - u.Cell.Row);
                    if (d <= u.Def.Range) DealDamageToEnemy(other, dmg * 0.5f, u);
                }
            }

            if (e.Hp <= 0f) KillEnemy(e, u);
        }

        private void ApplyQiaoBian(Enemy e)
        {
            if (S.Perks.Contains(PerkId.QiaoBian) && e.StunTimer > 0f)
                e.VulnerableTimer = Mathf.Max(e.VulnerableTimer, 2f);
        }

        private void DealDamageToEnemy(Enemy e, float dmg, Unit source)
        {
            float final = dmg * S.IncomingDamageMul(e);
            e.Hp -= final;
        }

        private void KillEnemy(Enemy e, Unit killer)
        {
            if (!e.Dead) return;
            S.Enemies.Remove(e);
            S.Gold += e.Def.KillGold;

            if (killer != null && S.Perks.Contains(PerkId.BingGe) && killer.Def.Faction == Faction.Bing)
            {
                killer.TempAtkTimer = 3f;
                killer.TempAtkAmount = 0.2f;
            }
        }

        private void DoHeal(Unit u)
        {
            float amount = (u.Def.Name == "许行" ? 3f : 4f) * S.HealingMul;
            List<Unit> targets = new List<Unit>();
            if (u.Def.Name == "许行")
            {
                for (int i = 0; i < S.Units.Count; i++) targets.Add(S.Units[i]);
            }
            else
            {
                Unit lowest = null;
                for (int i = 0; i < S.Units.Count; i++)
                {
                    Unit t = S.Units[i];
                    if (t.Hp >= t.MaxHp) continue;
                    if (lowest == null || t.Hp / t.MaxHp < lowest.Hp / lowest.MaxHp) lowest = t;
                }
                if (lowest != null) targets.Add(lowest);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Unit t = targets[i];
                t.Hp = Mathf.Min(t.MaxHp, t.Hp + amount);
                if (S.Perks.Contains(PerkId.ShunShi))
                {
                    t.TempAtkTimer = 3f;
                    t.TempAtkAmount = 0.1f;
                }
            }
        }

        private void UpdateEnemies(float dt)
        {
            for (int i = S.Enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = S.Enemies[i];
                if (e.Dead) { KillEnemy(e, null); continue; }

                if (e.SlowTimer > 0f) e.SlowTimer -= dt;
                if (e.StunTimer > 0f) e.StunTimer -= dt;
                if (e.BurnTimer > 0f)
                {
                    e.BurnTimer -= dt;
                    e.Hp -= e.BurnDps * dt;
                    if (e.Dead) { KillEnemy(e, null); continue; }
                }
                if (e.VulnerableTimer > 0f) e.VulnerableTimer -= dt;
                if (e.ArmorBreakTimer > 0f) e.ArmorBreakTimer -= dt;

                if (e.Def.Boss)
                {
                    e.BossSkillTimer -= dt;
                    if (e.BossSkillTimer <= 0f)
                    {
                        e.BossSkillTimer = 3f;
                        for (int u = 0; u < S.Units.Count; u++)
                        {
                            if (S.Units[u] == null) continue;
                            S.Units[u].AtkDebuffTimer = 2f;
                            S.Units[u].AtkDebuffAmount = 0.12f;
                        }
                    }
                }

                Unit blocker = FindAdjacentUnit(e);
                if (blocker != null)
                {
                    e.AttackTimer -= dt;
                    if (e.AttackTimer <= 0f)
                    {
                        float interval = e.Def.Interval / Mathf.Max(0.2f, AuraAttackSpeedFactor(e));
                        e.AttackTimer = interval;
                        DamageUnit(blocker, e.Def.Damage);
                    }
                    continue;
                }

                if (e.StunTimer > 0f) continue;

                float speed = e.Speed * (e.SlowTimer > 0f ? 1f - e.SlowFactor : 1f);
                e.PathPos += speed * dt;

                // 防重叠：保持 1 格间距
                // 前面的敌人 PathPos 比我大，后面的不能追上前面的（PathPos 大的走得远）
                float minGap = 1.0f;
                for (int j = 0; j < S.Enemies.Count; j++)
                {
                    Enemy other = S.Enemies[j];
                    if (other == null || other == e || other.Dead) continue;
                    float ahead = other.PathPos - e.PathPos; // 正=在我前面
                    if (ahead > 0f && ahead < minGap)
                    {
                        // 把自己往后拉到 前面那个敌人后面 minGap 的位置
                        e.PathPos = other.PathPos - minGap;
                        break;
                    }
                }

                if (e.PathPos >= GameData.Path.Count - 1)
                {
                    S.Enemies.RemoveAt(i);
                    int dmg = e.Def.Boss ? 2 : 1;
                    S.CoreHp -= dmg;
                    ui.Toast(DamageCoreMessage(e, dmg));
                    if (S.CoreHp <= 0)
                    {
                        S.CoreHp = 0;
                        CurrentPhase = Phase.GameOver;
                        ui.ShowResult(false);
                        return;
                    }
                }
            }
        }

        private string DamageCoreMessage(Enemy e, int dmg)
        {
            return "【典】受到 " + e.Def.Name + " 攻击 -" + dmg;
        }

        private float AuraAttackSpeedFactor(Enemy e)
        {
            float factor = 1f;
            Vector2Int pathCell = GameData.Path[e.CellIndex];
            for (int i = 0; i < S.Units.Count; i++)
            {
                Unit u = S.Units[i];
                int range;
                float slow;
                if (u.Def.Name == "儒生") { range = 2; slow = 0.15f; }
                else if (u.Def.Name == "孔子")
                {
                    range = 3; slow = 0.25f;
                    if (S.HasBond(Faction.Ru, 4)) range = 6;
                }
                else continue;
                if (S.Perks.Contains(PerkId.RuFeng)) range += 1;

                int d = Mathf.Abs(pathCell.x - u.Cell.Col) + Mathf.Abs(pathCell.y - u.Cell.Row);
                if (d <= range) factor = Mathf.Min(factor, 1f - slow);
            }
            return factor;
        }

        private Unit FindAdjacentUnit(Enemy e)
        {
            // 同时检查当前路径格和"即将进入"的下一格，消除整数帧边界误差
            int idx = Mathf.FloorToInt(e.PathPos);
            Vector2Int pc = GameData.Path[Mathf.Clamp(idx, 0, GameData.Path.Count - 1)];
            Vector2Int pn = GameData.Path[Mathf.Clamp(idx + 1, 0, GameData.Path.Count - 1)];

            Unit best = null;
            for (int i = 0; i < S.Units.Count; i++)
            {
                Unit u = S.Units[i];
                if (u == null || u.Cell == null) continue;
                int d = Mathf.Min(
                    Mathf.Abs(u.Cell.Col - pc.x) + Mathf.Abs(u.Cell.Row - pc.y),
                    Mathf.Abs(u.Cell.Col - pn.x) + Mathf.Abs(u.Cell.Row - pn.y));
                if (d <= 1 && (best == null || u.Hp < best.Hp)) best = u;
            }
            return best;
        }

        private void DamageUnit(Unit u, float dmg)
        {
            if (u.Def.Name == "庄子" && Random.value < 0.2f) return;

            float final = dmg * S.DamageTakenMul(u);
            if (u.Shield > 0f)
            {
                float absorbed = Mathf.Min(u.Shield, final);
                u.Shield -= absorbed;
                final -= absorbed;
                if (u.Shield <= 0f && (u.Def.Name == "墨子" || S.Perks.Contains(PerkId.MoShou)))
                {
                    Vector2Int pc = u.Cell != null ? new Vector2Int(u.Cell.Col, u.Cell.Row) : Vector2Int.zero;
                    for (int i = S.Enemies.Count - 1; i >= 0; i--)
                    {
                        Enemy e = S.Enemies[i];
                        if (e.Dead) continue;
                        Vector2Int ep = GameData.Path[e.CellIndex];
                        if (Mathf.Abs(ep.x - pc.x) + Mathf.Abs(ep.y - pc.y) <= 1)
                        {
                            e.StunTimer = Mathf.Max(e.StunTimer, 2f);
                            if (S.Perks.Contains(PerkId.MoShou)) DealDamageToEnemy(e, 15f, u);
                        }
                    }
                }
            }
            if (final <= 0f) return;

            u.Hp -= final;
            if (u.Hp <= 0f) DestroyUnit(u);
        }

        private void DestroyUnit(Unit u)
        {
            if (u.Cell != null) u.Cell.Unit = null;
            S.Units.Remove(u);

            if (S.Perks.Contains(PerkId.XinHuo) && S.XinHuoTriggers < 3 && S.Hand.Count < GameData.HandLimit)
            {
                S.XinHuoTriggers++;
                Faction f = (Faction)S.Rng.Next(8);
                S.Hand.Add(Glyph.FromDef(GameData.GetBaseGlyph(f)));
            }
        }

        private float TierMul(Unit u)
        {
            if (u.Tier >= 3) return GameData.Tier3Mul;
            if (u.Tier == 2) return GameData.Tier2Mul;
            return 1f;
        }

        private void EndWave()
        {
            int completed = S.Wave;
            S.WavesSurvived++;
            S.Gold += GameData.WaveGoldReward;
            S.XinHuoTriggers = 0;
            ui.Toast("第 " + completed + " 波已清空  +" + GameData.WaveGoldReward + " 金币");

            if (completed >= GameData.TotalWaves)
            {
                CurrentPhase = Phase.Victory;
                ui.ShowResult(true);
                return;
            }

            S.Wave = completed + 1;
            if (completed >= 3)
            {
                CurrentPhase = Phase.WaveEnd;
                OfferPerks();
            }
            else
            {
                CurrentPhase = Phase.Prepare;
            }
            ui.RefreshAll();
        }
    }
}
