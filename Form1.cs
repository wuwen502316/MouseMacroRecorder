using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Forms;

namespace MouseMacroRecorder
{
    // ==========================================
    // 1. 数据模型
    // ==========================================
    public enum MacroActionType
    {
        MouseMove, MouseClick, MouseWheel, 
        KeyPress, TextInput, Shortcut, Wait
    }

    public class MacroAction
    {
        public MacroActionType Type { get; set; }
        public int Delay { get; set; } = 0;

        public int X { get; set; }
        public int Y { get; set; }
        public string MouseButton { get; set; } = "Left";
        public bool IsDoubleClick { get; set; }
        public string Modifiers { get; set; } = ""; 
        public int ScrollDelta { get; set; } 

        public string KeyCode { get; set; } = ""; 
        public string TextContent { get; set; } = ""; 

        public string GetParamDescription()
        {
            switch (Type)
            {
                case MacroActionType.MouseMove: return $"移动到 (X:{X}, Y:{Y})";
                case MacroActionType.MouseClick:
                    string clickType = IsDoubleClick ? "双击" : "单击";
                    string modStr = string.IsNullOrEmpty(Modifiers) ? "" : $" 同时按下[{Modifiers}]";
                    return $"{MouseButton}键 {clickType}{modStr}";
                case MacroActionType.MouseWheel: return ScrollDelta > 0 ? $"向上滚动 {ScrollDelta} 格" : $"向下滚动 {Math.Abs(ScrollDelta)} 格";
                case MacroActionType.KeyPress: return $"按下 [{KeyCode}] 键";
                case MacroActionType.TextInput: return $"输入文本：\"{TextContent}\"";
                case MacroActionType.Shortcut: return $"快捷键：[{Modifiers}] + {KeyCode}";
                case MacroActionType.Wait: return $"等待 {Delay} 毫秒";
                default: return "";
            }
        }
    }

    // ==========================================
    // 2. 全屏十字准星拾取窗体
    // ==========================================
    public class CoordinatePickerForm : Form
    {
        private Point _mousePoint;
        private Rectangle _virtualScreen;
        public Point PickedPoint { get; private set; }
        public bool IsPicked { get; private set; } = false;

        public CoordinatePickerForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.Cursor = Cursors.Cross;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;
            this.Opacity = 0.3;
            
            _virtualScreen = SystemInformation.VirtualScreen;
            this.Bounds = _virtualScreen;

