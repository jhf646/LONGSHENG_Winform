using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Text;

namespace LongShenStorageSystem;

public partial class Form1 : Form
{
    private static readonly Color AppBackground = Color.FromArgb(242, 244, 240);
    private static readonly Color HeaderBackground = Color.FromArgb(20, 58, 92);
    private static readonly Color AccentColor = Color.FromArgb(201, 145, 71);
    private static readonly Color AccentSoft = Color.FromArgb(246, 235, 218);
    private static readonly Color CardBackground = Color.FromArgb(252, 252, 249);
    private static readonly Color CardBorder = Color.FromArgb(215, 221, 214);
    private static readonly Color TextPrimary = Color.FromArgb(34, 46, 58);
    private static readonly Color TextSecondary = Color.FromArgb(98, 109, 118);
    private static readonly Color GridAltRow = Color.FromArgb(247, 248, 244);
    private static readonly Color InputBackground = Color.FromArgb(248, 249, 245);

    private readonly SqlServerRepository _repository = new();
    private AppState _state = new();

    private readonly BindingList<WorkpieceRecord> _inventoryBinding = new();
    private readonly BindingList<StorageSlot> _slotBinding = new();
    private readonly BindingList<LedgerEntry> _ledgerBinding = new();

    private readonly PrintDocument _printDocument = new();
    private readonly ToolTip _slotToolTip = new();

    private TabControl _tabControl = null!;
    private Label _lblInventoryCount = null!;
    private Label _lblOccupiedCount = null!;
    private Label _lblFreeCount = null!;
    private Label _lblAlert = null!;
    private Label _lblHomeStatus = null!;

    private TextBox _txtInboundOperator = null!;
    private TextBox _txtInboundSlot = null!;
    private ComboBox _cmbInboundPallet = null!;
    private ComboBox _cmbInboundTooling = null!;
    private ComboBox _cmbInboundProject = null!;
    private ComboBox _cmbInboundModel = null!;
    private TextBox _txtInboundWorkOrder = null!;
    private TextBox _txtInboundCellNumber = null!;
    private NumericUpDown _numInboundComponentSections = null!;
    private ComboBox _cmbInboundCustomer = null!;
    private TextBox _txtInboundNotes = null!;
    private DataGridView _gridInboundPreview = null!;

    private ComboBox _cmbOutboundWorkpiece = null!;
    private TextBox _txtOutboundOperator = null!;
    private TextBox _txtOutboundSlot = null!;
    private DataGridView _gridOutboundQueue = null!;

    private DataGridView _gridSlots = null!;
    private DataGridView _gridInventory = null!;
    private DataGridView _gridLedger = null!;
    private TableLayoutPanel _homeSlotVisualPanel = null!;
    private TableLayoutPanel _slotManagementVisualPanel = null!;
    private Label _lblSelectedSlotCode = null!;
    private Label _lblSelectedSlotStatus = null!;
    private Label _lblSelectedSlotWorkpiece = null!;
    private Label _lblSelectedSlotLocation = null!;
    private Label _lblSelectedSlotHint = null!;
    private StorageSlot? _selectedVisualSlot;

    private TextBox _txtSearchCode = null!;
    private TextBox _txtSearchBatch = null!;
    private TextBox _txtSearchOperator = null!;
    private ComboBox _cmbSearchType = null!;
    private DateTimePicker _dtSearchStart = null!;
    private DateTimePicker _dtSearchEnd = null!;
    private ComboBox _cmbReportType = null!;
    private NumericUpDown _numMinThreshold = null!;
    private NumericUpDown _numMaxThreshold = null!;

    private IReadOnlyList<string> _lastPrintLines = Array.Empty<string>();

    public Form1()
    {
        InitializeComponent();
        BuildLayout();
        ConfigurePrint();
        LoadState();
    }

    private void BuildLayout()
    {
        SuspendLayout();

        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.jpg");

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = HeaderBackground,
            Padding = new Padding(24, 18, 24, 18)
        };

        var titleLabel = new Label
        {
            Text = "上海隆深库存管理系统",
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var subtitleLabel = new Label
        {
            Text = "工件入库、货位分配、台账追溯、库存预警一体化管理",
            ForeColor = Color.FromArgb(227, 233, 239),
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(2, 40)
        };

        _lblHomeStatus = new Label
        {
            ForeColor = Color.FromArgb(60, 83, 107),
            BackColor = Color.FromArgb(244, 247, 250),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0),
            Padding = new Padding(14, 7, 14, 7),
            Text = "库存状态：初始化中"
        };

        var titleHost = new Panel
        {
            Dock = DockStyle.Left,
            Width = 900,
            BackColor = Color.Transparent
        };

