using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using claudpro.Services;

namespace claudpro.UI
{
    public class UserEditForm : Form
    {
        private readonly DatabaseService dbService;
        private readonly int userId;
        private readonly string originalUsername;

        private TextBox usernameTextBox;
        private TextBox nameTextBox;
        private TextBox emailTextBox;
        private TextBox phoneTextBox;
        private TextBox passwordTextBox;
        private TextBox confirmPasswordTextBox;
        private ComboBox userTypeComboBox;
        private CheckBox changePasswordCheckBox;
        private Button saveButton;
        private Button cancelButton;
        private Label statusLabel;

        public UserEditForm(DatabaseService dbService, int userId, string username, string userType, string name, string email, string phone)
        {
            this.dbService = dbService;
            this.userId = userId;
            this.originalUsername = username;

            InitializeComponent();
            InitializeUI(username, userType, name, email, phone);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Edit User";
            this.Size = new Size(450, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;

            this.ResumeLayout(false);
        }

        private void InitializeUI(string username, string userType, string name, string email, string phone)
        {
            int y = 20;
            int labelWidth = 120;
            int inputWidth = 260;
            int spacing = 35;

            // Title
            Controls.Add(ControlExtensions.CreateLabel(
                "Edit User Information",
                new Point(20, y),
                new Size(400, 30),
                new Font("Arial", 12, FontStyle.Bold),
                ContentAlignment.MiddleCenter
            ));
            y += spacing + 10;

            // Username (read-only)
            Controls.Add(ControlExtensions.CreateLabel("Username:", new Point(20, y), new Size(labelWidth, 20)));
            usernameTextBox = ControlExtensions.CreateTextBox(new Point(150, y), new Size(inputWidth, 20), username, false, true);
            Controls.Add(usernameTextBox);
            y += spacing;

            // User Type
            Controls.Add(ControlExtensions.CreateLabel("User Type:", new Point(20, y), new Size(labelWidth, 20)));
            userTypeComboBox = ControlExtensions.CreateComboBox(
                new Point(150, y),
                new Size(inputWidth, 20),
                new string[] { "Admin", "Driver", "Passenger" }
            );
            userTypeComboBox.SelectedItem = userType;
            Controls.Add(userTypeComboBox);
            y += spacing;

            // Name
            Controls.Add(ControlExtensions.CreateLabel("Name:", new Point(20, y), new Size(labelWidth, 20)));
            nameTextBox = ControlExtensions.CreateTextBox(new Point(150, y), new Size(inputWidth, 20), name);
            Controls.Add(nameTextBox);
            y += spacing;

            // Email
            Controls.Add(ControlExtensions.CreateLabel("Email:", new Point(20, y), new Size(labelWidth, 20)));
            emailTextBox = ControlExtensions.CreateTextBox(new Point(150, y), new Size(inputWidth, 20), email);
            Controls.Add(emailTextBox);
            y += spacing;

            // Phone
            Controls.Add(ControlExtensions.CreateLabel("Phone:", new Point(20, y), new Size(labelWidth, 20)));
            phoneTextBox = ControlExtensions.CreateTextBox(new Point(150, y), new Size(inputWidth, 20), phone);
            Controls.Add(phoneTextBox);
            y += spacing;

            // Option to change password
            changePasswordCheckBox = ControlExtensions.CreateCheckBox(
                "Change Password",
                new Point(20, y),
                new Size(130, 20),
                false
            );
            changePasswordCheckBox.CheckedChanged += ChangePasswordCheckBox_CheckedChanged;
            Controls.Add(changePasswordCheckBox);
            y += spacing;

            // Password fields (initially hidden)
            Controls.Add(ControlExtensions.CreateLabel("New Password:", new Point(20, y), new Size(labelWidth, 20)));
            passwordTextBox = ControlExtensions.CreateTextBox(new Point(150, y), new Size(inputWidth, 20), "");
            passwordTextBox.PasswordChar = '*';
            passwordTextBox.Visible = false;
            Controls.Add(passwordTextBox);
            y += spacing;

            Controls.Add(ControlExtensions.CreateLabel("Confirm Password:", new Point(20, y), new Size(labelWidth, 20)));
            confirmPasswordTextBox = ControlExtensions.CreateTextBox(new Point(150, y), new Size(inputWidth, 20), "");
            confirmPasswordTextBox.PasswordChar = '*';
            confirmPasswordTextBox.Visible = false;
            Controls.Add(confirmPasswordTextBox);
            y += spacing;

            // Status label for error messages
            statusLabel = ControlExtensions.CreateLabel(
                "",
                new Point(20, y),
                new Size(390, 20),
                null,
                ContentAlignment.MiddleCenter
            );
            statusLabel.ForeColor = Color.Red;
            Controls.Add(statusLabel);
            y += 30;

            // Buttons
            saveButton = ControlExtensions.CreateButton(
                "Save Changes",
                new Point(150, y),
                new Size(120, 30),
                async (s, e) => await SaveChangesAsync()
            );
            Controls.Add(saveButton);

            cancelButton = ControlExtensions.CreateButton(
                "Cancel",
                new Point(280, y),
                new Size(120, 30),
                (s, e) => DialogResult = DialogResult.Cancel
            );
            Controls.Add(cancelButton);
        }

        private void ChangePasswordCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool showPasswordFields = changePasswordCheckBox.Checked;

            // Show or hide password fields
            var passwordLabel = Controls.Find("New Password:", false)[0];
            var confirmLabel = Controls.Find("Confirm Password:", false)[0];

            passwordLabel.Visible = showPasswordFields;
            confirmLabel.Visible = showPasswordFields;
            passwordTextBox.Visible = showPasswordFields;
            confirmPasswordTextBox.Visible = showPasswordFields;

            // Clear password fields when hiding
            if (!showPasswordFields)
            {
                passwordTextBox.Text = "";
                confirmPasswordTextBox.Text = "";
            }
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    statusLabel.Text = "Name cannot be empty";
                    return;
                }

                // Validate passwords if changing
                if (changePasswordCheckBox.Checked)
                {
                    if (string.IsNullOrWhiteSpace(passwordTextBox.Text))
                    {
                        statusLabel.Text = "Password cannot be empty";
                        return;
                    }

                    if (passwordTextBox.Text != confirmPasswordTextBox.Text)
                    {
                        statusLabel.Text = "Passwords do not match";
                        return;
                    }

                    if (passwordTextBox.Text.Length < 6)
                    {
                        statusLabel.Text = "Password must be at least 6 characters";
                        return;
                    }
                }

                // Disable controls during save
                saveButton.Enabled = false;
                statusLabel.Text = "Saving changes...";
                statusLabel.ForeColor = Color.Blue;

                // Update user profile information
                bool success = await dbService.UpdateUserProfileAsync(
                    userId,
                    userTypeComboBox.SelectedItem.ToString(),
                    nameTextBox.Text,
                    emailTextBox.Text,
                    phoneTextBox.Text
                );

                // Update password if requested
                if (success && changePasswordCheckBox.Checked)
                {
                    success = await dbService.ChangePasswordAsync(userId, passwordTextBox.Text);
                }

                if (success)
                {
                    MessageBox.Show("User information updated successfully",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    statusLabel.Text = "Failed to update user information";
                    statusLabel.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.ForeColor = Color.Red;
            }
            finally
            {
                saveButton.Enabled = true;
            }
        }

    }

}