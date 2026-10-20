using System;
using System.Windows.Forms;

// открывает форму для ввода текста
public static class InputDialog
{
    public static string? ShowDialog(string title, string promptText, string defaultText = "")
    {
        Form form = new Form();
        Label label = new Label();
        TextBox textBox = new TextBox();
        Button buttonOk = new Button();
        Button buttonCancel = new Button();

        form.Text = title;
        label.Text = promptText;
        textBox.Text = defaultText;

        buttonOk.Text = "OK";
        buttonCancel.Text = "Отмена";
        buttonOk.DialogResult = DialogResult.OK;
        buttonCancel.DialogResult = DialogResult.Cancel;

        textBox.Multiline = true;
        textBox.ScrollBars = ScrollBars.Vertical;
        textBox.WordWrap = true;
        textBox.AcceptsReturn = true;

        // Логика для Enter (Ввод) и Shift+Enter (Новая строка)
        textBox.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (e.Shift)
                {
                    int selectionStart = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Insert(selectionStart, Environment.NewLine);
                    textBox.SelectionStart = selectionStart + Environment.NewLine.Length;
                    e.SuppressKeyPress = true;
                }
                else
                {
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                    e.SuppressKeyPress = true;
                }
            }
        };

        label.SetBounds(12, 15, 460, 20);
        textBox.SetBounds(12, 38, 460, 100);
        buttonOk.SetBounds(312, 148, 75, 25);
        buttonCancel.SetBounds(397, 148, 75, 25);

        form.AcceptButton = buttonOk;
        form.CancelButton = buttonCancel;

        form.ClientSize = new System.Drawing.Size(484, 185);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.MaximizeBox = false;
        form.MinimizeBox = false;

        form.Controls.AddRange([label, textBox, buttonOk, buttonCancel]);

        // Выделяем переданный текст и ставим курсор в самый конец текста
        form.Load += (sender, e) =>
        {
            textBox.Focus();
            textBox.SelectionStart = textBox.Text.Length;
        };

        DialogResult dialogResult = form.ShowDialog();
        return dialogResult == DialogResult.OK ? textBox.Text : null;
    }
}
