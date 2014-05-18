using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Org.BouncyCastle;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

namespace serpent {
    class Serpent : IAlgorithm {
        const int BLOCK_SIZE = 16; // 128 b / 8 = 16 B
        const int BUFFER_SIZE = 1 << 13; // 8 kB

        // Flag: Has Dispose already been called? 
        private bool disposed = false;

        private FileStream mSrcFile;
        private FileStream mDstFile;
        private KeyParameter mSessionKey;
        private byte[] mIV;
        private String mOpMode;
        private int mSegmentSize;
        private int mBufferSize;
        public bool Encryption { get; private set; }

        private Int64 mSrcFileOffset;
        private Int64 mDstFileOffset;

        private IBufferedCipher mSerpent;
        

        public static KeyParameter generateKey(int keySize)
        {
            // walidacja parametru keySize
            //@todo

            CipherKeyGenerator keyGen = new CipherKeyGenerator();
            keyGen.Init(new KeyGenerationParameters(new SecureRandom(), keySize));
            byte[] sessionKey = keyGen.GenerateKey();
            var param = generateKeyFromBytes(sessionKey);

            return param;
        }

        public static KeyParameter generateKeyFromBytes(byte[] key)
        {
            ////@todo delete this
            //System.Console.WriteLine("key: {0}", BitConverter.ToString(key));
            //System.Console.WriteLine("key: {0}", Convert.ToBase64String(key));

            KeyParameter param = ParameterUtilities.CreateKeyParameter("Serpent", key);
            return param;
        }

        public static byte[] generateIV(bool zeros = false)
        {
            byte[] iv;
            if (!zeros)
            {
                CipherKeyGenerator keyGen = new CipherKeyGenerator();
                keyGen.Init(new KeyGenerationParameters(new SecureRandom(), BLOCK_SIZE << 3));
                iv = keyGen.GenerateKey();
            }
            else
            {
                iv = new byte[BLOCK_SIZE];
            }


            ////@todo delete this
            //System.Console.WriteLine("iv: {0}", BitConverter.ToString(iv));

            return iv;
        }

        public static ParametersWithIV combineKeyWithIV(KeyParameter key, byte[] iv)
        {
            ParametersWithIV param = new ParametersWithIV(key, iv);
            return param;
        }

        public Serpent(KeyParameter key, byte[] iv, bool encryption)
        {
            mSessionKey = key;
            mIV = iv;
            Encryption = encryption;
        }

        public void init(String srcFile, String dstFile, String opMode, int segmentSize,
                Int64 srcFileOffset = 0, Int64 dstFileOffset = 0) {
            //System.Console.WriteLine("cipherMode: {0}, segment: {1}, srcOffset: {2}, dstOffset: {3}",
            //    opMode, segmentSize, srcFileOffset, dstFileOffset);

            //@todo: walidacja opMode z segmentSize
            //@todo: try..catch wyrzucający System.ArgumentException
            // w OFB i CFB dł. podbloku musi być wielokrotnością 8 b, np. OFB8, OFB16

            mOpMode = opMode;
            mSegmentSize = segmentSize >> 3; // divide by 8, i.e. [b] => [B]
            mBufferSize = BUFFER_SIZE - (BUFFER_SIZE % mSegmentSize); // size of the single chunk of data read from disk
                                                                      // multiplicity of segment size

            //
            mSrcFileOffset = srcFileOffset;
            mDstFileOffset = dstFileOffset;

            // w trybie "in memory" nie potrzeba podawać plików
            if (srcFile != null && dstFile != null)
            {
                mSrcFile = File.OpenRead(srcFile);
                mDstFile = (Encryption
                    ? new FileStream(dstFile, FileMode.OpenOrCreate | FileMode.Append, FileAccess.Write)
                    : File.Create(dstFile));

                mSrcFile.Seek(mSrcFileOffset, SeekOrigin.Begin);
                mDstFile.Seek(mDstFileOffset, SeekOrigin.Begin);
            }

            // inicjalizacja algorytmu Serpent
            if (mOpMode == "OFB" || mOpMode == "CFB")
            {
                mOpMode += segmentSize.ToString();
            }

            if (mOpMode != "ECB")
            {
                var cipherId = "Serpent/" + mOpMode + "/NoPadding";
                //System.Console.WriteLine(cipherId);
                mSerpent = CipherUtilities.GetCipher(cipherId);
                mSerpent.Init(Encryption, combineKeyWithIV(mSessionKey, mIV));
            }
            else
            {
                mSerpent = new BufferedBlockCipher(new SerpentEngine());
                mSerpent.Init(Encryption, mSessionKey);
            }

            
            System.Console.WriteLine("serpent init");
        }

