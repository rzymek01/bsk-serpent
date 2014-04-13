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
                errors.Add("Podano niepoprawny plik źródłowy.");
            }

            if (!File.Exists(dstFile.Text)) {
                errors.Add("Podano niepoprawny plik wynikowy.");
            }

            /* wykonanie algorytmu */
            if (errors.Count == 0) {
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
                IAlgorithm alg = new Serpent();
                BackgroundWorker bg;

                // wygenerowanie klucza

                if (encrypt) {
                    // utworzenie nagłówka

                    // zapisanie nagłówka

                    // szyfrowanie
                    bg = encryptWorker;
                    alg.init(srcFile.Text, dstFile.Text, "sessKey", "ECB", 128, 0, 0);

                    statusBarLabel.Text = "trwa szyfrowanie...";
                } else {
                    // odszyfrowywanie
                    bg = decryptWorker;

                    statusBarLabel.Text = "trwa odszyfrowywanie...";
                }

                bg.RunWorkerAsync(alg);
            } else {
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
            alg.Dispose();

            restoreDefaultControlsState();
            statusBarLabel.Text = (e.Cancelled ? "przerwano" : "zaszyfrowano");
        }


        //todo: połączenie z biblioteką Bouncy Castle
        private void decryptWorker_DoWork(object sender, DoWorkEventArgs e) {
            System.Console.WriteLine("decryptWorker_DoWork");

            //@todo: decryptWorker_DoWork
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
