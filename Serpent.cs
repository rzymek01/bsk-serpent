using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace serpent {
    class Serpent : IAlgorithm {
        const int BLOCK_SIZE = 128;

        // Flag: Has Dispose already been called? 
        private bool disposed = false;

        private FileStream mSrcFile;
        private FileStream mDstFile;
        private String mSessionKey;
        private String mOpMode;
        private int mSegmentSize;

        private Int64 mSrcFileOffset = 0;
        private Int64 mDstFileOffset = 0;

        public void init(String srcFile, String dstFile, String sessionKey, String opMode, int segmentSize,
                Int64 srcFileOffset = 0, Int64 dstFileOffset = 0) {
            //@todo: try..catch wyrzucający System.ArgumentException

            mSrcFile = File.OpenRead(srcFile);
            mDstFile = File.OpenWrite(dstFile);
            mSessionKey = sessionKey;
            mOpMode = opMode;
            mSegmentSize = segmentSize;

            mSrcFileOffset += srcFileOffset;
            mDstFileOffset += dstFileOffset;

            //@todo: walidacja rozmiaru sessionKey, opMode z segmentSize


            //
            mSrcFile.Seek(mSrcFileOffset, SeekOrigin.Begin);
            mDstFile.Seek(mDstFileOffset, SeekOrigin.Begin);
        }

        public Int64 encrypt(Int64 minBytes) {
            Int64 countLoop = System.Convert.ToInt64(Math.Ceiling(minBytes / (double)mSegmentSize));

            byte[] b = new byte[mSegmentSize+1];
            int readBytes = 0;

            UTF8Encoding temp = new UTF8Encoding(true);

            for (Int64 i = 0; i < countLoop; ++i) {
                readBytes = mSrcFile.Read(b, 0, mSegmentSize);
                
                if (readBytes > 0) {
                    System.Console.WriteLine(temp.GetString(b));

                    //@todo: algorytm
                }
            }

            return mSegmentSize * (countLoop - 1) + readBytes;
        }

        public Int64 decrypt(Int64 minBytes) {
            return 0;
        }

        public Int64 getSrcLength() {
            return mSrcFile.Length - mSrcFileOffset;
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
