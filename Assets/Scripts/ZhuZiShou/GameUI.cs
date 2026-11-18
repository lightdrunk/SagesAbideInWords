using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZhuZiShou
{
    public class GameUI : MonoBehaviour
    {
        internal enum DragSourceType { Hand, Shop }

        // 挂在手牌/商店 slot 上。只处理拖拽（移动 > 8px 才触发），点击由 Button 负责。
        // 与 Button 共存：Button.onClick 在未移动时触发（点击），拖拽由 GameUI.Update 轮询鼠标完成。
        internal class DragSource : MonoBehaviour, IPointerDownHandler
        {
            public GameUI Ui;
            public int Index;
            public DragSourceType Type;

            public void OnPointerDown(PointerEventData e)
            {
                Ui.RegisterPendingDrag(this);
            }
        }

        // 挂在 Cell Bg 上接收 Drop
        internal class DropTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public GameUI Ui;
            public int Col, Row;
            public void OnPointerEnter(PointerEventData e) { Ui.HighlightCell(Col, Row, true); }
            public void OnPointerExit(PointerEventData e)  { Ui.HighlightCell(Col, Row, false); }
        }

        private class CellView
        {
            public Image Bg;
            public Text Main;
            public Text Sub;
            public Image HpBack;
            public Image HpFill;
        }

        private class EnemyView
        {
            public GameObject Root;
            public RectTransform Rt;
            public Image HpFill;
            public Text Label;
        }

        private GameObject canvasGo;
        private RectTransform layoutRoot;
        private Font font;
        private Sprite white;

        private Text waveText;
        private Text goldText;
        private Text coreText;
        private Text bondsText;
        private Text perksText;
        private Text handTitle;
        private Text shopTitle;
        private Text toastText;
        private Image toastBg;

        private CellView[,] cellViews;
        private readonly List<Button> handButtons = new List<Button>();
        private readonly List<Text> handTexts = new List<Text>();
        private readonly List<Image> handImages = new List<Image>();
        private readonly List<Button> shopButtons = new List<Button>();
        private readonly List<Text> shopTexts = new List<Text>();
        private readonly List<Text> shopPriceTexts = new List<Text>();
        private readonly List<Image> shopImages = new List<Image>();

        private Button refreshButton;
        private Text refreshLabel;
        private Button freeButton;
        private Text freeLabel;
        private Button sellButton;
        private Button startButton;
        private Text startLabel;

        private GameObject boardRoot;
        private Transform enemyLayer;

        // 拖拽状态
        private DragSource activeDrag;
        private DragSource pendingDrag;   // 已按下但尚未超过拖拽阈值
        private Vector2 pendingDownPos;   // 按下时的屏幕坐标
        private bool dragStarted;         // 是否已确认进入拖拽
        private RectTransform ghostRt;
        private Image ghostBg;
        private Text ghostLabel;
        private CellView highlightCell;
        private readonly Dictionary<Enemy, EnemyView> enemyViews = new Dictionary<Enemy, EnemyView>();

        private GameObject infoOverlay;
        private Text infoText;
        private Button infoSellButton;
        private Text infoSellLabel;

        private GameObject perkOverlay;
        private readonly List<Button> perkButtons = new List<Button>();
        private readonly List<Text> perkNames = new List<Text>();
        private readonly List<Text> perkDescs = new List<Text>();

        private GameObject matchOverlay;
        private Text matchTitle;
        private readonly List<Button> matchButtons = new List<Button>();
        private readonly List<Text> matchLabels = new List<Text>();

        private GameObject resultOverlay;
        private Text resultTitle;
        private Text resultDesc;
        private Button reviveButton;
        private Button doubleButton;
        private Button restartButton;

        private GameController c;
        private float toastTimer;

        private static readonly Color BgColor = new Color(0.07f, 0.07f, 0.1f, 1f);
        private static readonly Color PanelColor = new Color(0.11f, 0.11f, 0.16f, 1f);
        private static readonly Color CellColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color PathColor = new Color(0.24f, 0.19f, 0.14f, 1f);
        private static readonly Color Accent = new Color(0.95f, 0.82f, 0.45f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.65f, 0.72f, 1f);

        private static readonly Color[] FactionColors =
        {
            new Color(0.69f, 0.31f, 0.29f), new Color(0.27f, 0.45f, 0.66f),
            new Color(0.53f, 0.66f, 0.29f), new Color(0.45f, 0.36f, 0.60f),
            new Color(0.85f, 0.55f, 0.24f), new Color(0.26f, 0.62f, 0.70f),
            new Color(0.70f, 0.58f, 0.24f), new Color(0.55f, 0.55f, 0.55f)
        };

        public void Build(GameController controller)
        {
            c = controller;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun", "Arial" }, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            white = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f));

            EnsureEventSystem();
            BuildCanvas();
            BuildTopBar();
            BuildBoard();
            BuildBottom();
            BuildInfoPanel();
            BuildPerkPanel();
            BuildMatchPanel();
            BuildResultPanel();
            BuildToast();
            RefreshAll();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void BuildCanvas()
        {
            canvasGo = new GameObject("ZhuZiShouCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;

            RectTransform bg = NewRect("Background", canvasGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image img = bg.gameObject.AddComponent<Image>();
            img.sprite = white;
            img.color = BgColor;
            img.raycastTarget = false;

            // 固定 720x1280 竖屏版式容器，居中于任意分辨率，横向时自动留边
            layoutRoot = NewRect("Layout", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 1280));
        }

        private void BuildTopBar()
        {
            RectTransform bar = NewRect("TopBar", layoutRoot, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 150));
            Image bg = bar.gameObject.AddComponent<Image>();
            bg.sprite = white;
            bg.color = PanelColor;

            waveText = Label(bar, "Wave", "第 1 / 10 波", 26, Accent, new Vector2(0, 1), new Vector2(18, -14), new Vector2(260, 34), TextAnchor.MiddleLeft);
            goldText = Label(bar, "Gold", "金币 45", 24, new Color(1f, 0.9f, 0.6f), new Vector2(0, 1), new Vector2(18, -52), new Vector2(260, 32), TextAnchor.MiddleLeft);
            coreText = Label(bar, "Core", "【典】 5 / 5", 24, new Color(1f, 0.62f, 0.55f), new Vector2(1, 1), new Vector2(-18, -14), new Vector2(280, 34), TextAnchor.MiddleRight);
            bondsText = Label(bar, "Bonds", "羁绊：无", 18, Muted, new Vector2(0, 1), new Vector2(18, -94), new Vector2(684, 24), TextAnchor.MiddleLeft);
            perksText = Label(bar, "Perks", "词条：无", 17, Muted, new Vector2(0, 1), new Vector2(18, -120), new Vector2(684, 22), TextAnchor.MiddleLeft);
        }

        private void BuildBoard()
        {
            boardRoot = NewRect("Board", layoutRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(530, 636)).gameObject;
            Image bg = boardRoot.AddComponent<Image>();
            bg.sprite = white;
            bg.color = new Color(0.05f, 0.05f, 0.08f, 1f);

            cellViews = new CellView[GameData.Cols, GameData.Rows];
            for (int r = 0; r < GameData.Rows; r++)
            {
                for (int col = 0; col < GameData.Cols; col++)
                {
                    int cc = col, rr = r;
                    RectTransform cell = NewRect("Cell_" + col + "_" + r, boardRoot.transform,
                        new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(6 + col * 106, -(6 + r * 106)), new Vector2(100, 100));
                    Image img = cell.gameObject.AddComponent<Image>();
                    img.sprite = white;
                    img.color = CellColor;
                    Button btn = cell.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    btn.onClick.AddListener(() => c.OnCellClick(cc, rr));

                    Text main = Label(cell, "Main", "", 34, Color.white, new Vector2(0, 1), new Vector2(0, -6), new Vector2(100, 62), TextAnchor.MiddleCenter);
                    Text sub = Label(cell, "Sub", "", 16, Muted, new Vector2(0, 1), new Vector2(0, -66), new Vector2(100, 20), TextAnchor.MiddleCenter);

                    RectTransform hpBack = NewRect("HpBack", cell, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(82, 8));
                    Image hb = hpBack.gameObject.AddComponent<Image>();
                    hb.sprite = white;
                    hb.color = new Color(0, 0, 0, 0.6f);

                    RectTransform hpFill = NewRect("HpFill", hpBack, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                    Image hf = hpFill.gameObject.AddComponent<Image>();
                    hf.sprite = white;
                    hf.type = Image.Type.Filled;
                    hf.fillMethod = Image.FillMethod.Horizontal;
                    hf.fillOrigin = 0;
                    hf.color = new Color(0.4f, 0.85f, 0.4f);

                    hb.enabled = false;
                    hf.enabled = false;

                    DropTarget dt = img.gameObject.AddComponent<DropTarget>();
                    dt.Ui = this; dt.Col = col; dt.Row = r;

                    cellViews[col, r] = new CellView { Bg = img, Main = main, Sub = sub, HpBack = hb, HpFill = hf };
                }
            }

            enemyLayer = NewRect("Enemies", boardRoot.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private void BuildBottom()
        {
            RectTransform panel = NewRect("Bottom", layoutRoot, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 490));
            Image bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = white;
            bg.color = PanelColor;

            handTitle = Label(panel, "HandTitle", "待选字：点字选中，再点棋盘放置（同字合成 / 拼字合成）", 15, Muted, new Vector2(0, 1), new Vector2(16, -8), new Vector2(680, 22), TextAnchor.MiddleLeft);

            for (int i = 0; i < GameData.HandLimit; i++)
            {
                int idx = i;
                int row = i / 5;
                int col = i % 5;
                RectTransform slot = NewRect("Hand_" + i, panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(193 + col * 68, -34 - row * 68), new Vector2(62, 62));
                Image img = slot.gameObject.AddComponent<Image>();
                img.sprite = white;
                img.color = CellColor;
                Button btn = slot.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => c.OnHandClick(idx));
                Text txt = Label(slot, "Char", "", 30, Accent, Vector2.zero, Vector2.zero, new Vector2(62, 62), TextAnchor.MiddleCenter);
                handButtons.Add(btn);
                handTexts.Add(txt);
                handImages.Add(img);
                DragSource ds = slot.gameObject.AddComponent<DragSource>();
                ds.Ui = this; ds.Index = idx; ds.Type = DragSourceType.Hand;
            }

            shopTitle = Label(panel, "ShopTitle", "商店：点击买入（5 金）", 15, Muted, new Vector2(0, 1), new Vector2(16, -170), new Vector2(680, 22), TextAnchor.MiddleLeft);

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                RectTransform slot = NewRect("Shop_" + i, panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(170 + i * 98, -196), new Vector2(86, 86));
                Image img = slot.gameObject.AddComponent<Image>();
                img.sprite = white;
                img.color = CellColor;
                Button btn = slot.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => c.OnShopClick(idx));
                Text txt = Label(slot, "Char", "", 34, Color.white, new Vector2(0, 1), new Vector2(0, -6), new Vector2(86, 52), TextAnchor.MiddleCenter);
                Text price = Label(slot, "Price", "5 金", 14, new Color(1f, 0.88f, 0.55f), new Vector2(0, 0), new Vector2(0, 6), new Vector2(86, 18), TextAnchor.MiddleCenter);
                shopButtons.Add(btn);
                shopTexts.Add(txt);
                shopPriceTexts.Add(price);
                shopImages.Add(img);
                DragSource ds = slot.gameObject.AddComponent<DragSource>();
                ds.Ui = this; ds.Index = idx; ds.Type = DragSourceType.Shop;
            }

            refreshButton = MakeButton(panel, "Refresh", "刷新 10 金", new Vector2(16, -322), new Vector2(164, 64), () => c.OnRefresh());
            refreshLabel = refreshButton.GetComponentInChildren<Text>();
            freeButton = MakeButton(panel, "Free", "广告刷新 ×3", new Vector2(188, -322), new Vector2(140, 64), () => c.OnFreeRefresh());
            freeLabel = freeButton.GetComponentInChildren<Text>();
            sellButton = MakeButton(panel, "Sell", "出售", new Vector2(336, -322), new Vector2(96, 64), () => c.OnSellSelected());
            startButton = MakeButton(panel, "Start", "开始第 1 波", new Vector2(440, -322), new Vector2(264, 70), () => c.OnStartWave());
            startLabel = startButton.GetComponentInChildren<Text>();
            startButton.image.color = new Color(0.55f, 0.35f, 0.18f);
        }

        private void BuildInfoPanel()
        {
            infoOverlay = BuildOverlay("InfoOverlay", 440, 400, out RectTransform panel);
            Label(panel, "Title", "先贤信息", 22, Accent, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(400, 30), TextAnchor.MiddleCenter);

            RectTransform txt = NewRect("InfoText", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -56), new Vector2(380, 240));
            infoText = txt.gameObject.AddComponent<Text>();
            infoText.font = font;
            infoText.fontSize = 18;
            infoText.color = Color.white;
            infoText.alignment = TextAnchor.UpperLeft;
            infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            infoText.verticalOverflow = VerticalWrapMode.Overflow;

            infoSellButton = MakeButton(panel, "SellUnit", "出售", new Vector2(40, 56), new Vector2(150, 56), () => c.OnSellSelected(), new Vector2(0, 0));
            infoSellLabel = infoSellButton.GetComponentInChildren<Text>();
            MakeButton(panel, "CloseInfo", "关闭", new Vector2(250, 56), new Vector2(150, 56), () => c.OnCloseInfo(), new Vector2(0, 0));
            infoOverlay.SetActive(false);
        }

        private void BuildPerkPanel()
        {
            perkOverlay = BuildOverlay("PerkOverlay", 620, 560, out RectTransform panel);
            Label(panel, "Title", "选择词条（本局生效，不可更改）", 24, Accent, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(580, 34), TextAnchor.MiddleCenter);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                RectTransform card = NewRect("Perk_" + i, panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -70 - i * 150), new Vector2(560, 130));
                Image img = card.gameObject.AddComponent<Image>();
                img.sprite = white;
                img.color = new Color(0.17f, 0.17f, 0.25f);
                Button btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => c.OnPerkChosen(idx));
                Text name = Label(card, "Name", "", 24, Accent, new Vector2(0, 1), new Vector2(18, -12), new Vector2(520, 34), TextAnchor.MiddleLeft);
                Text desc = Label(card, "Desc", "", 17, Muted, new Vector2(0, 1), new Vector2(18, -54), new Vector2(520, 60), TextAnchor.UpperLeft);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                perkButtons.Add(btn);
                perkNames.Add(name);
                perkDescs.Add(desc);
            }
            perkOverlay.SetActive(false);
        }

        private void BuildMatchPanel()
        {
            matchOverlay = BuildOverlay("MatchOverlay", 600, 500, out RectTransform panel);
            matchTitle = Label(panel, "Title", "三消", 24, Accent, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(560, 34), TextAnchor.MiddleCenter);

            string[] names = { "A  返还 12 金币", "B  生成 1 个一阶单位", "C  该流派本局伤害 +8%" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                RectTransform card = NewRect("Match_" + i, panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(25, -72 - i * 132), new Vector2(550, 112));
                Image img = card.gameObject.AddComponent<Image>();
                img.sprite = white;
                img.color = new Color(0.17f, 0.17f, 0.25f);
                Button btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => c.OnMatch3Chosen(idx));
                Text label = Label(card, "Label", names[i], 20, Color.white, new Vector2(0, 1), new Vector2(16, -16), new Vector2(520, 70), TextAnchor.UpperLeft);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                matchButtons.Add(btn);
                matchLabels.Add(label);
            }
            matchOverlay.SetActive(false);
        }

        private void BuildResultPanel()
        {
            resultOverlay = BuildOverlay("ResultOverlay", 560, 460, out RectTransform panel);
            resultTitle = Label(panel, "Title", "", 40, Accent, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(520, 52), TextAnchor.MiddleCenter);
            RectTransform desc = NewRect("Desc", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -90), new Vector2(500, 120));
            resultDesc = desc.gameObject.AddComponent<Text>();
            resultDesc.font = font;
            resultDesc.fontSize = 19;
            resultDesc.color = Color.white;
            resultDesc.alignment = TextAnchor.UpperCenter;
            resultDesc.horizontalOverflow = HorizontalWrapMode.Wrap;

            reviveButton = MakeButton(panel, "Revive", "广告复活（保留单位）", new Vector2(30, 220), new Vector2(240, 56), () => c.OnRevive(), new Vector2(0, 0));
            doubleButton = MakeButton(panel, "Double", "金币奖励翻倍（广告）", new Vector2(290, 220), new Vector2(240, 56), () => c.OnDoubleReward(), new Vector2(0, 0));
            restartButton = MakeButton(panel, "Restart", "重新开始", new Vector2(160, 140), new Vector2(240, 60), () => c.OnRestart(), new Vector2(0, 0));
            resultOverlay.SetActive(false);
        }

        private void BuildToast()
        {
            RectTransform t = NewRect("Toast", canvasGo.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(620, 40));
            toastBg = t.gameObject.AddComponent<Image>();
            toastBg.sprite = white;
            toastBg.color = new Color(0, 0, 0, 0.65f);
            toastText = Label(t, "Text", "", 20, Accent, Vector2.zero, Vector2.zero, new Vector2(620, 40), TextAnchor.MiddleCenter);
            toastBg.enabled = false;
            toastText.enabled = false;
        }

        private GameObject BuildOverlay(string name, float width, float height, out RectTransform panel)
        {
            RectTransform root = NewRect(name, canvasGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.sprite = white;
            blocker.color = new Color(0, 0, 0, 0.72f);
            blocker.raycastTarget = true;

            panel = NewRect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            Image bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = white;
            bg.color = new Color(0.13f, 0.13f, 0.19f);
            return root.gameObject;
        }

        // ---------------- 刷新 ----------------

        public void RefreshAll()
        {
            GameState s = c.S;
            waveText.text = "第 " + s.Wave + " / " + GameData.TotalWaves + " 波";
            goldText.text = "金币 " + s.Gold;
            coreText.text = "【典】 " + s.CoreHp + " / " + s.CoreMaxHp;

            string bonds = "";
            for (int f = 0; f < 8; f++)
            {
                int n = s.BondCount((Faction)f);
                if (n >= 2) bonds += (bonds.Length > 0 ? "  " : "") + FactionInfo.Name((Faction)f) + " " + n;
            }
            bondsText.text = "羁绊：" + (bonds.Length > 0 ? bonds : "无");

            string perks = "";
            for (int i = 0; i < s.PickedPerks.Count; i++)
                perks += (perks.Length > 0 ? "  " : "") + GameData.GetPerk(s.PickedPerks[i]).Name;
            perksText.text = "词条：" + (perks.Length > 0 ? perks : "无");

            refreshLabel.text = "刷新 " + s.RefreshCost + " 金";
            refreshButton.interactable = s.Gold >= s.RefreshCost && !c.PerkPending;
            freeLabel.text = "广告刷新 ×" + s.FreeRefreshes;
            freeButton.interactable = s.FreeRefreshes > 0;

            bool canSelect = c.SelectedCell != null || c.SelectedHand >= 0;
            sellButton.interactable = canSelect;

            if (c.CurrentPhase == Phase.Combat) startLabel.text = "战斗中…";
            else if (c.CurrentPhase == Phase.WaveEnd) startLabel.text = "选择词条…";
            else if (c.CurrentPhase == Phase.GameOver) startLabel.text = "已失败";
            else if (c.CurrentPhase == Phase.Victory) startLabel.text = "已通关";
            else startLabel.text = "开始第 " + s.Wave + " 波";
            startButton.interactable = c.CurrentPhase == Phase.Prepare && !c.PerkPending;

            for (int i = 0; i < shopButtons.Count; i++)
            {
                bool has = i < s.Shop.Length && s.Shop[i] != null;
                shopTexts[i].text = has ? s.Shop[i].Char : "—";
                shopPriceTexts[i].text = has ? GameData.GlyphCost + " 金" : "";
                shopImages[i].color = has ? new Color(0.22f, 0.2f, 0.17f) : new Color(0.1f, 0.1f, 0.13f);
                shopButtons[i].interactable = has && s.Gold >= GameData.GlyphCost && s.Hand.Count < GameData.HandLimit;
            }

            for (int i = 0; i < handButtons.Count; i++)
            {
                bool has = i < s.Hand.Count;
                handTexts[i].text = has ? s.Hand[i].Char : "";
                bool selected = has && i == c.SelectedHand;
                handImages[i].color = selected ? new Color(0.55f, 0.45f, 0.2f)
                    : (has ? (s.Hand[i].Kind == GlyphKind.Base ? new Color(0.22f, 0.22f, 0.3f) : new Color(0.2f, 0.17f, 0.24f)) : new Color(0.1f, 0.1f, 0.13f));
                handButtons[i].interactable = has;
            }

            for (int r = 0; r < GameData.Rows; r++)
            {
                for (int col = 0; col < GameData.Cols; col++)
                {
                    Cell cell = s.Board[col, r];
                    CellView v = cellViews[col, r];
                    bool selected = c.SelectedCell == cell;

                    if (cell.IsCore)
                    {
                        v.Bg.color = selected ? new Color(0.65f, 0.3f, 0.28f) : new Color(0.38f, 0.14f, 0.14f);
                        v.Main.text = "典";
                        v.Main.fontSize = 36;
                        v.Main.color = Accent;
                        v.Sub.text = "生命 " + s.CoreHp;
                        v.Sub.color = new Color(1f, 0.7f, 0.6f);
                        SetHp(v, false, 0f);
                    }
                    else if (cell.Unit != null)
                    {
                        Unit u = cell.Unit;
                        Color fc = FactionColors[(int)u.Def.Faction];
                        v.Bg.color = selected ? Color.Lerp(fc, Accent, 0.55f) : Color.Lerp(fc, Color.black, 0.35f);
                        v.Main.text = u.DisplayName;
                        v.Main.fontSize = 20;
                        v.Main.color = Color.white;
                        v.Sub.text = u.Tier >= 3 ? "★★★" : (u.Tier == 2 ? "★★" : "★");
                        v.Sub.color = Accent;
                        SetHp(v, true, u.MaxHp > 0 ? u.Hp / u.MaxHp : 0f);
                    }
                    else if (cell.Glyph != null)
                    {
                        v.Bg.color = selected ? new Color(0.55f, 0.45f, 0.2f) : new Color(0.24f, 0.21f, 0.16f);
                        v.Main.text = cell.Glyph.Char;
                        v.Main.fontSize = 40;
                        v.Main.color = Accent;
                        v.Sub.text = "字";
                        v.Sub.color = Muted;
                        SetHp(v, false, 0f);
                    }
                    else if (cell.IsPath)
                    {
                        v.Bg.color = PathColor;
                        v.Main.text = "";
                        v.Sub.text = "·";
                        v.Sub.color = new Color(0.5f, 0.4f, 0.3f);
                        SetHp(v, false, 0f);
                    }
                    else
                    {
                        v.Bg.color = selected ? new Color(0.45f, 0.4f, 0.22f) : CellColor;
                        v.Main.text = "";
                        v.Sub.text = "";
                        SetHp(v, false, 0f);
                    }
                }
            }
        }

        private void SetHp(CellView v, bool visible, float ratio)
        {
            v.HpBack.enabled = visible;
            v.HpFill.enabled = visible;
            if (visible)
            {
                v.HpFill.fillAmount = Mathf.Clamp01(ratio);
                v.HpFill.color = ratio > 0.5f ? new Color(0.4f, 0.85f, 0.4f) : (ratio > 0.25f ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.9f, 0.35f, 0.3f));
            }
        }

        public void UpdateEnemies(List<Enemy> enemies, bool combat)
        {
            HashSet<Enemy> alive = new HashSet<Enemy>();
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e == null) continue;
                alive.Add(e);
                EnemyView view;
                if (!enemyViews.TryGetValue(e, out view))
                {
                    view = CreateEnemyView(e);
                    enemyViews[e] = view;
                }
                view.Rt.anchoredPosition = PathPosition(e.PathPos);
                float ratio = e.MaxHp > 0 ? e.Hp / e.MaxHp : 0f;
                view.HpFill.fillAmount = Mathf.Clamp01(ratio);
            }

            // 渲染层级：PathPos 大的（前面的敌人）盖在 PathPos 小的（后面的）上面
            var sorted = alive.ToList();
            sorted.Sort((a, b) => a.PathPos.CompareTo(b.PathPos));
            for (int i = 0; i < sorted.Count; i++)
                enemyViews[sorted[i]].Rt.SetAsLastSibling();

            List<Enemy> remove = null;
            foreach (KeyValuePair<Enemy, EnemyView> kv in enemyViews)
            {
                if (!alive.Contains(kv.Key))
                {
                    if (remove == null) remove = new List<Enemy>();
                    remove.Add(kv.Key);
                }
            }
            if (remove != null)
            {
                for (int i = 0; i < remove.Count; i++)
                {
                    Destroy(enemyViews[remove[i]].Root);
                    enemyViews.Remove(remove[i]);
                }
            }
        }

        private EnemyView CreateEnemyView(Enemy e)
        {
            EnemyView v = new EnemyView();
            RectTransform rt = NewRect("Enemy", enemyLayer, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = white;
            img.color = EnemyColor(e.Def.Kind);
            img.raycastTarget = false;

            Text label = Label(rt, "Char", e.Def.Name, 22, Color.white, Vector2.zero, Vector2.zero, new Vector2(44, 44), TextAnchor.MiddleCenter);
            label.raycastTarget = false;

            RectTransform hpBack = NewRect("HpBack", rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(40, 5));
            Image hb = hpBack.gameObject.AddComponent<Image>();
            hb.sprite = white;
            hb.color = new Color(0, 0, 0, 0.7f);
            hb.raycastTarget = false;

            RectTransform hpFill = NewRect("HpFill", hpBack, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            Image hf = hpFill.gameObject.AddComponent<Image>();
            hf.sprite = white;
            hf.type = Image.Type.Filled;
            hf.fillMethod = Image.FillMethod.Horizontal;
            hf.fillOrigin = 0;
            hf.color = new Color(0.95f, 0.4f, 0.35f);
            hf.raycastTarget = false;

            v.Root = rt.gameObject;
            v.Rt = rt;
            v.HpFill = hf;
            v.Label = label;
            return v;
        }

        private Color EnemyColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Huo: return new Color(0.45f, 0.3f, 0.5f);
                case EnemyKind.Wang: return new Color(0.5f, 0.35f, 0.2f);
                case EnemyKind.Luan: return new Color(0.3f, 0.3f, 0.35f);
                case EnemyKind.Gui: return new Color(0.25f, 0.45f, 0.5f);
                default: return new Color(0.6f, 0.15f, 0.2f);
            }
        }

        private Vector2 PathPosition(float pathPos)
        {
            int idx = Mathf.FloorToInt(pathPos);
            if (idx < 0) idx = 0;
            if (idx > GameData.Path.Count - 1) idx = GameData.Path.Count - 1;
            int next = Mathf.Min(idx + 1, GameData.Path.Count - 1);
            float t = Mathf.Clamp01(pathPos - idx);
            Vector2Int a = GameData.Path[idx];
            Vector2Int b = GameData.Path[next];
            Vector2 pa = CellCenter(a.x, a.y);
            Vector2 pb = CellCenter(b.x, b.y);
            return Vector2.Lerp(pa, pb, t);
        }

        // 沿路径方向微微错开，让同簇敌人前后各退/进一点，既不重叠也不分成两排
        private float ComputePathOffset(Enemy self, List<Enemy> all, int myIndex)
        {
            const float NEAR = 0.5f;  // 多少格内视为"挤在一起"
            const float STEP = 0.09f; // 每个敌人的错开量（格）
            var cluster = new List<Enemy> { self };
            for (int i = 0; i < all.Count; i++)
            {
                if (i == myIndex) continue;
                Enemy other = all[i];
                if (other == null || other.Dead) continue;
                if (Mathf.Abs(other.PathPos - self.PathPos) < NEAR) cluster.Add(other);
            }
            if (cluster.Count == 1) return 0f;

            // 按 PathPos 排序后均匀分布在 [-(n-1)/2, (n-1)/2] * STEP
            cluster.Sort((x, y) => x.PathPos.CompareTo(y.PathPos));
            int myPos = cluster.IndexOf(self);
            int n = cluster.Count;
            return (myPos - (n - 1) / 2f) * STEP;
        }

        private Vector2 CellCenter(int col, int row)
        {
            return new Vector2(6 + col * 106 + 50, -(6 + row * 106 + 50));
        }

        // ---------------- 弹层 ----------------

        public void ShowUnitInfo(Cell cell)
        {
            if (cell == null || cell.Unit == null) return;
            Unit u = cell.Unit;
            float atk = u.Def.Atk * (u.Tier >= 3 ? GameData.Tier3Mul : (u.Tier == 2 ? GameData.Tier2Mul : 1f)) * c.S.AttackMul(u);
            infoText.text =
                u.DisplayName + "（" + FactionInfo.Name(u.Def.Faction) + "）\n\n" +
                "生命：" + Mathf.CeilToInt(u.Hp) + " / " + Mathf.CeilToInt(u.MaxHp) + "    护盾：" + Mathf.CeilToInt(u.Shield) + "\n" +
                "攻击：" + Mathf.RoundToInt(atk) + "    攻速：" + u.Def.Interval.ToString("0.0") + " 秒/次\n" +
                "射程：" + u.Def.Range + " 格\n\n" +
                "技能：" + u.Def.Skill;
            infoSellLabel.text = "出售 +" + c.S.SellPrice(u) + " 金";
            infoOverlay.SetActive(true);
        }

        public void ShowHandInfo()
        {
        }

        public void HideUnitInfo()
        {
            if (infoOverlay != null) infoOverlay.SetActive(false);
        }

        public void ShowPerkChoices(List<PerkId> ids)
        {
            for (int i = 0; i < perkButtons.Count; i++)
            {
                bool show = i < ids.Count;
                perkButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                PerkDef def = GameData.GetPerk(ids[i]);
                perkNames[i].text = def.Name;
                perkDescs[i].text = def.Desc;
            }
            perkOverlay.SetActive(true);
        }

        public void HidePerkPanel()
        {
            if (perkOverlay != null) perkOverlay.SetActive(false);
        }

        public void ShowMatch3(string ch, Faction faction)
        {
            matchTitle.text = "三消 · 三个「" + ch + "」";
            string unitName = GameData.BaseUnitNames[(int)faction];
            matchLabels[0].text = "A  返还 12 金币\n适合需要连续刷新找关键字的局面";
            matchLabels[1].text = "B  直接生成 1 个「" + unitName + "」一阶单位\n省去两次合成";
            matchLabels[2].text = "C  " + FactionInfo.Name(faction) + " 本局伤害提升 8%\n强化已确定的主流派";
            matchOverlay.SetActive(true);
        }

        public void HideMatch3()
        {
            if (matchOverlay != null) matchOverlay.SetActive(false);
        }

        public void ShowResult(bool win)
        {
            resultTitle.text = win ? "通　关" : "失　败";
            resultTitle.color = win ? Accent : new Color(0.9f, 0.4f, 0.35f);
            string perks = "";
            for (int i = 0; i < c.S.PickedPerks.Count; i++)
                perks += (perks.Length > 0 ? "、" : "") + GameData.GetPerk(c.S.PickedPerks[i]).Name;
            resultDesc.text = win
                ? "你守住了【典】，通过第 10 波。\n\n本局词条：" + (perks.Length > 0 ? perks : "无")
                : "【典】被击破。\n\n场上单位将保留，可观看广告原地复活一次。";
            reviveButton.gameObject.SetActive(!win && c.S.RevivesLeft > 0);
            doubleButton.gameObject.SetActive(win && c.S.DoubleRewardLeft > 0);
            resultOverlay.SetActive(true);
        }

        public void HideAllOverlays()
        {
            if (infoOverlay != null) infoOverlay.SetActive(false);
            if (perkOverlay != null) perkOverlay.SetActive(false);
            if (matchOverlay != null) matchOverlay.SetActive(false);
            if (resultOverlay != null) resultOverlay.SetActive(false);
        }

        public void Toast(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            toastText.text = msg;
            toastTimer = 2.2f;
            toastBg.enabled = true;
            toastText.enabled = true;
            Color c1 = toastText.color;
            c1.a = 1f;
            toastText.color = c1;
            Color c2 = toastBg.color;
            c2.a = 0.65f;
            toastBg.color = c2;
        }

        private void Update()
        {
            UpdateDragPolling();

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                if (toastTimer <= 0f)
                {
                    toastBg.enabled = false;
                    toastText.enabled = false;
                }
                else if (toastTimer < 0.6f)
                {
                    Color t = toastText.color;
                    t.a = toastTimer / 0.6f;
                    toastText.color = t;
                    Color b = toastBg.color;
                    b.a = 0.65f * (toastTimer / 0.6f);
                    toastBg.color = b;
                }
            }
        }

        // ---------------- 控件工厂 ----------------

        private RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private Text Label(Transform parent, string name, string content, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align)
        {
            RectTransform rt = NewRect(name, parent, anchor, anchor, anchor, pos, box);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = content;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityAction onClick)
        {
            return MakeButton(parent, name, label, pos, size, onClick, new Vector2(0, 1));
        }

        private Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityAction onClick, Vector2 anchor)
        {
            RectTransform rt = NewRect(name, parent, anchor, anchor, anchor, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = white;
            img.color = new Color(0.3f, 0.28f, 0.36f);
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            Text text = Label(rt, "Text", label, 21, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            return btn;
        }

        // ================= 拖拽放置系统（轮询式，不依赖 EventSystem 拖拽事件） =================

        private const float ClickDragThreshold = 8f;

        // 原 slot Image 引用，拖拽时变半透明，结束恢复
        private Image dragSourceImg;
        private Color dragSourceOrigColor;
        private Text dragSourceText;   // 拖拽时隐藏原 slot 的字，松手恢复

        // DragSource 按下时调用
        internal void RegisterPendingDrag(DragSource src)
        {
            pendingDrag = src;
            pendingDownPos = Input.mousePosition;
            dragStarted = false;
        }

        private void UpdateDragPolling()
        {
            bool mouseDown = Input.GetMouseButton(0);

            // 松开但还没开始拖拽 → 清除 pending（点击交给 Button 处理）
            if (!mouseDown && !dragStarted)
            {
                pendingDrag = null;
                return;
            }

            // 按下且移动超过阈值 → 开始拖拽
            if (mouseDown && !dragStarted && pendingDrag != null)
            {
                if (Vector2.Distance(pendingDownPos, Input.mousePosition) > ClickDragThreshold)
                    StartDrag(pendingDrag);
            }

            if (!dragStarted) return;

            // 拖拽中：每帧跟随鼠标 + 高亮格子（Ghost 中心对准光标）
            Vector3 m = Input.mousePosition;
            if (ghostRt != null)
                ghostRt.position = m;

            int hCol, hRow;
            CellView hover = FindCellUnderScreen(m, out hCol, out hRow);
            HighlightCell(hCol, hRow, hover != null);

            // 松开 → 放置
            if (Input.GetMouseButtonUp(0))
                FinishDrag(m);
        }

        private void StartDrag(DragSource src)
        {
            Debug.Log("[Drag] StartDrag v3 开始拖拽：src=" + src.Type + "#" + src.Index);
            dragStarted = true;
            activeDrag = src;

            // 原 slot 变半透明 + 隐藏原字（拖拽中只看得到 Ghost）
            dragSourceImg = src.GetComponent<Image>();
            if (dragSourceImg != null)
            {
                dragSourceOrigColor = dragSourceImg.color;
                dragSourceImg.color = new Color(dragSourceOrigColor.r, dragSourceOrigColor.g, dragSourceOrigColor.b, 0.65f);
            }
            dragSourceText = src.Type == DragSourceType.Hand
                ? (src.Index < handTexts.Count ? handTexts[src.Index] : null)
                : (src.Index < shopTexts.Count ? shopTexts[src.Index] : null);
            if (dragSourceText != null) dragSourceText.enabled = false;

            // Ghost 大小：与原 slot 一致（手牌 62，商店 86）
            float ghostSize = src.Type == DragSourceType.Hand ? 62f : 86f;

            RectTransform canvasRt = (RectTransform)canvasGo.transform;
            GameObject ghost = new GameObject("DragGhost", typeof(RectTransform));
            ghost.transform.SetParent(canvasRt, false);
            ghostRt = (RectTransform)ghost.transform;
            ghostRt.sizeDelta = new Vector2(ghostSize, ghostSize);
            ghostRt.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRt.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRt.pivot = new Vector2(0.5f, 0.5f);

            ghostBg = ghost.AddComponent<Image>();
            ghostBg.sprite = white;
            // Ghost 外观与原字块一致：深色底 + 原字色（不再是白底）
            ghostBg.color = dragSourceImg != null ? dragSourceOrigColor : CellColor;
            ghostBg.raycastTarget = false;

            int ghostFont = src.Type == DragSourceType.Hand ? 30 : 34;
            ghostLabel = Label(ghostRt, "Char", "", ghostFont, Color.white,
                Vector2.zero, Vector2.zero, new Vector2(ghostSize, ghostSize), TextAnchor.MiddleCenter);
            ghostLabel.raycastTarget = false;

            string chr = "";
            Color chrColor = Color.white;
            if (src.Type == DragSourceType.Hand && src.Index < handTexts.Count)
            {
                chr = handTexts[src.Index].text;
                chrColor = handTexts[src.Index].color;
            }
            else if (src.Type == DragSourceType.Shop && src.Index < shopTexts.Count)
            {
                chr = shopTexts[src.Index].text;
                chrColor = shopTexts[src.Index].color;
            }

            ghostLabel.text = chr;
            ghostLabel.color = chrColor;
            Vector3 m = Input.mousePosition;
            ghostRt.position = m;
        }

        private void FinishDrag(Vector3 screenPos)
        {
            DragSource src = activeDrag;
            dragStarted = false;
            pendingDrag = null;
            activeDrag = null;

            // 恢复原 slot 透明度与原字
            if (dragSourceImg != null)
            {
                dragSourceImg.color = dragSourceOrigColor;
                dragSourceImg = null;
            }
            if (dragSourceText != null)
            {
                dragSourceText.enabled = true;
                dragSourceText = null;
            }

            if (ghostRt != null) Destroy(ghostRt.gameObject);
            ghostRt = null; ghostBg = null; ghostLabel = null;

            HighlightCell(-1, -1, false);

            if (src == null) return;

            int tCol, tRow;
            CellView target = FindCellUnderScreen(screenPos, out tCol, out tRow);
            if (target == null)
            {
                Debug.Log("[Drag] 松手未落在任何格子上：src=" + src.Type + "#" + src.Index + " pos=" + screenPos);
                return;
            }
            Debug.Log("[Drag] 松手放置 src=" + src.Type + "#" + src.Index + " → (" + tCol + "," + tRow + ")");

            if (src.Type == DragSourceType.Hand)
            {
                c.PlaceGlyphFromHand(src.Index, tCol, tRow);
            }
            else if (src.Type == DragSourceType.Shop)
            {
                c.BuyAndPlace(src.Index, tCol, tRow);
            }
        }

        // 屏幕坐标 → 棋盘格子（覆盖 Canvas 是 Overlay，camera 传 null）
        private CellView FindCellUnderScreen(Vector3 screenPos, out int hitCol, out int hitRow)
        {
            hitCol = -1; hitRow = -1;
            for (int r = 0; r < GameData.Rows; r++)
            for (int col = 0; col < GameData.Cols; col++)
            {
                CellView cv = cellViews[col, r];
                if (cv == null || cv.Bg == null) continue;
                RectTransform rt = cv.Bg.rectTransform;
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out lp)) continue;
                // rt.rect 以 pivot 为原点表达，Contains 对任意 pivot（格子是 (0,1) 左上原点）都正确
                if (rt.rect.Contains(lp))
                {
                    hitCol = col; hitRow = r;
                    return cv;
                }
            }
            return null;
        }

        internal void HighlightCell(int col, int row, bool on)
        {
            // 拖拽时才高亮
            if (activeDrag == null)
            {
                if (highlightCell != null) { highlightCell.Bg.color = CellColor; highlightCell = null; }
                return;
            }
            CellView cv = (col >= 0 && col < GameData.Cols && row >= 0 && row < GameData.Rows)
                ? cellViews[col, row] : null;
            if (highlightCell != null && highlightCell != cv)
                highlightCell.Bg.color = CellColor;
            highlightCell = cv;
            if (cv != null)
            {
                // 可放置=亮黄，路径/核心格=亮红（松手会被拒绝，提前给出反馈）
                bool ok = GameData.IsPlaceable(col, row);
                cv.Bg.color = on ? (ok ? new Color(0.9f, 0.75f, 0.35f) : new Color(0.8f, 0.3f, 0.3f)) : CellColor;
            }
        }
    }
}
