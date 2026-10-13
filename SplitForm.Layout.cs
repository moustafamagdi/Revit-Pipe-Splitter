using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Autodesk.Revit.UI;
using WF = System.Windows.Forms;

namespace PipeSplitter2024
{
    public partial class SplitForm
    {
        private readonly WF.ToolTip tips = new WF.ToolTip();
        private readonly WF.ErrorProvider inputErrors = new WF.ErrorProvider();
        private readonly WF.ComboBox quickStatus = new WF.ComboBox();
        private readonly WF.Label verifiedLabel = new WF.Label();
        private readonly WF.Label operationNote = new WF.Label();
        private readonly WF.Label ruleNotes = new WF.Label();
        private readonly WF.ProgressBar progress = new WF.ProgressBar();
        private readonly WF.Label progressText = new WF.Label();
        private readonly WF.FlowLayoutPanel toolbar = new WF.FlowLayoutPanel();
        private readonly WF.FlowLayoutPanel filterBar = new WF.FlowLayoutPanel();
        private readonly WF.StatusStrip statusStrip = new WF.StatusStrip();
        private readonly Dictionary<string, WF.ToolStripStatusLabel> kpis = new Dictionary<string, WF.ToolStripStatusLabel>();
        private WF.TableLayoutPanel content;
        private WF.Panel viewport;
        private WF.Button cancelRun;
        private bool uiReady;
        private bool resizingContent;
        private bool inputsValid;
        private bool cancelRequested;
        private string lastNotedRules;
        private double minimumSafeMetres;

        public SplitForm(UIDocument uidoc)
        {
            ui = uidoc;
            doc = ui.Document;
            minimumSafeMetres = doc.Application.ShortCurveTolerance * 2 * 0.3048;
            InitializeLayout();
            InitializePhase2();
            uiReady = true;
            WireInputEvents();
            ValidateInputs();
            ApplyView();
            RefreshKpis();
            Shown += delegate { FitContent(); };
            FormClosing += OnClosing;
        }

        private void InitializeLayout()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = WF.AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "Pipe Splitter " + SessionInfo.ToolVersion + " | Revit 2024";
            MinimumSize = new Size(950, 650);
            Size = new Size(1250, 930);
            StartPosition = WF.FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(244, 247, 250);
            inputErrors.ContainerControl = this;
            inputErrors.BlinkStyle = WF.ErrorBlinkStyle.NeverBlink;
            tips.ShowAlways = true;
            tips.AutoPopDelay = 20000;

            viewport = new WF.Panel { Dock = WF.DockStyle.Fill, AutoScroll = true };
            content = new WF.TableLayoutPanel
            {
                Dock = WF.DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = WF.AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Padding = new WF.Padding(12)
            };
            content.ColumnStyles.Add(new WF.ColumnStyle(WF.SizeType.Percent, 100));
            viewport.Controls.Add(content);
            Controls.Add(viewport);

            toolbar.AutoSize = true;
            toolbar.AutoSizeMode = WF.AutoSizeMode.GrowAndShrink;
            toolbar.Dock = WF.DockStyle.Fill;
            toolbar.WrapContents = true;
            AddContent(toolbar);
            BuildToolbar();

            filterBar.AutoSize = true;
            filterBar.Dock = WF.DockStyle.Fill;
            filterBar.Controls.Add(Label("Status"));
            quickStatus.DropDownStyle = WF.ComboBoxStyle.DropDownList;
            quickStatus.Items.AddRange(new object[] { "Actionable", "Will split", "Verified", "Skipped", "Blocked", "Failed", "Applied", "Unchanged", "All" });
            quickStatus.SelectedIndex = 0;
            quickStatus.Width = 140;
            filterBar.Controls.Add(quickStatus);
            Tip(quickStatus, "Combined with every column filter. Actionable includes eligible pipes, including retryable failures. Narrowing never shrinks the saved Verified set.");
            AddButton(filterBar, "Clear column filters", delegate
            {
                filters.Clear();
                ApplyView();
            }, "Clear all right-click column filters; retain the quick Status choice.");
            verifiedLabel.AutoSize = true;
            verifiedLabel.Padding = new WF.Padding(12, 6, 0, 0);
            filterBar.Controls.Add(verifiedLabel);
            Tip(verifiedLabel, "Apply uses exactly these successfully verified IDs, including rows hidden by filters.");
            AddContent(filterBar);

