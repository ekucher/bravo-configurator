namespace BravoBisConfigurator.App;

partial class EditorForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.bannerLabel = new System.Windows.Forms.Label();
        this.tabControl = new System.Windows.Forms.TabControl();
        this.bottomPanel = new System.Windows.Forms.TableLayoutPanel();
        this.summaryLabel = new System.Windows.Forms.Label();
        this.saveButton = new System.Windows.Forms.Button();
        this.closeButton = new System.Windows.Forms.Button();
        this.bottomPanel.SuspendLayout();
        this.SuspendLayout();
        //
        // bannerLabel
        //
        this.bannerLabel.AutoEllipsis = true;
        this.bannerLabel.Dock = System.Windows.Forms.DockStyle.Top;
        this.bannerLabel.Height = 20;
        this.bannerLabel.Name = "bannerLabel";
        this.bannerLabel.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
        //
        // tabControl
        //
        this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tabControl.Name = "tabControl";
        //
        // bottomPanel
        //
        this.bottomPanel.ColumnCount = 3;
        this.bottomPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.bottomPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        this.bottomPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        this.bottomPanel.Controls.Add(this.summaryLabel, 0, 0);
        this.bottomPanel.Controls.Add(this.saveButton, 1, 0);
        this.bottomPanel.Controls.Add(this.closeButton, 2, 0);
        this.bottomPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        this.bottomPanel.Height = 36;
        this.bottomPanel.Padding = new System.Windows.Forms.Padding(6);
        this.bottomPanel.Name = "bottomPanel";
        this.bottomPanel.RowCount = 1;
        //
        // summaryLabel
        //
        this.summaryLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.summaryLabel.AutoSize = true;
        this.summaryLabel.Name = "summaryLabel";
        //
        // saveButton
        //
        this.saveButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
        this.saveButton.Name = "saveButton";
        this.saveButton.Text = "Зберегти";
        this.saveButton.UseVisualStyleBackColor = true;
        this.saveButton.Click += new System.EventHandler(this.SaveButton_Click);
        //
        // closeButton
        //
        this.closeButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
        this.closeButton.Margin = new System.Windows.Forms.Padding(6, 3, 0, 3);
        this.closeButton.Name = "closeButton";
        this.closeButton.Text = "Закрити";
        this.closeButton.UseVisualStyleBackColor = true;
        this.closeButton.Click += new System.EventHandler(this.CloseButton_Click);
        //
        // EditorForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        this.ClientSize = new System.Drawing.Size(720, 520);
        this.MinimumSize = new System.Drawing.Size(720, 520);
        this.Controls.Add(this.tabControl);
        this.Controls.Add(this.bottomPanel);
        this.Controls.Add(this.bannerLabel);
        this.Name = "EditorForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "Конфігуратор BRAVO/BIS";
        this.bottomPanel.ResumeLayout(false);
        this.bottomPanel.PerformLayout();
        this.ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Label bannerLabel;
    private System.Windows.Forms.TabControl tabControl;
    private System.Windows.Forms.TableLayoutPanel bottomPanel;
    private System.Windows.Forms.Label summaryLabel;
    private System.Windows.Forms.Button saveButton;
    private System.Windows.Forms.Button closeButton;
}
