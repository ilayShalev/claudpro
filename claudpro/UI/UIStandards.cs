using System;
using System.Drawing;
using System.Windows.Forms;
using claudpro.Utilities;

namespace claudpro.UI
{
    /// <summary>
    /// Provides standardized UI elements and theming for the application
    /// </summary>
    public static class UIStandards
    {
        // Application color scheme
        public static readonly Color PrimaryColor = Color.FromArgb(0, 120, 215);     // Blue
        public static readonly Color SecondaryColor = Color.FromArgb(0, 99, 177);    // Darker blue
        public static readonly Color AccentColor = Color.FromArgb(255, 128, 0);      // Orange
        public static readonly Color BackgroundColor = Color.White;
        public static readonly Color TextColor = Color.FromArgb(51, 51, 51);         // Dark gray
        public static readonly Color ErrorColor = Color.FromArgb(200, 0, 0);         // Red
        public static readonly Color SuccessColor = Color.FromArgb(0, 150, 0);       // Green
        public static readonly Color WarningColor = Color.FromArgb(240, 173, 0);     // Yellow/Gold

        // Standard fonts
        public static readonly Font TitleFont = new Font("Arial", 16, FontStyle.Bold);
        public static readonly Font SubtitleFont = new Font("Arial", 12, FontStyle.Bold);
        public static readonly Font RegularFont = new Font("Arial", 10, FontStyle.Regular);
        public static readonly Font SmallFont = new Font("Arial", 8, FontStyle.Regular);
        public static readonly Font BoldFont = new Font("Arial", 10, FontStyle.Bold);

        // Standard spacing and sizes
        public const int StandardMargin = 20;
        public const int SmallMargin = 10;
        public const int ControlSpacing = 10;
        public const int ButtonHeight = 30;
        public const int StandardButtonWidth = 120;
        public const int WideButtonWidth = 150;
        public const int NarrowButtonWidth = 80;
        public const int StandardTextBoxHeight = 25;

        /// <summary>
        /// Applies standard styling to a form
        /// </summary>
        public static void ApplyFormStyling(Form form, string title = null, bool resizable = false)
        {
            if (form == null) return;

            // Set form title
            if (!string.IsNullOrEmpty(title))
            {
                form.Text = title;
            }

            // Set form properties
            form.BackColor = BackgroundColor;
            form.ForeColor = TextColor;
            form.Font = RegularFont;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.FormBorderStyle = resizable ? FormBorderStyle.Sizable : FormBorderStyle.FixedDialog;
            form.MinimizeBox = resizable;
            form.MaximizeBox = resizable;

            // Apply styles to all child controls
            ApplyControlStyling(form.Controls);
        }

        /// <summary>
        /// Recursively applies styling to controls
        /// </summary>
        public static void ApplyControlStyling(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                // Apply base styling
                control.Font = RegularFont;
                control.ForeColor = TextColor;

                // Apply specific control styling
                if (control is Button button)
                {
                    StyleButton(button);
                }
                else if (control is Label label)
                {
                    // Check if it's a title or section header
                    if (label.Font.Size > 12 || label.Font.Bold)
                    {
                        label.ForeColor = PrimaryColor;
                    }
                }
                else if (control is TextBox textBox)
                {
                    StyleTextBox(textBox);
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.FlatStyle = FlatStyle.Flat;
                }
                else if (control is CheckBox checkBox)
                {
                    checkBox.FlatStyle = FlatStyle.Standard;
                }
                else if (control is RadioButton radioButton)
                {
                    radioButton.FlatStyle = FlatStyle.Standard;
                }
                else if (control is GroupBox groupBox)
                {
                    groupBox.ForeColor = PrimaryColor;
                    groupBox.Font = BoldFont;
                }

                // Recursively apply to child controls
                if (control.Controls.Count > 0)
                {
                    ApplyControlStyling(control.Controls);
                }
            }
        }

        /// <summary>
        /// Styles a button with standardized appearance
        /// </summary>
        public static void StyleButton(Button button, ButtonStyle style = ButtonStyle.Standard)
        {
            if (button == null) return;

            // Apply base styling
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Height = ButtonHeight;

            // Apply style-specific styling
            switch (style)
            {
                case ButtonStyle.Primary:
                    button.BackColor = PrimaryColor;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = SecondaryColor;
                    button.Font = BoldFont;
                    break;

                case ButtonStyle.Secondary:
                    button.BackColor = SecondaryColor;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = PrimaryColor;
                    break;

                case ButtonStyle.Accent:
                    button.BackColor = AccentColor;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.FromArgb(240, 100, 0);
                    break;

                case ButtonStyle.Danger:
                    button.BackColor = ErrorColor;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.FromArgb(160, 0, 0);
                    break;

                case ButtonStyle.Success:
                    button.BackColor = SuccessColor;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 0);
                    break;

                case ButtonStyle.Standard:
                default:
                    button.BackColor = Color.FromArgb(240, 240, 240);
                    button.ForeColor = TextColor;
                    button.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                    break;
            }

