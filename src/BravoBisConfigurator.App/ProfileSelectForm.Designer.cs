namespace BravoBisConfigurator.App;

partial class ProfileSelectForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.promptLabel = new System.Windows.Forms.Label();
        this.bravoButton = new System.Windows.Forms.Button();
        this.bisButton = new System.Windows.Forms.Button();
        this.cancelButton = new System.Windows.Forms.Button();
        this.layoutPanel = new System.Windows.Forms.TableLayoutPanel();
        this.layoutPanel.SuspendLayout();
        this.SuspendLayout();
        //
        // promptLabel
        //
        this.promptLabel.AutoSize = true;
        this.promptLabel.Dock = System.Windows.Forms.DockStyle.Top;
        this.promptLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
        this.promptLabel.Name = "promptLabel";
        this.promptLabel.Text = "Яку конфігурацію ви хочете редагувати?";
        //
        // bravoButton
        //
        this.bravoButton.Dock = System.Windows.Forms.DockStyle.Top;
        this.bravoButton.Height = 32;
        this.bravoButton.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
        this.bravoButton.Name = "bravoButton";
        this.bravoButton.Text = "BRAVO (сервер) — bravo.ini";
        this.bravoButton.UseVisualStyleBackColor = true;
        this.bravoButton.Click += new System.EventHandler(this.BravoButton_Click);
        //
        // bisButton
        //
        this.bisButton.Dock = System.Windows.Forms.DockStyle.Top;
        this.bisButton.Height = 32;
        this.bisButton.Margin = new System.Windows.Forms.Padding(0, 0, 0, 18);
        this.bisButton.Name = "bisButton";
        this.bisButton.Text = "BIS (клієнт) — bis.ini";
        this.bisButton.UseVisualStyleBackColor = true;
        this.bisButton.Click += new System.EventHandler(this.BisButton_Click);
        //
        // cancelButton
        //
        this.cancelButton.Dock = System.Windows.Forms.DockStyle.Top;
        this.cancelButton.Height = 28;
        this.cancelButton.Name = "cancelButton";
        this.cancelButton.Text = "Скасувати";
        this.cancelButton.UseVisualStyleBackColor = true;
        this.cancelButton.Click += new System.EventHandler(this.CancelButton_Click);
        //
        // layoutPanel
        //
        this.layoutPanel.ColumnCount = 1;
        this.layoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.layoutPanel.Controls.Add(this.promptLabel, 0, 0);
        this.layoutPanel.Controls.Add(this.bravoButton, 0, 1);
        this.layoutPanel.Controls.Add(this.bisButton, 0, 2);
        this.layoutPanel.Controls.Add(this.cancelButton, 0, 3);
        this.layoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this.layoutPanel.Padding = new System.Windows.Forms.Padding(16);
        this.layoutPanel.RowCount = 4;
        this.layoutPanel.Name = "layoutPanel";
        //
        // ProfileSelectForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        this.ClientSize = new System.Drawing.Size(360, 190);
        this.Controls.Add(this.layoutPanel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "ProfileSelectForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Конфігуратор BRAVO/BIS";
        this.layoutPanel.ResumeLayout(false);
        this.layoutPanel.PerformLayout();
        this.ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Label promptLabel;
    private System.Windows.Forms.Button bravoButton;
    private System.Windows.Forms.Button bisButton;
    private System.Windows.Forms.Button cancelButton;
    private System.Windows.Forms.TableLayoutPanel layoutPanel;
}
