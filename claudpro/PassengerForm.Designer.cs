﻿namespace claudpro
{
    partial class PassengerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // PassengerForm
            // 
            this.ClientSize = new System.Drawing.Size(1000, 700);
            this.Name = "PassengerForm";
            this.Text = "RideMatch - Passenger Interface";
            this.Load += new System.EventHandler(this.PassengerForm_Load);
            this.ResumeLayout(false);
        }

        #endregion
    }
}