            this.MouseMove += (s, e) => { _mousePoint = e.Location; this.Invalidate(); };
            this.MouseClick += OnMouseClick;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
        }

        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PickedPoint = new Point(_virtualScreen.X + e.X, _virtualScreen.Y + e.Y);
                IsPicked = true;
                this.Close();
            }
            else if (e.Button == MouseButtons.Right) this.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (Pen p = new Pen(Color.Lime, 2))
            {
                g.DrawLine(p, _mousePoint.X, 0, _mousePoint.X, this.Height);
                g.DrawLine(p, 0, _mousePoint.Y, this.Width, _mousePoint.Y);
            }
            using (Font font = new Font("Consolas", 14, FontStyle.Bold))
            using (Brush b = new SolidBrush(Color.Yellow))
            {
                string text = $"X: {_virtualScreen.X + _mousePoint.X}, Y: {_virtualScreen.Y + _mousePoint.Y}";
                g.DrawString(text, font, b, _mousePoint.X + 15, _mousePoint.Y + 15);
            }
        }
    }

    // ==========================================
    // 3. 参数设置弹窗
    // ==========================================
    public class ActionEditorForm : Form
    {
        public MacroAction Action { get; private set; }
        private ComboBox cbMouseBtn, cbModifiers, cbKeyCode;
        private NumericUpDown numX, numY, numScroll, numDelay;
        private CheckBox chkDoubleClick;
        private TextBox txtContent;

        public ActionEditorForm(MacroAction action)
        {
            Action = JsonSerializer.Deserialize<MacroAction>(JsonSerializer.Serialize(action));
            
            this.Text = "配置参数";
            this.Size = new Size(380, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false; this.MinimizeBox = false;
            this.Font = new Font("Microsoft YaHei", 9);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            BuildUI();
        }

        private void BuildUI()
        {
            int y = 20;
            this.Controls.Add(new Label { Text = "执行前延迟(ms):", Location = new Point(20, y), AutoSize = true });
            numDelay = new NumericUpDown { Location = new Point(150, y - 3), Width = 100, Minimum = 0, Maximum = 600000, Value = Action.Delay };
            this.Controls.Add(numDelay);
            y += 40;

            switch (Action.Type)
            {
                case MacroActionType.MouseMove:
                    this.Controls.Add(new Label { Text = "X 坐标:", Location = new Point(20, y), AutoSize = true });
                    numX = new NumericUpDown { Location = new Point(150, y - 3), Width = 100, Minimum = -99999, Maximum = 99999, Value = Action.X };
                    this.Controls.Add(numX);
                    y += 30;
                    this.Controls.Add(new Label { Text = "Y 坐标:", Location = new Point(20, y), AutoSize = true });
                    numY = new NumericUpDown { Location = new Point(150, y - 3), Width = 100, Minimum = -99999, Maximum = 99999, Value = Action.Y };
                    this.Controls.Add(numY);
                    y += 40;
                    Button btnPick = new Button { Text = "📍 拾取屏幕坐标", Location = new Point(150, y), Width = 120 };
                    btnPick.Click += (s, e) => {
                        this.Hide();
                        using (var picker = new CoordinatePickerForm()) {
                            picker.ShowDialog();
                            if (picker.IsPicked) {
                                numX.Value = picker.PickedPoint.X;
                                numY.Value = picker.PickedPoint.Y;
                            }
                        }
                        this.Show();
                    };
                    this.Controls.Add(btnPick);
                    break;

                case MacroActionType.MouseClick:
                    this.Controls.Add(new Label { Text = "鼠标按键:", Location = new Point(20, y), AutoSize = true });
                    cbMouseBtn = new ComboBox { Location = new Point(150, y - 3), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
                    cbMouseBtn.Items.AddRange(new[] { "Left", "Right" });
                    cbMouseBtn.SelectedItem = Action.MouseButton;
                    this.Controls.Add(cbMouseBtn);
                    y += 30;
                    chkDoubleClick = new CheckBox { Text = "双击", Location = new Point(150, y), Checked = Action.IsDoubleClick, AutoSize = true };
                    this.Controls.Add(chkDoubleClick);
                    y += 30;
                    this.Controls.Add(new Label { Text = "修饰键(如Ctrl):", Location = new Point(20, y), AutoSize = true });
                    cbModifiers = new ComboBox { Location = new Point(150, y - 3), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
                    cbModifiers.Items.AddRange(new[] { "", "Ctrl", "Shift", "Alt", "Ctrl,Shift" });
                    cbModifiers.SelectedItem = Action.Modifiers;
                    if (cbModifiers.SelectedIndex < 0) cbModifiers.SelectedIndex = 0;
                    this.Controls.Add(cbModifiers);
                    break;

                case MacroActionType.MouseWheel:
                    this.Controls.Add(new Label { Text = "滚动格数(正上负下):", Location = new Point(20, y), AutoSize = true });
                    numScroll = new NumericUpDown { Location = new Point(150, y - 3), Width = 100, Minimum = -100, Maximum = 100, Value = Action.ScrollDelta };
                    this.Controls.Add(numScroll);
                    break;

                case MacroActionType.KeyPress:
                case MacroActionType.Shortcut:
                    this.Controls.Add(new Label { Text = "按键 (如 Enter):", Location = new Point(20, y), AutoSize = true });
                    cbKeyCode = new ComboBox { Location = new Point(150, y - 3), Width = 100, DropDownStyle = ComboBoxStyle.DropDown };
                    cbKeyCode.Items.AddRange(new[] { "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z", "Enter", "Space", "Esc", "Tab", "F1", "F2", "F3", "F4" });
                    cbKeyCode.Text = Action.KeyCode;
                    this.Controls.Add(cbKeyCode);
                    y += 30;
                    if (Action.Type == MacroActionType.Shortcut) {
                        this.Controls.Add(new Label { Text = "组合修饰键:", Location = new Point(20, y), AutoSize = true });
                        cbModifiers = new ComboBox { Location = new Point(150, y - 3), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
                        cbModifiers.Items.AddRange(new[] { "Ctrl", "Shift", "Alt", "Ctrl,Shift", "Ctrl,Alt" });
                        cbModifiers.SelectedItem = Action.Modifiers;
                        if (cbModifiers.SelectedIndex < 0) cbModifiers.SelectedIndex = 0;
                        this.Controls.Add(cbModifiers);
                    }
                    break;

                case MacroActionType.TextInput:
                    this.Controls.Add(new Label { Text = "输入内容:", Location = new Point(20, y), AutoSize = true });
                    txtContent = new TextBox { Location = new Point(20, y + 20), Width = 320, Height = 100, Multiline = true, Text = Action.TextContent };
                    this.Controls.Add(txtContent);
                    break;

                case MacroActionType.Wait:
                    this.Controls.Add(new Label { Text = "等待时长(毫秒):", Location = new Point(20, y), AutoSize = true });
                    numDelay = new NumericUpDown { Location = new Point(150, y - 3), Width = 100, Minimum = 0, Maximum = 999999, Value = Action.Delay };
                    this.Controls.Add(numDelay);
                    break;
            }

            Button btnOk = new Button { Text = "确定", Location = new Point(180, 260), Size = new Size(80, 30), DialogResult = DialogResult.OK };
            Button btnCancel = new Button { Text = "取消", Location = new Point(270, 260), Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
            
            btnOk.Click += (s, e) => {
                SaveInputs();
                this.DialogResult = DialogResult.OK;
            };

            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void SaveInputs()
        {
            Action.Delay = (int)numDelay.Value;
            switch (Action.Type)
            {
                case MacroActionType.MouseMove:
                    Action.X = (int)numX.Value; Action.Y = (int)numY.Value; break;
                case MacroActionType.MouseClick:
                    Action.MouseButton = cbMouseBtn.SelectedItem.ToString();
                    Action.IsDoubleClick = chkDoubleClick.Checked;
                    Action.Modifiers = cbModifiers.SelectedItem.ToString();
                    break;
                case MacroActionType.MouseWheel:
                    Action.ScrollDelta = (int)numScroll.Value; break;
                case MacroActionType.KeyPress:
                case MacroActionType.Shortcut:
                    Action.KeyCode = cbKeyCode.Text;
                    if (cbModifiers != null) Action.Modifiers = cbModifiers.SelectedItem.ToString();
                    break;
                case MacroActionType.TextInput:
                    Action.TextContent = txtContent.Text; break;
                case MacroActionType.Wait:
                    break;
            }
        }
    }

    // ==========================================
    // 4. 主窗体
    // ==========================================
    public partial class Form1 : Form
    {
        // Win32 API
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion U; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        private const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_ABSOLUTE = 0x8000, 
                           MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004, 
                           MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010, 
                           MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_UNICODE = 0x0004;
        private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

        // UI 控件
        private TabControl tabControl;
        private TabPage tabPageRecord, tabPageManual;
        
        private Label lblStatusRec, lblStatusManual;
        
        private ListBox lstActionsRec;
        private Button btnRec, btnPlayRec, btnStopRec, btnSaveRec, btnLoadRec;
        private CheckBox chkLoopRec;

        private Panel pnlLeft;
        private ListBox lstActionsManual;
        private Button btnPlayManual, btnStopManual, btnSaveManual, btnLoadManual, btnClearManual;
        private CheckBox chkLoopManual;

        // 状态
        private List<MacroAction> _actions = new List<MacroAction>();
        private volatile bool _isPlaying = false, _isRecording = false, _loopPlayback = false, _stopRequested = false;
        private Thread _playThread;
        private string _saveDir;
        
        // 录制相关
        private LowLevelMouseProc _mouseHook;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private Stopwatch _recSw;
        private List<MouseEventRecord> _rawEvents = new List<MouseEventRecord>();

        public Form1()
        {
            _saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseMacroRecorder", "Scripts");
            Directory.CreateDirectory(_saveDir);
            InitializeUI();
            this.AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void InitializeUI()
        {
            this.Text = "动作流宏录制器";
            this.ClientSize = new Size(680, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei", 9);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            tabControl = new TabControl { Location = new Point(10, 10), Size = new Size(660, 560) };
            tabPageRecord = new TabPage(" 智能录制 ") { BackColor = Color.White };
            tabPageManual = new TabPage(" 鼠标宏手动设置 ") { BackColor = Color.White };

            // ============ 页面1：智能录制 ============
            lblStatusRec = new Label { Text = "就绪。", Location = new Point(15, 15), Size = new Size(620, 40), ForeColor = Color.DimGray };
            
            lstActionsRec = new ListBox { Location = new Point(15, 60), Size = new Size(620, 350), Font = new Font("Consolas", 10), HorizontalScrollbar = true };
            lstActionsRec.DoubleClick += (s, e) => EditSelectedAction(lstActionsRec.SelectedIndex);
            lstActionsRec.MouseUp += (s, e) => ShowContextMenu(lstActionsRec, e);

            btnRec = new Button { Text = "🔴 智能录制 (F6)", Location = new Point(15, 425), Size = new Size(130, 40) };
            btnRec.Click += (s, e) => ToggleRecording();
            
            btnPlayRec = new Button { Text = "▶️ 回放 (F7)", Location = new Point(155, 425), Size = new Size(120, 40), Font = new Font("Microsoft YaHei", 10, FontStyle.Bold) };
            btnPlayRec.Click += (s, e) => StartPlayback();
            
            btnStopRec = new Button { Text = "⏹️ 停止 (F8)", Location = new Point(285, 425), Size = new Size(100, 40) };
            btnStopRec.Click += (s, e) => StopPlayback();
            
            btnSaveRec = new Button { Text = "💾 保存脚本", Location = new Point(395, 425), Size = new Size(100, 40) };
            btnSaveRec.Click += (s, e) => SaveScript();
            
            btnLoadRec = new Button { Text = "📂 加载脚本", Location = new Point(505, 425), Size = new Size(130, 40) };
            btnLoadRec.Click += (s, e) => LoadScript();
            
            chkLoopRec = new CheckBox { Text = "循环回放", Location = new Point(15, 480), Size = new Size(150, 30) };

            tabPageRecord.Controls.AddRange(new Control[] { lblStatusRec, lstActionsRec, btnRec, btnPlayRec, btnStopRec, btnSaveRec, btnLoadRec, chkLoopRec });

            // ============ 页面2：手动设置 ============
            lblStatusManual = new Label { Text = "就绪。", Location = new Point(15, 15), Size = new Size(620, 40), ForeColor = Color.DimGray };

            pnlLeft = new Panel { Location = new Point(15, 60), Size = new Size(160, 390), BorderStyle = BorderStyle.FixedSingle };
            AddLibButton(pnlLeft, "🖱️ 移动鼠标到", 10, MacroActionType.MouseMove);
            AddLibButton(pnlLeft, "🖱️ 点击鼠标", 50, MacroActionType.MouseClick);
            AddLibButton(pnlLeft, "🖱️ 滚动鼠标滚轮", 90, MacroActionType.MouseWheel);
            AddLibButton(pnlLeft, "⌨️ 按键点击", 140, MacroActionType.KeyPress);
            AddLibButton(pnlLeft, "⌨️ 快捷输入", 180, MacroActionType.TextInput);
            AddLibButton(pnlLeft, "⌨️ 快捷按键", 220, MacroActionType.Shortcut);
            AddLibButton(pnlLeft, "⏳ 添加等待", 280, MacroActionType.Wait);

            lstActionsManual = new ListBox { Location = new Point(185, 60), Size = new Size(450, 390), Font = new Font("Consolas", 10), HorizontalScrollbar = true };
            lstActionsManual.DoubleClick += (s, e) => EditSelectedAction(lstActionsManual.SelectedIndex);
            lstActionsManual.MouseUp += (s, e) => ShowContextMenu(lstActionsManual, e);

            btnPlayManual = new Button { Text = "▶️ 运行 (F7)", Location = new Point(15, 465), Size = new Size(110, 40), Font = new Font("Microsoft YaHei", 10, FontStyle.Bold) };
            btnPlayManual.Click += (s, e) => StartPlayback();
            
            btnStopManual = new Button { Text = "⏹️ 停止 (F8)", Location = new Point(135, 465), Size = new Size(100, 40) };
            btnStopManual.Click += (s, e) => StopPlayback();
            
            btnSaveManual = new Button { Text = "💾 保存脚本", Location = new Point(245, 465), Size = new Size(100, 40) };
            btnSaveManual.Click += (s, e) => SaveScript();
            
            btnLoadManual = new Button { Text = "📂 加载脚本", Location = new Point(355, 465), Size = new Size(100, 40) };
            btnLoadManual.Click += (s, e) => LoadScript();
            
            btnClearManual = new Button { Text = "🗑️ 清空", Location = new Point(465, 465), Size = new Size(100, 40) };
            btnClearManual.Click += (s, e) => { 
                if(MessageBox.Show("确定清空当前动作流？","提示",MessageBoxButtons.YesNo)==DialogResult.Yes) { 
                    _actions.Clear(); 
                    UpdateList(); 
                } 
            };

            chkLoopManual = new CheckBox { Text = "循环运行", Location = new Point(15, 515), Size = new Size(150, 30) };

            tabPageManual.Controls.AddRange(new Control[] { lblStatusManual, pnlLeft, lstActionsManual, btnPlayManual, btnStopManual, btnSaveManual, btnLoadManual, btnClearManual, chkLoopManual });

            // 组合页面
            tabControl.TabPages.Add(tabPageRecord);
            tabControl.TabPages.Add(tabPageManual);
            this.Controls.Add(tabControl);

            UpdateUIState();
        }

        // --- 全局热键 ---
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(this.Handle, 1, 0, (int)Keys.F6);
            RegisterHotKey(this.Handle, 2, 0, (int)Keys.F7);
            RegisterHotKey(this.Handle, 3, 0, (int)Keys.F8);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(this.Handle, 1);
            UnregisterHotKey(this.Handle, 2);
            UnregisterHotKey(this.Handle, 3);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) // WM_HOTKEY
            {
                int id = m.WParam.ToInt32();
                if (id == 1) ToggleRecording();
                else if (id == 2 && !_isPlaying && !_isRecording) StartPlayback();
                else if (id == 3) StopPlayback();
            }
            base.WndProc(ref m);
        }

        private void AddLibButton(Panel parent, string text, int top, MacroActionType type)
        {
            Button btn = new Button { Text = text, Location = new Point(5, top), Size = new Size(148, 30), TextAlign = ContentAlignment.MiddleLeft };
            btn.Click += (s, e) => {
                MacroAction act = new MacroAction { Type = type };
                if (type == MacroActionType.Wait) act.Delay = 1000;
                using (var editor = new ActionEditorForm(act)) {
                    if (editor.ShowDialog(this) == DialogResult.OK) {
                        _actions.Add(editor.Action);
                        UpdateList();
                    }
                }
            };
            parent.Controls.Add(btn);
        }

        private void UpdateStatus(string msg)
        {
            lblStatusRec.Text = msg;
            lblStatusManual.Text = msg;
        }

        private void UpdateList()
        {
            lstActionsRec.BeginUpdate();
            lstActionsManual.BeginUpdate();
            lstActionsRec.Items.Clear();
            lstActionsManual.Items.Clear();
            
            for (int i = 0; i < _actions.Count; i++)
            {
                string delayStr = _actions[i].Delay > 0 ? $"[延迟 {_actions[i].Delay}ms] " : "";
                string itemText = $"{i + 1:D2}. {delayStr}{_actions[i].GetParamDescription()}";
                lstActionsRec.Items.Add(itemText);
                lstActionsManual.Items.Add(itemText);
            }
            
            lstActionsRec.EndUpdate();
            lstActionsManual.EndUpdate();
        }

        private void EditSelectedAction(int index)
        {
            if (index < 0 || index >= _actions.Count) return;
            using (var editor = new ActionEditorForm(_actions[index])) {
                if (editor.ShowDialog(this) == DialogResult.OK) {
                    _actions[index] = editor.Action;
                    UpdateList();
                    if (index < lstActionsRec.Items.Count) lstActionsRec.SelectedIndex = index;
                    if (index < lstActionsManual.Items.Count) lstActionsManual.SelectedIndex = index;
                }
            }
        }

        private void ShowContextMenu(ListBox listBox, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = listBox.IndexFromPoint(e.Location);
                if (index >= 0)
                {
                    listBox.SelectedIndex = index;
                    ContextMenuStrip cms = new ContextMenuStrip();
                    
                    cms.Items.Add("修改参数").Click += (s, ev) => EditSelectedAction(index);
                    
                    // === 新增：插入动作的子菜单 ===
                    ToolStripMenuItem insertMenuBefore = new ToolStripMenuItem("在此条前插入动作");
                    ToolStripMenuItem insertMenuAfter = new ToolStripMenuItem("在此条后插入动作");
                    
                    // 添加二级子菜单项
                    AddInsertSubItems(insertMenuBefore, index);
                    AddInsertSubItems(insertMenuAfter, index + 1);

                    cms.Items.Add(insertMenuBefore);
                    cms.Items.Add(insertMenuAfter);
                    cms.Items.Add(new ToolStripSeparator());
                    
                    cms.Items.Add("删除此项").Click += (s, ev) => { _actions.RemoveAt(index); UpdateList(); };
                    cms.Items.Add("上移").Click += (s, ev) => {
                        if (index > 0) {
                            var item = _actions[index];
                            _actions.RemoveAt(index);
                            _actions.Insert(index - 1, item);
                            UpdateList();
                            listBox.SelectedIndex = index - 1;
                        }
                    };
                    cms.Items.Add("下移").Click += (s, ev) => {
                        if (index < _actions.Count - 1) {
                            var item = _actions[index];
                            _actions.RemoveAt(index);
                            _actions.Insert(index + 1, item);
                            UpdateList();
                            listBox.SelectedIndex = index + 1;
                        }
                    };
                    
                    cms.Show(listBox, e.Location);
                }
            }
        }

        // 动态生成插入动作的二级菜单项
        private void AddInsertSubItems(ToolStripMenuItem parentMenu, int insertIndex)
        {
            var actionTypes = new[]
            {
                new { Text = "🖱️ 移动鼠标到", Type = MacroActionType.MouseMove },
                new { Text = "🖱️ 点击鼠标", Type = MacroActionType.MouseClick },
                new { Text = "🖱️ 滚动鼠标滚轮", Type = MacroActionType.MouseWheel },
                new { Text = "⌨️ 按键点击", Type = MacroActionType.KeyPress },
                new { Text = "⌨️ 快捷输入", Type = MacroActionType.TextInput },
                new { Text = "⌨️ 快捷按键", Type = MacroActionType.Shortcut },
                new { Text = "⏳ 添加等待", Type = MacroActionType.Wait }
            };

            foreach (var item in actionTypes)
            {
                ToolStripMenuItem subItem = new ToolStripMenuItem(item.Text);
                subItem.Click += (s, ev) =>
                {
                    MacroAction act = new MacroAction { Type = item.Type };
                    if (item.Type == MacroActionType.Wait) act.Delay = 1000;
                    
                    using (var editor = new ActionEditorForm(act))
                    {
                        if (editor.ShowDialog(this) == DialogResult.OK)
                        {
                            // 插入到指定位置
                            _actions.Insert(insertIndex, editor.Action);
                            UpdateList();
                        }
                    }
                };
                parentMenu.DropDownItems.Add(subItem);
            }
        }

        private void SaveScript()
        {
            if (_actions.Count == 0) return;
            using (var sfd = new SaveFileDialog { InitialDirectory = _saveDir, Filter = "宏脚本 (*.json)|*.json", FileName = $"script_{DateTime.Now:yyyyMMdd_HHmmss}.json" }) {
                if (sfd.ShowDialog() == DialogResult.OK) {
                    File.WriteAllText(sfd.FileName, JsonSerializer.Serialize(_actions, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
                    UpdateStatus("脚本已保存。");
                }
            }
        }

        private void LoadScript()
        {
            if (_isPlaying || _isRecording) return;
            using (var ofd = new OpenFileDialog { InitialDirectory = _saveDir, Filter = "宏脚本 (*.json)|*.json" }) {
                if (ofd.ShowDialog() == DialogResult.OK) {
                    try {
                        var acts = JsonSerializer.Deserialize<List<MacroAction>>(File.ReadAllText(ofd.FileName), new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
                        if (acts != null) { _actions = acts; UpdateList(); UpdateStatus("脚本加载成功。按 F7 或点击运行按钮。"); }
                    } catch (Exception ex) { MessageBox.Show("加载失败：" + ex.Message); }
                }
            }
        }

        private void StartPlayback()
        {
            if (_isPlaying || _isRecording || _actions.Count == 0) return;
            
            // 根据当前激活的页面读取不同的循环勾选框
            _loopPlayback = (tabControl.SelectedTab == tabPageRecord) ? chkLoopRec.Checked : chkLoopManual.Checked;
            
            _isPlaying = true;
            _stopRequested = false;
            UpdateUIState();
            
            _playThread = new Thread(() => {
                do {
                    foreach (var act in _actions) {
                        if (!_isPlaying) break;
                        if (act.Type == MacroActionType.Wait) { PreciseWait(act.Delay); continue; }
                        if (act.Delay > 0) PreciseWait(act.Delay);
                        ExecuteAction(act);
                    }
                } while (_isPlaying && _loopPlayback);

                this.BeginInvoke(new Action(() => {
                    _isPlaying = false;
                    UpdateStatus(_stopRequested ? "已停止。" : "回放完成。");
                    UpdateUIState();
                }));
            }) { IsBackground = true };
            _playThread.Start();
        }

        private void StopPlayback()
        {
            _stopRequested = true;
            _isPlaying = false;
        }

        private void ExecuteAction(MacroAction act)
        {
            switch (act.Type) {
                case MacroActionType.MouseMove:
                    SmoothMoveTo(act.X, act.Y); break;
                case MacroActionType.MouseClick:
                    if (act.Modifiers.Contains("Ctrl")) PressKey(0x11, true);
                    if (act.Modifiers.Contains("Shift")) PressKey(0x10, true);
                    
                    uint downFlag = act.MouseButton == "Left" ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN;
                    uint upFlag = act.MouseButton == "Left" ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP;
                    
                    SendMouse(downFlag); SendMouse(upFlag);
                    if (act.IsDoubleClick) { Thread.Sleep(50); SendMouse(downFlag); SendMouse(upFlag); }

                    if (act.Modifiers.Contains("Ctrl")) PressKey(0x11, false);
                    if (act.Modifiers.Contains("Shift")) PressKey(0x10, false);
                    break;
                case MacroActionType.MouseWheel:
                    SendMouse(MOUSEEVENTF_WHEEL, 0, 0, (uint)(act.ScrollDelta * 120)); break;
                case MacroActionType.KeyPress:
                    ushort vk = GetVirtualKey(act.KeyCode);
                    PressKey(vk); break;
                case MacroActionType.Shortcut:
                    if (act.Modifiers.Contains("Ctrl")) PressKey(0x11, true);
                    if (act.Modifiers.Contains("Shift")) PressKey(0x10, true);
                    ushort vk2 = GetVirtualKey(act.KeyCode);
                    PressKey(vk2);
                    if (act.Modifiers.Contains("Ctrl")) PressKey(0x11, false);
                    if (act.Modifiers.Contains("Shift")) PressKey(0x10, false);
                    break;
                case MacroActionType.TextInput:
                    foreach (char c in act.TextContent) SendChar(c); break;
            }
        }

        private ushort GetVirtualKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            if (key.Length == 1) return (ushort)char.ToUpper(key[0]);
            try { return (ushort)Enum.Parse(typeof(Keys), key); } catch { return 0; }
        }

        // --- 录制引擎 ---
        [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private class MouseEventRecord { public long Time; public int Msg; public Point Pos; public short WheelDelta; }
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);

        private void ToggleRecording()
        {
            if (_isRecording) StopRecording();
            else StartRecording();
        }

        private void StartRecording()
        {
            if (_isPlaying) return;
            _rawEvents.Clear();
            _actions.Clear();
            UpdateList();
            _recSw = Stopwatch.StartNew();
            _mouseHook = HookCallback;
            
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                _mouseHookId = SetWindowsHookEx(14, _mouseHook, GetModuleHandle(curModule.ModuleName), 0);

            _isRecording = true;
            UpdateStatus("录制中…… 请执行鼠标操作（停止后将自动转化为动作流）。");
            UpdateUIState();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                var hs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int msg = wParam.ToInt32();
                
                if (msg == 0x0201 || msg == 0x0204 || msg == 0x020A) {
                    short delta = 0;
                    if (msg == 0x020A) delta = (short)((hs.mouseData >> 16) & 0xFFFF);
                    _rawEvents.Add(new MouseEventRecord { Time = _recSw.ElapsedMilliseconds, Msg = msg, Pos = new Point(hs.pt.X, hs.pt.Y), WheelDelta = delta });
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private void StopRecording()
        {
            _isRecording = false;
            UnhookWindowsHookEx(_mouseHookId);
            _recSw.Stop();

            long lastTime = 0;
            foreach (var ev in _rawEvents)
            {
                long gap = ev.Time - lastTime;
                lastTime = ev.Time;

                if (gap > 100) {
                    _actions.Add(new MacroAction { Type = MacroActionType.Wait, Delay = (int)gap });
                } else if (gap > 0) {
                    if (_actions.Count > 0) _actions.Last().Delay += (int)gap;
                    else _actions.Add(new MacroAction { Type = MacroActionType.Wait, Delay = (int)gap });
                }

                if (ev.Msg == 0x0201 || ev.Msg == 0x0204 || ev.Msg == 0x020A)
                {
                    _actions.Add(new MacroAction { Type = MacroActionType.MouseMove, X = ev.Pos.X, Y = ev.Pos.Y });
                }

                if (ev.Msg == 0x0201) _actions.Add(new MacroAction { Type = MacroActionType.MouseClick, MouseButton = "Left" });
                else if (ev.Msg == 0x0204) _actions.Add(new MacroAction { Type = MacroActionType.MouseClick, MouseButton = "Right" });
                else if (ev.Msg == 0x020A) {
                    int scroll = ev.WheelDelta > 0 ? 1 : -1;
                    _actions.Add(new MacroAction { Type = MacroActionType.MouseWheel, ScrollDelta = scroll });
                }
            }

            UpdateList();
            UpdateStatus($"录制结束。生成 {_actions.Count} 条动作，请手动微调参数。");
            UpdateUIState();
        }

        // --- 底层输入辅助 ---
        private void SendMouse(uint flags, int dx = 0, int dy = 0, uint data = 0) {
            var input = new INPUT { 
                type = INPUT_MOUSE, 
                U = new InputUnion { 
                    mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags } 
                } 
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        private void PressKey(ushort vk, bool isDown = false) {
            if (isDown) {
                var input = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk } } };
                SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT))); 
            }
            else {
                var down = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk } } };
                var up = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } };
                SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        private void SendChar(char c) {
            var down = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = (ushort)c, dwFlags = KEYEVENTF_UNICODE } } };
            var up = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = (ushort)c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } } };
            SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(INPUT)));
        }

        private void SmoothMoveTo(int x, int y) {
            int vx = (int)((x - GetSystemMetrics(SM_XVIRTUALSCREEN)) * 65535.0 / Math.Max(GetSystemMetrics(SM_CXVIRTUALSCREEN) - 1, 1));
            int vy = (int)((y - GetSystemMetrics(SM_YVIRTUALSCREEN)) * 65535.0 / Math.Max(GetSystemMetrics(SM_CYVIRTUALSCREEN) - 1, 1));
            SendMouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, vx, vy);
        }

        private void PreciseWait(long ms) {
            long target = Stopwatch.GetTimestamp() + (ms * Stopwatch.Frequency / 1000);
            while (_isPlaying) {
                long rem = target - Stopwatch.GetTimestamp();
                if (rem <= 0) break;
                double msLeft = rem * 1000.0 / Stopwatch.Frequency;
                if (msLeft > 2.0) Thread.Sleep((int)(msLeft - 1));
                else Thread.SpinWait(10);
            }
        }

        private void UpdateUIState() {
            bool idle = !_isPlaying && !_isRecording;
            bool playing = _isPlaying;
            
            // 全局状态
            tabControl.Enabled = true;
            
            // 录制页状态
            btnRec.Text = _isRecording ? "⏹️ 停止录制" : "🔴 智能录制 (F6)";
            btnRec.Enabled = !_isPlaying;
            btnPlayRec.Enabled = idle;
            btnStopRec.Enabled = playing;
            btnSaveRec.Enabled = idle;
            btnLoadRec.Enabled = idle;
            lstActionsRec.Enabled = idle;
            chkLoopRec.Enabled = !playing;

            // 手动页状态
            btnPlayManual.Enabled = idle;
            btnStopManual.Enabled = playing;
            btnSaveManual.Enabled = idle;
            btnLoadManual.Enabled = idle;
            btnClearManual.Enabled = idle;
            pnlLeft.Enabled = idle;
            lstActionsManual.Enabled = idle;
            chkLoopManual.Enabled = !playing;
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isPlaying) StopPlayback();
            if (_isRecording) StopRecording();
            base.OnFormClosing(e);
        }
    }
}