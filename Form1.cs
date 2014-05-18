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
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;

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

                    alg = encryptFile(srcFile.Text, dstFile.Text, cipherMode, segment, sessionKeySize, password.Text);

                    statusBarLabel.Text = "trwa szyfrowanie...";
                }
                else
                {
                    var password = decryptPassword.Text;

                    alg = decryptFile(srcFile.Text, dstFile.Text, password);

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

            BackgroundWorker[] workers = new BackgroundWorker[]{encryptWorker};

            foreach (BackgroundWorker w in workers) {
                if (w.IsBusy) {
                    w.CancelAsync();
                }
            }
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            //System.Console.WriteLine("encryptWorker_DoWork");
            IAlgorithm alg = e.Argument as IAlgorithm;
            Int64 length = alg.getSrcLength();
            Int64 step = System.Convert.ToInt64(Math.Ceiling(length / (double)100));
            Int64 countBytes = 0;
            Int64 bytes = 1;

            while (bytes > 0 && !encryptWorker.CancellationPending) {
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
            //System.Console.WriteLine("encryptWorker_ProgressChanged: " + e.ProgressPercentage.ToString());

            progressBar.Value = e.ProgressPercentage;
        }

        private void encryptWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            //System.Console.WriteLine("encryptWorker_RunWorkerCompleted");

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

            //System.Console.WriteLine("srcChecksum = {0}", srcChecksum);

            // utworzenie listy parametrów
            String encPath = "E:\\Projekty\\BSK\\dane-testowe\\pattern.enc";
            String decPath = "E:\\Projekty\\BSK\\dane-testowe\\pattern.dec";

            AlgorithmParams[] algParams = new AlgorithmParams[34];
            algParams[0] = new AlgorithmParams(path, encPath, "ECB", 128, 256, "1234567890");
            algParams[1] = new AlgorithmParams(path, encPath, "CBC", 128, 128, "");

            algParams[2] = new AlgorithmParams(path, encPath, "CFB", 128, 128, "0987654321");
            algParams[3] = new AlgorithmParams(path, encPath, "CFB", 120, 128, "");
            algParams[32] = new AlgorithmParams(path, encPath, "CFB", 112, 128, "");
            algParams[4] = new AlgorithmParams(path, encPath, "CFB", 104, 128, "");
            algParams[5] = new AlgorithmParams(path, encPath, "CFB", 96, 128, "testtest");
            algParams[6] = new AlgorithmParams(path, encPath, "CFB", 88, 128, "");
            algParams[7] = new AlgorithmParams(path, encPath, "CFB", 80, 128, "");
            algParams[8] = new AlgorithmParams(path, encPath, "CFB", 72, 128, "");
            algParams[9] = new AlgorithmParams(path, encPath, "CFB", 64, 128, "");
            algParams[10] = new AlgorithmParams(path, encPath, "CFB", 56, 128, "");
            algParams[11] = new AlgorithmParams(path, encPath, "CFB", 48, 128, "");
            algParams[12] = new AlgorithmParams(path, encPath, "CFB", 40, 128, "");
            algParams[13] = new AlgorithmParams(path, encPath, "CFB", 32, 128, "");
            algParams[14] = new AlgorithmParams(path, encPath, "CFB", 24, 128, "");
            algParams[15] = new AlgorithmParams(path, encPath, "CFB", 16, 128, "");
            algParams[16] = new AlgorithmParams(path, encPath, "CFB", 8, 128, "");

            algParams[17] = new AlgorithmParams(path, encPath, "OFB", 128, 128, "");
            algParams[33] = new AlgorithmParams(path, encPath, "OFB", 120, 128, "");
            algParams[18] = new AlgorithmParams(path, encPath, "OFB", 112, 128, "");
            algParams[19] = new AlgorithmParams(path, encPath, "OFB", 104, 128, "");
            algParams[20] = new AlgorithmParams(path, encPath, "OFB", 96, 128, "");
            algParams[21] = new AlgorithmParams(path, encPath, "OFB", 88, 128, "");
            algParams[22] = new AlgorithmParams(path, encPath, "OFB", 80, 128, "");
            algParams[23] = new AlgorithmParams(path, encPath, "OFB", 72, 128, "");
            algParams[24] = new AlgorithmParams(path, encPath, "OFB", 64, 128, "");
            algParams[25] = new AlgorithmParams(path, encPath, "OFB", 56, 128, "");
            algParams[26] = new AlgorithmParams(path, encPath, "OFB", 48, 128, "");
            algParams[27] = new AlgorithmParams(path, encPath, "OFB", 40, 128, "");
            algParams[28] = new AlgorithmParams(path, encPath, "OFB", 32, 128, "");
            algParams[29] = new AlgorithmParams(path, encPath, "OFB", 24, 128, "");
            algParams[30] = new AlgorithmParams(path, encPath, "OFB", 16, 128, "");
            algParams[31] = new AlgorithmParams(path, encPath, "OFB", 8, 128, "");

            // wykonanie testów
            Stopwatch sw = new Stopwatch();

            foreach (var p in algParams)
            {
                if (null == p)
                    continue;

                String testSignature = p.CipherMode + "/" + p.SegmentSize.ToString() + "/" + p.SessionKeySize.ToString();

                using (var alg = encryptFile(p))
                {
                    sw.Start();
                    alg.encrypt(Int64.MaxValue);
                    sw.Stop();
                }
                Console.WriteLine(testSignature + " encrypt time: {0}", sw.Elapsed);
                sw.Reset();


                p.Src = p.Dst;
                p.Dst = decPath;

                using (var alg = decryptFile(p))
                {
                    sw.Start();
                    alg.encrypt(Int64.MaxValue);
                    sw.Stop();
                }
                Console.WriteLine(testSignature + " decrypt time: {0}", sw.Elapsed);
                sw.Reset();
            
                var resultChecksum = computeFileChecksum(decPath);
                //System.Console.WriteLine("resultChecksum = {0}", resultChecksum);

                // porównanie pliku źródłowego z wynikowym
                if (srcChecksum != resultChecksum)
                {
                    MessageBox.Show("Niepoprawny wynik podczas testu " + testSignature);

                    return;
                }
            }

            MessageBox.Show("Wszystko OK");
        }

        private IAlgorithm encryptFile(AlgorithmParams p)
        {
            return encryptFile(p.Src, p.Dst, p.CipherMode, p.SegmentSize, p.SessionKeySize, p.Password);
        }

        private IAlgorithm decryptFile(AlgorithmParams p)
        {
            return decryptFile(p.Src, p.Dst, p.Password);
        }

        private IAlgorithm encryptFile(String src, String dst, String cipherMode, int segment, int sessionKeySize, String password)
        {
            // wykonanie algorytmu
            //  utworzenie obiektu klasy algorithm (IAlgorithm)
            //  wywołanie metody encrypt z odpowiednimi parametrami
            //  w tym obsługa paska postepu i przerwania operacji
            IAlgorithm alg;

            var key = Serpent.generateKey(sessionKeySize);
            //var key = Serpent.generateKeyFromBytes(Convert.FromBase64String("ZgCKtGo7pgmpRw7EFHJTGQ=="));
            var iv = Serpent.generateIV();

            // zaszyfrowanie klucza sesyjnego algorytmem Serpent/ECB hasłem `password`
            var sessionKeyAlg = getSessionKeyAlg(true, password);
            var encryptedKey = sessionKeyAlg.encryptInMemory(key.GetKey());

            // utworzenie nagłówka
            XDocument miXML = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("EncryptedFileHeader",
                    new XElement("Algorithm", "SERPENT"),
                    new XElement("CipherMode", cipherMode),
                    new XElement("BlockSize", 128),
                    new XElement("SegmentSize", segment),
                    new XElement("KeySize", sessionKeySize),
                    new XElement("EncryptedKey", Convert.ToBase64String(encryptedKey)),
                    new XElement("IV", Convert.ToBase64String(iv))
                )
            );

            using (StreamWriter sw = new StreamWriter(dst, false, Encoding.ASCII))
            {
                miXML.Save(sw);
                sw.WriteLine();
            }
            long xmlSize = new FileInfo(dst).Length;

            // zapisanie nagłówka
            var headerOffset = xmlSize;

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

            // wczytanie nagłówka
            String xmlHeader = "";
            using (StreamReader sr = new StreamReader(src))
            { 
                String line;
                while (sr.Peek() >= 0)
                {
                    line = sr.ReadLine();
                    xmlHeader += line + "\r\n";

                    if (line.Equals("</EncryptedFileHeader>"))
                    {
                        break;
                    }
                }
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlHeader);

            int headerOffset = xmlHeader.Length;

            // wyświetlenie nagłówka w GUI
            fileHeader.Text = xmlHeader;

            // odczytanie parametrów z nagłówka
            XmlNode node = doc.DocumentElement.SelectSingleNode("/EncryptedFileHeader/KeySize");
            var keySize = Convert.ToInt32(node.InnerText);
            keySize = keySize >> 3;

            node = doc.DocumentElement.SelectSingleNode("/EncryptedFileHeader/EncryptedKey");
            byte[] encryptedKey = Convert.FromBase64String(node.InnerText);

            // odszyfrowanie klucza sesyjnego algorytmem Serpent/ECB hasłem `password`
            var sessionKeyAlg = getSessionKeyAlg(false, password);
            var decryptedKey = sessionKeyAlg.encryptInMemory(encryptedKey);
            if (decryptedKey.Length != keySize)
            {
                var truncatedKey = new byte[keySize];
                System.Buffer.BlockCopy(decryptedKey, 0, truncatedKey, 0, keySize);
                decryptedKey = truncatedKey;
            }

            var sessionKey = Serpent.generateKeyFromBytes(decryptedKey);

            //
            node = doc.DocumentElement.SelectSingleNode("/EncryptedFileHeader/IV");
            byte[] iv = Convert.FromBase64String(node.InnerText);

            node = doc.DocumentElement.SelectSingleNode("/EncryptedFileHeader/SegmentSize");
            var segment = Convert.ToInt32(node.InnerText);

            node = doc.DocumentElement.SelectSingleNode("/EncryptedFileHeader/CipherMode");
            var cipherMode = node.InnerText;

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

        private byte[] computeHash(String data)
        {
            using (var alg = SHA256.Create())
            {
                //return BitConverter.ToString(alg.ComputeHash(GetBytes(data))).Replace("-", "").ToLower();
                return alg.ComputeHash(GetBytes(data));
            }
        }

        private IAlgorithm getSessionKeyAlg(bool encryption, string password)
        {
            var SKkey = Serpent.generateKeyFromBytes(computeHash(password));
            var SKiv = Serpent.generateIV(true);
            IAlgorithm sessionKeyAlg = new Serpent(SKkey, SKiv, encryption);
            sessionKeyAlg.init(null, null, "ECB", 128);

            return sessionKeyAlg;
        }

        static byte[] GetBytes(String str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static String GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new String(chars);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Console.WriteLine("tab: {0}", tabControl1.SelectedIndex);
            if (1 == tabControl1.SelectedIndex)
            {
                fileHeader.Text = "";
            }
        }
    }
}
