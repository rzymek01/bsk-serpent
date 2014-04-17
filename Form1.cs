using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;

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
            if (operationMode.SelectedItem.Equals("ECB") || operationMode.SelectedItem.Equals("CBC"))
            {
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
                if (encrypt)
                {
                    var cipherMode = operationMode.Text;
                    var segment = Convert.ToInt32(segmentSize.Text.Split(' ')[0]);
                    var sessionKeySize = Convert.ToInt32(keySize.Text.Split(' ')[0]);

                    encryptFile(srcFile.Text, dstFile.Text, cipherMode, segment, sessionKeySize, password.Text);

                    statusBarLabel.Text = "trwa szyfrowanie...";
                }
                else
                {
                    var password = decryptPassword.Text;

                    decryptFile(srcFile.Text, dstFile.Text, password);

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

        /**
         * Testowanie poprawności 
         */
        private void button1_Click_1(object sender, EventArgs e)
        {
            String path = "E:\\Projekty\\BSK\\dane-testowe\\pattern.in";
            var srcChecksum = computeFileChecksum(path);

            System.Console.WriteLine("srcChecksum = {0}", srcChecksum);


            //@todo: utworzyć klasę AlgorithmParam ze składowymi src, dst, cipherMode, segmentSize, sessionKeySize, password
            // utworzyć listę obiektów tej klasy z różnymi wartościami

            // foreach po liście
            //   encryptFile
            //   alg.encrypt(srcFile.length)
            //   decryptFile
            //   alg.encrypt(srcFile'.length)
            //
            //   obliczenie dstChecksum
            //   porównanie srcChecksum z dstChecksum

            // komunikaty błędu mogą iść jako MessageBox
        }

        private IAlgorithm encryptFile(String src, String dst, String cipherMode, int segment, int sessionKeySize, String password)
        {
            // wykonanie algorytmu
            //  utworzenie obiektu klasy algorithm (IAlgorithm)
            //  wywołanie metody encrypt lub decrypt z odpowiednimi parametrami
            //  w tym obsługa paska postepu i przerwania operacji
            IAlgorithm alg;

            //var key = Serpent.generateKey(sessionKeySize);
            var key = Serpent.generateKeyFromBytes(Convert.FromBase64String("ZgCKtGo7pgmpRw7EFHJTGQ=="));
            var iv = Serpent.generateIV();

            // utworzenie nagłówka
            //@todo: ustalić wielkość paddingu, również dla OFB i CFB
            // zaszyfrowanie KS Serpent/ECB/NoPadding? hasłem password

            // zapisanie nagłówka
            var headerOffset = 0;

            // szyfrowanie
            alg = new Serpent(key, iv, true);
            alg.init(src, dst, cipherMode, segment, 0, headerOffset);

            return alg;
        }

        private IAlgorithm decryptFile(String src, String dst, String password)
        {
            // wykonanie algorytmu
            //  utworzenie obiektu klasy algorithm (IAlgorithm)
            //  wywołanie metody encrypt lub decrypt z odpowiednimi parametrami
            //  w tym obsługa paska postepu i przerwania operacji
            IAlgorithm alg;

            // odczytanie parametrów z nagłówka
            //@todo

            byte[] encryptedKey = Convert.FromBase64String("ZgCKtGo7pgmpRw7EFHJTGQ==");
            byte[] decryptedKey = encryptedKey;
            var sessionKey = Serpent.generateKeyFromBytes(decryptedKey);

            byte[] iv = Serpent.generateIV(); // same 0
            var segment = 128;
            var cipherMode = "CBC";

            var headerOffset = 0;

            // odszyfrowywanie
            alg = new Serpent(sessionKey, iv, false);
            alg.init(src, dst, cipherMode, segment, headerOffset, 0);

            return alg;
        }

        private String computeFileChecksum(String path)
        {
            using (var alg = SHA256.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(alg.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }
        }
    }
}