            filterInfo.AutoSize = true;
            filterInfo.Dock = WF.DockStyle.Fill;
            AddContent(filterInfo);
            ConfigureGrid();
            AddContent(grid);

            var progressPanel = new WF.FlowLayoutPanel { AutoSize = true, Dock = WF.DockStyle.Bottom, WrapContents = true, Padding = new WF.Padding(12, 3, 12, 3) };
            progress.Width = 240;
            progressPanel.Controls.Add(progress);
            progressText.AutoSize = true;
            progressText.Padding = new WF.Padding(0, 6, 0, 0);
            progressText.Text = "Ready";
            progressPanel.Controls.Add(progressText);
            cancelRun = new WF.Button { Text = "Cancel", AutoSize = true, Enabled = false };
            cancelRun.Click += delegate
            {
                if (!running) return;
                cancelRequested = true;
                cancelRun.Enabled = false;
                progressText.Text = "Cancel requested; finishing the current pipe...";
            };
            progressPanel.Controls.Add(cancelRun);
            Tip(progress, "Progress advances after each complete pipe transaction; no interruption inside a pipe.");
            Tip(cancelRun, "Stop between pipes. During Apply, choose whether to keep completed pipes in one Undo step or roll back everything.");
            Controls.Add(progressPanel);
            progressPanel.BringToFront();

            operationNote.AutoSize = true;
            operationNote.Dock = WF.DockStyle.Fill;
            operationNote.Text = "Set Max finished length and Min piece length, then Scan.";
            AddContent(operationNote);
            ruleNotes.AutoSize = true;
            ruleNotes.Dock = WF.DockStyle.Fill;
            ruleNotes.ForeColor = Color.FromArgb(125, 75, 0);
            ruleNotes.Visible = false;
            AddContent(ruleNotes);

            var help = new WF.Label
            {
                AutoSize = true,
                Dock = WF.DockStyle.Fill,
                Text = "How it works: Scan > Preview > Apply. Preview creates and rolls back trial splits; Apply reuses the verified cuts and checks every pipe again. "
                    + "The Verified set stays fixed when rows are hidden. Adding eligible rows or changing settings/rules requires a new Preview. "
                    + "Rules: %APPDATA%\\PipeSplitter\\rules.json (first enabled match wins). Start/End follow stored curve direction. "
                    + "Select in Revit closes this dialog and zooms; reopen and Scan again."
            };
            AddContent(help);

            statusStrip.Dock = WF.DockStyle.Bottom;
            statusStrip.SizingGrip = false;
            statusStrip.ShowItemToolTips = true;
            statusStrip.LayoutStyle = WF.ToolStripLayoutStyle.Flow;
            statusStrip.AutoSize = true;
            foreach (string name in new[] { "Scanned", "To split", "Verified", "Applied", "Failed", "Warnings", "Pieces", "Unions" })
            {
                var item = new WF.ToolStripStatusLabel(name + ": 0");
                item.ToolTipText = "Full scan: " + name + ". Pieces and unions include current estimates or verified/applied results. Running counts are provisional until the batch finishes.";
                kpis.Add(name, item);
                statusStrip.Items.Add(item);
            }
            Controls.Add(statusStrip);
            statusStrip.BringToFront();
            AddDefaultTips(content);
            AddDefaultTips(progressPanel);
            viewport.SizeChanged += delegate { FitContent(); };
            content.SizeChanged += delegate { FitContent(); };
            Disposed += delegate
            {
                tips.Dispose();
                inputErrors.Dispose();
            };
            ResumeLayout(true);
        }

