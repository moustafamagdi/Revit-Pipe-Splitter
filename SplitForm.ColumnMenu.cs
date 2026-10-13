using System;
using WF = System.Windows.Forms;

namespace PipeSplitter2024
{
    public partial class SplitForm
    {
        private readonly WF.ContextMenuStrip columnMenu = new WF.ContextMenuStrip();
        private string menuColumn;

        private void InitializeColumnMenu()
        {
            columnMenu.ShowItemToolTips = true;
            columnMenu.Items.Add("Filter...", null, delegate { QueueColumnAction(EditFilter); });
            columnMenu.Items.Add("Clear this filter", null, delegate
            {
                QueueColumnAction(delegate(string column)
                {
                    filters.Remove(column);
                    ApplyView();
                });
            });
            columnMenu.Items.Add("Sort ascending", null, delegate
            {
                QueueColumnAction(delegate(string column)
                {
                    sortColumn = column;
                    descending = false;
                    ApplyView();
                });
            });
            columnMenu.Items.Add("Sort descending", null, delegate
            {
                QueueColumnAction(delegate(string column)
                {
                    sortColumn = column;
                    descending = true;
                    ApplyView();
                });
            });
            // Keep the strip alive through WinForms' full close/message-filter sequence.
            // Do not dispose it in Closed: the same form is also reused after Pick pipes.
            Disposed += delegate { columnMenu.Dispose(); };
        }

        private void ShowColumnMenu(string column)
        {
            if (running || IsDisposed || Disposing) return;
            columnMenu.Close();
            menuColumn = column;
            columnMenu.Items[0].Text = "Filter " + Header(column) + "...";
            foreach (WF.ToolStripItem item in columnMenu.Items)
                item.ToolTipText = item.Text + " for " + Header(column);
            columnMenu.Show(grid, grid.PointToClient(WF.Cursor.Position));
        }

        private void QueueColumnAction(Action<string> action)
        {
            string column = menuColumn;
            if (running || IsDisposed || Disposing || !IsHandleCreated || string.IsNullOrEmpty(column)) return;
            columnMenu.Close();
            // Defer dialog opening/grid rebinding until the menu click/close stack has unwound.
            BeginInvoke(new WF.MethodInvoker(delegate
            {
                if (running || IsDisposed || Disposing || !Visible || PickRequested) return;
                try
                {
                    action(column);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Error("Column menu " + column, ex);
                    WF.MessageBox.Show(this, ex.Message, "Pipe Splitter");
                }
            }));
        }
    }
}
