using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace claudpro.UI
{
    /// <summary>
    /// Extensions for UI controls to simplify UI operations
    /// </summary>
    public static class ControlExtensions
    {
        /// <summary>
        /// Creates a new label control with the specified properties
        /// </summary>
        public static Label CreateLabel(string text, Point location, Size size, Font font = null, ContentAlignment alignment = ContentAlignment.MiddleLeft)
        {
            return new Label
            {
                Text = text,
                Location = location,
                Size = size,
                Font = font,
                TextAlign = alignment
            };
        }

        /// <summary>
        /// Creates a new button control with the specified properties
        /// </summary>
        public static Button CreateButton(string text, Point location, Size size, EventHandler onClick = null)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size
            };

            if (onClick != null)
                button.Click += onClick;

            return button;
        }

        /// <summary>
        /// Creates a new textbox control with the specified properties
        /// </summary>
        public static TextBox CreateTextBox(Point location, Size size, string text = "", bool multiline = false, bool readOnly = false)
        {
            return new TextBox
            {
                Location = location,
                Size = size,
                Text = text,
                Multiline = multiline,
                ReadOnly = readOnly,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
            };
        }

        /// <summary>
        /// Creates a new rich text box with the specified properties
        /// </summary>
        public static RichTextBox CreateRichTextBox(Point location, Size size, bool readOnly = false)
        {
            return new RichTextBox
            {
                Location = location,
                Size = size,
                ReadOnly = readOnly,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White
            };
        }

        /// <summary>
        /// Creates a new numeric up down control with the specified properties
        /// </summary>
        public static NumericUpDown CreateNumericUpDown(Point location, Size size, decimal min, decimal max, decimal value, decimal increment = 1)
        {
            return new NumericUpDown
            {
                Location = location,
                Size = size,
                Minimum = min,
                Maximum = max,
                Value = value,
                Increment = increment
            };
        }

        /// <summary>
        /// Creates a new combo box with the specified properties
        /// </summary>
        public static ComboBox CreateComboBox(Point location, Size size, string[] items, int selectedIndex = 0)
        {
            var comboBox = new ComboBox
            {
                Location = location,
                Size = size,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            if (items != null)
                comboBox.Items.AddRange(items);

            if (items != null && items.Length > 0 && selectedIndex >= 0 && selectedIndex < items.Length)
                comboBox.SelectedIndex = selectedIndex;

            return comboBox;
        }

        /// <summary>
        /// Creates a new checkbox with the specified properties
        /// </summary>
        public static CheckBox CreateCheckBox(string text, Point location, Size size, bool isChecked = false)
        {
            return new CheckBox
            {
                Text = text,
                Location = location,
                Size = size,
                Checked = isChecked
            };
        }

        /// <summary>
        /// Creates a new radio button with the specified properties
        /// </summary>
        public static RadioButton CreateRadioButton(string text, Point location, Size size, bool isChecked = false)
        {
            return new RadioButton
            {
                Text = text,
                Location = location,
                Size = size,
                Checked = isChecked
            };
        }

        /// <summary>
        /// Creates a new panel with the specified properties
        /// </summary>
        public static Panel CreatePanel(Point location, Size size, BorderStyle borderStyle = BorderStyle.None)
        {
            return new Panel
            {
                Location = location,
                Size = size,
                BorderStyle = borderStyle,
                AutoScroll = true
            };
        }

        /// <summary>
        /// Adds a log message to a text box with timestamp
        /// </summary>
        public static void AppendLog(this TextBox textBox, string message)
        {
            if (textBox.InvokeRequired)
            {
                textBox.Invoke(new Action(() => AppendLog(textBox, message)));
                return;
            }

            textBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}{Environment.NewLine}");
            textBox.ScrollToCaret();
        }


    }
}