        private void BuildToolbar()
        {
            WF.FlowLayoutPanel scopeTools = Group("Scope");
            scope.DropDownStyle = WF.ComboBoxStyle.DropDownList;
            scope.Width = 180;
            scope.Items.AddRange(new object[] { "All model pipes", "Selected pipes only", "Pipes in active view" });
            scope.SelectedIndex = 0;
            Labeled(scopeTools, "Scan scope", scope);
            Tip(scope, "Scan all host pipes, picked/preselected pipes, or pipes returned by Revit for the active view. Linked pipes are excluded.");
            AddButton(scopeTools, "Pick pipes", PickPipes, "Temporarily exit this dialog to pick pipes or their insulation in Revit. Finish returns here; Esc preserves the previous selection and scan.");
            AddButton(scopeTools, "Select pipes in active view", SelectActiveViewPipes, "Select host pipes visible to Revit's active-view collector, including visible pipe insulation hosts. Sets Scan scope to Pipes in active view. Click Scan afterwards.");
            executionScope.DropDownStyle = WF.ComboBoxStyle.DropDownList;
            executionScope.Width = 140;
            executionScope.Items.AddRange(new object[] { "Visible rows", "All eligible" });
            executionScope.SelectedIndex = 0;
            Labeled(scopeTools, "Preview scope", executionScope);
            Tip(executionScope, "Choose IDs for the next Preview. Always starts on Visible rows; Apply uses the frozen successful Preview set.");

            WF.FlowLayoutPanel limitTools = Group("Limits");
            ConfigureNumber(maximum, 6m);
            ConfigureNumber(minimum, .5m);
            Labeled(limitTools, "Max finished length (m)", maximum);
            Labeled(limitTools, "Min piece length (m)", minimum);
            Tip(maximum, "Maximum finished pipe length AFTER unions. Rules may override this global default.");
            Tip(minimum, "Smallest permitted resulting piece. Must not exceed Max finished length and must exceed Revit's short-curve safety limit.");
            remainder.DropDownStyle = WF.ComboBoxStyle.DropDownList;
            remainder.Width = 105;
            remainder.Items.AddRange(new object[] { "End", "Start", "Balanced" });
            remainder.SelectedIndex = 0;
            Labeled(limitTools, "Remainder", remainder);
            Tip(remainder, "End/Start follow the stored curve direction, not flow direction. Balanced uses equal end pieces when feasible.");
            limitTools.Controls.Add(strictMode);
            Tip(strictMode, "Off: collect and dismiss warnings. On: a warning rolls back the affected pipe.");

            WF.FlowLayoutPanel ruleTools = Group("Rules");
            AddButton(ruleTools, "Open rules", OpenRules, "Edit rules.json in Notepad. Scan and Preview again after changes.");
            AddButton(ruleTools, "Reload", ReloadRules, "Validate the rules file, invalidate the scan and show shadow warnings in Notes.");
            AddButton(ruleTools, "Insert example", InsertExampleRule, "Insert a disabled example only when the rules list is empty.");
            AddButton(ruleTools, "Export combinations", ExportCombinations, "Export all scanned PipeType/SystemType/DiameterMm combinations and counts, independent of filters.");

            WF.FlowLayoutPanel runTools = Group("Run");
            scan = AddButton(runTools, "1. Scan", Scan, "Read pipes and estimate splits with the current rules and limits; does not change the model.");
            preview = AddButton(runTools, "2. Preview", delegate { Run(false); }, "Verify actual splits and union takeout, then roll back the entire trial. Saves the successful IDs and final cuts.");
            apply = AddButton(runTools, "3. Apply", delegate { Run(true); }, "Confirm and apply the exact saved Verified set. All final checks remain; one Undo restores kept changes.");

            WF.FlowLayoutPanel resultTools = Group("Results");
            AddButton(resultTools, "Select rows", delegate { SelectRowsInRevit(false); }, "Close this dialog, select highlighted rows and zoom in Revit. Reopen and Scan again to continue.");
            AddButton(resultTools, "Select Failed/Skipped", delegate { SelectRowsInRevit(true); }, "Close and zoom to all Failed/Skipped/Blocked scan rows, including hidden rows. Reopen and Scan again.");
            export = AddButton(resultTools, "Export visible CSV", Export, "Export only rows surviving the quick Status and all column filters, in the current sort order.");
            AddButton(resultTools, "Export audit CSV", ExportAudit, "Export all recorded attempts in this session, including cancelled/unprocessed rows. Existing filenames are refused.");
        }