            // Add hover effect
            button.MouseEnter += (s, e) =>
            {
                button.BackColor = ControlPaint.Light(button.BackColor);
            };

            button.MouseLeave += (s, e) =>
            {
                button.BackColor = ControlPaint.Dark(button.BackColor);
            };
        }

        /// <summary>
        /// Styles a text box with standardized appearance
        /// </summary>
        public static void StyleTextBox(TextBox textBox)
        {
            if (textBox == null) return;

            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Height = StandardTextBoxHeight;
        }

        /// <summary>
        /// Creates a styled title label
        /// </summary>
        public static Label CreateTitleLabel(string text, Point location, Size size)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                Size = size,
                Font = TitleFont,
                ForeColor = PrimaryColor,
                TextAlign = ContentAlignment.MiddleCenter
            };

            return label;
        }

        /// <summary>
        /// Creates a styled section header
        /// </summary>
        public static Label CreateSectionHeader(string text, Point location, Size size)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                Size = size,
                Font = SubtitleFont,
                ForeColor = PrimaryColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            return label;
        }

        /// <summary>
        /// Creates a horizontal separator line
        /// </summary>
        public static Panel CreateSeparator(Point location, int width)
        {
            var separator = new Panel
            {
                Location = location,
                Size = new Size(width, 2),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(220, 220, 220)
            };

            return separator;
        }

        /// <summary>
        /// Creates a status strip with standardized appearance
        /// </summary>
        public static StatusStrip CreateStatusStrip()
        {
            var statusStrip = new StatusStrip
            {
                BackColor = PrimaryColor,
                ForeColor = Color.White,
                Font = SmallFont,
                SizingGrip = false
            };

            return statusStrip;
        }

        /// <summary>
        /// Creates a formatted error message panel
        /// </summary>
        public static Panel CreateErrorPanel(string message, Point location, int width)
        {
            var panel = new Panel
            {
                Location = location,
                Size = new Size(width, StandardTextBoxHeight * 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(255, 240, 240),
                Visible = !string.IsNullOrEmpty(message)
            };

            var label = new Label
            {
                Text = message,
                Location = new Point(5, 5),
                Size = new Size(width - 10, StandardTextBoxHeight * 2 - 10),
                ForeColor = ErrorColor,
                Font = RegularFont,
                TextAlign = ContentAlignment.MiddleCenter
            };

            panel.Controls.Add(label);

            return panel;
        }

        /// <summary>
        /// Creates a formatted success message panel
        /// </summary>
        public static Panel CreateSuccessPanel(string message, Point location, int width)
        {
            var panel = new Panel
            {
                Location = location,
                Size = new Size(width, StandardTextBoxHeight * 2),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(240, 255, 240),
                Visible = !string.IsNullOrEmpty(message)
            };

            var label = new Label
            {
                Text = message,
                Location = new Point(5, 5),
                Size = new Size(width - 10, StandardTextBoxHeight * 2 - 10),
                ForeColor = SuccessColor,
                Font = RegularFont,
                TextAlign = ContentAlignment.MiddleCenter
            };

            panel.Controls.Add(label);

            return panel;
        }

        /// <summary>
        /// Shows a standardized validation error message
        /// </summary>
        public static void ShowValidationError(Control container, string message)
        {
            // Find existing error panel
            Control existingError = null;
            foreach (Control control in container.Controls)
            {
                if (control.Tag != null && control.Tag.ToString() == "ValidationError")
                {
                    existingError = control;
                    break;
                }
            }

            if (existingError != null)
            {
                // Update existing error
                if (existingError is Panel panel && panel.Controls.Count > 0 && panel.Controls[0] is Label label)
                {
                    label.Text = message;
                    panel.Visible = !string.IsNullOrEmpty(message);
                }
            }
            else
            {
                // Create new error panel
                var errorPanel = CreateErrorPanel(message, new Point(StandardMargin, container.ClientSize.Height - 100),
                    container.ClientSize.Width - 2 * StandardMargin);
                errorPanel.Tag = "ValidationError";

                container.Controls.Add(errorPanel);
                errorPanel.BringToFront();
            }
        }
    }

    /// <summary>
    /// Defines button styling options
    /// </summary>
    public enum ButtonStyle
    {
        Standard,
        Primary,
        Secondary,
        Accent,
        Danger,
        Success
    }
}