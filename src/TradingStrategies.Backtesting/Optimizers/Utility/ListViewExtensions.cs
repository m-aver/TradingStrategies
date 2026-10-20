using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

internal static class ListViewExtensions
{
    private static readonly MethodInfo _onDoubleClickMethod = typeof(ListView)
        .GetMethod("OnDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void OnDoubleClick(this ListView listView) => _onDoubleClickMethod.Invoke(listView, [EventArgs.Empty]);

    public static void AsNotable(this ListView listView)
    {
        ToolStripMenuItem mniAddNote;

        if (listView.ContextMenuStrip is not null &&
            !listView.ContextMenuStrip.Items.ContainsKey(nameof(mniAddNote)))
        {
            var separator = new ToolStripSeparator();
            listView.ContextMenuStrip.Items.Insert(0, separator);

            mniAddNote = new ToolStripMenuItem();
            mniAddNote.Checked = false;
            mniAddNote.CheckState = CheckState.Unchecked;
            mniAddNote.Text = "Add a note";
            mniAddNote.Name = nameof(mniAddNote);
            mniAddNote.Click += (_, e) =>
            {
                //заметка всплывает только при наведении на первую ячейку строки
                var note = InputDialog.ShowDialog(
                    "Note input", "Input a note or keep empty to clear existing note", listView.FocusedItem.ToolTipText);
                if (note is not null)
                    listView.FocusedItem.ToolTipText = note;
            };

            listView.ContextMenuStrip.Items.Insert(0, mniAddNote);
            listView.ShowItemToolTips = true;
            listView.ItemSelectionChanged += (_, e) =>
            {
                //выделяем при нажатии на строку с зажатым ALT, при повторном нажатии сбрасываем выделение
                if (e.Item.Selected && (Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                    e.Item.BackColor = e.Item.BackColor == Color.PaleGreen ? Color.White : Color.PaleGreen;
            };
        }
    }

    public static void AsFilterable(this ListView listView)
    {
        ToolStripMenuItem mniAddFilter;

        if (listView.ContextMenuStrip is not null &&
            !listView.ContextMenuStrip.Items.ContainsKey(nameof(mniAddFilter)))
        {
            var separator = new ToolStripSeparator();
            listView.ContextMenuStrip.Items.Insert(0, separator);

            // Подписываемся на открытие меню, чтобы запомнить точную колонку под курсором
            listView.ContextMenuStrip.Opening += (sender, e) =>
            {
                // Определяем индекс колонки по позиции курсора
                var clientPoint = listView.PointToClient(Cursor.Position);
                var hitTest = listView.HitTest(clientPoint);

                listView.ContextMenuStrip.Tag = hitTest.SubItem != null && hitTest.Item != null
                    ? hitTest.Item.SubItems.IndexOf(hitTest.SubItem)
                    : -1; // Кликнули по пустому месту
            };

            mniAddFilter = new ToolStripMenuItem();
            mniAddFilter.Checked = false;
            mniAddFilter.CheckState = CheckState.Unchecked;
            mniAddFilter.Text = "Add a column filter";
            mniAddFilter.Name = nameof(mniAddFilter);
            mniAddFilter.Click += (_, e) =>
            {
                var columnIndex = (listView.ContextMenuStrip.Tag is int idx) ? idx : -1;

                if (columnIndex is -1) return;

                var existingFilterInput = listView.Columns[columnIndex].Tag is ListViewColumnFilter filter 
                    ? filter.RawInput 
                    : string.Empty;

                var filterInput = InputDialog.ShowDialog(
                    "Filter input",
                    "Input valid values for filter or keep empty to clear existing filter " +
                    $"(Example: v1{ListViewColumnFilter.IndividualSeparator}v2{ListViewColumnFilter.RangeSeparator}v3 - v1 is a individual value, v2{ListViewColumnFilter.RangeSeparator}v3 is a range)",
                    existingFilterInput);

                if (filterInput is not null)
                {
                    filter = string.IsNullOrWhiteSpace(filterInput) ? ListViewColumnFilter.NoFilter : new ListViewColumnFilter(filterInput);

                    var column = listView.Columns[columnIndex].Tag = filter;

                    ApplyFilters(listView);
                }
            };

            listView.ContextMenuStrip.Items.Insert(0, mniAddFilter);
        }
    }

    private static void ApplyFilters(ListView listView)
    {
        listView.BeginUpdate();
        try
        {
            foreach (ListViewItem item in listView.Items)
            {
                var match = true;

                var cells = item.SubItems;
                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    var column = listView.Columns[i];

                    if (column.Tag is not ListViewColumnFilter filter) continue;

                    match = match && filter.IsMatch(cell.Text);
                }

                if (match)
                {
                    // восстанавливаем
                    item.ForeColor = Color.Black;
                    item.BackColor = Color.White;
                }
                else
                {
                    item.ForeColor = Color.DarkGray;
                    item.BackColor = Color.LightGray;
                }
            }
        }
        finally
        {
            listView.EndUpdate();
        }
    }
}