        public Int64 encrypt(Int64 minBytes) {
            Int64 countLoop = System.Convert.ToInt64(Math.Ceiling(minBytes / (double)mBufferSize));
            
            byte[] b = new byte[mBufferSize];
            int readBytes = 0;
            int lastReadBytes = 0;
            int paddingSize = 0;

            ////@todo: delete this debug
            //UTF8Encoding temp = new UTF8Encoding(true);

            Int64 i;
            for (i = 0; i < countLoop; ++i) {
                readBytes = mSrcFile.Read(b, 0, mBufferSize);
                
                if (readBytes > 0) {
                    //var tmp = temp.GetString(b);
                    //System.Console.WriteLine("\r\n[" + readBytes + "]\r\n" + tmp);

                    // compute padding                    
                    paddingSize = mSegmentSize - (readBytes % mSegmentSize);

                    if (paddingSize == mSegmentSize)
                    {
                        byte[] output = mSerpent.ProcessBytes(b, 0, readBytes);
                        mDstFile.Write(output, 0, output.Length);
                    }
                    else
                    {
                        lastReadBytes = readBytes;
                    }
                }
                else
                {
                    i++;
                    break;
                }
            }

            if (paddingSize > 0 && readBytes < mBufferSize)
            { // zabezpieczenie przed kolejnym wywołanem funkcji na już przeczytanym do końca pliku
              // wykonanie tylko pod koniec pliku
                if (Encryption)
                { // writing padding
                    for (int j = 0; j < paddingSize; ++j)
                    {
                        b[lastReadBytes + j] = (byte)(paddingSize % mSegmentSize);
                    }

                    byte[] output = mSerpent.ProcessBytes(b, 0, lastReadBytes + paddingSize);
                    mDstFile.Write(output, 0, output.Length);

                    //Console.WriteLine("encrypt: paddingSize = {0}, readBytes = {1}", paddingSize, readBytes);
                }
                else if (!Encryption)
                { // removing padding 
                    // read the last char - assume it has 'x' value in ASCII
                    // if 'x' == 0 then
                    //   let x := mSegmentSize
                    //
                    // remove 'x' chars from the end of file

                    mDstFile.Seek(-1, System.IO.SeekOrigin.End);
                    paddingSize = mDstFile.ReadByte();

                    if (0 == paddingSize)
                    {
                        paddingSize = mSegmentSize;
                    }

                    mDstFile.SetLength(mDstFile.Length - paddingSize);
                    //Console.WriteLine("decrypt: paddingSize = {0}, readBytes = {1}", paddingSize, readBytes);
                }
            }

            return mBufferSize * (i - 1) + readBytes;
        }

        public Int64 decrypt(Int64 minBytes) {
            return encrypt(minBytes);
        }

        public Int64 getSrcLength() {
            return mSrcFile.Length - mSrcFileOffset;
        }

        public byte[] encryptInMemory(byte[] data)
        {
            // padding
            int paddingSize = data.Length % BLOCK_SIZE;
            if (paddingSize > 0)
            {
                var paddedData = new byte[data.Length + paddingSize];
                System.Buffer.BlockCopy(data, 0, paddedData, 0, data.Length);
                data = paddedData;
            }

            //
            byte[] output = mSerpent.ProcessBytes(data);
            return output;
        }

        public byte[] decryptInMemory(byte[] cryptogram)
        {
            return encryptInMemory(cryptogram);
        }


        // Public implementation of Dispose pattern callable by consumers. 
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern. 
        protected virtual void Dispose(bool disposing) {
            if (disposed)
                return;

            if (disposing) {
                // Free any other managed objects here. 
                //
                mSrcFile.Dispose();
                mDstFile.Dispose();
            }

            // Free any unmanaged objects here. 
            //
            disposed = true;
        }

        ~Serpent() {
            Dispose(false);
        }
    }
}