        var logoBox = new PictureBox
        {
            Location = new Point(0, 2),
            Size = new Size(84, 84),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        if (File.Exists(logoPath))
        {
            logoBox.Image = Image.FromFile(logoPath);
        }

        titleLabel.Location = new Point(102, 0);
        subtitleLabel.Location = new Point(104, 40);

        titleHost.Controls.Add(logoBox);
        titleHost.Controls.Add(titleLabel);
        titleHost.Controls.Add(subtitleLabel);

        var statusHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 320,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 18, 0, 0)
        };
        statusHost.Controls.Add(_lblHomeStatus);

        headerPanel.Controls.Add(statusHost);
        headerPanel.Controls.Add(titleHost);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Padding = new Point(20, 10),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(146, 40)
        };
        _tabControl.DrawItem += TabControl_DrawItem;

        _tabControl.TabPages.Add(CreateHomePage());
        _tabControl.TabPages.Add(CreateInboundPage());
        _tabControl.TabPages.Add(CreateOutboundPage());
        _tabControl.TabPages.Add(CreateSlotPage());
        _tabControl.TabPages.Add(CreateLedgerPage());
        _tabControl.TabPages.Add(CreateReportPage());

        Controls.Add(_tabControl);
        Controls.Add(headerPanel);

        BackColor = AppBackground;
        ApplyControlTheme(this);

        ResumeLayout();
    }

    private TabPage CreateHomePage()
    {
        var page = CreatePage("库存总览");
        var layout = CreatePageLayout();
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 188F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var topSection = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        topSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        topSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var welcomePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AccentSoft,
            Padding = new Padding(18, 10, 18, 10),
            Margin = new Padding(8, 0, 8, 12)
        };
        welcomePanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "库存总览聚焦在库数量、货位占用和预警状态，适合班组快速盘点与日常管控。",
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = TextPrimary
        });

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 12)
        };

        for (var i = 0; i < 4; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        _lblInventoryCount = CreateMetricCard(cards, 0, "库存数量", "0");
        _lblOccupiedCount = CreateMetricCard(cards, 1, "占用库位", "0");
        _lblFreeCount = CreateMetricCard(cards, 2, "空闲库位", "0");
        _lblAlert = CreateMetricCard(cards, 3, "库存预警", "正常");

        topSection.Controls.Add(welcomePanel, 0, 0);
        topSection.Controls.Add(cards, 0, 1);

        var bottomSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760,
            BackColor = Color.WhiteSmoke
        };

        var inventoryPanel = CreateSlotVisualizationPanel("库位可视化监控", isHomePagePanel: true);

        _gridSlots = CreateGrid();
        _gridSlots.DataSource = _slotBinding;

        var slotPanel = CreateCardPanel("货位占用状态");
        slotPanel.Controls.Add(_gridSlots);

        bottomSplit.Panel1.Controls.Add(inventoryPanel);
        bottomSplit.Panel2.Controls.Add(slotPanel);

        layout.Controls.Add(topSection, 0, 0);
        layout.Controls.Add(bottomSplit, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage CreateInboundPage()
    {
        var page = CreatePage("人工入库");
        var split = CreateSplitPage();

        var formPanel = CreateCardPanel("入库信息录入");

        // 外层滚动容器
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(6)
        };

        // 双列表单布局
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(18, 12, 18, 12),
            Width = 860,
            Height = 700,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        for (var i = 0; i < 8; i++)
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));

        _cmbInboundPallet = AddInboundComboBox(form, 0, 0, "托盘号", _state.PalletNumbers);
        _cmbInboundTooling = AddInboundComboBox(form, 0, 1, "工装号", _state.ToolingNumbers);
        _txtInboundOperator = AddInboundField(form, 2, 0, "操作人员", out _);
        _cmbInboundProject = AddInboundComboBox(form, 2, 1, "项目号", _state.ProjectNumbers);
        _txtInboundSlot = AddInboundField(form, 3, 0, "指定货位(可选)", out _);
        _cmbInboundModel = AddInboundComboBox(form, 3, 1, "型号", _state.ModelTypes);

        _txtInboundWorkOrder = AddInboundField(form, 4, 0, "工单号", out _);
        _txtInboundCellNumber = AddInboundField(form, 4, 1, "电解槽编号", out _);

        // 组件节数(1-20)
        _numInboundComponentSections = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            Minimum = 1,
            Maximum = 20,
            Value = 1,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center
        };
        AddInboundControl(form, 5, 0, "组件节数", _numInboundComponentSections);

        _cmbInboundCustomer = AddInboundComboBox(form, 5, 1, "客户名称", _state.CustomerNames);

        // 备注占两列
        _txtInboundNotes = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 12F),
            Multiline = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = ScrollBars.Vertical,
            MinimumSize = new Size(0, 60)
        };
        var notesContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 6)
        };
        notesContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        notesContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        notesContainer.Controls.Add(new Label
        {
            Text = "备注",
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Dock = DockStyle.Fill
        }, 0, 0);
        notesContainer.Controls.Add(_txtInboundNotes, 0, 1);
        form.Controls.Add(notesContainer, 0, 6);
        form.SetColumnSpan(notesContainer, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
            Margin = new Padding(0)
        };

        buttonPanel.Controls.Add(CreateIndustrialActionButton("自动分配入库", (_, _) => HandleInbound(false), true));
        buttonPanel.Controls.Add(CreateIndustrialActionButton("指定货位入库", (_, _) => HandleInbound(true), false));
        buttonPanel.Controls.Add(CreateIndustrialActionButton("清空录入", (_, _) => ClearInboundInputs(), false));

        var buttonHost = new Panel { Dock = DockStyle.Fill };
        buttonHost.Controls.Add(buttonPanel);
        form.Controls.Add(buttonHost, 0, 7);
        form.SetColumnSpan(buttonHost, 2);
        // 让按钮行自适应
        form.RowStyles[7] = new RowStyle(SizeType.Absolute, 60F);

        scrollPanel.Controls.Add(form);
        formPanel.Controls.Add(scrollPanel);

        var rightPanel = CreateCardPanel("待入库 / 最近入库记录");
        _gridInboundPreview = CreateGrid();
        _gridInboundPreview.DataSource = _ledgerBinding;
        rightPanel.Controls.Add(_gridInboundPreview);

        split.Panel1.Controls.Add(formPanel);
        split.Panel2.Controls.Add(rightPanel);
        page.Controls.Add(split);
        return page;
    }

    private TextBox AddInboundField(TableLayoutPanel parent, int row, int col, string label, out Panel container)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            MinimumSize = new Size(0, 42)
        };
        container = AddInboundControl(parent, row, col, label, textBox);
        return textBox;
    }

    private ComboBox AddInboundComboBox(TableLayoutPanel parent, int row, int col, string label, List<string> items)
    {
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems,
            IntegralHeight = false,
            MinimumSize = new Size(0, 42)
        };
        if (items.Count > 0)
        {
            combo.Items.AddRange(items.ToArray());
        }
        AddInboundControl(parent, row, col, label, combo);
        return combo;
    }

    private Panel AddInboundControl(TableLayoutPanel parent, int row, int col, string label, Control control)
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(4, 0, 4, 6)
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        container.Controls.Add(new Label
        {
            Text = label,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Dock = DockStyle.Fill
        }, 0, 0);

        if (control is TextBox tb)
        {
            tb.BackColor = Color.White;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is ComboBox cb)
        {
            cb.FlatStyle = FlatStyle.Flat;
            cb.BackColor = Color.White;
        }

        container.Controls.Add(control, 0, 1);
        parent.Controls.Add(container, col, row);
        return container;
    }

    private TabPage CreateOutboundPage()
    {
        var page = CreatePage("人工出库");
        var split = CreateSplitPage();

        var leftPanel = CreateCardPanel("出库指令下达");
        var form = CreateIndustrialFormLayout();
        _cmbOutboundWorkpiece = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            IntegralHeight = false,
            Height = 52
        };
        AddIndustrialControl(form, 0, "目标工件", _cmbOutboundWorkpiece);

        _txtOutboundOperator = AddIndustrialTextBox(form, 1, "操作人员");
        _txtOutboundSlot = AddIndustrialTextBox(form, 2, "指定货位(可选)");

        var instruction = new Label
        {
            Dock = DockStyle.Fill,
            Text = "支持触摸屏下达取件指令，系统会校验货位并更新出库工位状态。",
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(86, 96, 112),
            Padding = new Padding(0, 10, 0, 0)
        };
        AddIndustrialControl(form, 3, "说明", instruction);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(18, 14, 0, 0)
        };
        buttonPanel.Controls.Add(CreateIndustrialActionButton("执行出库", (_, _) => HandleOutbound(), true));
        buttonPanel.Controls.Add(CreateIndustrialActionButton("刷新可选工件", (_, _) => RefreshOutboundOptions(), false));

        leftPanel.Controls.Add(form);
        leftPanel.Controls.Add(buttonPanel);

        var rightPanel = CreateCardPanel("当前出库队列 / 台账");
        _gridOutboundQueue = CreateGrid();
        _gridOutboundQueue.DataSource = _ledgerBinding;
        rightPanel.Controls.Add(_gridOutboundQueue);

        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightPanel);
        page.Controls.Add(split);
        return page;
    }

    private TabPage CreateSlotPage()
    {
        var page = CreatePage("货位管理");
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 930,
            BackColor = Color.FromArgb(243, 247, 252)
        };

        var visualPanel = CreateSlotVisualizationPanel("库位可视化监控", isHomePagePanel: false);

        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 320,
            BackColor = Color.FromArgb(243, 247, 252)
        };

        var slotPanel = CreateCardPanel("货位实时状态");
        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        _gridSlots = CreateGrid();
        _gridSlots.DataSource = _slotBinding;
        leftLayout.Controls.Add(_gridSlots, 0, 0);

        var slotButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 0, 0)
        };
        slotButtonPanel.Controls.Add(CreateActionButton("自动分配空闲货位", (_, _) => ShowNextAvailableSlot(), true));
        slotButtonPanel.Controls.Add(CreateActionButton("释放选中货位", (_, _) => ReleaseSelectedSlot(), false));
        leftLayout.Controls.Add(slotButtonPanel, 0, 1);
        slotPanel.Controls.Add(leftLayout);

        var inventoryPanel = CreateCardPanel("在库工件明细");
        _gridInventory = CreateGrid();
        _gridInventory.DataSource = _inventoryBinding;
        inventoryPanel.Controls.Add(_gridInventory);

        rightSplit.Panel1.Controls.Add(slotPanel);
        rightSplit.Panel2.Controls.Add(inventoryPanel);

        split.Panel1.Controls.Add(visualPanel);
        split.Panel2.Controls.Add(rightSplit);
        page.Controls.Add(split);
        return page;
    }

    private Panel CreateSlotVisualizationPanel(string title, bool isHomePagePanel)
    {
        var visualPanel = CreateCardPanel(title);
        var visualLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        visualLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        visualLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        visualLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));

        var legendPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4, 6, 0, 0)
        };
        legendPanel.Controls.Add(CreateLegendItem(Color.FromArgb(224, 243, 229), "空闲"));
        legendPanel.Controls.Add(CreateLegendItem(Color.FromArgb(255, 228, 228), "占用"));

        var slotVisualPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        slotVisualPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        slotVisualPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        slotVisualPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        if (isHomePagePanel)
        {
            _homeSlotVisualPanel = slotVisualPanel;
        }
        else
        {
            _slotManagementVisualPanel = slotVisualPanel;
        }

        var detailPanel = CreateSlotDetailPanel();

        visualLayout.Controls.Add(legendPanel, 0, 0);
        visualLayout.Controls.Add(slotVisualPanel, 0, 1);
        visualLayout.Controls.Add(detailPanel, 0, 2);
        visualPanel.Controls.Add(visualLayout);
        return visualPanel;
    }

    private Control CreateLegendItem(Color color, string text)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 18, 0)
        };

        var swatch = new Panel
        {
            Width = 22,
            Height = 22,
            BackColor = color,
            Margin = new Padding(0, 0, 6, 0)
        };

        var label = new Label
        {
            AutoSize = true,
            Text = text,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            Padding = new Padding(0, 2, 0, 0)
        };

        panel.Controls.Add(swatch);
        panel.Controls.Add(label);
        return panel;
    }

    private Control CreateSlotDetailPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(246, 249, 253),
            Padding = new Padding(12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = "选中库位详情",
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 44, 64),
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 3);

        _lblSelectedSlotCode = CreateDetailLabel("库位：未选择");
        _lblSelectedSlotStatus = CreateDetailLabel("状态：- ");
        _lblSelectedSlotLocation = CreateDetailLabel("位置：- ");
        _lblSelectedSlotWorkpiece = CreateDetailLabel("工件：- ");
        _lblSelectedSlotHint = CreateDetailLabel("操作：点击空闲库位可直接带入指定入库，点击占用库位可查看详情。", true);

        layout.Controls.Add(_lblSelectedSlotCode, 0, 1);
        layout.Controls.Add(_lblSelectedSlotStatus, 1, 1);
        layout.Controls.Add(_lblSelectedSlotLocation, 2, 1);
        layout.Controls.Add(_lblSelectedSlotWorkpiece, 0, 2);
        layout.Controls.Add(_lblSelectedSlotHint, 1, 2);
        layout.SetColumnSpan(_lblSelectedSlotHint, 2);

        panel.Controls.Add(layout);
        return panel;
    }

    private Label CreateDetailLabel(string text, bool multiline = false)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = Color.FromArgb(86, 96, 112),
            TextAlign = ContentAlignment.MiddleLeft,
            MaximumSize = multiline ? new Size(0, 0) : Size.Empty
        };
    }

    private TabPage CreateLedgerPage()
    {
        var page = CreatePage("台账查询");
        var layout = CreatePageLayout();
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var queryPanel = CreateCardPanel("查询条件");
        var queryLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(16, 8, 16, 8)
        };

        for (var i = 0; i < 4; i++)
        {
            queryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        queryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        queryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _cmbSearchType = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 10F)
        };
        _cmbSearchType.Items.AddRange(new object[] { "全部类型", "入库", "出库" });
        _cmbSearchType.SelectedIndex = 0;
        AddCompactControl(queryLayout, 0, 0, "出入库类型", _cmbSearchType);

        _txtSearchCode = AddCompactField(queryLayout, 1, 0, "工件编码");
        _txtSearchBatch = AddCompactField(queryLayout, 2, 0, "工单号");
        _txtSearchOperator = AddCompactField(queryLayout, 3, 0, "操作人员");

        _dtSearchStart = AddDateField(queryLayout, 0, 1, "开始时间");
        _dtSearchEnd = AddDateField(queryLayout, 1, 1, "结束时间");

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 6, 0, 0)
        };
        buttonPanel.Controls.Add(CreateActionButton("执行查询", (_, _) => ApplyLedgerFilters(), true));
        buttonPanel.Controls.Add(CreateActionButton("重置条件", (_, _) => ResetLedgerFilters(), false));
        queryLayout.Controls.Add(buttonPanel, 2, 1);
        queryLayout.SetColumnSpan(buttonPanel, 2);

        queryPanel.Controls.Add(queryLayout);

        var resultPanel = CreateCardPanel("进出台账与追溯记录");
        _gridLedger = CreateGrid();
        _gridLedger.DataSource = _ledgerBinding;
        resultPanel.Controls.Add(_gridLedger);

        layout.Controls.Add(queryPanel, 0, 0);
        layout.Controls.Add(resultPanel, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage CreateReportPage()
    {
        var page = CreatePage("报表与预警");
        var split = CreateSplitPage();

        var reportPanel = CreateCardPanel("报表导出 / 打印");
        var reportForm = CreateFormLayout();
        _cmbReportType = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 10F)
        };
        _cmbReportType.Items.AddRange(new object[] { "进出台账", "库存清单", "操作记录" });
        _cmbReportType.SelectedIndex = 0;
        AddLabeledControl(reportForm, 0, "报表类型", _cmbReportType);

        _numMinThreshold = AddNumericField(reportForm, 1, "库存下限");
        _numMaxThreshold = AddNumericField(reportForm, 2, "库存上限");

        var reportButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(16, 10, 0, 0)
        };
        reportButtonPanel.Controls.Add(CreateActionButton("导出 CSV", (_, _) => ExportReport(), true));
        reportButtonPanel.Controls.Add(CreateActionButton("打印预览", (_, _) => PrintReport(), false));
        reportButtonPanel.Controls.Add(CreateActionButton("保存预警阈值", (_, _) => SaveAlertSettings(), false));

        reportPanel.Controls.Add(reportForm);
        reportPanel.Controls.Add(reportButtonPanel);

        var previewPanel = CreateCardPanel("报表预览");
        var previewBox = new RichTextBox
        {
            Name = "ReportPreviewBox",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10.5F),
            BackColor = Color.White
        };
        previewPanel.Controls.Add(previewBox);

        split.Panel1.Controls.Add(reportPanel);
        split.Panel2.Controls.Add(previewPanel);
        page.Controls.Add(split);
        return page;
    }

    private TabPage CreatePage(string title)
    {
        return new TabPage(title)
        {
            BackColor = AppBackground,
            Padding = new Padding(14)
        };
    }

    private TableLayoutPanel CreatePageLayout()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
    }

    private SplitContainer CreateSplitPage()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 460,
            BackColor = AppBackground,
            Padding = new Padding(4)
        };

        split.SplitterWidth = 10;
        return split;
    }

    private Panel CreateCardPanel(string title)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardBackground,
            Padding = new Padding(18, 5, 18, 18),
            Margin = new Padding(8)
        };

        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CardBackground,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var accentBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 5,
            BackColor = AccentColor
        };

        var headerHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = CardBackground
        };

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 34,
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = CardBackground,
            Image = CreateTitleIcon(title)
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = CardBackground
        };

        var divider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = CardBorder
        };

        panel.Paint += CardPanel_Paint;
        panel.ControlAdded += (_, e) =>
        {
            if (e.Control == accentBar || e.Control == headerHost || e.Control == divider || e.Control == contentHost)
            {
                return;
            }

            panel.Controls.Remove(e.Control);
            contentHost.Controls.Add(e.Control);
        };
        panel.Controls.Add(contentHost);
        panel.Controls.Add(divider);
        headerHost.Controls.Add(titleLabel);
        headerHost.Controls.Add(iconBox);
        panel.Controls.Add(headerHost);
        panel.Controls.Add(accentBar);
        return panel;
    }

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = CardBackground,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            GridColor = CardBorder,
            EnableHeadersVisualStyles = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            RowTemplate = { Height = 34 }
        };

        grid.CellFormatting += (_, e) =>
        {
            if (e.Value is null || e.RowIndex < 0)
            {
                return;
            }

            var columnName = grid.Columns[e.ColumnIndex]?.DataPropertyName;

            if (columnName == nameof(StorageSlot.IsOccupied) && e.Value is bool isOccupied)
            {
                e.Value = isOccupied ? "占用" : "空闲";
                e.FormattingApplied = true;
            }
            else if (columnName == nameof(LedgerEntry.Type) && e.Value is TransactionType type)
            {
                e.Value = type switch
                {
                    TransactionType.Inbound => "入库",
                    TransactionType.Outbound => "出库",
                    _ => type.ToString()
                };
                e.FormattingApplied = true;
            }
        };

        grid.DataBindingComplete += (_, _) =>
        {
            var checkboxCols = grid.Columns.Cast<DataGridViewColumn>()
                .Where(c => c is DataGridViewCheckBoxColumn)
                .ToList();

            foreach (var col in checkboxCols)
            {
                var txtCol = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = col.DataPropertyName,
                    HeaderText = col.HeaderText,
                    Name = col.Name,
                    Width = col.Width,
                    FillWeight = col.FillWeight,
                    SortMode = DataGridViewColumnSortMode.Automatic,
                    ValueType = typeof(bool)
                };
                var idx = col.Index;
                grid.Columns.Remove(col);
                grid.Columns.Insert(idx, txtCol);
            }
        };

        grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
        };

        return grid;
    }

    private TableLayoutPanel CreateFormLayout()
    {
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16, 12, 16, 12)
        };

        for (var i = 0; i < 5; i++)
        {
            form.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        }

        return form;
    }

    private TableLayoutPanel CreateIndustrialFormLayout()
    {
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22, 18, 22, 12)
        };

        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        return form;
    }

    private Label CreateMetricCard(TableLayoutPanel parent, int column, string title, string initialValue)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            BackColor = CardBackground,
            Padding = new Padding(18),
            Tag = column
        };

        var accent = new Panel
        {
            Dock = DockStyle.Left,
            Width = 6,
            BackColor = column switch
            {
                0 => Color.FromArgb(34, 104, 148),
                1 => Color.FromArgb(176, 104, 35),
                2 => Color.FromArgb(56, 128, 88),
                _ => Color.FromArgb(187, 74, 57)
            }
        };

        var iconBox = new PictureBox
        {
            Dock = DockStyle.Right,
            Width = 48,
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent,
            Image = CreateMetricIcon(title)
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = TextSecondary,
            Dock = DockStyle.Top,
            Height = 24
        };

        var valueLabel = new Label
        {
            Text = initialValue,
            Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Dock = DockStyle.Fill
        };

        card.Paint += CardPanel_Paint;
    card.Controls.Add(iconBox);
        card.Controls.Add(accent);
        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);
        parent.Controls.Add(card, column, 0);
        return valueLabel;
    }

    private TextBox AddLabeledTextBox(TableLayoutPanel parent, int row, string label)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F),
            BackColor = InputBackground,
            BorderStyle = BorderStyle.FixedSingle
        };
        AddLabeledControl(parent, row, label, textBox);
        return textBox;
    }

    private TextBox AddIndustrialTextBox(TableLayoutPanel parent, int row, string label)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0),
            MinimumSize = new Size(0, 48)
        };
        AddIndustrialControl(parent, row, label, textBox);
        return textBox;
    }

    private TextBox AddLabeledMultilineTextBox(TableLayoutPanel parent, int row, string label)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F),
            Multiline = true,
            Height = 84,
            BackColor = InputBackground,
            BorderStyle = BorderStyle.FixedSingle
        };
        AddLabeledControl(parent, row, label, textBox);
        return textBox;
    }

    private TextBox AddIndustrialMultilineTextBox(TableLayoutPanel parent, int row, string label)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            Multiline = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = ScrollBars.Vertical
        };
        AddIndustrialControl(parent, row, label, textBox);
        return textBox;
    }

    private void AddLabeledControl(TableLayoutPanel parent, int row, string label, Control control)
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 6)
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = label,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextSecondary,
            Dock = DockStyle.Fill
        };

        StyleInputControl(control);

        container.Controls.Add(title, 0, 0);
        container.Controls.Add(control, 0, 1);
        parent.Controls.Add(container, 0, row);
    }

    private void AddIndustrialControl(TableLayoutPanel parent, int row, string label, Control control)
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = label,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Dock = DockStyle.Fill
        };

        StyleIndustrialInputControl(control);

        container.Controls.Add(title, 0, 0);
        container.Controls.Add(control, 0, 1);
        parent.Controls.Add(container, 0, row);
    }

    private TextBox AddCompactField(TableLayoutPanel parent, int column, int row, string label)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        panel.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            ForeColor = TextSecondary
        }, 0, 0);

        var textBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 10F), BackColor = InputBackground, BorderStyle = BorderStyle.FixedSingle };
        panel.Controls.Add(textBox, 0, 1);
        parent.Controls.Add(panel, column, row);
        return textBox;
    }

    private void AddCompactControl(TableLayoutPanel parent, int column, int row, string label, Control control)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        panel.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            ForeColor = TextSecondary
        }, 0, 0);

        control.Dock = DockStyle.Fill;
        StyleInputControl(control);
        panel.Controls.Add(control, 0, 1);
        parent.Controls.Add(panel, column, row);
    }

    private DateTimePicker AddDateField(TableLayoutPanel parent, int column, int row, string label)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = TextSecondary }, 0, 0);
        var picker = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", CalendarMonthBackground = InputBackground };
        panel.Controls.Add(picker, 0, 1);
        parent.Controls.Add(panel, column, row);
        return picker;
    }

    private NumericUpDown AddNumericField(TableLayoutPanel parent, int row, string label)
    {
        var numeric = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F),
            Minimum = 0,
            Maximum = 999,
            Value = 2,
            BackColor = InputBackground,
            BorderStyle = BorderStyle.FixedSingle
        };
        AddLabeledControl(parent, row, label, numeric);
        return numeric;
    }

    private Button CreateActionButton(string text, EventHandler clickHandler, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(16, 8, 16, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? HeaderBackground : CardBackground,
            ForeColor = primary ? Color.White : TextPrimary,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Margin = new Padding(0, 0, 10, 0),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        button.FlatAppearance.BorderColor = primary ? HeaderBackground : CardBorder;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(15, 48, 77) : Color.FromArgb(242, 238, 232);
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(27, 77, 121) : Color.FromArgb(247, 244, 238);
        button.Image = CreateButtonIcon(text, primary);
        button.Click += clickHandler;
        return button;
    }

    private Button CreateIndustrialActionButton(string text, EventHandler clickHandler, bool primary)
    {
        var button = CreateActionButton(text, clickHandler, primary);
        button.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
        button.Padding = new Padding(22, 10, 22, 10);
        button.Height = 50;
        button.MinimumSize = new Size(158, 50);
        return button;
    }

    private Image CreateTitleIcon(string title)
    {
        var glyph = title switch
        {
            var value when value.Contains("库存") => "📦",
            var value when value.Contains("入库") => "📥",
            var value when value.Contains("出库") => "📤",
            var value when value.Contains("货位") => "🗄️",
            var value when value.Contains("查询") => "🔍",
            var value when value.Contains("报表") => "📊",
            _ => "📋"
        };

        return CreateGlyphBadgeIcon(glyph, 14F, AccentSoft, AccentColor, Color.WhiteSmoke);
    }

    private Image CreateMetricIcon(string title)
    {
        return title switch
        {
            "库存数量" => CreateGlyphBadgeIcon("📦", 15F, Color.FromArgb(226, 237, 247), Color.FromArgb(34, 104, 148), Color.WhiteSmoke),
            "占用库位" => CreateGlyphBadgeIcon("🔴", 15F, Color.FromArgb(248, 232, 211), Color.FromArgb(176, 104, 35), Color.WhiteSmoke),
            "空闲库位" => CreateGlyphBadgeIcon("🟢", 15F, Color.FromArgb(225, 241, 231), Color.FromArgb(56, 128, 88), Color.WhiteSmoke),
            _ => CreateGlyphBadgeIcon("⚠️", 15F, Color.FromArgb(248, 227, 224), Color.FromArgb(187, 74, 57), Color.WhiteSmoke)
        };
    }

    private Image CreateButtonIcon(string text, bool primary)
    {
        var glyph = text switch
        {
            var value when value.Contains("导出") => "📥",
            var value when value.Contains("打印") => "🖨️",
            var value when value.Contains("保存") => "💾",
            var value when value.Contains("刷新") => "🔄",
            var value when value.Contains("查询") => "🔍",
            var value when value.Contains("重置") => "🔄",
            var value when value.Contains("清空") => "🗑️",
            var value when value.Contains("释放") => "🔓",
            var value when value.Contains("执行") => "▶️",
            _ => "📋"
        };

        return CreateGlyphBadgeIcon(
            glyph,
            12.5F,
            primary ? Color.FromArgb(32, 85, 132) : AccentSoft,
            primary ? Color.FromArgb(255, 244, 224) : AccentColor,
            primary ? Color.White : Color.WhiteSmoke);
    }

    private Image CreateTabIcon(string title, bool selected)
    {
        var glyph = title switch
        {
            "库存总览" => "📊",
            "人工入库" => "📥",
            "人工出库" => "📤",
            "货位管理" => "🗄️",
            "台账查询" => "🔍",
            "报表与预警" => "📊",
            _ => "📋"
        };

        return CreateGlyphBadgeIcon(
            glyph,
            11.5F,
            selected ? Color.FromArgb(234, 221, 199) : Color.FromArgb(214, 220, 214),
            selected ? AccentColor : Color.FromArgb(117, 130, 139),
            Color.WhiteSmoke);
    }

    private Bitmap CreateGlyphBadgeIcon(string glyph, float glyphFontSize, Color badgeColor, Color innerColor, Color glyphColor)
    {
        var bitmap = new Bitmap(28, 28);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);

        using var badgeBrush = new SolidBrush(badgeColor);
        graphics.FillEllipse(badgeBrush, 0, 0, 28, 28);

        using var iconBackBrush = new SolidBrush(innerColor);
        using var glyphBrush = new SolidBrush(glyphColor);
        using var glyphFont = new Font("Segoe UI Emoji", glyphFontSize, FontStyle.Regular, GraphicsUnit.Point);
        graphics.FillEllipse(iconBackBrush, 5, 5, 18, 18);
        var rect = new RectangleF(5, 5, 18, 18);
        using var stringFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(glyph, glyphFont, glyphBrush, rect, stringFormat);
        return bitmap;
    }

    private void StyleInputControl(Control control)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.BackColor = InputBackground;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox comboBox:
                comboBox.BackColor = InputBackground;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;
            case DateTimePicker picker:
                picker.CalendarMonthBackground = InputBackground;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = InputBackground;
                numeric.BorderStyle = BorderStyle.FixedSingle;
                break;
            case RichTextBox richTextBox:
                richTextBox.BackColor = InputBackground;
                richTextBox.BorderStyle = BorderStyle.FixedSingle;
                break;
        }
    }

    private void StyleIndustrialInputControl(Control control)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.BackColor = Color.White;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox comboBox:
                comboBox.BackColor = Color.White;
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
                break;
            case Label label:
                label.BackColor = InputBackground;
                label.Padding = new Padding(10, 8, 10, 8);
                break;
        }
    }

    private void ApplyControlTheme(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case SplitContainer splitContainer:
                    splitContainer.BackColor = AppBackground;
                    splitContainer.Panel1.BackColor = AppBackground;
                    splitContainer.Panel2.BackColor = AppBackground;
                    break;
                case GroupBox groupBox:
                    groupBox.ForeColor = TextPrimary;
                    groupBox.BackColor = CardBackground;
                    break;
                case FlowLayoutPanel flowLayoutPanel:
                    if (flowLayoutPanel.Parent is not TabPage)
                    {
                        flowLayoutPanel.BackColor = Color.Transparent;
                    }
                    break;
                case DataGridView grid:
                    StyleGrid(grid);
                    break;
            }

            StyleInputControl(control);
            ApplyControlTheme(control);
        }
    }

    private void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = CardBackground;
        grid.DefaultCellStyle.BackColor = CardBackground;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 232, 243);
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9.5F);
        grid.AlternatingRowsDefaultCellStyle.BackColor = GridAltRow;
        grid.ColumnHeadersVisible = true;
        grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        grid.ColumnHeadersHeight = 38;
    }

    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabControl || e.Index < 0)
        {
            return;
        }

        var tabPage = tabControl.TabPages[e.Index];
        var bounds = e.Bounds;
        var isSelected = e.State.HasFlag(DrawItemState.Selected);
        using var backBrush = new SolidBrush(isSelected ? CardBackground : Color.FromArgb(226, 231, 227));
        using var textBrush = new SolidBrush(isSelected ? TextPrimary : TextSecondary);
        using var accentBrush = new SolidBrush(isSelected ? AccentColor : Color.Transparent);
        using var borderPen = new Pen(isSelected ? AccentColor : CardBorder);

        e.Graphics.FillRectangle(backBrush, bounds);
        e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        if (isSelected)
        {
            e.Graphics.FillRectangle(accentBrush, new Rectangle(bounds.X, bounds.Bottom - 4, bounds.Width, 4));
        }

        var tabIcon = CreateTabIcon(tabPage.Text, isSelected);
        var iconRect = new Rectangle(bounds.X + 16, bounds.Y + 7, 24, 24);
        e.Graphics.DrawImage(tabIcon, iconRect);
        var textRect = new Rectangle(bounds.X + 44, bounds.Y, bounds.Width - 52, bounds.Height - 2);

        TextRenderer.DrawText(
            e.Graphics,
            tabPage.Text,
            new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            textRect,
            textBrush.Color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void CardPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel)
        {
            return;
        }

        using var pen = new Pen(CardBorder);
        e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
    }

    private void ConfigurePrint()
    {
        _printDocument.PrintPage += (_, eventArgs) =>
        {
            if (eventArgs.Graphics is null)
            {
                return;
            }

            using var font = new Font("Consolas", 10F);
            var y = eventArgs.MarginBounds.Top;
            foreach (var line in _lastPrintLines)
            {
                eventArgs.Graphics.DrawString(line, font, Brushes.Black, eventArgs.MarginBounds.Left, y);
                y += 22;
            }
        };
    }

    private void LoadState()
    {
        _state = _repository.Load();
        BindState();
        RefreshAllViews();
    }

    private void BindState()
    {
        ResetBinding(_inventoryBinding, _state.Inventory.OrderByDescending(item => item.InboundTime));
        ResetBinding(_slotBinding, _state.Slots.OrderBy(slot => slot.RowNumber).ThenBy(slot => slot.ColumnNumber).ThenBy(slot => slot.LevelNumber));
        ResetBinding(_ledgerBinding, _state.Ledger.OrderByDescending(item => item.Timestamp));
        _numMinThreshold.Value = _state.AlertSettings.MinThreshold;
        _numMaxThreshold.Value = _state.AlertSettings.MaxThreshold;
        RefreshOutboundOptions();
        RefreshComboSource(_cmbInboundPallet, _state.PalletNumbers);
        RefreshComboSource(_cmbInboundTooling, _state.ToolingNumbers);
        RefreshComboSource(_cmbInboundProject, _state.ProjectNumbers);
        RefreshComboSource(_cmbInboundModel, _state.ModelTypes);
        RefreshComboSource(_cmbInboundCustomer, _state.CustomerNames);
    }

    private static void RefreshComboSource(ComboBox combo, List<string> items)
    {
        var currentText = combo.Text;
        combo.Items.Clear();
        if (items.Count > 0)
        {
            combo.Items.AddRange(items.ToArray());
        }
        combo.Text = currentText;
    }

    private static void ResetBinding<T>(BindingList<T> binding, IEnumerable<T> items)
    {
        binding.Clear();
        foreach (var item in items)
        {
            binding.Add(item);
        }
    }

    private void HandleInbound(bool useSpecifiedSlot)
    {
        var operatorName = _txtInboundOperator.Text.Trim();
        var requestedSlot = _txtInboundSlot.Text.Trim();

        if (string.IsNullOrWhiteSpace(operatorName))
        {
            MessageBox.Show("请输入操作人员。", "入库校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var targetSlot = ResolveTargetSlot(useSpecifiedSlot ? requestedSlot : null);
        if (targetSlot is null)
        {
            MessageBox.Show(useSpecifiedSlot ? "指定货位不可用。" : "当前无空闲货位可分配。", "货位分配", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 收集新字段
        var palletNumber = _cmbInboundPallet.Text.Trim();
        var toolingNumber = _cmbInboundTooling.Text.Trim();
        var projectNumber = _cmbInboundProject.Text.Trim();
        var modelType = _cmbInboundModel.Text.Trim();
        var workOrder = _txtInboundWorkOrder.Text.Trim();
        var cellNumber = _txtInboundCellNumber.Text.Trim();
        var componentSections = (int)_numInboundComponentSections.Value;
        var customerName = _cmbInboundCustomer.Text.Trim();

        // 将新输入的值保存到下拉选项中（可拓展）
        SaveNewComboItem(_state.PalletNumbers, palletNumber);
        SaveNewComboItem(_state.ToolingNumbers, toolingNumber);
        SaveNewComboItem(_state.ProjectNumbers, projectNumber);
        SaveNewComboItem(_state.ModelTypes, modelType);
        SaveNewComboItem(_state.CustomerNames, customerName);

        var record = new WorkpieceRecord
        {
            PalletNumber = palletNumber,
            ToolingNumber = toolingNumber,
            ProjectNumber = projectNumber,
            ModelType = modelType,
            WorkOrder = workOrder,
            CellNumber = cellNumber,
            ComponentSections = componentSections,
            CustomerName = customerName,
            SlotCode = targetSlot.SlotCode,
            InboundTime = DateTime.Now,
            LastOperator = operatorName,
            LastUpdated = DateTime.Now,
            Notes = _txtInboundNotes.Text.Trim()
        };

        targetSlot.IsOccupied = true;
        targetSlot.WorkpieceId = record.Id;
        _state.Inventory.Add(record);
        AddLedgerEntry(TransactionType.Inbound, record, operatorName, $"托盘{record.PalletNumber}入库至 {targetSlot.SlotCode}");
        SaveAndRefresh();
        ClearInboundInputs();
    }

    private static void SaveNewComboItem(List<string> items, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !items.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(value);
        }
    }

    private void HandleOutbound()
    {
        if (_cmbOutboundWorkpiece.SelectedItem is not WorkpieceRecord record)
        {
            MessageBox.Show("请先选择需要出库的工件。", "出库校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var operatorName = _txtOutboundOperator.Text.Trim();
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            MessageBox.Show("请输入操作人员。", "出库校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var specifiedSlot = _txtOutboundSlot.Text.Trim();
        if (!string.IsNullOrWhiteSpace(specifiedSlot) && !record.SlotCode.Equals(specifiedSlot, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("指定货位与工件当前所在货位不一致。", "出库校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var slot = _state.Slots.FirstOrDefault(item => item.SlotCode == record.SlotCode);
        if (slot is null)
        {
            MessageBox.Show("未找到对应货位。", "出库失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        slot.IsOccupied = false;
        slot.WorkpieceId = null;
        _state.Inventory.Remove(record);
        AddLedgerEntry(TransactionType.Outbound, record, operatorName, $"工件出库至取件工位：{record.SlotCode}");
        SaveAndRefresh();
        _txtOutboundSlot.Clear();
        _txtOutboundOperator.Clear();
    }

    private StorageSlot? ResolveTargetSlot(string? specifiedSlot)
    {
        if (!string.IsNullOrWhiteSpace(specifiedSlot))
        {
            return _state.Slots.FirstOrDefault(slot => slot.SlotCode.Equals(specifiedSlot, StringComparison.OrdinalIgnoreCase) && !slot.IsOccupied);
        }

        return _state.Slots.FirstOrDefault(slot => !slot.IsOccupied);
    }

    private void AddLedgerEntry(TransactionType type, WorkpieceRecord record, string operatorName, string description)
    {
        _state.Ledger.Add(new LedgerEntry
        {
            Type = type,
            Timestamp = DateTime.Now,
            OperatorName = operatorName,
            PalletNumber = record.PalletNumber,
            ToolingNumber = record.ToolingNumber,
            ProjectNumber = record.ProjectNumber,
            ModelType = record.ModelType,
            WorkOrder = record.WorkOrder,
            CellNumber = record.CellNumber,
            ComponentSections = record.ComponentSections,
            CustomerName = record.CustomerName,
            SlotCode = record.SlotCode,
            ActionDescription = description
        });
    }

    private void SaveAndRefresh()
    {
        _repository.Save(_state);
        BindState();
        RefreshAllViews();
    }

    private void RefreshAllViews()
    {
        _lblInventoryCount.Text = _state.Inventory.Count.ToString();
        _lblOccupiedCount.Text = _state.Slots.Count(slot => slot.IsOccupied).ToString();
        _lblFreeCount.Text = _state.Slots.Count(slot => !slot.IsOccupied).ToString();
        _lblAlert.Text = BuildInventoryAlertText();
        _lblAlert.ForeColor = IsInventoryInAlert() ? Color.FromArgb(186, 40, 40) : Color.FromArgb(18, 66, 110);
        _lblHomeStatus.Text = $"库存状态：数据更新于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        RenderSlotVisualization(_homeSlotVisualPanel);
        RenderSlotVisualization(_slotManagementVisualPanel);
        UpdateSelectedSlotDetails();
        UpdateReportPreview();
    }

    private void RenderSlotVisualization(TableLayoutPanel? targetPanel)
    {
        if (targetPanel is null)
        {
            return;
        }

        targetPanel.SuspendLayout();
        targetPanel.Controls.Clear();
        targetPanel.ColumnStyles.Clear();
        targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        targetPanel.RowStyles.Clear();
        targetPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var row = 1; row <= 2; row++)
        {
            var groupBox = new GroupBox
            {
                Text = $"第 {row} 排",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                Padding = new Padding(12),
                Margin = row == 1 ? new Padding(0, 0, 8, 0) : new Padding(8, 0, 0, 0),
                MinimumSize = new Size(380, 0)
            };

            groupBox.Controls.Add(CreateRackFrontView(row));
            targetPanel.Controls.Add(groupBox, row - 1, 0);
        }

        targetPanel.ResumeLayout();
    }

    private Control CreateRackFrontView(int row)
    {
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0, 2, 0, 0)
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        var axisLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "层/列",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 8.5F),
            ForeColor = Color.FromArgb(86, 96, 112)
        };

        var columnHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0)
        };
        for (var column = 0; column < 4; column++)
        {
            columnHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            columnHeader.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = $"{column + 1} 列",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 70, 96)
            }, column, 0);
        }

        var levelHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Margin = new Padding(0)
        };
        for (var levelIndex = 0; levelIndex < 8; levelIndex++)
        {
            levelHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
            var levelNumber = 8 - levelIndex;
            levelHeader.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = $"{levelNumber}层",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = Color.FromArgb(86, 96, 112)
            }, 0, levelIndex);
        }

        var rackGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 8,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            BackColor = Color.FromArgb(189, 202, 219),
            Margin = new Padding(0)
        };

        for (var column = 0; column < 4; column++)
        {
            rackGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        for (var level = 0; level < 8; level++)
        {
            rackGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        }

        if (_state?.Slots is null)
        {
            return outer;
        }

        for (var visualLevel = 8; visualLevel >= 1; visualLevel--)
        {
            var rowIndex = 8 - visualLevel;
            for (var column = 1; column <= 4; column++)
            {
                var slot = _state.Slots.FirstOrDefault(item => item.RowNumber == row && item.ColumnNumber == column && item.LevelNumber == visualLevel);
                if (slot is null)
                {
                    continue;
                }

                rackGrid.Controls.Add(CreateSlotTile(slot), column - 1, rowIndex);
            }
        }

        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(223, 230, 238),
            Margin = new Padding(0, 6, 0, 0)
        };
        footer.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = row == 1 ? "入/出库巷道前视图 - 一排货架" : "入/出库巷道前视图 - 二排货架",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 70, 96)
        });

        outer.Controls.Add(axisLabel, 0, 0);
        outer.Controls.Add(columnHeader, 1, 0);
        outer.Controls.Add(levelHeader, 0, 1);
        outer.Controls.Add(rackGrid, 1, 1);
        outer.Controls.Add(footer, 0, 2);
        outer.SetColumnSpan(footer, 2);
        return outer;
    }

    private Control CreateSlotTile(StorageSlot slot)
    {
        var isSelected = _selectedVisualSlot?.SlotCode == slot.SlotCode;
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(1),
            BackColor = isSelected ? Color.FromArgb(255, 244, 197) : slot.IsOccupied ? Color.FromArgb(255, 228, 228) : Color.FromArgb(224, 243, 229),
            Padding = new Padding(4, 3, 4, 3),
            Cursor = Cursors.Hand,
            BorderStyle = isSelected ? BorderStyle.FixedSingle : BorderStyle.None,
            Tag = slot
        };
        panel.Paint += (_, e) => PaintSlotTile(panel, e, slot, isSelected);

        var codeLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 12,
            Text = string.Empty,
            Font = new Font("Microsoft YaHei UI", 7F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 44, 64),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = slot.IsOccupied ? "占用" : "空闲",
            Font = new Font("Microsoft YaHei UI", 8.2F, FontStyle.Bold),
            ForeColor = slot.IsOccupied ? Color.FromArgb(175, 36, 36) : Color.FromArgb(30, 112, 52),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var workpiece = _state.Inventory?.FirstOrDefault(item => item.Id == slot.WorkpieceId);
        var detailLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 12,
            Text = workpiece?.PalletNumber ?? string.Empty,
            Font = new Font("Microsoft YaHei UI", 7F),
            ForeColor = Color.FromArgb(86, 96, 112),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };

        _slotToolTip.SetToolTip(panel, BuildSlotTooltip(slot, workpiece));
        _slotToolTip.SetToolTip(codeLabel, BuildSlotTooltip(slot, workpiece));
        _slotToolTip.SetToolTip(statusLabel, BuildSlotTooltip(slot, workpiece));
        _slotToolTip.SetToolTip(detailLabel, BuildSlotTooltip(slot, workpiece));

        BindSlotTileClick(panel, slot);
        BindSlotTileClick(codeLabel, slot);
        BindSlotTileClick(statusLabel, slot);
        BindSlotTileClick(detailLabel, slot);

        panel.Controls.Add(statusLabel);
        panel.Controls.Add(detailLabel);
        panel.Controls.Add(codeLabel);
        return panel;
    }

    private void PaintSlotTile(Panel panel, PaintEventArgs e, StorageSlot slot, bool isSelected)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var outerPen = new Pen(isSelected ? Color.FromArgb(189, 123, 31) : slot.IsOccupied ? Color.FromArgb(196, 102, 88) : Color.FromArgb(104, 156, 124), isSelected ? 2.2F : 1.3F);
        e.Graphics.DrawRectangle(outerPen, 1, 1, panel.Width - 3, panel.Height - 3);

        var dotColor = isSelected
            ? Color.FromArgb(189, 123, 31)
            : slot.IsOccupied
                ? Color.FromArgb(196, 72, 72)
                : Color.FromArgb(56, 150, 96);

        using var dotBrush = new SolidBrush(dotColor);
        e.Graphics.FillEllipse(dotBrush, 5, 4, 7, 7);

        var badgeText = isSelected ? "选" : slot.IsOccupied ? "占" : "空";
        var badgeBack = isSelected ? Color.FromArgb(255, 236, 180) : slot.IsOccupied ? Color.FromArgb(255, 214, 214) : Color.FromArgb(214, 242, 222);
        var badgeRect = new Rectangle(panel.Width - 20, 3, 16, 12);
        using var badgeBrush = new SolidBrush(badgeBack);
        using var badgePen = new Pen(dotColor);
        using var badgeFont = new Font("Microsoft YaHei UI", 6.6F, FontStyle.Bold);
        using var badgeTextBrush = new SolidBrush(dotColor);
        using var badgePath = CreateRoundedRectanglePath(badgeRect, 3);

        e.Graphics.FillPath(badgeBrush, badgePath);
        e.Graphics.DrawPath(badgePen, badgePath);
        TextRenderer.DrawText(e.Graphics, badgeText, badgeFont, badgeRect, badgeTextBrush.Color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    private string BuildSlotTooltip(StorageSlot slot, WorkpieceRecord? workpiece)
    {
        return workpiece is null
            ? $"{slot.SlotCode}\n状态：空闲\n点击后可直接指定入库"
            : $"{slot.SlotCode}\n状态：占用\n托盘：{workpiece.PalletNumber}\n工单：{workpiece.WorkOrder}";
    }

    private void BindSlotTileClick(Control control, StorageSlot slot)
    {
        control.Cursor = Cursors.Hand;
        control.Click += (_, _) => HandleSlotTileClick(slot);
    }

    private void HandleSlotTileClick(StorageSlot slot)
    {
        if (slot.IsOccupied)
        {
            _selectedVisualSlot = slot;
            SelectSlotInGrid(slot.SlotCode);
            UpdateSelectedSlotDetails();
            RenderSlotVisualization(_tabControl.SelectedTab?.Text == "库存总览" ? _homeSlotVisualPanel : _slotManagementVisualPanel);
            ShowSlotDetails(slot);
            return;
        }

        _selectedVisualSlot = slot;
        _txtInboundSlot.Text = slot.SlotCode;
        _tabControl.SelectedTab = _tabControl.TabPages.Cast<TabPage>().FirstOrDefault(page => page.Text == "人工入库");
        BeginInvoke(() => _cmbInboundPallet.Focus());
    }

    private void UpdateSelectedSlotDetails()
    {
        if (_lblSelectedSlotCode is null)
        {
            return;
        }

        if (_selectedVisualSlot is null)
        {
            _lblSelectedSlotCode.Text = "库位：未选择";
            _lblSelectedSlotStatus.Text = "状态：-";
            _lblSelectedSlotLocation.Text = "位置：-";
            _lblSelectedSlotWorkpiece.Text = "工件：-";
            _lblSelectedSlotHint.Text = "操作：点击空闲库位可直接带入指定入库，点击占用库位可查看详情。";
            return;
        }

        var slot = _state.Slots.FirstOrDefault(item => item.SlotCode == _selectedVisualSlot.SlotCode) ?? _selectedVisualSlot;
        var workpiece = _state.Inventory.FirstOrDefault(item => item.Id == slot.WorkpieceId);
        _lblSelectedSlotCode.Text = $"库位：{slot.SlotCode}";
        _lblSelectedSlotStatus.Text = $"状态：{(slot.IsOccupied ? "占用" : "空闲")}";
        _lblSelectedSlotLocation.Text = $"位置：{slot.RowNumber}排 / {slot.ColumnNumber}列 / {slot.LevelNumber}层";
        _lblSelectedSlotWorkpiece.Text = $"托盘：{workpiece?.PalletNumber ?? "无"}";
        _lblSelectedSlotHint.Text = slot.IsOccupied
            ? "操作：该库位已占用，点击库位会弹出工件详情，可用于核对位置与追溯。"
            : "操作：该库位空闲，点击库位后系统会自动跳转到人工入库页并带入指定货位。";
    }

    private void ShowSlotDetails(StorageSlot slot)
    {
        var workpiece = _state.Inventory.FirstOrDefault(item => item.Id == slot.WorkpieceId);
        var detailLines = new[]
        {
            $"库位号：{slot.SlotCode}",
            $"货架位置：{slot.RowNumber}排 {slot.ColumnNumber}列 {slot.LevelNumber}层",
            $"状态：{(slot.IsOccupied ? "占用" : "空闲")}",
            $"托盘号：{workpiece?.PalletNumber ?? "无"}",
            $"工单号：{workpiece?.WorkOrder ?? "无"}",
            $"托盘号：{workpiece?.PalletNumber ?? "无"}",
            $"工装号：{workpiece?.ToolingNumber ?? "无"}",
            $"项目号：{workpiece?.ProjectNumber ?? "无"}",
            $"型号：{workpiece?.ModelType ?? "无"}",
            $"工单号：{workpiece?.WorkOrder ?? "无"}",
            $"电解槽编号：{workpiece?.CellNumber ?? "无"}",
            $"组件节数：{workpiece?.ComponentSections ?? 0}",
            $"客户名称：{workpiece?.CustomerName ?? "无"}",
            $"入库时间：{(workpiece is null ? "无" : workpiece.InboundTime.ToString("yyyy-MM-dd HH:mm:ss"))}",
            $"操作人员：{workpiece?.LastOperator ?? "无"}",
            $"备注：{workpiece?.Notes ?? "无"}"
        };

        MessageBox.Show(string.Join(Environment.NewLine, detailLines), "库位详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SelectSlotInGrid(string slotCode)
    {
        foreach (DataGridViewRow row in _gridSlots.Rows)
        {
            if (row.DataBoundItem is StorageSlot slot && slot.SlotCode == slotCode)
            {
                row.Selected = true;
                _gridSlots.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private string BuildInventoryAlertText()
    {
        if (_state.Inventory.Count < _state.AlertSettings.MinThreshold)
        {
            return "低于下限";
        }

        if (_state.Inventory.Count > _state.AlertSettings.MaxThreshold)
        {
            return "高于上限";
        }

        return "正常";
    }

    private bool IsInventoryInAlert()
    {
        return _state.Inventory.Count < _state.AlertSettings.MinThreshold || _state.Inventory.Count > _state.AlertSettings.MaxThreshold;
    }

    private void RefreshOutboundOptions()
    {
        var available = _state.Inventory.OrderBy(item => item.PalletNumber).ToList();
        _cmbOutboundWorkpiece.DataSource = null;
        _cmbOutboundWorkpiece.DataSource = available;
        _cmbOutboundWorkpiece.DisplayMember = nameof(WorkpieceRecord.PalletNumber);
    }

    private void ClearInboundInputs()
    {
        _txtInboundOperator.Clear();
        _txtInboundSlot.Clear();
        // 下拉框不清除文本，重置到空
        _cmbInboundPallet.SelectedIndex = -1;
        _cmbInboundTooling.SelectedIndex = -1;
        _cmbInboundProject.SelectedIndex = -1;
        _cmbInboundModel.SelectedIndex = -1;
        // 批次入库保留工单号和电解槽编号
        // _txtInboundWorkOrder.Clear();
        // _txtInboundCellNumber.Clear();
        _numInboundComponentSections.Value = 1;
        _cmbInboundCustomer.SelectedIndex = -1;
        _txtInboundNotes.Clear();
    }

    private void ApplyLedgerFilters()
    {
        TransactionType? typeFilter = _cmbSearchType.SelectedIndex switch
        {
            1 => TransactionType.Inbound,
            2 => TransactionType.Outbound,
            _ => null
        };

        var filtered = _state.Ledger.Where(entry =>
            (typeFilter is null || entry.Type == typeFilter.Value) &&
            (string.IsNullOrWhiteSpace(_txtSearchCode.Text) || entry.PalletNumber.Contains(_txtSearchCode.Text.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(_txtSearchBatch.Text) || entry.WorkOrder.Contains(_txtSearchBatch.Text.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(_txtSearchOperator.Text) || entry.OperatorName.Contains(_txtSearchOperator.Text.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            entry.Timestamp >= _dtSearchStart.Value &&
            entry.Timestamp <= _dtSearchEnd.Value)
            .OrderByDescending(entry => entry.Timestamp)
            .ToList();

        ResetBinding(_ledgerBinding, filtered);
    }

    private void ResetLedgerFilters()
    {
        _cmbSearchType.SelectedIndex = 0;
        _txtSearchCode.Clear();
        _txtSearchBatch.Clear();
        _txtSearchOperator.Clear();
        _dtSearchStart.Value = DateTime.Today.AddDays(-7);
        _dtSearchEnd.Value = DateTime.Now;
        ResetBinding(_ledgerBinding, _state.Ledger.OrderByDescending(entry => entry.Timestamp));
    }

    private void ExportReport()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"{_cmbReportType.Text}-{DateTime.Now:yyyyMMddHHmmss}.csv"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var lines = BuildReportLines();
        File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
        UpdateReportPreview();
        MessageBox.Show("报表导出成功。", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void PrintReport()
    {
        _lastPrintLines = BuildReportLines();
        using var previewDialog = new PrintPreviewDialog
        {
            Document = _printDocument,
            Width = 1100,
            Height = 760
        };
        previewDialog.ShowDialog();
    }

    private void SaveAlertSettings()
    {
        if (_numMinThreshold.Value > _numMaxThreshold.Value)
        {
            MessageBox.Show("库存下限不能大于上限。", "参数校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _state.AlertSettings.MinThreshold = (int)_numMinThreshold.Value;
        _state.AlertSettings.MaxThreshold = (int)_numMaxThreshold.Value;
        SaveAndRefresh();
    }

    private string[] BuildReportLines()
    {
        return _cmbReportType.Text switch
        {
            "库存清单" => BuildInventoryReportLines(),
            "操作记录" => BuildOperationReportLines(),
            _ => BuildLedgerReportLines()
        };
    }

    private string[] BuildLedgerReportLines()
    {
        var lines = new List<string> { "时间,类型,操作人员,托盘号,工装号,项目号,型号,工单号,电解槽编号,组件节数,客户名称,货位,说明" };
        lines.AddRange(_state.Ledger.OrderByDescending(entry => entry.Timestamp).Select(entry =>
            $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.Type},{entry.OperatorName},{entry.PalletNumber},{entry.ToolingNumber},{entry.ProjectNumber},{entry.ModelType},{entry.WorkOrder},{entry.CellNumber},{entry.ComponentSections},{entry.CustomerName},{entry.SlotCode},{entry.ActionDescription}"));
        return lines.ToArray();
    }

    private string[] BuildInventoryReportLines()
    {
        var lines = new List<string> { "托盘号,工装号,项目号,型号,工单号,电解槽编号,组件节数,客户名称,入库时间,货位,操作人员,备注" };
        lines.AddRange(_state.Inventory.OrderBy(item => item.SlotCode).Select(item =>
            $"{item.PalletNumber},{item.ToolingNumber},{item.ProjectNumber},{item.ModelType},{item.WorkOrder},{item.CellNumber},{item.ComponentSections},{item.CustomerName},{item.InboundTime:yyyy-MM-dd HH:mm:ss},{item.SlotCode},{item.LastOperator},{item.Notes}"));
        return lines.ToArray();
    }

    private string[] BuildOperationReportLines()
    {
        var lines = new List<string> { "时间,操作人员,托盘号,工装号,项目号,型号,工单号,电解槽编号,客户名称,货位,操作说明" };
        lines.AddRange(_state.Ledger.OrderByDescending(entry => entry.Timestamp).Select(entry =>
            $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.OperatorName},{entry.PalletNumber},{entry.ToolingNumber},{entry.ProjectNumber},{entry.ModelType},{entry.WorkOrder},{entry.CellNumber},{entry.CustomerName},{entry.SlotCode},{entry.ActionDescription}"));
        return lines.ToArray();
    }

    private void UpdateReportPreview()
    {
        if (_tabControl.SelectedTab is null)
        {
            return;
        }

        var reportPage = _tabControl.TabPages.Cast<TabPage>().FirstOrDefault(page => page.Text == "报表与预警");
        var previewBox = reportPage?.Controls.Find("ReportPreviewBox", true).OfType<RichTextBox>().FirstOrDefault();
        if (previewBox is null)
        {
            return;
        }

        previewBox.Text = string.Join(Environment.NewLine, BuildReportLines());
    }

    private void ShowNextAvailableSlot()
    {
        var slot = _state.Slots.FirstOrDefault(item => !item.IsOccupied);
        MessageBox.Show(slot is null ? "当前没有空闲货位。" : $"推荐空闲货位：{slot.SlotCode}", "货位分配", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ReleaseSelectedSlot()
    {
        if (_gridSlots.CurrentRow?.DataBoundItem is not StorageSlot slot)
        {
            MessageBox.Show("请先选择一个货位。", "货位管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (slot.IsOccupied)
        {
            MessageBox.Show("选中货位当前有工件占用，不能直接释放。", "货位管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        slot.WorkpieceId = null;
        SaveAndRefresh();
    }

}
