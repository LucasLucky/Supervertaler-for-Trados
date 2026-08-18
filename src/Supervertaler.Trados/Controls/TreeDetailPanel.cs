using System;
using System.Drawing;
using System.Windows.Forms;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// The tree-on-the-left, detail-on-the-right shell used by the Settings
    /// tabs that browse a folder of Markdown files.
    ///
    /// <para>Extracted from <see cref="PromptManagerPanel"/> so the Library tab
    /// can show memory banks with the same interaction rather than a second
    /// implementation of it. See <c>docs/design/library-tab.md</c>.</para>
    ///
    /// <para><b>It owns the shell, not the content.</b> Splitter, docking, the
    /// tree's appearance, the toolbar and footer strips, and swapping the detail
    /// pane. What goes in them — which buttons, how the tree is populated, what
    /// each detail panel shows — belongs to the consumer, because that is
    /// exactly where prompts and memory banks differ.</para>
    ///
    /// <para>A <see cref="Panel"/> rather than a <see cref="UserControl"/> on
    /// purpose: the hosting tab already sets <c>AutoScaleMode.Dpi</c>, and a
    /// nested UserControl with its own scaling would apply it twice.</para>
    /// </summary>
    public class TreeDetailPanel : Panel
    {
        /// <summary>The tree. The consumer populates it and handles its events.</summary>
        public TreeView Tree { get; private set; }

        /// <summary>Strip across the top of the left pane. The consumer adds its
        /// own buttons and positions them — spacing differs per tab, and the
        /// groupings are deliberate.</summary>
        public Panel Toolbar { get; private set; }

        /// <summary>Strip across the bottom of the left pane, for an
        /// "Open … folder" link or similar. Empty until the consumer fills it.</summary>
        public Panel Footer { get; private set; }

        /// <summary>Right pane. The consumer adds its detail panels here and
        /// switches between them with <see cref="ShowDetail"/>.</summary>
        public Panel DetailHost { get; private set; }

        /// <summary>Fraction of the width given to the tree, applied once, on the
        /// first resize wide enough to be meaningful.</summary>
        public double InitialSplitRatio { get; set; } = 0.55;

        private SplitContainer _splitter;
        private bool _splitInitialised;

        // Button -> "should this be enabled for the selected node".
        private readonly System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<Button, Func<TreeNode, bool>>> _toolbarRules =
            new System.Collections.Generic.List<
                System.Collections.Generic.KeyValuePair<Button, Func<TreeNode, bool>>>();

        public TreeDetailPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            DetailHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            Toolbar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White };
            Footer = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.White };

            Tree = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowLines = true,
                FullRowSelect = true,
                Font = new Font("Segoe UI", 8.5f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(250, 250, 250)
            };

            var treePanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 0, 4, 0),
                BackColor = Color.White
            };
            treePanel.Controls.Add(Tree);

            // Reverse order: WinForms docks later-added controls closer to the
            // edge, so Fill must go in before Bottom and Top.
            leftPanel.Controls.Add(treePanel);
            leftPanel.Controls.Add(Footer);
            leftPanel.Controls.Add(Toolbar);

            _splitter = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(220, 220, 220),
                SplitterWidth = 5,
                FixedPanel = FixedPanel.None,
                BorderStyle = BorderStyle.None
            };
            _splitter.Panel1.Controls.Add(leftPanel);
            _splitter.Panel2.Controls.Add(DetailHost);
            Controls.Add(_splitter);

            // Temporary: a SplitContainer will not accept a distance wider than
            // its current width, and at construction that width is meaningless.
            _splitter.SplitterDistance = 100;
            Resize += OnResizeSetInitialSplit;

            // The shell owns re-evaluation so a consumer cannot forget it. Its
            // own AfterSelect handler runs alongside the consumer's; order does
            // not matter, because the rules read the tree rather than any state
            // the consumer sets.
            Tree.AfterSelect += (s, e) => RefreshToolbarState();
        }

        private void OnResizeSetInitialSplit(object sender, EventArgs e)
        {
            if (_splitInitialised || Width <= 100) return;
            _splitter.SplitterDistance = (int)(Width * InitialSplitRatio);
            _splitInitialised = true;
        }

        /// <summary>
        /// Shows one detail panel and hides its siblings. Pass null to hide all.
        ///
        /// <para>Worth having as a method rather than a line of assignments: the
        /// original hid four named panels and showed one, written out twice in
        /// different places. Adding a fifth meant finding both, and missing one
        /// leaves two panels drawn on top of each other.</para>
        /// </summary>
        public void ShowDetail(Panel panel)
        {
            foreach (Control child in DetailHost.Controls)
                child.Visible = ReferenceEquals(child, panel);
        }

        /// <summary>Hides every detail panel.</summary>
        public void HideAllDetails() => ShowDetail(null);

        /// <summary>
        /// Declares when <paramref name="button"/> should be enabled, given the
        /// selected node. Re-evaluated on every selection change.
        ///
        /// <para>Disabled rather than hidden, deliberately. Buttons that vanish
        /// and reappear make the toolbar jump as you move through the tree, and
        /// you can never learn what is possible where, because you only ever see
        /// what applies right now. Greyed buttons stay put and teach the shape of
        /// the thing. It also fails better: a wrongly-enabled button is a
        /// nuisance, a wrongly-missing one looks like a bug.</para>
        ///
        /// <para>The button is not added to the toolbar here — consumers position
        /// their own, because the spacing is deliberately uneven and differs per
        /// tab.</para>
        /// </summary>
        public void RegisterToolbarButton(Button button, Func<TreeNode, bool> isEnabled)
        {
            if (button == null) return;
            _toolbarRules.Add(new System.Collections.Generic.KeyValuePair<Button, Func<TreeNode, bool>>(
                button, isEnabled ?? (_ => true)));
        }

        /// <summary>
        /// Re-applies every registered rule against the current selection.
        /// Called automatically on selection change; call it directly after
        /// rebuilding the tree, when no selection event fires.
        /// </summary>
        public void RefreshToolbarState()
        {
            var node = Tree.SelectedNode;
            foreach (var rule in _toolbarRules)
            {
                bool enabled;
                // Fail OPEN. A rule that throws must not leave a button dead for
                // the rest of the session: a wrongly-enabled button does nothing
                // when clicked, a wrongly-disabled one blocks the user entirely.
                try { enabled = rule.Value(node); }
                catch { enabled = true; }

                if (rule.Key.Enabled != enabled) rule.Key.Enabled = enabled;
            }
        }

        /// <summary>
        /// A toolbar button styled like the rest.
        ///
        /// <para>AutoSize so labels grow with DPI and font size — a fixed width
        /// clipped "Restore" and "Refresh" at 150% Windows scaling.
        /// <paramref name="minWidth"/> survives as a minimum so short labels
        /// still get a comfortable click target instead of collapsing to the
        /// width of their text.</para>
        /// </summary>
        public static Button CreateToolbarButton(string text, int minWidth)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(minWidth, 25),
                Padding = new Padding(8, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(80, 80, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 220);
            return btn;
        }
    }
}
