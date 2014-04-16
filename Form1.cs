using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace serpent {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        private int mLastSegmentSize;

        private void label2_Click(object sender, EventArgs e) {

        }

        private void textBox1_TextChanged(object sender, EventArgs e) {

        }

        private void button1_Click(object sender, EventArgs e) {
            DialogResult r = saveFileDialog.ShowDialog();
            if (DialogResult.OK == r) {
                dstFile.Text = saveFileDialog.FileName;
            }
        }

        private void groupBox4_Enter(object sender, EventArgs e) {

        }

        private void textBox2_TextChanged(object sender, EventArgs e) {

        }

        private void zakończToolStripMenuItem_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e) {
            keySize.SelectedIndex = 0;
            operationMode.SelectedIndex = 1;
            segmentSize.SelectedIndex = 0;
            abortOperation.Enabled = false;

            mLastSegmentSize = segmentSize.SelectedIndex;
        }

        private void adjustSegmentSizeToOpMode() {
            if (operationMode.SelectedItem.Equals("ECB")) {
                segmentSize.Enabled = false;

                mLastSegmentSize = segmentSize.SelectedIndex;
                segmentSize.SelectedIndex = 0;
            } else {
                segmentSize.Enabled = true;

                segmentSize.SelectedIndex = mLastSegmentSize;
            }
        }

        private void operationMode_SelectedIndexChanged(object sender, EventArgs e) {
            adjustSegmentSizeToOpMode();
        }

        private void segmentSize_SelectedIndexChanged(object sender, EventArgs e) {
            if (segmentSize.Enabled) {
                mLastSegmentSize = segmentSize.SelectedIndex;
            }
        }

        private void openSrcFile_Click(object sender, EventArgs e) {
            DialogResult r = openFileDialog.ShowDialog();
            if (DialogResult.OK == r) {
                srcFile.Text = openFileDialog.FileName;
            }
        }

        private void performOperation_Click(object sender, EventArgs e) {
            // określenienie czy szyfrowanie czy odszyfrowywanie
            bool encrypt = true;
            if (tabControl1.SelectedIndex != 0) {
                encrypt = false;
            }

            /* walidacja */
            List<String> errors = new List<String>();

            // zgodność haseł
            String secret = "";
            if (encrypt) {
                if (password.Text.Length == 0) {
                    errors.Add("Hasło nie może być puste.");
                }

                if (password.Text != retypedPassword.Text) {
                    errors.Add("Wpisane hasła nie są ze sobą zgodne.");
                } else {
                    secret = password.Text;
                }
            } else {
                secret = decryptPassword.Text;
            }

            // istnienie plików
            if (!File.Exists(srcFile.Text)) {
                errors.Add("Podano nieistniejący plik źródłowy.");
            }

            if (File.Exists(dstFile.Text)) {
                var content = "Plik wynikowy już istnieje.\nCzy chcesz go nadpisać?";
                var result = MessageBox.Show(content, "Potwierdzenie", MessageBoxButtons.YesNo);

                if (DialogResult.No == result)
                {
                    return;
                }
            }

            /* wykonanie algorytmu */
            BackgroundWorker bg = encryptWorker;
            try
            {
                if (errors.Count > 0)
                {
                    throw new Exception();
                }

                // zmiana stanu kontrolek
                openSrcFile.Enabled = false;
                saveDstFile.Enabled = false;
                srcFile.Enabled = false;
                dstFile.Enabled = false;
                performOperation.Enabled = false;
                abortOperation.Enabled = true;
                tabControl1.Enabled = false;

                progressBar.Value = 0;
                progressBar.Visible = true;

                // wykonanie algorytmu
                //  utworzenie obiektu klasy algorithm (IAlgorithm)
                //  wywołanie metody encrypt lub decrypt z odpowiednimi parametrami
                //  w tym obsługa paska postepu i przerwania operacji
                IAlgorithm alg;
                
                //
                var cipherMode = operationMode.Text;
                var segment = Convert.ToInt32(segmentSize.Text.Split(' ')[0]);

                if (encrypt)
                {
                    //
                    var sessionKeySize = Convert.ToInt32(keySize.Text.Split(' ')[0]);

                    //var key = Serpent.generateKey(sessionKeySize);
                    var key = Serpent.generateKeyFromBytes(Convert.FromBase64String("ZgCKtGo7pgmpRw7EFHJTGQ=="));
                    var iv = Serpent.generateIV();

                    //@todo: czy da się ustalić długość podbloku dla CBC?

                    // utworzenie nagłówka
                    //@todo: ustalić wielkość paddingu, również dla OFB i CFB

                    // zapisanie nagłówka
                    var headerOffset = 0;

                    // szyfrowanie
                    alg = new Serpent(key, iv, true);

                    //bg = encryptWorker;
                    alg.init(srcFile.Text, dstFile.Text, cipherMode, segment, 0, headerOffset);

                    statusBarLabel.Text = "trwa szyfrowanie...";
                }
                else
                {
                    // odczytanie parametrów z nagłówka
                    //@todo

                    byte[] encryptedKey = Convert.FromBase64String("ZgCKtGo7pgmpRw7EFHJTGQ==");
                    byte[] decryptedKey = encryptedKey;
                    var sessionKey = Serpent.generateKeyFromBytes(decryptedKey);

                    byte[] iv = Serpent.generateIV(); // same 0
                    segment = 128;
                    cipherMode = "OFB";

                    var headerOffset = 0;

                    // odszyfrowywanie
                    alg = new Serpent(sessionKey, iv, false);

                    //bg = encryptWorker;
                    alg.init(srcFile.Text, dstFile.Text, cipherMode, segment, headerOffset, 0);

                    statusBarLabel.Text = "trwa odszyfrowywanie...";
                }

                bg.RunWorkerAsync(alg);
            }
            catch (System.IO.IOException ex)
            {
                bg.CancelAsync();
                MessageBox.Show("Wystąpił błąd przy zapisywaniu lub odczytywaniu pliku.");
            }
            catch (ArgumentException ex)
            {
                bg.CancelAsync();
                MessageBox.Show("Wystąpił błąd przy inicjalizacji algorytmu szyfrowania.");
            }
            catch (Exception ex)
            {
                bg.CancelAsync();

                String errorStr = "Podano niepoprawne parametry.\n\nSzczegóły:\n" +
                    string.Join("\n", errors.ToArray());
                MessageBox.Show(errorStr);
            }
        }

        private void restoreDefaultControlsState() {
            // zmiana stanu kontrolek
            openSrcFile.Enabled = true;
            saveDstFile.Enabled = true;
            srcFile.Enabled = true;
            dstFile.Enabled = true;
            performOperation.Enabled = true;
            abortOperation.Enabled = false;
            tabControl1.Enabled = true;

            progressBar.Visible = false;
        }

        private void abortOperation_Click(object sender, EventArgs e) {
            statusBarLabel.Text = "przerywanie operacji...";

            BackgroundWorker[] workers = new BackgroundWorker[]{encryptWorker, decryptWorker};

            foreach (BackgroundWorker w in workers) {
                if (w.IsBusy) {
                    w.CancelAsync();
                }
            }
        }

        //todo: połączenie z biblioteką Bouncy Castle
        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            System.Console.WriteLine("encryptWorker_DoWork");
            IAlgorithm alg = e.Argument as IAlgorithm;
            Int64 length = alg.getSrcLength();
            Int64 step = System.Convert.ToInt64(Math.Ceiling(length / (double)100));
            Int64 countBytes = 0;
            Int64 bytes = 1;

            while (bytes > 0 && !encryptWorker.CancellationPending) {
                System.Threading.Thread.Sleep(500);
                
                // "unit work" szyfrowanie fragmentu pliku
                bytes = alg.encrypt(step);
                countBytes += bytes;

                int progress = (int)(countBytes / length * 100);
                encryptWorker.ReportProgress(progress);
            }

            if (encryptWorker.CancellationPending) {
                e.Cancel = true;
            }

            e.Result = alg;
        }

        private void encryptWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            System.Console.WriteLine("encryptWorker_ProgressChanged: " + e.ProgressPercentage.ToString());

            progressBar.Value = e.ProgressPercentage;
        }

        private void encryptWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            System.Console.WriteLine("encryptWorker_RunWorkerCompleted");

            IAlgorithm alg = e.Result as IAlgorithm;
            bool encryption = alg.Encryption;
            alg.Dispose();

            restoreDefaultControlsState();
            var opStatus = (encryption ? "zaszyfrowano" : "odszyfrowano");
            statusBarLabel.Text = (e.Cancelled ? "przerwano" : opStatus);
        }

        //@todo: usunąć decrypt workera
        private void decryptWorker_DoWork(object sender, DoWorkEventArgs e) {
            System.Console.WriteLine("decryptWorker_DoWork");

        }

        private void decryptWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            System.Console.WriteLine("decryptWorker_ProgressChanged");

            progressBar.Value = e.ProgressPercentage;
        }

        private void decryptWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            System.Console.WriteLine("decryptWorker_RunWorkerCompleted");

            restoreDefaultControlsState();
            statusBarLabel.Text = (e.Cancelled ? "przerwano" : "odszyfrowano");
        }
    }
}
