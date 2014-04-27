using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace serpent
{
    class AlgorithmParams
    {
        public String Src { get; set; }
        public String Dst { get; set; }
        public String CipherMode { get; set; }
        public int SegmentSize { get; set; }
        public int SessionKeySize { get; set; }
        public String Password { get; set; }

        public AlgorithmParams()
        {

        }

        public AlgorithmParams(String src, String dst, String cipherMode, int segmentSize, int sessionKeySize, String password)
        {
            Src = src;
            Dst = dst;
            CipherMode = cipherMode;
            SegmentSize = segmentSize;
            SessionKeySize = sessionKeySize;
            Password = password;
        }
    }
}
