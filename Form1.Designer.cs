namespace ses_uyg
{
    partial class Form1
    {
        /// <summary>
        ///Gerekli tasarımcı değişkeni.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///Kullanılan tüm kaynakları temizleyin.
        /// </summary>
        ///<param name="disposing">yönetilen kaynaklar dispose edilmeliyse doğru; aksi halde yanlış.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing) { DisposeTray(); StopAudio(); CloseAi(); }
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer üretilen kod

        /// <summary>
        /// Tasarımcı desteği için gerekli metot - bu metodun 
        ///içeriğini kod düzenleyici ile değiştirmeyin.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.btnStart = new GlassButton();
            this.btnStop = new GlassButton();
            this.trackBarDogshit = new System.Windows.Forms.TrackBar();
            this.trackBarPitch = new System.Windows.Forms.TrackBar();
            this.trackBarEcho = new System.Windows.Forms.TrackBar();
            this.btnReset = new GlassButton();
            this.lblPitch = new System.Windows.Forms.Label();
            this.lblDogshit = new System.Windows.Forms.Label();
            this.lblEcho = new System.Windows.Forms.Label();
            this.listBoxSounds = new System.Windows.Forms.ListBox();
            this.btnAddSound = new GlassButton();
            this.btnDeleteSound = new GlassButton();
            this.trackBarEQBass = new System.Windows.Forms.TrackBar();
            this.trackBarEQMid = new System.Windows.Forms.TrackBar();
            this.trackBarEQTreble = new System.Windows.Forms.TrackBar();
            this.lblEQBass = new System.Windows.Forms.Label();
            this.lblEQMid = new System.Windows.Forms.Label();
            this.lblEQTreble = new System.Windows.Forms.Label();
            this.btnRadioMode = new GlassButton();
            this.btnCaveMode = new GlassButton();
            this.label1 = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.btnAnimeMode = new GlassButton();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarDogshit)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarPitch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEcho)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEQBass)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEQMid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEQTreble)).BeginInit();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.BackColor = System.Drawing.Color.YellowGreen;
            this.btnStart.ForeColor = System.Drawing.Color.White;
            this.btnStart.Location = new System.Drawing.Point(500, 381);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 1;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = false;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.BackColor = System.Drawing.Color.OrangeRed;
            this.btnStop.ForeColor = System.Drawing.Color.White;
            this.btnStop.Location = new System.Drawing.Point(500, 410);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(75, 23);
            this.btnStop.TabIndex = 2;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = false;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // trackBarDogshit
            // 
            this.trackBarDogshit.BackColor = System.Drawing.SystemColors.Control;
            this.trackBarDogshit.Location = new System.Drawing.Point(11, 109);
            this.trackBarDogshit.Maximum = 20;
            this.trackBarDogshit.Minimum = 1;
            this.trackBarDogshit.Name = "trackBarDogshit";
            this.trackBarDogshit.Size = new System.Drawing.Size(342, 56);
            this.trackBarDogshit.TabIndex = 5;
            this.trackBarDogshit.Value = 1;
            this.trackBarDogshit.Scroll += new System.EventHandler(this.trackBarDogshit_Scroll);
            // 
            // trackBarPitch
            // 
            this.trackBarPitch.BackColor = System.Drawing.SystemColors.Control;
            this.trackBarPitch.Location = new System.Drawing.Point(11, 28);
            this.trackBarPitch.Maximum = 20;
            this.trackBarPitch.Minimum = 5;
            this.trackBarPitch.Name = "trackBarPitch";
            this.trackBarPitch.Size = new System.Drawing.Size(342, 56);
            this.trackBarPitch.TabIndex = 7;
            this.trackBarPitch.Value = 10;
            this.trackBarPitch.Scroll += new System.EventHandler(this.trackBarPitch_Scroll);
            // 
            // trackBarEcho
            // 
            this.trackBarEcho.BackColor = System.Drawing.SystemColors.Control;
            this.trackBarEcho.Location = new System.Drawing.Point(11, 187);
            this.trackBarEcho.Name = "trackBarEcho";
            this.trackBarEcho.Size = new System.Drawing.Size(342, 56);
            this.trackBarEcho.TabIndex = 9;
            // 
            // btnReset
            // 
            this.btnReset.BackColor = System.Drawing.Color.Blue;
            this.btnReset.ForeColor = System.Drawing.Color.White;
            this.btnReset.Location = new System.Drawing.Point(500, 439);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 23);
            this.btnReset.TabIndex = 11;
            this.btnReset.Text = "Reset";
            this.btnReset.UseVisualStyleBackColor = false;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // lblPitch
            // 
            this.lblPitch.AutoSize = true;
            this.lblPitch.Location = new System.Drawing.Point(12, 9);
            this.lblPitch.Name = "lblPitch";
            this.lblPitch.Size = new System.Drawing.Size(44, 16);
            this.lblPitch.TabIndex = 13;
            this.lblPitch.Text = "label2";
            // 
            // lblDogshit
            // 
            this.lblDogshit.AutoSize = true;
            this.lblDogshit.Location = new System.Drawing.Point(12, 87);
            this.lblDogshit.Name = "lblDogshit";
            this.lblDogshit.Size = new System.Drawing.Size(44, 16);
            this.lblDogshit.TabIndex = 14;
            this.lblDogshit.Text = "label3";
            // 
            // lblEcho
            // 
            this.lblEcho.AutoSize = true;
            this.lblEcho.Location = new System.Drawing.Point(12, 168);
            this.lblEcho.Name = "lblEcho";
            this.lblEcho.Size = new System.Drawing.Size(44, 16);
            this.lblEcho.TabIndex = 15;
            this.lblEcho.Text = "label4";
            // 
            // listBoxSounds
            // 
            this.listBoxSounds.FormattingEnabled = true;
            this.listBoxSounds.ItemHeight = 16;
            this.listBoxSounds.Location = new System.Drawing.Point(581, 240);
            this.listBoxSounds.Name = "listBoxSounds";
            this.listBoxSounds.Size = new System.Drawing.Size(207, 308);
            this.listBoxSounds.TabIndex = 16;
            this.listBoxSounds.DoubleClick += new System.EventHandler(this.ListBoxSounds_DoubleClick);
            // 
            // btnAddSound
            // 
            this.btnAddSound.Location = new System.Drawing.Point(494, 493);
            this.btnAddSound.Name = "btnAddSound";
            this.btnAddSound.Size = new System.Drawing.Size(81, 23);
            this.btnAddSound.TabIndex = 17;
            this.btnAddSound.Text = "Ses ekle";
            this.btnAddSound.UseVisualStyleBackColor = true;
            this.btnAddSound.Click += new System.EventHandler(this.btnAddSound_Click);
            // 
            // btnDeleteSound
            // 
            this.btnDeleteSound.Location = new System.Drawing.Point(494, 522);
            this.btnDeleteSound.Name = "btnDeleteSound";
            this.btnDeleteSound.Size = new System.Drawing.Size(81, 23);
            this.btnDeleteSound.TabIndex = 18;
            this.btnDeleteSound.Text = "Seçileni sil";
            this.btnDeleteSound.UseVisualStyleBackColor = true;
            this.btnDeleteSound.Click += new System.EventHandler(this.btnDeleteSound_Click);
            // 
            // trackBarEQBass
            // 
            this.trackBarEQBass.Location = new System.Drawing.Point(11, 265);
            this.trackBarEQBass.Maximum = 15;
            this.trackBarEQBass.Minimum = -15;
            this.trackBarEQBass.Name = "trackBarEQBass";
            this.trackBarEQBass.Size = new System.Drawing.Size(342, 56);
            this.trackBarEQBass.TabIndex = 19;
            // 
            // trackBarEQMid
            // 
            this.trackBarEQMid.Location = new System.Drawing.Point(11, 343);
            this.trackBarEQMid.Maximum = 15;
            this.trackBarEQMid.Minimum = -15;
            this.trackBarEQMid.Name = "trackBarEQMid";
            this.trackBarEQMid.Size = new System.Drawing.Size(342, 56);
            this.trackBarEQMid.TabIndex = 20;
            // 
            // trackBarEQTreble
            // 
            this.trackBarEQTreble.Location = new System.Drawing.Point(11, 421);
            this.trackBarEQTreble.Maximum = 15;
            this.trackBarEQTreble.Minimum = -15;
            this.trackBarEQTreble.Name = "trackBarEQTreble";
            this.trackBarEQTreble.Size = new System.Drawing.Size(342, 56);
            this.trackBarEQTreble.TabIndex = 21;
            // 
            // lblEQBass
            // 
            this.lblEQBass.AutoSize = true;
            this.lblEQBass.Location = new System.Drawing.Point(12, 246);
            this.lblEQBass.Name = "lblEQBass";
            this.lblEQBass.Size = new System.Drawing.Size(44, 16);
            this.lblEQBass.TabIndex = 22;
            this.lblEQBass.Text = "label1";
            // 
            // lblEQMid
            // 
            this.lblEQMid.AutoSize = true;
            this.lblEQMid.Location = new System.Drawing.Point(12, 324);
            this.lblEQMid.Name = "lblEQMid";
            this.lblEQMid.Size = new System.Drawing.Size(44, 16);
            this.lblEQMid.TabIndex = 23;
            this.lblEQMid.Text = "label2";
            // 
            // lblEQTreble
            // 
            this.lblEQTreble.AutoSize = true;
            this.lblEQTreble.Location = new System.Drawing.Point(12, 402);
            this.lblEQTreble.Name = "lblEQTreble";
            this.lblEQTreble.Size = new System.Drawing.Size(44, 16);
            this.lblEQTreble.TabIndex = 24;
            this.lblEQTreble.Text = "label3";
            // 
            // btnRadioMode
            // 
            this.btnRadioMode.Location = new System.Drawing.Point(456, 28);
            this.btnRadioMode.Name = "btnRadioMode";
            this.btnRadioMode.Size = new System.Drawing.Size(119, 23);
            this.btnRadioMode.TabIndex = 25;
            this.btnRadioMode.Text = "📻 Telsiz Modu";
            this.btnRadioMode.UseVisualStyleBackColor = true;
            this.btnRadioMode.Click += new System.EventHandler(this.btnRadioMode_Click);
            // 
            // btnCaveMode
            // 
            this.btnCaveMode.Location = new System.Drawing.Point(456, 57);
            this.btnCaveMode.Name = "btnCaveMode";
            this.btnCaveMode.Size = new System.Drawing.Size(119, 23);
            this.btnCaveMode.TabIndex = 26;
            this.btnCaveMode.Text = "🕳️ Mağara Modu";
            this.btnCaveMode.UseVisualStyleBackColor = true;
            this.btnCaveMode.Click += new System.EventHandler(this.btnCaveMode_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(453, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(41, 16);
            this.label1.TabIndex = 27;
            this.label1.Text = "Mods";
            // 
            // txtSearch
            // 
            this.txtSearch.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.txtSearch.Location = new System.Drawing.Point(581, 212);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(205, 22);
            this.txtSearch.TabIndex = 28;
            this.txtSearch.Text = "Aramak için yazın ";
            // 
            // btnAnimeMode
            // 
            this.btnAnimeMode.Location = new System.Drawing.Point(581, 28);
            this.btnAnimeMode.Name = "btnAnimeMode";
            this.btnAnimeMode.Size = new System.Drawing.Size(119, 23);
            this.btnAnimeMode.TabIndex = 29;
            this.btnAnimeMode.Text = "Sivrisinek Modu ";
            this.btnAnimeMode.UseVisualStyleBackColor = true;
            this.btnAnimeMode.Click += new System.EventHandler(this.btnAnimeMode_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(798, 557);
            this.Controls.Add(this.btnAnimeMode);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCaveMode);
            this.Controls.Add(this.btnRadioMode);
            this.Controls.Add(this.lblEQTreble);
            this.Controls.Add(this.lblEQMid);
            this.Controls.Add(this.lblEQBass);
            this.Controls.Add(this.trackBarEQTreble);
            this.Controls.Add(this.trackBarEQMid);
            this.Controls.Add(this.trackBarEQBass);
            this.Controls.Add(this.btnDeleteSound);
            this.Controls.Add(this.btnAddSound);
            this.Controls.Add(this.listBoxSounds);
            this.Controls.Add(this.lblEcho);
            this.Controls.Add(this.lblDogshit);
            this.Controls.Add(this.lblPitch);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.trackBarEcho);
            this.Controls.Add(this.trackBarPitch);
            this.Controls.Add(this.trackBarDogshit);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.trackBarDogshit)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarPitch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEcho)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEQBass)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEQMid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarEQTreble)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.TrackBar trackBarDogshit;
        private System.Windows.Forms.TrackBar trackBarPitch;
        private System.Windows.Forms.TrackBar trackBarEcho;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Label lblPitch;
        private System.Windows.Forms.Label lblDogshit;
        private System.Windows.Forms.Label lblEcho;
        private System.Windows.Forms.ListBox listBoxSounds;
        private System.Windows.Forms.Button btnAddSound;
        private System.Windows.Forms.Button btnDeleteSound;
        private System.Windows.Forms.TrackBar trackBarEQBass;
        private System.Windows.Forms.TrackBar trackBarEQMid;
        private System.Windows.Forms.TrackBar trackBarEQTreble;
        private System.Windows.Forms.Label lblEQBass;
        private System.Windows.Forms.Label lblEQMid;
        private System.Windows.Forms.Label lblEQTreble;
        private System.Windows.Forms.Button btnRadioMode;
        private System.Windows.Forms.Button btnCaveMode;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnAnimeMode;
    }
}