        private void AddContent(WF.Control control)
        {
            int row = content.RowCount++;
            content.RowStyles.Add(new WF.RowStyle(WF.SizeType.AutoSize));
            content.Controls.Add(control, 0, row);
        }

        private WF.FlowLayoutPanel Group(string title)
        {
            var box = new WF.GroupBox { Text = title, AutoSize = true, AutoSizeMode = WF.AutoSizeMode.GrowAndShrink, Padding = new WF.Padding(8), Margin = new WF.Padding(0, 0, 8, 4) };
            var flow = new WF.FlowLayoutPanel { AutoSize = true, AutoSizeMode = WF.AutoSizeMode.GrowAndShrink, Dock = WF.DockStyle.Fill, FlowDirection = WF.FlowDirection.TopDown, WrapContents = false };
            box.Controls.Add(flow);
            toolbar.Controls.Add(box);
            return flow;
        }

        private WF.Label Label(string text)
        {
            return new WF.Label { Text = text, AutoSize = true, Padding = new WF.Padding(0, 4, 0, 0) };
        }

        private void Labeled(WF.Control parent, string text, WF.Control control)
        {
            var pair = new WF.FlowLayoutPanel { AutoSize = true, AutoSizeMode = WF.AutoSizeMode.GrowAndShrink, WrapContents = false };
            pair.Controls.Add(Label(text));
            control.Margin = new WF.Padding(3, 3, 22, 3);
            pair.Controls.Add(control);
            parent.Controls.Add(pair);
        }

        private WF.Button AddButton(WF.Control parent, string text, Action action, string tooltip)
        {
            var button = new WF.Button { Text = text, AutoSize = true, AutoSizeMode = WF.AutoSizeMode.GrowAndShrink, Padding = new WF.Padding(5, 2, 5, 2) };
            button.Click += delegate
            {
                if (running) return;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("UI action", ex);
                    WF.MessageBox.Show(this, ex.Message, "Pipe Splitter");
                }
            };
            parent.Controls.Add(button);
            Tip(button, tooltip);
            return button;
        }

        private void ConfigureNumber(WF.NumericUpDown number, decimal value)
        {
            number.DecimalPlaces = 3;
            number.Minimum = .001m;
            number.Maximum = 100000m;
            number.Increment = .1m;
            number.Value = value;
            number.Width = 110;
        }

        private void Tip(WF.Control control, string text)
        {
            tips.SetToolTip(control, text);
        }

        private void AddDefaultTips(WF.Control parent)
        {
            foreach (WF.Control control in parent.Controls)
            {
                if (string.IsNullOrEmpty(tips.GetToolTip(control)))
                    Tip(control, string.IsNullOrEmpty(control.Text) ? tips.GetToolTip(parent) : control.Text);
                AddDefaultTips(control);
            }
        }

        private void FitContent()
        {
            if (!uiReady || resizingContent || viewport == null) return;
            resizingContent = true;
            try
            {
                int width = Math.Max(1, viewport.ClientSize.Width - WF.SystemInformation.VerticalScrollBarWidth - content.Padding.Horizontal);
                foreach (WF.Label label in new[] { filterInfo, operationNote, ruleNotes })
                    label.MaximumSize = new Size(width, 0);
                foreach (WF.Label label in content.Controls.OfType<WF.Label>())
                    label.MaximumSize = new Size(width, 0);
                int minimumGrid = (int)Math.Ceiling(Font.Height * 10.0);
                grid.Height = Math.Max(minimumGrid, viewport.ClientSize.Height - (content.Height - grid.Height));
                // The outer viewport scrolls vertically if auto-sized groups exceed the available height.
                content.PerformLayout();
            }
            finally
            {
                resizingContent = false;
            }
        }

        private void OnClosing(object sender, WF.FormClosingEventArgs e)
        {
            if (running)
            {
                e.Cancel = true;
                SetOperationNote("An operation is running. Use Cancel and wait for the current pipe to finish.");
                return;
            }
            if (!PickRequested) SaveSettings();
        }
    }
}
